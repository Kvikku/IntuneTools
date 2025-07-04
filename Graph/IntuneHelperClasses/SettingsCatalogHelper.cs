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
                        //HandleException(ex, $"Error importing policy {policy}", false);
                        //rtb.AppendText($"Failed to import {ex.Message}");

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

                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    var result = await destinationGraphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].Assign.PostAsAssignPostResponseAsync(requestBody);
                    WriteToImportStatusFile("Assigned groups to policy " + policyID + " with filter type" + deviceAndAppManagementAssignmentFilterType.ToString());
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
    }
}
