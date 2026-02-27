using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class PowerShellScriptsHelper
    {
        public static async Task<List<DeviceManagementScript>> SearchForPowerShellScripts(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Searching for PowerShell scripts. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.DeviceManagementScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

                List<DeviceManagementScript> scripts = new List<DeviceManagementScript>();
                var pageIterator = PageIterator<DeviceManagementScript, DeviceManagementScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    scripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {scripts.Count} PowerShell scripts.");

                return scripts;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while searching for PowerShell scripts", LogLevels.Error);
                return new List<DeviceManagementScript>();
            }
        }

        public static async Task<List<DeviceManagementScript>> GetAllPowerShellScripts(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all PowerShell scripts.");

                var result = await graphServiceClient.DeviceManagement.DeviceManagementScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                List<DeviceManagementScript> scripts = new List<DeviceManagementScript>();
                var pageIterator = PageIterator<DeviceManagementScript, DeviceManagementScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    scripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {scripts.Count} PowerShell scripts.");

                return scripts;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all PowerShell scripts", LogLevels.Error);
                return new List<DeviceManagementScript>();
            }
        }

        public static async Task ImportMultiplePowerShellScripts(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scripts, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Importing {scripts.Count} PowerShell scripts.");

                foreach (var script in scripts)
                {
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.DeviceManagementScripts[script].GetAsync();

                        var requestBody = new DeviceManagementScript
                        {
                        };

                        foreach (var property in result.GetType().GetProperties())
                        {
                            var value = property.GetValue(result);
                            if (value != null && property.CanWrite)
                            {
                                property.SetValue(requestBody, value);
                            }
                        }

                        requestBody.Id = "";

                        var import = await destinationGraphServiceClient.DeviceManagement.DeviceManagementScripts.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Imported script: {requestBody.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSinglePowerShellScript(import.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error importing script {script}", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred during the import process", LogLevels.Error);
            }
        }

        public static async Task AssignGroupsToSinglePowerShellScript(string scriptID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (scriptID == null)
                {
                    throw new ArgumentNullException(nameof(scriptID));
                }

                if (groupID == null)
                {
                    throw new ArgumentNullException(nameof(groupID));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<DeviceManagementScriptAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllUsers = false;
                var hasAllDevices = false;

                // Step 1: Add new assignments to request body
                foreach (var group in groupID)
                {
                    if (string.IsNullOrWhiteSpace(group) || !seenGroupIds.Add(group))
                    {
                        continue;
                    }

                    DeviceManagementScriptAssignment assignment;

                    // Check if this is a virtual group (All Users or All Devices)
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignment = new DeviceManagementScriptAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptAssignment",
                            Target = new AllLicensedUsersAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            }
                        };
                    }
                    else if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        assignment = new DeviceManagementScriptAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptAssignment",
                            Target = new AllDevicesAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allDevicesAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            }
                        };
                    }
                    else
                    {
                        // Regular group assignment
                        assignment = new DeviceManagementScriptAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptAssignment",
                            Target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType,
                                GroupId = group
                            }
                        };
                    }

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .DeviceManagementScripts[scriptID]
                    .Assignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        // Check the type of assignment target
                        if (existing.Target is AllLicensedUsersAssignmentTarget)
                        {
                            // Skip if we're already adding All Users
                            if (!hasAllUsers)
                            {
                                assignments.Add(existing);
                            }
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip if we're already adding All Devices
                            if (!hasAllDevices)
                            {
                                assignments.Add(existing);
                            }
                        }
                        else if (existing.Target is GroupAssignmentTarget groupTarget)
                        {
                            var existingGroupId = groupTarget.GroupId;

                            // Only add if not already in the new assignments
                            if (!string.IsNullOrWhiteSpace(existingGroupId) && seenGroupIds.Add(existingGroupId))
                            {
                                assignments.Add(existing);
                            }
                        }
                        else
                        {
                            // Include any other assignment types (e.g., exclusions, all users with exclusions, etc.)
                            assignments.Add(existing);
                        }
                    }
                }

                // Step 3: Update the script with the assignments
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceManagementScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceManagementScriptAssignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].Assign.PostAsync(requestBody);
                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to script {scriptID} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to PowerShell script", LogLevels.Warning);
                    LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to a single PowerShell script", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }
        public static async Task DeletePowerShellScript(GraphServiceClient graphServiceClient, string scriptID)
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
                await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].DeleteAsync();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting PowerShell scripts", LogLevels.Error);
            }
        }
        public static async Task RenamePowerShellScript(GraphServiceClient graphServiceClient, string scriptID, string newName)
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
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingScript.DisplayName ?? string.Empty, newName);

                    var script = new DeviceManagementScript
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].PatchAsync(script);
                    LogToFunctionFile(appFunction.Main, $"Renamed Powershell script {scriptID} to {name}");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing script
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var script = new DeviceManagementScript
                    {
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].PatchAsync(script);
                    LogToFunctionFile(appFunction.Main, $"Updated description for Powershell script {scriptID} to {newName}");
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingScript.DisplayName);

                    var script = new DeviceManagementScript
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].PatchAsync(script);
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from PowerShell script {scriptID}, new name: '{name}'");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming PowerShell scripts", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllPowerShellScriptContentAsync(GraphServiceClient graphServiceClient)
        {
            var scripts = await GetAllPowerShellScripts(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var script in scripts)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "PowerShell Script",
                    ContentPlatform = "Windows",
                    ContentId = script.Id,
                    ContentDescription = script.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchPowerShellScriptContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var scripts = await SearchForPowerShellScripts(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var script in scripts)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "PowerShell Script",
                    ContentPlatform = "Windows",
                    ContentId = script.Id,
                    ContentDescription = script.Description
                });
            }

            return content;
        }
    }
}
