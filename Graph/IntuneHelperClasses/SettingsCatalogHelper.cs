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
                LogToImportStatusFile("An error occurred while searching for settings catalog policies",Utilities.Variables.LogLevels.Warning);
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

        public static async Task AssignGroupsToSingleSettingsCatalog(string policyID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
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

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                List<DeviceManagementConfigurationPolicyAssignment> assignments = new List<DeviceManagementConfigurationPolicyAssignment>();

                foreach (var group in groupID)
                {
                    var assignment = new DeviceManagementConfigurationPolicyAssignment
                    {
                        OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                        Id = group,
                        Target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                            DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType,
                            GroupId = group,
                        },
                        Source = DeviceAndAppManagementAssignmentSource.Direct,
                        SourceId = group,
                    };
                    assignments.Add(assignment);
                }

                // Merge existing assignments
                var existingAssignments = await destinationGraphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].Assignments.GetAsync();
                if (existingAssignments?.Value != null)
                {
                    assignments.AddRange(existingAssignments.Value);
                }

                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                // Remove duplicates by target.groupId (keep first occurrence).
                // Pseudocode:
                // Initialize a HashSet<string> for seen groupIds.
                // Iterate over all assignments:
                //   Extract groupId if Target is GroupAssignmentTarget.
                //   If groupId is null -> keep (non-group target).
                //   If groupId not in HashSet -> add to distinct list and record.
                //   Else -> skip (duplicate).
                // Replace requestBody.Assignments with distinct list.
                // Optionally log number removed.
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var distinct = new List<DeviceManagementConfigurationPolicyAssignment>();
                int duplicates = 0;

                foreach (var assignment in requestBody.Assignments)
                {
                    var groupTarget = assignment.Target as GroupAssignmentTarget;
                    var groupId = groupTarget?.GroupId;

                    if (groupId == null)
                    {
                        // Non-group assignment (or malformed) - keep.
                        distinct.Add(assignment);
                        continue;
                    }

                    if (seenGroupIds.Add(groupId))
                    {
                        distinct.Add(assignment);
                    }
                    else
                    {
                        duplicates++;
                    }
                }

                if (duplicates > 0)
                {
                    WriteToImportStatusFile($"Removed {duplicates} duplicate group assignments for policy {policyID}.");
                }

                requestBody.Assignments = distinct;

                try
                {
                    var result = await destinationGraphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].Assign.PostAsAssignPostResponseAsync(requestBody);
                    WriteToImportStatusFile("Assigned groups to policy " + policyID + " with filter type " + deviceAndAppManagementAssignmentFilterType.ToString());
                }
                catch (Exception ex)
                {
                    LogToImportStatusFile("An error occurred while searching for settings catalog policies", Utilities.Variables.LogLevels.Warning);
                    LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while searching for settings catalog policies", Utilities.Variables.LogLevels.Warning);
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

                var existingPolicy = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].GetAsync();

                var name = FindPreFixInPolicyName(existingPolicy.Name,newName);

                var policy = new DeviceManagementConfigurationPolicy
                {
                    Name = name
                };

                await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].PatchAsync(policy);
                LogToImportStatusFile($"Renamed policy {policyID} to {name}");
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while renaming settings catalog policies", Utilities.Variables.LogLevels.Warning);
                LogToImportStatusFile(ex.Message, Utilities.Variables.LogLevels.Error);
            }
        }
    }
}
