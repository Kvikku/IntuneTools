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
                                await AssignGroupsToSingleShellScriptmacOS(importResult.Id, groups, destinationGraphServiceClient, filter); // Pass filter bool if needed for assignment logic
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
        public static async Task AssignGroupsToSingleShellScriptmacOS(string scriptId, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient, bool applyFilter)
        {
            if (string.IsNullOrEmpty(scriptId))
            {
                throw new ArgumentNullException(nameof(scriptId));
            }
            if (groupIDs == null || !groupIDs.Any())
            {
                WriteToImportStatusFile($"No group IDs provided for assignment to script {scriptId}. Skipping assignment.");
                return; // Nothing to assign
            }
            if (destinationGraphServiceClient == null)
            {
                throw new ArgumentNullException(nameof(destinationGraphServiceClient));
            }

            WriteToImportStatusFile($"Assigning {groupIDs.Count} groups to macOS shell script {scriptId}. Apply Filter: {applyFilter}");


            var assignments = new List<DeviceManagementScriptGroupAssignment>();

            foreach (var groupId in groupIDs)
            {
                if (string.IsNullOrEmpty(groupId))
                {
                    WriteToImportStatusFile($"Skipping empty or null group ID during assignment to script {scriptId}.");
                    continue;
                }

                assignments.Add(new DeviceManagementScriptGroupAssignment
                {
                    OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                    TargetGroupId = groupId
                    // Filters are not directly part of the group assignment object for shell scripts.
                    // They are associated with the assignment target within the policy/script object itself,
                    // but the Assign action for scripts might not support setting filters directly.
                    // This might require updating the script object after creation if filters are needed.
                });
            }

            if (!assignments.Any())
            {
                WriteToImportStatusFile($"No valid group assignments to process for script {scriptId}.");
                return;
            }


            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceShellScripts.Item.Assign.AssignPostRequestBody
            {
                DeviceManagementScriptGroupAssignments = assignments,
                // DeviceManagementScriptAssignments = null // Use GroupAssignments for assigning to groups
            };

            try
            {
                // The Assign action for shell scripts might return void or a different response type. Adjust accordingly.
                await destinationGraphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].Assign.PostAsync(requestBody);
                WriteToImportStatusFile($"Successfully submitted assignment request for {assignments.Count} groups to macOS shell script {scriptId}.");

                // If filters need to be applied, it might require a separate PATCH request to update the script's assignments property,
                // potentially fetching the script again to get assignment IDs if necessary. This is more complex.
                if (applyFilter && !string.IsNullOrEmpty(SelectedFilterID))
                {
                    WriteToImportStatusFile($"Filter application requested for script {scriptId}, but direct filter assignment via Assign action might not be supported for shell scripts. Manual verification/update might be needed.");
                    // TODO: Implement filter application logic if possible/required via script update.
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error assigning groups to macOS shell script {scriptId}: {ex.Message}", LogType.Error);
                // Rethrow or handle as appropriate for the application flow
                // throw;
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

                // Look up the existing script
                var existingScript = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].GetAsync();

                if (existingScript == null)
                {
                    throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingScript.DisplayName, newName);

                // Create an instance of the specific script type using reflection
                var scriptType = existingScript.GetType();
                var script = (DeviceShellScript)Activator.CreateInstance(scriptType);

                // Set the DisplayName on the new instance
                script.DisplayName = name;

                await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].PatchAsync(script);
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming macOS shell scripts", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
