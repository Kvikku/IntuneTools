using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsQualityUpdateProfileHelper
    {
        private class Helper : GraphHelper<WindowsQualityUpdateProfile, WindowsQualityUpdateProfileCollectionResponse>
        {
            protected override string ResourceName => "Windows Quality Update profiles";
            protected override string ContentTypeName => "Windows Quality Update Profile";
            protected override string? FixedPlatform => "Windows";

            protected override string? GetPolicyName(WindowsQualityUpdateProfile policy) => policy.DisplayName;
            protected override string? GetPolicyId(WindowsQualityUpdateProfile policy) => policy.Id;
            protected override string? GetPolicyDescription(WindowsQualityUpdateProfile policy) => policy.Description;

            protected override Task<WindowsQualityUpdateProfileCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.WindowsQualityUpdateProfiles.GetAsync();

            // No server-side filter support; client-side filtering is done in the public static methods
            protected override Task<WindowsQualityUpdateProfileCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.WindowsQualityUpdateProfiles.GetAsync();

            protected override Task<WindowsQualityUpdateProfile?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsQualityUpdateProfiles[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.WindowsQualityUpdateProfiles[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var profile = new WindowsQualityUpdateProfile { DisplayName = newName };
                await client.DeviceManagement.WindowsQualityUpdateProfiles[id].PatchAsync(profile);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var profile = new WindowsQualityUpdateProfile { Description = description };
                await client.DeviceManagement.WindowsQualityUpdateProfiles[id].PatchAsync(profile);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exportedProfile = GraphImportHelper.DeserializeFromJson(policyData, WindowsQualityUpdateProfile.CreateFromDiscriminatorValue);

                    if (exportedProfile == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize Windows Quality Update profile data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newProfile = new WindowsQualityUpdateProfile();
                    GraphImportHelper.CopyProperties(exportedProfile, newProfile, new[] { "Assignments", "AdditionalData", "BackingStore" });
                    newProfile.OdataType = "#microsoft.graph.windowsQualityUpdateProfile";

                    var imported = await client.DeviceManagement.WindowsQualityUpdateProfiles.PostAsync(newProfile);

                    LogToFunctionFile(appFunction.Main, $"Imported Windows Quality Update profile: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.WindowsQualityUpdateProfiles[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.WindowsQualityUpdateProfiles[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.WindowsQualityUpdateProfiles[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Windows Quality Update Profile {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = new List<WindowsQualityUpdateProfileAssignment>()
                };

                await client.DeviceManagement.WindowsQualityUpdateProfiles[id].Assign.PostAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Windows Quality Update Profile {id}.");
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
                        var sourceProfile = await sourceClient.DeviceManagement.WindowsQualityUpdateProfiles[id].GetAsync();

                        if (sourceProfile == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping profile ID {id}: Not found in source tenant.");
                            return;
                        }

                        var newProfile = new WindowsQualityUpdateProfile();
                        GraphImportHelper.CopyProperties(sourceProfile, newProfile, new[] { "Assignments" });
                        newProfile.OdataType = "#microsoft.graph.windowsQualityUpdateProfile";

                        var importedProfile = await destinationClient.DeviceManagement.WindowsQualityUpdateProfiles.PostAsync(newProfile);

                        LogToFunctionFile(appFunction.Main, $"Imported profile: {importedProfile?.DisplayName ?? "Unnamed Profile"} (ID: {importedProfile?.Id ?? "Unknown ID"})");

                        if (assignments && groups != null && groups.Any() && importedProfile?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsQualityUpdateProfile(importedProfile.Id, groups, destinationClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error importing profile: {ex.Message}", LogLevels.Error);
                        LogToFunctionFile(appFunction.Main, "There is currently a known bug with importing Windows Quality Update profiles. " +
                                                "This will be fixed in a future release. " +
                                                "For now, please manually assign the groups to the imported profiles.", LogLevels.Warning);
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

                    var assignments = new List<WindowsQualityUpdateProfileAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var groupId in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                            continue;

                        if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        var target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = groupId
                        };
                        GraphAssignmentHelper.ApplySelectedFilter(target);

                        assignments.Add(new WindowsQualityUpdateProfileAssignment
                        {
                            OdataType = "#microsoft.graph.windowsQualityUpdateProfileAssignment",
                            Target = target
                        });
                    }

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .WindowsQualityUpdateProfiles[id]
                        .Assignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            if (existing.Target is AllLicensedUsersAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on Quality Update profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
                                continue;
                            }
                            else if (existing.Target is AllDevicesAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Quality Update profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
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

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdateProfiles.Item.Assign.AssignPostRequestBody
                    {
                        Assignments = assignments
                    };

                    try
                    {
                        await client.DeviceManagement.WindowsQualityUpdateProfiles[id].Assign.PostAsync(requestBody);
                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to Quality Update profile {id}.");
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

        public static async Task<List<WindowsQualityUpdateProfile>> SearchForWindowsQualityUpdateProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var all = await _helper.GetAllAsync(graphServiceClient);
            return all.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static Task<List<WindowsQualityUpdateProfile>> GetAllWindowsQualityUpdateProfiles(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleWindowsQualityUpdateProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIDs, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, profileIDs, assignments, filter, groups);

        public static Task AssignGroupsToSingleWindowsQualityUpdateProfile(string profileID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(profileID, groupIDs, destinationGraphServiceClient);

        public static Task DeleteWindowsQualityUpdateProfile(GraphServiceClient graphServiceClient, string profileID)
            => _helper.DeleteAsync(graphServiceClient, profileID);

        public static Task RenameWindowsQualityUpdateProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
            => _helper.RenameAsync(graphServiceClient, profileID, newName);

        public static Task<List<CustomContentInfo>> GetAllWindowsQualityUpdateProfileContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static async Task<List<CustomContentInfo>> SearchWindowsQualityUpdateProfileContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var profiles = await SearchForWindowsQualityUpdateProfiles(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();
            foreach (var profile in profiles)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows Quality Update Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }
            return content;
        }

        public static Task<JsonElement?> ExportWindowsQualityUpdateProfileDataAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.ExportDataAsync(graphServiceClient, profileId);

        public static Task<string?> ImportWindowsQualityUpdateProfileFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasWindowsQualityUpdateProfileAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.HasAssignmentsAsync(graphServiceClient, profileId);

        public static Task<List<AssignmentInfo>?> GetWindowsQualityUpdateProfileAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, profileId);

        public static Task RemoveAllWindowsQualityUpdateProfileAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, profileId);
    }
}
