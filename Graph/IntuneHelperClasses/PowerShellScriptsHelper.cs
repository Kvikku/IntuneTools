using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class PowerShellScriptsHelper
    {
        private class Helper : GraphHelper<DeviceManagementScript, DeviceManagementScriptCollectionResponse>
        {
            protected override string ResourceName => "PowerShell scripts";
            protected override string ContentTypeName => "PowerShell Script";
            protected override string? FixedPlatform => "Windows";

            protected override string? GetPolicyName(DeviceManagementScript policy) => policy.DisplayName;
            protected override string? GetPolicyId(DeviceManagementScript policy) => policy.Id;
            protected override string? GetPolicyDescription(DeviceManagementScript policy) => policy.Description;

            protected override Task<DeviceManagementScriptCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.DeviceManagementScripts.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

            protected override Task<DeviceManagementScriptCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.DeviceManagementScripts.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

            protected override Task<DeviceManagementScript?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceManagementScripts[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceManagementScripts[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var script = new DeviceManagementScript { DisplayName = newName };
                await client.DeviceManagement.DeviceManagementScripts[id].PatchAsync(script);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var script = new DeviceManagementScript { Description = description };
                await client.DeviceManagement.DeviceManagementScripts[id].PatchAsync(script);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exported = GraphImportHelper.DeserializeFromJson(policyData, DeviceManagementScript.CreateFromDiscriminatorValue);

                    if (exported == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize PowerShell script data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newScript = GraphImportHelper.CloneForImport(exported);

                    var imported = await client.DeviceManagement.DeviceManagementScripts.PostAsync(newScript);

                    LogToFunctionFile(appFunction.Main, $"Imported PowerShell script: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.DeviceManagementScripts[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.DeviceManagementScripts[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.DeviceManagementScripts[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"PowerShell Script {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceManagementScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceManagementScriptAssignments = new List<DeviceManagementScriptAssignment>()
                };

                await client.DeviceManagement.DeviceManagementScripts[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from PowerShell Script {id}.");
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
                    var result = await sourceClient.DeviceManagement.DeviceManagementScripts[id].GetAsync();

                    var requestBody = GraphImportHelper.CloneForImport(result);

                    var import = await destinationClient.DeviceManagement.DeviceManagementScripts.PostAsync(requestBody);
                    LogToFunctionFile(appFunction.Main, $"Imported script: {requestBody.DisplayName}");

                    if (assignments)
                    {
                        await AssignGroupsToSinglePowerShellScript(import.Id, groups, destinationClient);
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

                    var assignments = new List<DeviceManagementScriptAssignment>();

                    var buildResult = GraphAssignmentHelper.BuildAssignments<DeviceManagementScriptAssignment>(
                        groupIds,
                        (target, groupId) =>
                        {
                            return new DeviceManagementScriptAssignment
                            {
                                OdataType = "#microsoft.graph.deviceManagementScriptAssignment",
                                Target = target
                            };
                        },
                        assignments);

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .DeviceManagementScripts[id]
                        .Assignments
                        .GetAsync();

                    GraphAssignmentHelper.MergeExistingAssignments(
                        existingAssignments?.Value,
                        assignments,
                        buildResult,
                        a => a.Target);

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceManagementScripts.Item.Assign.AssignPostRequestBody
                    {
                        DeviceManagementScriptAssignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.DeviceManagementScripts[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to script {id} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to PowerShell script: {ex.Message}", LogLevels.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to PowerShell script: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<DeviceManagementScript>> SearchForPowerShellScripts(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task<List<DeviceManagementScript>> GetAllPowerShellScripts(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultiplePowerShellScripts(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scripts, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, scripts, assignments, filter, groups);

        public static Task AssignGroupsToSinglePowerShellScript(string scriptID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(scriptID, groupID, destinationGraphServiceClient);

        public static Task DeletePowerShellScript(GraphServiceClient graphServiceClient, string scriptID)
            => _helper.DeleteAsync(graphServiceClient, scriptID);

        public static Task RenamePowerShellScript(GraphServiceClient graphServiceClient, string scriptID, string newName)
            => _helper.RenameAsync(graphServiceClient, scriptID, newName);

        public static Task<List<CustomContentInfo>> GetAllPowerShellScriptContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchPowerShellScriptContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportPowerShellScriptDataAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.ExportDataAsync(graphServiceClient, scriptId);

        public static Task<string?> ImportPowerShellScriptFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasPowerShellScriptAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.HasAssignmentsAsync(graphServiceClient, scriptId);

        public static Task<List<AssignmentInfo>?> GetPowerShellScriptAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, scriptId);

        public static Task RemoveAllPowerShellScriptAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, scriptId);
    }
}
