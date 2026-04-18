using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class macOSShellScript
    {
        private class Helper : GraphHelper<DeviceShellScript, DeviceShellScriptCollectionResponse>
        {
            protected override string ResourceName => "macOS shell scripts";
            protected override string ContentTypeName => "MacOS Shell Script";
            protected override string? FixedPlatform => "macOS";

            protected override string? GetPolicyName(DeviceShellScript policy) => policy.DisplayName;
            protected override string? GetPolicyId(DeviceShellScript policy) => policy.Id;
            protected override string? GetPolicyDescription(DeviceShellScript policy) => policy.Description;

            protected override Task<DeviceShellScriptCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.DeviceShellScripts.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

            protected override Task<DeviceShellScriptCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.DeviceShellScripts.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                    rc.QueryParameters.Top = 1000;
                });

            protected override Task<DeviceShellScript?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceShellScripts[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceShellScripts[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var script = new DeviceShellScript { DisplayName = newName };
                await client.DeviceManagement.DeviceShellScripts[id].PatchAsync(script);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var script = new DeviceShellScript { Description = description };
                await client.DeviceManagement.DeviceShellScripts[id].PatchAsync(script);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exported = GraphImportHelper.DeserializeFromJson(policyData, DeviceShellScript.CreateFromDiscriminatorValue);

                    if (exported == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize macOS shell script data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newScript = GraphImportHelper.CloneForImport(exported);

                    var imported = await client.DeviceManagement.DeviceShellScripts.PostAsync(newScript);

                    LogToFunctionFile(appFunction.Main, $"Imported macOS shell script: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.DeviceShellScripts[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.DeviceShellScripts[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.DeviceShellScripts[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"macOS Shell Script {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceShellScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceManagementScriptGroupAssignments = new List<DeviceManagementScriptGroupAssignment>()
                };

                await client.DeviceManagement.DeviceShellScripts[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from macOS Shell Script {id}.");
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
                    var sourceScript = await sourceClient.DeviceManagement.DeviceShellScripts[id].GetAsync();

                    if (sourceScript == null)
                    {
                        LogToFunctionFile(appFunction.Main, $"Script with ID {id} not found in source tenant. Skipping.");
                        return;
                    }

                    var newScript = GraphImportHelper.CloneForImport(sourceScript);

                    var importResult = await destinationClient.DeviceManagement.DeviceShellScripts.PostAsync(newScript);

                    if (importResult != null)
                    {
                        LogToFunctionFile(appFunction.Main, $"Imported script: {importResult.DisplayName} (ID: {importResult.Id})");

                        if (assignments && groups != null && groups.Any())
                        {
                            await AssignGroupsToSingleShellScriptmacOS(importResult.Id, groups, destinationClient);
                        }
                    }
                    else
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import script: {sourceScript.DisplayName} (ID: {id}). Result was null.");
                    }
                });
            }

            // Shell script assignments use DeviceManagementScriptGroupAssignment with TargetGroupId (not Target objects),
            // so we cannot use GraphAssignmentHelper.BuildAssignments. Custom assignment building is required.
            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<DeviceManagementScriptGroupAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var hasAllUsers = false;
                    var hasAllDevices = false;

                    LogToFunctionFile(appFunction.Main, $"Assigning {groupIds.Count} groups to macOS shell script {id}.");

                    foreach (var groupId in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                            continue;

                        if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            hasAllUsers = true;
                            assignments.Add(new DeviceManagementScriptGroupAssignment
                            {
                                OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                                TargetGroupId = allUsersVirtualGroupID
                            });
                        }
                        else if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            hasAllDevices = true;
                            assignments.Add(new DeviceManagementScriptGroupAssignment
                            {
                                OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                                TargetGroupId = allDevicesVirtualGroupID
                            });
                        }
                        else
                        {
                            assignments.Add(new DeviceManagementScriptGroupAssignment
                            {
                                OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                                TargetGroupId = groupId
                            });
                        }
                    }

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .DeviceShellScripts[id]
                        .GroupAssignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            var existingGroupId = existing.TargetGroupId;

                            if (string.IsNullOrWhiteSpace(existingGroupId))
                                continue;

                            if (existingGroupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!hasAllUsers)
                                    assignments.Add(existing);
                            }
                            else if (existingGroupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!hasAllDevices)
                                    assignments.Add(existing);
                            }
                            else
                            {
                                if (seenGroupIds.Add(existingGroupId))
                                    assignments.Add(existing);
                            }
                        }
                    }

                    if (!assignments.Any())
                    {
                        LogToFunctionFile(appFunction.Main, $"No valid group assignments to process for script {id}.");
                        return;
                    }

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceShellScripts.Item.Assign.AssignPostRequestBody
                    {
                        DeviceManagementScriptGroupAssignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.DeviceShellScripts[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to macOS shell script {id}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);

                        // Note: Filters are not directly supported in the Assign action for shell scripts
                        if (!string.IsNullOrEmpty(SelectedFilterID))
                        {
                            LogToFunctionFile(appFunction.Main, $"Filter application requested for script {id}, but direct filter assignment via Assign action is not supported for shell scripts. Manual verification/update might be needed.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to macOS shell script: {ex.Message}", LogLevels.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to macOS shell script: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<DeviceShellScript>> SearchForShellScriptmacOS(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task<List<DeviceShellScript>> GetAllmacOSShellScripts(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultiplemacOSShellScripts(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scriptIDs, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, scriptIDs, assignments, filter, groups);

        public static Task AssignGroupsToSingleShellScriptmacOS(string scriptId, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(scriptId, groupIDs, destinationGraphServiceClient);

        public static Task DeleteMacosShellScript(GraphServiceClient graphServiceClient, string profileID)
            => _helper.DeleteAsync(graphServiceClient, profileID);

        public static Task RenameMacOSShellScript(GraphServiceClient graphServiceClient, string scriptID, string newName)
            => _helper.RenameAsync(graphServiceClient, scriptID, newName);

        public static Task<List<CustomContentInfo>> GetAllMacOSShellScriptContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchMacOSShellScriptContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportMacOSShellScriptDataAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.ExportDataAsync(graphServiceClient, scriptId);

        public static Task<string?> ImportMacOSShellScriptFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasMacOSShellScriptAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.HasAssignmentsAsync(graphServiceClient, scriptId);

        public static Task<List<AssignmentInfo>?> GetMacOSShellScriptAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, scriptId);

        public static Task RemoveAllMacOSShellScriptAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, scriptId);
    }
}
