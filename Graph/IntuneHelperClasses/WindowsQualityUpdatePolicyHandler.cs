using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsQualityUpdatePolicyHandler
    {
        private class Helper : GraphHelper<WindowsQualityUpdatePolicy, WindowsQualityUpdatePolicyCollectionResponse>
        {
            protected override string ResourceName => "Windows Quality Update policies";
            protected override string ContentTypeName => "Windows Quality Update Policy";
            protected override string? FixedPlatform => "Windows";

            protected override string? GetPolicyName(WindowsQualityUpdatePolicy policy) => policy.DisplayName;
            protected override string? GetPolicyId(WindowsQualityUpdatePolicy policy) => policy.Id;
            protected override string? GetPolicyDescription(WindowsQualityUpdatePolicy policy) => policy.Description;

            protected override Task<WindowsQualityUpdatePolicyCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.WindowsQualityUpdatePolicies.GetAsync();

            // No server-side filter support; client-side filtering is done in the public static methods
            protected override Task<WindowsQualityUpdatePolicyCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.WindowsQualityUpdatePolicies.GetAsync();

            protected override Task<WindowsQualityUpdatePolicy?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsQualityUpdatePolicies[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsQualityUpdatePolicies[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var policy = new WindowsQualityUpdatePolicy { DisplayName = newName };
                await client.DeviceManagement.WindowsQualityUpdatePolicies[id].PatchAsync(policy);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var policy = new WindowsQualityUpdatePolicy { Description = description };
                await client.DeviceManagement.WindowsQualityUpdatePolicies[id].PatchAsync(policy);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exportedPolicy = GraphImportHelper.DeserializeFromJson(policyData, WindowsQualityUpdatePolicy.CreateFromDiscriminatorValue);

                    if (exportedPolicy == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize Windows Quality Update policy data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newPolicy = new WindowsQualityUpdatePolicy();
                    GraphImportHelper.CopyProperties(exportedPolicy, newPolicy, new[] { "Assignments", "AdditionalData", "BackingStore" });
                    newPolicy.OdataType = "#microsoft.graph.windowsQualityUpdatePolicy";

                    var imported = await client.DeviceManagement.WindowsQualityUpdatePolicies.PostAsync(newPolicy);

                    LogToFunctionFile(appFunction.Main, $"Imported Windows Quality Update policy: {imported?.DisplayName}");
                    return imported?.DisplayName;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "importing from JSON", ResourceName);
                    LogToFunctionFile(appFunction.Main, "This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogLevels.Warning);
                    return null;
                }
            }

            public override async Task<bool?> HasAssignmentsAsync(GraphServiceClient client, string id)
            {
                try
                {
                    var result = await client.DeviceManagement.WindowsQualityUpdatePolicies[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.WindowsQualityUpdatePolicies[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.WindowsQualityUpdatePolicies[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Windows Quality Update Policy {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdatePolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = new List<WindowsQualityUpdatePolicyAssignment>()
                };

                await client.DeviceManagement.WindowsQualityUpdatePolicies[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Windows Quality Update Policy {id}.");
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
                    try
                    {
                        var sourcePolicy = await sourceClient.DeviceManagement.WindowsQualityUpdatePolicies[id].GetAsync();

                        if (sourcePolicy == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping policy ID {id}: Not found in source tenant.");
                            return;
                        }

                        var newPolicy = new WindowsQualityUpdatePolicy();
                        GraphImportHelper.CopyProperties(sourcePolicy, newPolicy, new[] { "Assignments" });
                        newPolicy.OdataType = "#microsoft.graph.windowsQualityUpdatePolicy";

                        var importedPolicy = await destinationClient.DeviceManagement.WindowsQualityUpdatePolicies.PostAsync(newPolicy);

                        LogToFunctionFile(appFunction.Main, $"Imported policy: {importedPolicy?.DisplayName ?? "Unnamed Policy"} (ID: {importedPolicy?.Id ?? "Unknown ID"})");

                        if (assignments && groups != null && groups.Any() && importedPolicy?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsQualityUpdatePolicy(importedPolicy.Id, groups, destinationClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import Windows Quality Update policy: {ex.Message}", LogLevels.Error);
                        LogToFunctionFile(appFunction.Main, "This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogLevels.Warning);
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

                    var assignments = new List<WindowsQualityUpdatePolicyAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var groupId in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                            continue;

                        if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update policies cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update policies cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        var target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = groupId
                        };
                        GraphAssignmentHelper.ApplySelectedFilter(target);

                        assignments.Add(new WindowsQualityUpdatePolicyAssignment
                        {
                            OdataType = "#microsoft.graph.windowsQualityUpdatePolicyAssignment",
                            Target = target
                        });
                    }

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .WindowsQualityUpdatePolicies[id]
                        .Assignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            if (existing.Target is AllLicensedUsersAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on Quality Update policy {id}. This should not exist and will be skipped.", LogLevels.Warning);
                                continue;
                            }
                            else if (existing.Target is AllDevicesAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Quality Update policy {id}. This should not exist and will be skipped.", LogLevels.Warning);
                                continue;
                            }
                            else if (existing.Target is GroupAssignmentTarget groupTarget)
                            {
                                var existingGroupId = groupTarget.GroupId;
                                if (!string.IsNullOrWhiteSpace(existingGroupId) && seenGroupIds.Add(existingGroupId))
                                {
                                    assignments.Add(existing);
                                }
                            }
                            else
                            {
                                assignments.Add(existing);
                            }
                        }
                    }

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdatePolicies.Item.Assign.AssignPostRequestBody
                    {
                        Assignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.WindowsQualityUpdatePolicies[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to Quality Update policy {id}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error assigning groups to policy {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while preparing assignment for policy {id}: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static async Task<List<WindowsQualityUpdatePolicy>> SearchForWindowsQualityUpdatePolicies(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var all = await _helper.GetAllAsync(graphServiceClient);
            return all.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static Task<List<WindowsQualityUpdatePolicy>> GetAllWindowsQualityUpdatePolicies(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleWindowsQualityUpdatePolicies(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policyIDs, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, policyIDs, assignments, filter, groups);

        public static Task AssignGroupsToSingleWindowsQualityUpdatePolicy(string policyID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(policyID, groupIDs, destinationGraphServiceClient);

        public static Task DeleteWindowsQualityUpdatePolicy(GraphServiceClient graphServiceClient, string policyID)
            => _helper.DeleteAsync(graphServiceClient, policyID);

        public static Task RenameWindowsQualityUpdatePolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
            => _helper.RenameAsync(graphServiceClient, policyID, newName);

        public static Task<List<CustomContentInfo>> GetAllWindowsQualityUpdatePolicyContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static async Task<List<CustomContentInfo>> SearchWindowsQualityUpdatePolicyContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var policies = await SearchForWindowsQualityUpdatePolicies(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();
            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Windows Quality Update Policy",
                    ContentPlatform = "Windows",
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }
            return content;
        }

        public static Task<JsonElement?> ExportWindowsQualityUpdatePolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.ExportDataAsync(graphServiceClient, policyId);

        public static Task<string?> ImportWindowsQualityUpdatePolicyFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasWindowsQualityUpdatePolicyAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.HasAssignmentsAsync(graphServiceClient, policyId);

        public static Task<List<AssignmentInfo>?> GetWindowsQualityUpdatePolicyAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, policyId);

        public static Task RemoveAllWindowsQualityUpdatePolicyAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, policyId);
    }
}
