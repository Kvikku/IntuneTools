using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ProactiveRemediationsHelper
    {
        public static async Task<List<DeviceHealthScript>> SearchForProactiveRemediations(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Searching for proactive remediation scripts. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.DeviceHealthScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

                List<DeviceHealthScript> healthScripts = new List<DeviceHealthScript>();
                var pageIterator = PageIterator<DeviceHealthScript, DeviceHealthScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    if (!script.Publisher.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
                    {
                        healthScripts.Add(script);
                    }
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {healthScripts.Count} proactive remediation scripts.");

                return healthScripts;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while searching for proactive remediation scripts", LogLevels.Error);
                return new List<DeviceHealthScript>();
            }
        }

        public static async Task<List<DeviceHealthScript>> GetAllProactiveRemediations(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all proactive remediation scripts.");

                var result = await graphServiceClient.DeviceManagement.DeviceHealthScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                List<DeviceHealthScript> healthScripts = new List<DeviceHealthScript>();
                var pageIterator = PageIterator<DeviceHealthScript, DeviceHealthScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    if (!script.Publisher.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
                    {
                        healthScripts.Add(script);
                    }
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {healthScripts.Count} proactive remediation scripts.");

                return healthScripts;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all proactive remediation scripts", LogLevels.Error);
                return new List<DeviceHealthScript>();
            }
        }

        public static async Task ImportMultipleProactiveRemediations(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scripts, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Importing {scripts.Count} proactive remediation scripts.");

                foreach (var script in scripts)
                {
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.DeviceHealthScripts[script].GetAsync();

                        var requestBody = new DeviceHealthScript
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


                        var import = await destinationGraphServiceClient.DeviceManagement.DeviceHealthScripts.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Imported script: {import.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSingleProactiveRemediation(import.Id, groups, destinationGraphServiceClient);
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

        public static async Task AssignGroupsToSingleProactiveRemediation(string scriptID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
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

                var assignments = new List<DeviceHealthScriptAssignment>();
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

                    DeviceHealthScriptAssignment assignment;

                    // Check if this is a virtual group (All Users or All Devices)
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignment = new DeviceHealthScriptAssignment
                        {
                            OdataType = "#microsoft.graph.deviceHealthScriptAssignment",
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
                        assignment = new DeviceHealthScriptAssignment
                        {
                            OdataType = "#microsoft.graph.deviceHealthScriptAssignment",
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
                        assignment = new DeviceHealthScriptAssignment
                        {
                            OdataType = "#microsoft.graph.deviceHealthScriptAssignment",
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
                    .DeviceHealthScripts[scriptID]
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
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceHealthScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceHealthScriptAssignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].Assign.PostAsync(requestBody);
                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to proactive remediation script {scriptID} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to proactive remediation script", LogLevels.Warning);
                    LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to a single proactive remediation script", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }
        public static async Task DeleteProactiveRemediationScript(GraphServiceClient graphServiceClient, string policyID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (policyID == null)
                {
                    throw new InvalidOperationException("Policy ID cannot be null.");
                }
                await graphServiceClient.DeviceManagement.DeviceHealthScripts[policyID].DeleteAsync();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting proactive remediation scripts", LogLevels.Error);
            }
        }

        public static async Task RenameProactiveRemediation(GraphServiceClient graphServiceClient, string scriptID, string newName)
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
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingScript.DisplayName ?? string.Empty, newName);

                    var script = new DeviceHealthScript
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].PatchAsync(script);
                    LogToFunctionFile(appFunction.Main, $"Renamed Proactive remediation script {scriptID} to {name}");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing script
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var script = new DeviceHealthScript
                    {
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].PatchAsync(script);
                    LogToFunctionFile(appFunction.Main, $"Updated description for Proactive remediation script {scriptID} to {newName}");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming proactive remediation scripts", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllProactiveRemediationContentAsync(GraphServiceClient graphServiceClient)
        {
            var scripts = await GetAllProactiveRemediations(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var script in scripts)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "Proactive Remediation",
                    ContentPlatform = "Windows",
                    ContentId = script.Id,
                    ContentDescription = script.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchProactiveRemediationContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var scripts = await SearchForProactiveRemediations(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var script in scripts)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "Proactive Remediation",
                    ContentPlatform = "Windows",
                    ContentId = script.Id,
                    ContentDescription = script.Description
                });
            }

            return content;
        }
    }
}
