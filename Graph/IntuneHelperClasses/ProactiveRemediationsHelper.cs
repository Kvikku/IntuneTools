using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ProactiveRemediationsHelper
    {
        private class Helper : GraphHelper<DeviceHealthScript, DeviceHealthScriptCollectionResponse>
        {
            protected override string ResourceName => "proactive remediation scripts";
            protected override string ContentTypeName => "Proactive Remediation";
            protected override string? FixedPlatform => "Windows";

            protected override string? GetPolicyName(DeviceHealthScript policy) => policy.DisplayName;
            protected override string? GetPolicyId(DeviceHealthScript policy) => policy.Id;
            protected override string? GetPolicyDescription(DeviceHealthScript policy) => policy.Description;

            protected override Task<DeviceHealthScriptCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.DeviceHealthScripts.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

            protected override Task<DeviceHealthScriptCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.DeviceHealthScripts.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

            protected override Task<DeviceHealthScript?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceHealthScripts[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceHealthScripts[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var script = new DeviceHealthScript { DisplayName = newName };
                await client.DeviceManagement.DeviceHealthScripts[id].PatchAsync(script);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var script = new DeviceHealthScript { Description = description };
                await client.DeviceManagement.DeviceHealthScripts[id].PatchAsync(script);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exported = GraphImportHelper.DeserializeFromJson(policyData, DeviceHealthScript.CreateFromDiscriminatorValue);

                    if (exported == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize proactive remediation data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newScript = GraphImportHelper.CloneForImport(exported);

                    var imported = await client.DeviceManagement.DeviceHealthScripts.PostAsync(newScript);

                    LogToFunctionFile(appFunction.Main, $"Imported proactive remediation: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.DeviceHealthScripts[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.DeviceHealthScripts[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.DeviceHealthScripts[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Proactive Remediation {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceHealthScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceHealthScriptAssignments = new List<DeviceHealthScriptAssignment>()
                };

                await client.DeviceManagement.DeviceHealthScripts[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Proactive Remediation script {id}.");
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
                    var result = await sourceClient.DeviceManagement.DeviceHealthScripts[id].GetAsync();

                    var requestBody = GraphImportHelper.CloneForImport(result);

                    var import = await destinationClient.DeviceManagement.DeviceHealthScripts.PostAsync(requestBody);
                    LogToFunctionFile(appFunction.Main, $"Imported script: {import.DisplayName}");

                    if (assignments)
                    {
                        await AssignGroupsToSingleProactiveRemediation(import.Id, groups, destinationClient);
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

                    var assignments = new List<DeviceHealthScriptAssignment>();

                    var buildResult = GraphAssignmentHelper.BuildAssignments<DeviceHealthScriptAssignment>(
                        groupIds,
                        (target, groupId) =>
                        {
                            return new DeviceHealthScriptAssignment
                            {
                                OdataType = "#microsoft.graph.deviceHealthScriptAssignment",
                                Target = target
                            };
                        },
                        assignments);

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .DeviceHealthScripts[id]
                        .Assignments
                        .GetAsync();

                    GraphAssignmentHelper.MergeExistingAssignments(
                        existingAssignments?.Value,
                        assignments,
                        buildResult,
                        a => a.Target);

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceHealthScripts.Item.Assign.AssignPostRequestBody
                    {
                        DeviceHealthScriptAssignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.DeviceHealthScripts[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to proactive remediation script {id} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to proactive remediation script: {ex.Message}", LogLevels.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to proactive remediation script: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Filtering helper: exclude Microsoft-published scripts ──

        private static List<DeviceHealthScript> FilterOutMicrosoftPublished(List<DeviceHealthScript> scripts)
            => scripts.Where(s => !s.Publisher.Equals("Microsoft", StringComparison.OrdinalIgnoreCase)).ToList();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static async Task<List<DeviceHealthScript>> SearchForProactiveRemediations(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var results = await _helper.SearchAsync(graphServiceClient, searchQuery);
            return FilterOutMicrosoftPublished(results);
        }

        public static async Task<List<DeviceHealthScript>> GetAllProactiveRemediations(GraphServiceClient graphServiceClient)
        {
            var all = await _helper.GetAllAsync(graphServiceClient);
            return FilterOutMicrosoftPublished(all);
        }

        public static Task ImportMultipleProactiveRemediations(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scripts, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, scripts, assignments, filter, groups);

        public static Task AssignGroupsToSingleProactiveRemediation(string scriptID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(scriptID, groupID, destinationGraphServiceClient);

        public static Task DeleteProactiveRemediationScript(GraphServiceClient graphServiceClient, string policyID)
            => _helper.DeleteAsync(graphServiceClient, policyID);

        public static Task RenameProactiveRemediation(GraphServiceClient graphServiceClient, string scriptID, string newName)
            => _helper.RenameAsync(graphServiceClient, scriptID, newName);

        public static async Task<List<CustomContentInfo>> GetAllProactiveRemediationContentAsync(GraphServiceClient graphServiceClient)
        {
            var scripts = await GetAllProactiveRemediations(graphServiceClient);
            return scripts.Select(s => new CustomContentInfo
            {
                ContentName = s.DisplayName,
                ContentType = "Proactive Remediation",
                ContentPlatform = "Windows",
                ContentId = s.Id,
                ContentDescription = s.Description
            }).ToList();
        }

        public static async Task<List<CustomContentInfo>> SearchProactiveRemediationContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var scripts = await SearchForProactiveRemediations(graphServiceClient, searchQuery);
            return scripts.Select(s => new CustomContentInfo
            {
                ContentName = s.DisplayName,
                ContentType = "Proactive Remediation",
                ContentPlatform = "Windows",
                ContentId = s.Id,
                ContentDescription = s.Description
            }).ToList();
        }

        public static Task<JsonElement?> ExportProactiveRemediationDataAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.ExportDataAsync(graphServiceClient, scriptId);

        public static Task<string?> ImportProactiveRemediationFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasProactiveRemediationAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.HasAssignmentsAsync(graphServiceClient, scriptId);

        public static Task<List<AssignmentInfo>?> GetProactiveRemediationAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, scriptId);

        public static Task RemoveAllProactiveRemediationAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, scriptId);
    }
}
