using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class SettingsCatalogHelper
    {
        public static async Task<List<DeviceManagementConfigurationPolicy>> SearchForSettingsCatalog(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToImportStatusFile("Searching for settings catalog policies. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.ConfigurationPolicies.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(Name,'{searchQuery}')";
                });

                List<DeviceManagementConfigurationPolicy> configurationPolicies = new List<DeviceManagementConfigurationPolicy>();
                var pageIterator = PageIterator<DeviceManagementConfigurationPolicy, DeviceManagementConfigurationPolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    configurationPolicies.Add(policy);
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToImportStatusFile($"Found {configurationPolicies.Count} settings catalog policies.");

                return configurationPolicies;
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while searching for settings catalog policies", Utilities.Variables.LogLevels.Warning);
                LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
                return new List<DeviceManagementConfigurationPolicy>();
            }
        }

        public static async Task<List<DeviceManagementConfigurationPolicy>> GetAllSettingsCatalogPolicies(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToImportStatusFile("Retrieving all settings catalog policies.");

                var result = await graphServiceClient.DeviceManagement.ConfigurationPolicies.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                List<DeviceManagementConfigurationPolicy> configurationPolicies = new List<DeviceManagementConfigurationPolicy>();
                var pageIterator = PageIterator<DeviceManagementConfigurationPolicy, DeviceManagementConfigurationPolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    configurationPolicies.Add(policy);
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToImportStatusFile($"Found {configurationPolicies.Count} settings catalog policies.");

                return configurationPolicies;
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while searching for settings catalog policies", Utilities.Variables.LogLevels.Warning);
                LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
                return new List<DeviceManagementConfigurationPolicy>();
            }
        }

        public static async Task ImportMultipleSettingsCatalog(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policies, bool assignments, bool filter, List<string> groups)
        {
            try
            {

                WriteToImportStatusFile($"Importing {policies.Count} settings catalog policies.");

                foreach (var policy in policies)
                {
                    var policyName = "";
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.ConfigurationPolicies[policy].GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Expand = new[] { "settings" };
                        });

                        var newPolicy = new DeviceManagementConfigurationPolicy
                        {
                            Name = result.Name,
                            Description = result.Description,
                            Platforms = result.Platforms,
                            Technologies = result.Technologies,
                            RoleScopeTagIds = result.RoleScopeTagIds,
                            Settings = result.Settings,
                            Assignments = new List<DeviceManagementConfigurationPolicyAssignment>()
                        };

                        policyName = newPolicy.Name;

                        var import = await destinationGraphServiceClient.DeviceManagement.ConfigurationPolicies.PostAsync(newPolicy);

                        WriteToImportStatusFile($"Imported policy: {import.Name}");

                        if (assignments)
                        {
                            await AssignGroupsToSingleSettingsCatalog(import.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToImportStatusFile("An error occurred while searching for settings catalog policies", Utilities.Variables.LogLevels.Warning);
                        LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while searching for settings catalog policies", Utilities.Variables.LogLevels.Warning);
                LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
            }
        }

        public static async Task AssignGroupsToSingleSettingsCatalog(string policyID, List<string> groupID, GraphServiceClient _graphServiceClient)
        {
            try
            {
                if (policyID == null)
                {
                    throw new ArgumentNullException(nameof(policyID));
                }
                if (groupID == null)
                {
                    throw new ArgumentNullException(nameof(groupID));
                }
                if (_graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(_graphServiceClient));
                }

                var assignments = new List<DeviceManagementConfigurationPolicyAssignment>();
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

                    DeviceManagementConfigurationPolicyAssignment assignment;

                    // Check if this is a virtual group (All Users or All Devices)
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignment = new DeviceManagementConfigurationPolicyAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                            Target = new AllLicensedUsersAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            },
                            Source = DeviceAndAppManagementAssignmentSource.Direct
                        };
                    }
                    else if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        assignment = new DeviceManagementConfigurationPolicyAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                            Target = new AllDevicesAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allDevicesAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            },
                            Source = DeviceAndAppManagementAssignmentSource.Direct
                        };
                    }
                    else
                    {
                        // Regular group assignment
                        assignment = new DeviceManagementConfigurationPolicyAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                            Id = group,
                            Target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType,
                                GroupId = group
                            },
                            Source = DeviceAndAppManagementAssignmentSource.Direct,
                            SourceId = group
                        };
                    }

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await _graphServiceClient
                    .DeviceManagement
                    .ConfigurationPolicies[policyID]
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

                // Step 3: Update the policy with the request body
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await _graphServiceClient
                        .DeviceManagement
                        .ConfigurationPolicies[policyID]
                        .Assign
                        .PostAsAssignPostResponseAsync(requestBody);

                    WriteToImportStatusFile($"Assigned {assignments.Count} assignments to policy {policyID} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                }
                catch (Exception ex)
                {
                    LogToImportStatusFile("An error occurred while assigning groups to settings catalog policy", Utilities.Variables.LogLevels.Warning);
                    LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while assigning groups to settings catalog policy", Utilities.Variables.LogLevels.Warning);
                LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
            }
        }

        public static async Task DeleteSettingsCatalog(GraphServiceClient graphServiceClient, string policyID)
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
                await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].DeleteAsync();
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while searching for settings catalog policies", Utilities.Variables.LogLevels.Warning);
                LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
            }
        }

        public static async Task RenameSettingsCatalogPolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
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

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }


                if (selectedRenameMode == "Prefix")
                {
                    var existingPolicy = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].GetAsync();

                    var name = FindPreFixInPolicyName(existingPolicy.Name, newName);

                    var policy = new DeviceManagementConfigurationPolicy
                    {
                        Name = name
                    };

                    await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].PatchAsync(policy);
                    LogToImportStatusFile($"Renamed policy {policyID} to {name}");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    //var existingPolicy = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].GetAsync();


                    var policy = new DeviceManagementConfigurationPolicy
                    {
                        Description = newName
                    };

                    await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].PatchAsync(policy);
                    LogToImportStatusFile($"Updated description for {policyID} to {newName}");
                }



            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while renaming settings catalog policies", Utilities.Variables.LogLevels.Warning);
                LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
            }
        }
    }
}
