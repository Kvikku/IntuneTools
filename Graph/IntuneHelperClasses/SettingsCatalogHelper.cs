using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class SettingsCatalogHelper
    {
        private class Helper : GraphHelper<DeviceManagementConfigurationPolicy, DeviceManagementConfigurationPolicyCollectionResponse>
        {
            protected override string ResourceName => "settings catalog policies";
            protected override string ContentTypeName => "Settings Catalog";

            protected override string? GetPolicyPlatform(DeviceManagementConfigurationPolicy policy)
                => HelperClass.TranslatePolicyPlatformName(policy.Platforms.ToString());

            protected override string? GetPolicyName(DeviceManagementConfigurationPolicy policy) => policy.Name;
            protected override string? GetPolicyId(DeviceManagementConfigurationPolicy policy) => policy.Id;
            protected override string? GetPolicyDescription(DeviceManagementConfigurationPolicy policy) => policy.Description;

            protected override Task<DeviceManagementConfigurationPolicyCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.ConfigurationPolicies.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

            protected override Task<DeviceManagementConfigurationPolicyCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.ConfigurationPolicies.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(Name,'{searchQuery}')";
                });

            protected override Task<DeviceManagementConfigurationPolicy?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.ConfigurationPolicies[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.ConfigurationPolicies[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var policy = new DeviceManagementConfigurationPolicy { Name = newName };
                await client.DeviceManagement.ConfigurationPolicies[id].PatchAsync(policy);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var policy = new DeviceManagementConfigurationPolicy { Description = description };
                await client.DeviceManagement.ConfigurationPolicies[id].PatchAsync(policy);
            }

            protected override Task<DeviceManagementConfigurationPolicy?> GetByIdForExportAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.ConfigurationPolicies[id].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Expand = new[] { "settings" };
                });

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exportedPolicy = GraphImportHelper.DeserializeFromJson(policyData, DeviceManagementConfigurationPolicy.CreateFromDiscriminatorValue);

                    if (exportedPolicy == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize settings catalog policy data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newPolicy = new DeviceManagementConfigurationPolicy
                    {
                        Name = exportedPolicy.Name,
                        Description = exportedPolicy.Description,
                        Platforms = exportedPolicy.Platforms,
                        Technologies = exportedPolicy.Technologies,
                        RoleScopeTagIds = exportedPolicy.RoleScopeTagIds,
                        Settings = exportedPolicy.Settings,
                        Assignments = new List<DeviceManagementConfigurationPolicyAssignment>()
                    };

                    var imported = await client.DeviceManagement.ConfigurationPolicies.PostAsync(newPolicy);

                    LogToFunctionFile(appFunction.Main, $"Imported settings catalog policy: {imported?.Name}");
                    return imported?.Name;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "importing from JSON", ResourceName);
                    return null;
                }
            }

            public override async Task<bool?> HasAssignmentsAsync(GraphServiceClient client, string id)
            {
                try
                {
                    var result = await client.DeviceManagement.ConfigurationPolicies[id].Assignments.GetAsync(rc =>
                    {
                        rc.QueryParameters.Top = 1;
                    });
                    return result?.Value != null && result.Value.Count > 0;
                }
                catch
                {
                    return null;
                }
            }

            public override async Task<List<AssignmentInfo>?> GetAssignmentDetailsAsync(GraphServiceClient client, string id)
            {
                try
                {
                    var details = new List<AssignmentInfo>();
                    var result = await client.DeviceManagement.ConfigurationPolicies[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.ConfigurationPolicies[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Settings Catalog {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = new List<DeviceManagementConfigurationPolicyAssignment>()
                };

                await client.DeviceManagement.ConfigurationPolicies[id].Assign.PostAsAssignPostResponseAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Settings Catalog policy {id}.");
            }

            public override async Task ImportMultipleAsync(
                GraphServiceClient sourceClient,
                GraphServiceClient destinationClient,
                List<string> ids,
                bool assignments,
                bool filter,
                List<string> groups)
            {
                await GraphImportHelper.ImportBatchAsync(ids, ResourceName, async id =>
                {
                    var result = await sourceClient.DeviceManagement.ConfigurationPolicies[id].GetAsync(requestConfiguration =>
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

                    var import = await destinationClient.DeviceManagement.ConfigurationPolicies.PostAsync(newPolicy);

                    LogToFunctionFile(appFunction.Main, $"Imported policy: {import.Name}");

                    if (assignments)
                    {
                        await AssignGroupsToSingleSettingsCatalog(import.Id, groups, destinationClient);
                    }
                });
            }

            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<DeviceManagementConfigurationPolicyAssignment>();

                    var buildResult = GraphAssignmentHelper.BuildAssignments<DeviceManagementConfigurationPolicyAssignment>(
                        groupIds,
                        (target, groupId) =>
                        {
                            var assignment = new DeviceManagementConfigurationPolicyAssignment
                            {
                                OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                                Target = target,
                                Source = DeviceAndAppManagementAssignmentSource.Direct
                            };

                            if (groupId != null)
                            {
                                assignment.Id = groupId;
                                assignment.SourceId = groupId;
                            }

                            return assignment;
                        },
                        assignments);

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .ConfigurationPolicies[id]
                        .Assignments
                        .GetAsync();

                    GraphAssignmentHelper.MergeExistingAssignments(
                        existingAssignments?.Value,
                        assignments,
                        buildResult,
                        a => a.Target);

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
                    {
                        Assignments = assignments
                    };

                    try
                    {
                        await client
                            .DeviceManagement
                            .ConfigurationPolicies[id]
                            .Assign
                            .PostAsAssignPostResponseAsync(requestBody);

                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to policy {id} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to settings catalog policy: {ex.Message}", LogLevels.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to settings catalog policy: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for using static consumers) ──

        public static Task<List<DeviceManagementConfigurationPolicy>> SearchForSettingsCatalog(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task<List<DeviceManagementConfigurationPolicy>> GetAllSettingsCatalogPolicies(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleSettingsCatalog(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policies, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, policies, assignments, filter, groups);

        public static Task AssignGroupsToSingleSettingsCatalog(string policyID, List<string> groupID, GraphServiceClient _graphServiceClient)
            => _helper.AssignGroupsAsync(policyID, groupID, _graphServiceClient);

        public static Task DeleteSettingsCatalog(GraphServiceClient graphServiceClient, string policyID)
            => _helper.DeleteAsync(graphServiceClient, policyID);

        public static Task RenameSettingsCatalogPolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
            => _helper.RenameAsync(graphServiceClient, policyID, newName);

        public static Task<List<CustomContentInfo>> GetAllSettingsCatalogContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchSettingsCatalogContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportSettingsCatalogPolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.ExportDataAsync(graphServiceClient, policyId);

        public static Task<string?> ImportSettingsCatalogFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasSettingsCatalogAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.HasAssignmentsAsync(graphServiceClient, policyId);

        public static Task<List<AssignmentInfo>?> GetSettingsCatalogAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, policyId);

        public static Task RemoveAllSettingsCatalogAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, policyId);
    }
}
