using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class macOSShellScript
    {
        public static async Task<List<DeviceShellScript>> SearchForShellScriptmacOS(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile("Searching for macOS shell scripts. Search query: " + searchQuery);

                // Note: The Graph API for DeviceShellScript might not support filtering by name directly in the same way.
                // This might require fetching all and filtering locally, or adjusting the query if supported.
                // For now, let's assume a similar filter structure, but this might need adjustment.
                var result = await graphServiceClient.DeviceManagement.DeviceShellScripts.GetAsync((requestConfiguration) =>
                {
                    // Filter for macOS platform and name contains search query
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                    requestConfiguration.QueryParameters.Top = 1000; // Adjust as needed
                });

                List<DeviceShellScript> shellScripts = new List<DeviceShellScript>();
                var pageIterator = PageIterator<DeviceShellScript, DeviceShellScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    shellScripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {shellScripts.Count} macOS shell scripts matching the search.");

                return shellScripts;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while searching for macOS shell scripts", LogType.Error);
                return new List<DeviceShellScript>();
            }
        }

        public static async Task<List<DeviceShellScript>> GetAllmacOSShellScripts(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile("Retrieving all macOS shell scripts.");

                var result = await graphServiceClient.DeviceManagement.DeviceShellScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000; // Adjust as needed
                });

                List<DeviceShellScript> shellScripts = new List<DeviceShellScript>();
                var pageIterator = PageIterator<DeviceShellScript, DeviceShellScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    shellScripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {shellScripts.Count} macOS shell scripts.");

                return shellScripts;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all macOS shell scripts",LogType.Error);
                return new List<DeviceShellScript>();
            }
        }
        public static async Task ImportMultiplemacOSShellScripts(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scriptIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {scriptIDs.Count} macOS shell scripts.");

                foreach (var scriptId in scriptIDs)
                {
                    try
                    {
                        // Get the full script object, including script content
                        var sourceScript = await sourceGraphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].GetAsync();


                        if (sourceScript == null)
                        {
                            WriteToImportStatusFile($"Script with ID {scriptId} not found in source tenant. Skipping.");
                            continue;
                        }

                        var newScript = new DeviceShellScript
                        {

                        };

                        foreach (var property in sourceScript.GetType().GetProperties())
                        {
                            var value = property.GetValue(sourceScript);
                            if (value != null && property.CanWrite)
                            {
                                property.SetValue(newScript, value);
                            }
                        }

                        newScript.Id = "";

                        var importResult = await destinationGraphServiceClient.DeviceManagement.DeviceShellScripts.PostAsync(newScript);

                        if (importResult != null)
                        {
                            WriteToImportStatusFile($"Imported script: {importResult.DisplayName} (ID: {importResult.Id})");

                            if (assignments && groups != null && groups.Any())
                            {
                                // Shell script assignments use a different structure
                                await AssignGroupsToSingleShellScriptmacOS(importResult.Id, groups, destinationGraphServiceClient); // Pass filter bool if needed for assignment logic
                            }
                        }
                        else
                        {
                            WriteToImportStatusFile($"Failed to import script: {sourceScript.DisplayName} (ID: {scriptId}). Result was null.");
                        }

                    }
                    catch (Exception ex)
                    {
                        WriteToImportStatusFile($"Failed to import script  (ID: {scriptId}): {ex.Message}", LogType.Error);
                    }
                }
                WriteToImportStatusFile("macOS shell script import process finished.");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred during the macOS shell script import process: {ex.Message}", LogType.Error);
            }
        }


        // Note: Assignment structure for Shell Scripts is different from Configuration Policies
        public static async Task AssignGroupsToSingleShellScriptmacOS(string scriptId, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (string.IsNullOrEmpty(scriptId))
                {
                    throw new ArgumentNullException(nameof(scriptId));
                }

                if (groupIDs == null)
                {
                    throw new ArgumentNullException(nameof(groupIDs));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<DeviceManagementScriptGroupAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllUsers = false;
                var hasAllDevices = false;

                WriteToImportStatusFile($"Assigning {groupIDs.Count} groups to macOS shell script {scriptId}.");

                // Step 1: Add new assignments to request body
                foreach (var groupId in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    // Check if this is a virtual group (All Users or All Devices)
                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignments.Add(new DeviceManagementScriptGroupAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                            TargetGroupId = allUsersVirtualGroupID
                        });
                    }
                    else if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        assignments.Add(new DeviceManagementScriptGroupAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                            TargetGroupId = allDevicesVirtualGroupID
                        });
                    }
                    else
                    {
                        // Regular group assignment
                        assignments.Add(new DeviceManagementScriptGroupAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                            TargetGroupId = groupId
                        });
                    }
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .DeviceShellScripts[scriptId]
                    .GroupAssignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        var existingGroupId = existing.TargetGroupId;

                        if (string.IsNullOrWhiteSpace(existingGroupId))
                        {
                            continue;
                        }

                        // Check if this is a virtual group
                        if (existingGroupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip if we're already adding All Users
                            if (!hasAllUsers)
                            {
                                assignments.Add(existing);
                            }
                        }
                        else if (existingGroupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip if we're already adding All Devices
                            if (!hasAllDevices)
                            {
                                assignments.Add(existing);
                            }
                        }
                        else
                        {
                            // Only add if not already in the new assignments
                            if (seenGroupIds.Add(existingGroupId))
                            {
                                assignments.Add(existing);
                            }
                        }
                    }
                }

                if (!assignments.Any())
                {
                    WriteToImportStatusFile($"No valid group assignments to process for script {scriptId}.");
                    return;
                }

                // Step 3: Update the script with the assignments
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceShellScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceManagementScriptGroupAssignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile($"Assigned {assignments.Count} assignments to macOS shell script {scriptId}.");

                    // Note: Filters are not directly supported in the Assign action for shell scripts
                    // They would need to be applied via a separate PATCH operation if supported
                    if (!string.IsNullOrEmpty(SelectedFilterID))
                    {
                        WriteToImportStatusFile($"Filter application requested for script {scriptId}, but direct filter assignment via Assign action is not supported for shell scripts. Manual verification/update might be needed.");
                    }
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile("An error occurred while assigning groups to macOS shell script", LogType.Warning);
                    WriteToImportStatusFile(ex.Message, LogType.Error);
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while assigning groups to macOS shell script", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
        public static async Task DeleteMacosShellScript(GraphServiceClient graphServiceClient, string profileID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (profileID == null)
                {
                    throw new InvalidOperationException("Profile ID cannot be null.");
                }
                await graphServiceClient.DeviceManagement.DeviceShellScripts[profileID].DeleteAsync();
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while deleting macOS shell script",LogType.Error);
            }
        }
        public static async Task RenameMacOSShellScript(GraphServiceClient graphServiceClient, string scriptID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (scriptID == null)
                {
                    throw new InvalidOperationException("Script ID cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                if (selectedRenameMode == "Prefix")
                {
                    // Look up the existing script
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingScript.DisplayName ?? string.Empty, newName);

                    // Create an instance of the specific script type using reflection
                    var scriptType = existingScript.GetType();
                    var script = (DeviceShellScript?)Activator.CreateInstance(scriptType);

                    if (script == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {scriptType.Name}");
                    }

                    // Set the DisplayName on the new instance
                    script.DisplayName = name;

                    await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].PatchAsync(script);
                    WriteToImportStatusFile($"Renamed macOS shell script '{existingScript.DisplayName}' to '{name}' (ID: {scriptID})");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing script
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    // Create an instance of the specific script type using reflection
                    var scriptType = existingScript.GetType();
                    var script = (DeviceShellScript?)Activator.CreateInstance(scriptType);

                    if (script == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {scriptType.Name}");
                    }

                    script.Description = newName;

                    await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].PatchAsync(script);
                    WriteToImportStatusFile($"Updated description for macOS shell script {scriptID} to '{newName}'");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming macOS shell scripts", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
