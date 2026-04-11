using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class DeviceCompliancePolicyHelper
    {
        private class Helper : GraphHelper<DeviceCompliancePolicy, DeviceCompliancePolicyCollectionResponse>
        {
            protected override string ResourceName => "device compliance policies";
            protected override string ContentTypeName => "Device Compliance Policy";

            protected override string? GetPolicyPlatform(DeviceCompliancePolicy policy)
                => HelperClass.TranslatePolicyPlatformName(policy.OdataType?.ToString() ?? string.Empty);

            protected override string? GetPolicyName(DeviceCompliancePolicy policy) => policy.DisplayName;
            protected override string? GetPolicyId(DeviceCompliancePolicy policy) => policy.Id;
            protected override string? GetPolicyDescription(DeviceCompliancePolicy policy) => policy.Description;

            protected override Task<DeviceCompliancePolicyCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.DeviceCompliancePolicies.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

            // Server-side filtering not supported for compliance policies; SearchAsync is overridden for client-side filtering
            protected override Task<DeviceCompliancePolicyCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.DeviceCompliancePolicies.GetAsync();

            // Graph API does not allow server-side filtering for compliance policies — get all then filter client-side
            public override async Task<List<DeviceCompliancePolicy>> SearchAsync(GraphServiceClient client, string searchQuery)
            {
                try
                {
                    LogToFunctionFile(appFunction.Main, $"Searching for {ResourceName}. Search query: {searchQuery}");

                    var all = await GetAllAsync(client);

                    // Filter the collected policies based on the searchQuery - Graph API does not allow for server side filtering
                    var filtered = all
                        .Where(policy => policy.DisplayName != null && policy.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    LogToFunctionFile(appFunction.Main, $"Filtered policies count: {filtered.Count}");
                    return filtered;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "searching for", ResourceName);
                    return new List<DeviceCompliancePolicy>();
                }
            }

            protected override Task<DeviceCompliancePolicy?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceCompliancePolicies[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceCompliancePolicies[id].DeleteAsync();

            // DeviceCompliancePolicy is polymorphic — must use reflection to create derived type for PATCH
            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var existing = await client.DeviceManagement.DeviceCompliancePolicies[id].GetAsync();
                if (existing == null) return;

                var policyType = existing.GetType();
                var policy = (DeviceCompliancePolicy?)Activator.CreateInstance(policyType);
                if (policy == null) return;

                policy.DisplayName = newName;
                await client.DeviceManagement.DeviceCompliancePolicies[id].PatchAsync(policy);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var existing = await client.DeviceManagement.DeviceCompliancePolicies[id].GetAsync();
                if (existing == null) return;

                var policyType = existing.GetType();
                var policy = (DeviceCompliancePolicy?)Activator.CreateInstance(policyType);
                if (policy == null) return;

                policy.Description = description;
                await client.DeviceManagement.DeviceCompliancePolicies[id].PatchAsync(policy);
            }

            protected override Task<DeviceCompliancePolicy?> GetByIdForExportAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceCompliancePolicies[id].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Expand = new[] { "scheduledActionsForRule($expand=scheduledActionConfigurations)" };
                });

            /// <summary>
            /// Rebuilds ScheduledActionsForRule with clean objects (no server-generated IDs).
            /// Collects all action configs from all source rules into a single rule,
            /// ensures exactly one Block action exists, and creates a new rule with RuleName="PasswordRequired".
            /// </summary>
            private static void RebuildScheduledActionsForRule(DeviceCompliancePolicy source, DeviceCompliancePolicy target)
            {
                var allConfigs = new List<DeviceComplianceActionItem>();
                if (source.ScheduledActionsForRule != null)
                {
                    foreach (var action in source.ScheduledActionsForRule)
                    {
                        if (action.ScheduledActionConfigurations != null)
                        {
                            foreach (var config in action.ScheduledActionConfigurations)
                            {
                                allConfigs.Add(new DeviceComplianceActionItem
                                {
                                    ActionType = config.ActionType,
                                    GracePeriodHours = config.GracePeriodHours,
                                    NotificationMessageCCList = config.NotificationMessageCCList ?? new List<string>(),
                                    NotificationTemplateId = config.NotificationTemplateId ?? ""
                                });
                            }
                        }
                    }
                }

                // Ensure exactly one Block action exists
                var blockActions = allConfigs.Where(c => c.ActionType == DeviceComplianceActionType.Block).ToList();
                var nonBlockActions = allConfigs.Where(c => c.ActionType != DeviceComplianceActionType.Block).ToList();

                var finalConfigs = new List<DeviceComplianceActionItem>();
                if (blockActions.Count > 0)
                {
                    finalConfigs.Add(blockActions.First());
                }
                else
                {
                    finalConfigs.Add(new DeviceComplianceActionItem
                    {
                        ActionType = DeviceComplianceActionType.Block,
                        GracePeriodHours = 0,
                        NotificationMessageCCList = new List<string>(),
                        NotificationTemplateId = ""
                    });
                }
                finalConfigs.AddRange(nonBlockActions);

                target.ScheduledActionsForRule = new List<DeviceComplianceScheduledActionForRule>
                {
                    new DeviceComplianceScheduledActionForRule
                    {
                        RuleName = "PasswordRequired",
                        ScheduledActionConfigurations = finalConfigs
                    }
                };
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exportedPolicy = GraphImportHelper.DeserializeFromJson(policyData, DeviceCompliancePolicy.CreateFromDiscriminatorValue);

                    if (exportedPolicy == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize device compliance policy data from JSON.", LogLevels.Error);
                        return null;
                    }

                    // Use reflection to clone, skipping ScheduledActionsForRule (rebuilt separately to strip server-generated IDs)
                    var newPolicy = GraphImportHelper.CloneForImport(exportedPolicy, new[] { "ScheduledActionsForRule" });

                    // Rebuild ScheduledActionsForRule with clean objects
                    RebuildScheduledActionsForRule(exportedPolicy, newPolicy);

                    var imported = await client.DeviceManagement.DeviceCompliancePolicies.PostAsync(newPolicy);

                    LogToFunctionFile(appFunction.Main, $"Imported device compliance policy: {imported?.DisplayName}");
                    return imported?.DisplayName;
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
                    var result = await client.DeviceManagement.DeviceCompliancePolicies[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.DeviceCompliancePolicies[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.DeviceCompliancePolicies[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Device Compliance {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceCompliancePolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = new List<DeviceCompliancePolicyAssignment>()
                };

                await client.DeviceManagement.DeviceCompliancePolicies[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Device Compliance policy {id}.");
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
                    var result = await sourceClient.DeviceManagement.DeviceCompliancePolicies[id].GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Expand = new[] { "scheduledActionsForRule($expand=scheduledActionConfigurations)" };
                    });

                    // Use reflection to clone, skipping ScheduledActionsForRule (rebuilt separately to strip server-generated IDs)
                    var newPolicy = GraphImportHelper.CloneForImport(result, new[] { "ScheduledActionsForRule" });

                    // Rebuild ScheduledActionsForRule with clean objects
                    RebuildScheduledActionsForRule(result, newPolicy);

                    var import = await destinationClient.DeviceManagement.DeviceCompliancePolicies.PostAsync(newPolicy);

                    LogToFunctionFile(appFunction.Main, $"Successfully imported {import.DisplayName}");

                    if (assignments)
                    {
                        await AssignGroupsToSingleDeviceCompliance(import.Id, groups, destinationClient);
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

                    var assignments = new List<DeviceCompliancePolicyAssignment>();

                    var buildResult = GraphAssignmentHelper.BuildAssignments<DeviceCompliancePolicyAssignment>(
                        groupIds,
                        (target, groupId) => new DeviceCompliancePolicyAssignment
                        {
                            Target = target
                        },
                        assignments);

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .DeviceCompliancePolicies[id]
                        .Assignments
                        .GetAsync();

                    GraphAssignmentHelper.MergeExistingAssignments(
                        existingAssignments?.Value,
                        assignments,
                        buildResult,
                        a => a.Target);

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceCompliancePolicies.Item.Assign.AssignPostRequestBody
                    {
                        Assignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.DeviceCompliancePolicies[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to policy {id} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to device compliance policy: {ex.Message}", LogLevels.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to device compliance policy: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<DeviceCompliancePolicy>> GetAllDeviceCompliancePolicies(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task<List<DeviceCompliancePolicy>> SearchForDeviceCompliancePolicies(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task ImportMultipleDeviceCompliancePolicies(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policies, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, policies, assignments, filter, groups);

        public static Task AssignGroupsToSingleDeviceCompliance(string policyID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(policyID, groupIDs, destinationGraphServiceClient);

        public static Task DeleteDeviceCompliancePolicy(GraphServiceClient graphServiceClient, string policyID)
            => _helper.DeleteAsync(graphServiceClient, policyID);

        public static Task RenameDeviceCompliancePolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
            => _helper.RenameAsync(graphServiceClient, policyID, newName);

        public static Task<List<CustomContentInfo>> GetAllDeviceComplianceContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchDeviceComplianceContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportDeviceCompliancePolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.ExportDataAsync(graphServiceClient, policyId);

        public static Task<string?> ImportDeviceComplianceFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasDeviceCompliancePolicyAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.HasAssignmentsAsync(graphServiceClient, policyId);

        public static Task<List<AssignmentInfo>?> GetDeviceComplianceAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, policyId);

        public static Task RemoveAllDeviceComplianceAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, policyId);

        public static string TranslateComplianceODataTypeToPlatform(string odatatype)
        {
            string platform = "Unknown";

            if (string.IsNullOrEmpty(odatatype))
            {
                return platform;
            }

            switch (odatatype.ToLower())
            {
                case "#microsoft.graph.ioscompliancepolicy":
                    platform = "iOS";
                    break;
                case "#microsoft.graph.windows10compliancepolicy":
                    platform = "Windows";
                    break;
                case "#microsoft.graph.macoscompliancepolicy":
                    platform = "macOS";
                    break;
                case "#microsoft.graph.androidworkprofilecompliancepolicy":
                    platform = "Android Work Profile";
                    break;
                case "#microsoft.graph.androiddeviceownercompliancepolicy":
                    platform = "Android Device Owner";
                    break;
                default:
                    platform = "Unknown";
                    break;
            }

            return platform;
        }
    }
}
