using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsDriverUpdateHelper
    {
        private class Helper : GraphHelper<WindowsDriverUpdateProfile, WindowsDriverUpdateProfileCollectionResponse>
        {
            protected override string ResourceName => "Windows Driver Update profiles";
            protected override string ContentTypeName => "Windows Driver Update";
            protected override string? FixedPlatform => "Windows";

            protected override string? GetPolicyName(WindowsDriverUpdateProfile policy) => policy.DisplayName;
            protected override string? GetPolicyId(WindowsDriverUpdateProfile policy) => policy.Id;
            protected override string? GetPolicyDescription(WindowsDriverUpdateProfile policy) => policy.Description;

            protected override Task<WindowsDriverUpdateProfileCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.WindowsDriverUpdateProfiles.GetAsync();

            // No server-side filter support; client-side filtering is applied in the public static method
            protected override Task<WindowsDriverUpdateProfileCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.WindowsDriverUpdateProfiles.GetAsync();

            protected override Task<WindowsDriverUpdateProfile?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsDriverUpdateProfiles[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsDriverUpdateProfiles[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var profile = new WindowsDriverUpdateProfile { DisplayName = newName };
                await client.DeviceManagement.WindowsDriverUpdateProfiles[id].PatchAsync(profile);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var profile = new WindowsDriverUpdateProfile { Description = description };
                await client.DeviceManagement.WindowsDriverUpdateProfiles[id].PatchAsync(profile);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exportedProfile = GraphImportHelper.DeserializeFromJson(policyData, WindowsDriverUpdateProfile.CreateFromDiscriminatorValue);

                    if (exportedProfile == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize Windows Driver Update profile data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newProfile = new WindowsDriverUpdateProfile
                    {
                        OdataType = "#microsoft.graph.windowsDriverUpdateProfile",
                        DisplayName = exportedProfile.DisplayName,
                        Description = exportedProfile.Description,
                        ApprovalType = exportedProfile.ApprovalType,
                        RoleScopeTagIds = exportedProfile.RoleScopeTagIds,
                        DeploymentDeferralInDays = exportedProfile.DeploymentDeferralInDays
                    };

                    var imported = await client.DeviceManagement.WindowsDriverUpdateProfiles.PostAsync(newProfile);

                    LogToFunctionFile(appFunction.Main, $"Imported Windows Driver Update profile: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.WindowsDriverUpdateProfiles[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.WindowsDriverUpdateProfiles[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.WindowsDriverUpdateProfiles[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Windows Driver Update {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsDriverUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = new List<WindowsDriverUpdateProfileAssignment>()
                };

                await client.DeviceManagement.WindowsDriverUpdateProfiles[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Windows Driver Update profile {id}.");
            }

            public override async Task ImportMultipleAsync(
                GraphServiceClient sourceClient,
                GraphServiceClient destinationClient,
                List<string> ids,
                bool assignments,
                bool filter,
                List<string> groups)
            {
                try
                {
                    LogToFunctionFile(appFunction.Main, $"Importing {ids.Count} {ResourceName}.");

                    foreach (var id in ids)
                    {
                        var profileName = "";
                        try
                        {
                            var sourceProfile = await sourceClient.DeviceManagement.WindowsDriverUpdateProfiles[id].GetAsync();

                            if (sourceProfile == null)
                            {
                                LogToFunctionFile(appFunction.Main, $"Profile with ID {id} not found in source tenant.");
                                continue;
                            }

                            profileName = sourceProfile.DisplayName ?? "Unknown Profile";

                            var newProfile = new WindowsDriverUpdateProfile
                            {
                                OdataType = "#microsoft.graph.windowsDriverUpdateProfile",
                                DisplayName = sourceProfile.DisplayName,
                                Description = sourceProfile.Description,
                                ApprovalType = sourceProfile.ApprovalType,
                                RoleScopeTagIds = sourceProfile.RoleScopeTagIds,
                                DeploymentDeferralInDays = sourceProfile.DeploymentDeferralInDays
                            };

                            var importResult = await destinationClient.DeviceManagement.WindowsDriverUpdateProfiles.PostAsync(newProfile);
                            LogToFunctionFile(appFunction.Main, $"Imported profile: {importResult?.DisplayName ?? "Unknown"}");

                            if (assignments && importResult?.Id != null)
                            {
                                await AssignGroupsToSingleDriverProfile(importResult.Id, groups, destinationClient);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToFunctionFile(appFunction.Main, $"Failed to import Windows Driver Update policy {profileName}: {ex.Message}", LogLevels.Error);
                            LogToFunctionFile(appFunction.Main, "This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogLevels.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "during import process for", ResourceName);
                }
            }

            // Driver Update profiles can ONLY be assigned to device groups - not All Users or All Devices
            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<WindowsDriverUpdateProfileAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var groupId in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                            continue;

                        if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Driver Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Driver Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        var target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = groupId
                        };
                        GraphAssignmentHelper.ApplySelectedFilter(target);

                        assignments.Add(new WindowsDriverUpdateProfileAssignment
                        {
                            OdataType = "#microsoft.graph.windowsDriverUpdateProfileAssignment",
                            Target = target
                        });
                    }

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .WindowsDriverUpdateProfiles[id]
                        .Assignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            if (existing.Target is AllLicensedUsersAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on Driver Update profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
                                continue;
                            }
                            else if (existing.Target is AllDevicesAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Driver Update profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
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

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsDriverUpdateProfiles.Item.Assign.AssignPostRequestBody
                    {
                        Assignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.WindowsDriverUpdateProfiles[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to driver profile {id}. Filter: {SelectedFilterID ?? "None"}");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error assigning groups to profile {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An unexpected error occurred while preparing group assignments for a driver profile: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static async Task<List<WindowsDriverUpdateProfile>> SearchForDriverProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var all = await _helper.SearchAsync(graphServiceClient, searchQuery);
            return all.Where(p => !string.IsNullOrEmpty(p.DisplayName) && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static Task<List<WindowsDriverUpdateProfile>> GetAllDriverProfiles(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleDriverProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIds, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, profileIds, assignments, filter, groups);

        public static Task AssignGroupsToSingleDriverProfile(string profileID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(profileID, groupIDs, destinationGraphServiceClient);

        public static Task DeleteDriverProfile(GraphServiceClient graphServiceClient, string profileID)
            => _helper.DeleteAsync(graphServiceClient, profileID);

        public static Task RenameDriverProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
            => _helper.RenameAsync(graphServiceClient, profileID, newName);

        public static Task<List<CustomContentInfo>> GetAllWindowsDriverUpdateContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static async Task<List<CustomContentInfo>> SearchWindowsDriverUpdateContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var profiles = await SearchForDriverProfiles(graphServiceClient, searchQuery);
            return profiles.Select(p => new CustomContentInfo
            {
                ContentName = p.DisplayName,
                ContentType = "Windows Driver Update",
                ContentPlatform = "Windows",
                ContentId = p.Id,
                ContentDescription = p.Description
            }).ToList();
        }

        public static Task<JsonElement?> ExportWindowsDriverUpdatePolicyDataAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.ExportDataAsync(graphServiceClient, profileId);

        public static Task<string?> ImportWindowsDriverUpdateFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasWindowsDriverUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.HasAssignmentsAsync(graphServiceClient, profileId);

        public static Task<List<AssignmentInfo>?> GetWindowsDriverUpdateAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, profileId);

        public static Task RemoveAllWindowsDriverUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, profileId);
    }
}
