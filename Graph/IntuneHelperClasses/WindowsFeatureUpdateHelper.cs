using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsFeatureUpdateHelper
    {
        private class Helper : GraphHelper<WindowsFeatureUpdateProfile, WindowsFeatureUpdateProfileCollectionResponse>
        {
            protected override string ResourceName => "Windows Feature Update profiles";
            protected override string ContentTypeName => "Windows Feature Update";
            protected override string? FixedPlatform => "Windows";

            protected override string? GetPolicyName(WindowsFeatureUpdateProfile policy) => policy.DisplayName;
            protected override string? GetPolicyId(WindowsFeatureUpdateProfile policy) => policy.Id;
            protected override string? GetPolicyDescription(WindowsFeatureUpdateProfile policy) => policy.Description;

            protected override Task<WindowsFeatureUpdateProfileCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.WindowsFeatureUpdateProfiles.GetAsync();

            // No server-side filter support; client-side filtering is applied in the public static method
            protected override Task<WindowsFeatureUpdateProfileCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.WindowsFeatureUpdateProfiles.GetAsync();

            protected override Task<WindowsFeatureUpdateProfile?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsFeatureUpdateProfiles[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsFeatureUpdateProfiles[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var profile = new WindowsFeatureUpdateProfile { DisplayName = newName };
                await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].PatchAsync(profile);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var profile = new WindowsFeatureUpdateProfile { Description = description };
                await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].PatchAsync(profile);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exportedProfile = GraphImportHelper.DeserializeFromJson(policyData, WindowsFeatureUpdateProfile.CreateFromDiscriminatorValue);

                    if (exportedProfile == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize Windows Feature Update profile data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newProfile = new WindowsFeatureUpdateProfile
                    {
                        OdataType = "#microsoft.graph.windowsFeatureUpdateProfile",
                        DisplayName = exportedProfile.DisplayName,
                        Description = exportedProfile.Description,
                        FeatureUpdateVersion = exportedProfile.FeatureUpdateVersion,
                        RoleScopeTagIds = exportedProfile.RoleScopeTagIds,
                        RolloutSettings = exportedProfile.RolloutSettings,
                    };

                    var imported = await client.DeviceManagement.WindowsFeatureUpdateProfiles.PostAsync(newProfile);

                    LogToFunctionFile(appFunction.Main, $"Imported Windows Feature Update profile: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.WindowsFeatureUpdateProfiles[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Windows Feature Update {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsFeatureUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = new List<WindowsFeatureUpdateProfileAssignment>()
                };

                await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Windows Feature Update profile {id}.");
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
                            var sourceProfile = await sourceClient.DeviceManagement.WindowsFeatureUpdateProfiles[id].GetAsync();

                            if (sourceProfile == null)
                            {
                                LogToFunctionFile(appFunction.Main, $"Skipping profile ID {id}: Not found in source tenant.");
                                continue;
                            }

                            profileName = sourceProfile.DisplayName ?? "Unnamed Profile";

                            var newProfile = new WindowsFeatureUpdateProfile();
                            GraphImportHelper.CopyProperties(sourceProfile, newProfile);
                            newProfile.OdataType = "#microsoft.graph.windowsFeatureUpdateProfile";

                            var importedProfile = await destinationClient.DeviceManagement.WindowsFeatureUpdateProfiles.PostAsync(newProfile);
                            LogToFunctionFile(appFunction.Main, $"Imported profile: {importedProfile?.DisplayName ?? "Unnamed Profile"} (ID: {importedProfile?.Id ?? "Unknown ID"})");

                            if (assignments && groups != null && groups.Any() && importedProfile?.Id != null)
                            {
                                await AssignGroupsToSingleWindowsFeatureUpdateProfile(importedProfile.Id, groups, destinationClient);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToFunctionFile(appFunction.Main, $"Failed to import Windows Feature Update profile {profileName}: {ex.Message}", LogLevels.Error);
                            LogToFunctionFile(appFunction.Main, "This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogLevels.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "during import process for", ResourceName);
                }
            }

            // Feature Update profiles can ONLY be assigned to device groups - not All Users or All Devices
            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<WindowsFeatureUpdateProfileAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var groupId in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                            continue;

                        if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Feature Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Feature Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        var target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = groupId
                        };
                        GraphAssignmentHelper.ApplySelectedFilter(target);

                        assignments.Add(new WindowsFeatureUpdateProfileAssignment
                        {
                            OdataType = "#microsoft.graph.windowsFeatureUpdateProfileAssignment",
                            Target = target
                        });
                    }

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .WindowsFeatureUpdateProfiles[id]
                        .Assignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            if (existing.Target is AllLicensedUsersAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on Feature Update profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
                                continue;
                            }
                            else if (existing.Target is AllDevicesAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Feature Update profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
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

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsFeatureUpdateProfiles.Item.Assign.AssignPostRequestBody
                    {
                        Assignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to Feature Update profile {id}");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error assigning groups to profile {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while preparing assignment for profile {id}: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static async Task<List<WindowsFeatureUpdateProfile>> SearchForWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var all = await _helper.GetAllAsync(graphServiceClient);
            return all.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static Task<List<WindowsFeatureUpdateProfile>> GetAllWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleWindowsFeatureUpdateProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIDs, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, profileIDs, assignments, filter, groups);

        public static Task AssignGroupsToSingleWindowsFeatureUpdateProfile(string profileID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(profileID, groupIDs, destinationGraphServiceClient);

        public static Task DeleteWindowsFeatureUpdateProfile(GraphServiceClient graphServiceClient, string profileID)
            => _helper.DeleteAsync(graphServiceClient, profileID);

        public static Task RenameWindowsFeatureUpdateProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
            => _helper.RenameAsync(graphServiceClient, profileID, newName);

        public static Task<List<CustomContentInfo>> GetAllWindowsFeatureUpdateContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static async Task<List<CustomContentInfo>> SearchWindowsFeatureUpdateContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var profiles = await SearchForWindowsFeatureUpdateProfiles(graphServiceClient, searchQuery);
            return profiles.Select(p => new CustomContentInfo
            {
                ContentName = p.DisplayName,
                ContentType = "Windows Feature Update",
                ContentPlatform = "Windows",
                ContentId = p.Id,
                ContentDescription = p.Description
            }).ToList();
        }

        public static Task<JsonElement?> ExportWindowsFeatureUpdatePolicyDataAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.ExportDataAsync(graphServiceClient, profileId);

        public static Task<string?> ImportWindowsFeatureUpdateFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasWindowsFeatureUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.HasAssignmentsAsync(graphServiceClient, profileId);

        public static Task<List<AssignmentInfo>?> GetWindowsFeatureUpdateAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, profileId);

        public static Task RemoveAllWindowsFeatureUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, profileId);
    }
}
