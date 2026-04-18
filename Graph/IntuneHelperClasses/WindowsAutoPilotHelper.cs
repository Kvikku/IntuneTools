using IntuneTools.Utilities;
using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsAutoPilotHelper
    {
        private class Helper : GraphHelper<WindowsAutopilotDeploymentProfile, WindowsAutopilotDeploymentProfileCollectionResponse>
        {
            protected override string ResourceName => "Windows AutoPilot profiles";
            protected override string ContentTypeName => "Windows AutoPilot Profile";
            protected override string? FixedPlatform => "Windows";

            protected override string? GetPolicyName(WindowsAutopilotDeploymentProfile policy) => policy.DisplayName;
            protected override string? GetPolicyId(WindowsAutopilotDeploymentProfile policy) => policy.Id;
            protected override string? GetPolicyDescription(WindowsAutopilotDeploymentProfile policy) => policy.Description;

            protected override Task<WindowsAutopilotDeploymentProfileCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.WindowsAutopilotDeploymentProfiles.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

            protected override Task<WindowsAutopilotDeploymentProfileCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.WindowsAutopilotDeploymentProfiles.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

            protected override Task<WindowsAutopilotDeploymentProfile?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].DeleteAsync();

            /// <summary>
            /// AutoPilot profiles use polymorphic types (ActiveDirectory vs AzureAD) for patch operations.
            /// </summary>
            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var existing = await GetByIdAsync(client, id);
                if (existing == null) return;

                if (existing.OdataType?.Contains("activeDirectory", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var profile = new ActiveDirectoryWindowsAutopilotDeploymentProfile
                    {
                        OdataType = existing.OdataType,
                        DisplayName = newName
                    };
                    await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].PatchAsync(profile);
                }
                else
                {
                    var profile = new AzureADWindowsAutopilotDeploymentProfile
                    {
                        OdataType = existing.OdataType ?? "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile",
                        DisplayName = newName
                    };
                    await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].PatchAsync(profile);
                }
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var existing = await GetByIdAsync(client, id);
                if (existing == null) return;

                if (existing.OdataType?.Contains("activeDirectory", StringComparison.OrdinalIgnoreCase) == true)
                {
                    LogToFunctionFile(appFunction.Main, "Active Directory Autopilot profiles are not supported yet. Skipping.", LogLevels.Warning);
                    return;
                }

                var profile = new AzureADWindowsAutopilotDeploymentProfile
                {
                    OdataType = existing.OdataType ?? "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile",
                    Description = description,
                };
                await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].PatchAsync(profile);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exported = GraphImportHelper.DeserializeFromJson(policyData, WindowsAutopilotDeploymentProfile.CreateFromDiscriminatorValue);

                    if (exported == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize Windows AutoPilot profile data from JSON.", LogLevels.Error);
                        return null;
                    }

                    // Hybrid AD join profiles are not supported via Graph API
                    if (exported.OdataType != null && exported.OdataType.Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
                    {
                        LogToFunctionFile(appFunction.Main, $"Skipping Hybrid Azure AD join AutoPilot profile '{exported.DisplayName}' - not supported via Graph API.", LogLevels.Warning);
                        return null;
                    }

                    var newProfile = GraphImportHelper.CloneForImport(exported);

                    var imported = await client.DeviceManagement.WindowsAutopilotDeploymentProfiles.PostAsync(newProfile);

                    LogToFunctionFile(appFunction.Main, $"Imported Windows AutoPilot profile: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].Assignments.GetAsync(rc =>
                    {
                        rc.QueryParameters.Top = 1000;
                    });

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Windows AutoPilot Profile {id}");
                    return null;
                }
            }

            /// <summary>
            /// Removes all assignments using individual DELETE calls since AutoPilot does not support batch assignment removal.
            /// </summary>
            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var result = await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].Assignments.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

                while (result?.Value != null && result.Value.Count > 0)
                {
                    foreach (var assignment in result.Value)
                    {
                        await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].Assignments[assignment.Id].DeleteAsync();
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Windows AutoPilot Profile {id}.");
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
                        var result = await sourceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].GetAsync();

                        if (result == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping profile ID {id}: Not found in source tenant.");
                            return;
                        }

                        if (result.OdataType != null && result.OdataType.Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Hybrid Autopilot profiles are currently bugged in Graph API/C# SDK. Please handle manually for now.", LogLevels.Warning);
                            return;
                        }

                        var requestBody = GraphImportHelper.CloneForImport(result);

                        var import = await destinationClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Imported profile: {requestBody.DisplayName}");

                        if (assignments && groups != null && groups.Any())
                        {
                            await AssignGroupsToSingleWindowsAutoPilotProfile(import.Id, groups, destinationClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error importing profile {id}: {ex.Message}", LogLevels.Error);
                    }
                });
            }

            /// <summary>
            /// AutoPilot profiles cannot be assigned to 'All Users'. Only All Devices + regular groups are supported.
            /// Assignments are posted individually since AutoPilot profiles require individual POSTs.
            /// </summary>
            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<WindowsAutopilotDeploymentProfileAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var hasAllDevices = false;

                    foreach (var groupId in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                            continue;

                        // AutoPilot profiles cannot be assigned to All Users
                        if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: AutoPilot profiles cannot be assigned to 'All Users'. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        WindowsAutopilotDeploymentProfileAssignment assignment;

                        if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            hasAllDevices = true;
                            var target = new AllDevicesAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allDevicesAssignmentTarget"
                            };
                            GraphAssignmentHelper.ApplySelectedFilter(target);

                            assignment = new WindowsAutopilotDeploymentProfileAssignment
                            {
                                Source = DeviceAndAppManagementAssignmentSource.Direct,
                                SourceId = id,
                                Target = target
                            };
                        }
                        else
                        {
                            var target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                GroupId = groupId
                            };
                            GraphAssignmentHelper.ApplySelectedFilter(target);

                            assignment = new WindowsAutopilotDeploymentProfileAssignment
                            {
                                Source = DeviceAndAppManagementAssignmentSource.Direct,
                                SourceId = id,
                                Target = target
                            };
                        }

                        assignments.Add(assignment);
                    }

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .WindowsAutopilotDeploymentProfiles[id]
                        .Assignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            if (existing.Target is AllLicensedUsersAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on AutoPilot profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
                                continue;
                            }
                            else if (existing.Target is AllDevicesAssignmentTarget)
                            {
                                if (!hasAllDevices)
                                {
                                    assignments.Add(existing);
                                }
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

                    // Post assignments individually (AutoPilot profiles require individual posts)
                    int successCount = 0;
                    foreach (var assignment in assignments)
                    {
                        if (!string.IsNullOrEmpty(assignment.Id))
                        {
                            successCount++;
                            continue;
                        }

                        try
                        {
                            await client
                                .DeviceManagement
                                .WindowsAutopilotDeploymentProfiles[id]
                                .Assignments
                                .PostAsync(assignment);

                            successCount++;

                            string targetType = assignment.Target switch
                            {
                                AllDevicesAssignmentTarget => "All Devices",
                                GroupAssignmentTarget gt => $"group {gt.GroupId}",
                                _ => "unknown target"
                            };

                            LogToFunctionFile(appFunction.Main, $"Assigned {targetType} to AutoPilot profile {id}.");
                            UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                        }
                        catch (ODataError odataError)
                        {
                            LogToFunctionFile(appFunction.Main, $"Graph API error assigning to profile {id}: {odataError.Error?.Message}", LogLevels.Error);
                        }
                        catch (Exception ex)
                        {
                            LogToFunctionFile(appFunction.Main, $"Error assigning to profile {id}: {ex.Message}", LogLevels.Error);
                        }
                    }

                    LogToFunctionFile(appFunction.Main, $"Assigned {successCount} of {assignments.Count} assignments to AutoPilot profile {id}.");
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to a single Windows AutoPilot profile: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<WindowsAutopilotDeploymentProfile>> SearchForWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task<List<WindowsAutopilotDeploymentProfile>> GetAllWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleWindowsAutoPilotProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profiles, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, profiles, assignments, filter, groups);

        public static Task AssignGroupsToSingleWindowsAutoPilotProfile(string profileID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(profileID, groupID, destinationGraphServiceClient);

        public static Task<bool?> CheckIfAutoPilotProfileHasAssignments(GraphServiceClient graphServiceClient, string profileID)
            => _helper.HasAssignmentsAsync(graphServiceClient, profileID);

        public static Task DeleteWindowsAutoPilotProfileAssignments(GraphServiceClient graphServiceClient, string profileID)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, profileID);

        public static Task DeleteWindowsAutopilotProfile(GraphServiceClient graphServiceClient, string profileID)
            => _helper.DeleteAsync(graphServiceClient, profileID);

        public static Task RenameWindowsAutoPilotProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
            => _helper.RenameAsync(graphServiceClient, profileID, newName);

        public static Task<List<CustomContentInfo>> GetAllWindowsAutoPilotContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchWindowsAutoPilotContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportWindowsAutoPilotProfileDataAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.ExportDataAsync(graphServiceClient, profileId);

        public static Task<string?> ImportWindowsAutoPilotProfileFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<List<AssignmentInfo>?> GetWindowsAutoPilotAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, profileId);

        public static Task RemoveAllWindowsAutoPilotAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, profileId);
    }
}
