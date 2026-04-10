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
    public class AppleBYODEnrollmentProfileHelper
    {
        private class Helper : GraphHelper<AppleUserInitiatedEnrollmentProfile, AppleUserInitiatedEnrollmentProfileCollectionResponse>
        {
            protected override string ResourceName => "Apple BYOD enrollment profiles";
            protected override string ContentTypeName => "Apple BYOD Enrollment Profile";

            protected override string? GetPolicyPlatform(AppleUserInitiatedEnrollmentProfile policy)
                => HelperClass.TranslatePolicyPlatformName(policy.Platform.ToString());

            protected override string? GetPolicyName(AppleUserInitiatedEnrollmentProfile policy) => policy.DisplayName;
            protected override string? GetPolicyId(AppleUserInitiatedEnrollmentProfile policy) => policy.Id;
            protected override string? GetPolicyDescription(AppleUserInitiatedEnrollmentProfile policy) => policy.Description;

            protected override Task<AppleUserInitiatedEnrollmentProfileCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 999;
                });

            protected override Task<AppleUserInitiatedEnrollmentProfileCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(DisplayName,'{searchQuery}')";
                });

            protected override Task<AppleUserInitiatedEnrollmentProfile?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var existing = await GetByIdAsync(client, id);
                if (existing == null) return;

                var profileType = existing.GetType();
                var profile = (AppleUserInitiatedEnrollmentProfile?)Activator.CreateInstance(profileType);
                if (profile == null) return;

                profile.DisplayName = newName;
                await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].PatchAsync(profile);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var existing = await GetByIdAsync(client, id);
                if (existing == null) return;

                var profileType = existing.GetType();
                var profile = (AppleUserInitiatedEnrollmentProfile?)Activator.CreateInstance(profileType);
                if (profile == null) return;

                profile.Description = description;
                await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].PatchAsync(profile);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exported = GraphImportHelper.DeserializeFromJson(policyData, AppleUserInitiatedEnrollmentProfile.CreateFromDiscriminatorValue);

                    if (exported == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize Apple BYOD enrollment profile data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newProfile = new AppleUserInitiatedEnrollmentProfile();
                    GraphImportHelper.CopyProperties(exported, newProfile);

                    var imported = await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.PostAsync(newProfile);

                    LogToFunctionFile(appFunction.Main, $"Imported Apple BYOD enrollment profile: {imported?.DisplayName}");
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
                    var result = await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].Assignments.GetAsync(rc =>
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
                    var result = await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Apple BYOD Profile {id}");
                    return null;
                }
            }

            /// <summary>
            /// Removes all assignments using individual DELETE calls since Apple enrollment profiles
            /// don't support batch assignment removal.
            /// </summary>
            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var result = await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].Assignments.GetAsync();

                while (result?.Value != null && result.Value.Count > 0)
                {
                    foreach (var assignment in result.Value)
                    {
                        await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].Assignments[assignment.Id].DeleteAsync();
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Apple BYOD Enrollment Profile {id}.");
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
                    var profileName = string.Empty;
                    try
                    {
                        var sourceProfile = await sourceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].GetAsync();

                        if (sourceProfile == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping profile ID {id}: Not found in source tenant.");
                            return;
                        }

                        profileName = sourceProfile.DisplayName ?? "Unknown Profile";

                        var newProfile = new AppleUserInitiatedEnrollmentProfile();
                        GraphImportHelper.CopyProperties(sourceProfile, newProfile);

                        var importedProfile = await destinationClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.PostAsync(newProfile);

                        LogToFunctionFile(appFunction.Main, $"Successfully imported {importedProfile.DisplayName}");

                        if (assignments && groups != null && groups.Any())
                        {
                            await AssignGroupsToSingleAppleBYODEnrollmentProfile(importedProfile.Id, groups, destinationClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import {profileName}: {ex.Message}", LogLevels.Error);
                    }
                });
            }

            /// <summary>
            /// Apple BYOD Enrollment profiles support All Users and regular user groups, but NOT All Devices.
            /// Assignments are posted individually since Apple enrollment profiles don't support batch assign.
            /// </summary>
            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<AppleEnrollmentProfileAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var hasAllUsers = false;

                    foreach (var groupId in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                            continue;

                        // Apple BYOD Enrollment profiles cannot be assigned to All Devices
                        if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Warning: Apple BYOD Enrollment profiles cannot be assigned to 'All Devices'. Only All Users and user groups are supported. Skipping this assignment.", LogLevels.Warning);
                            continue;
                        }

                        AppleEnrollmentProfileAssignment assignment;

                        if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            hasAllUsers = true;
                            var target = new AllLicensedUsersAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget"
                            };
                            GraphAssignmentHelper.ApplySelectedFilter(target);

                            assignment = new AppleEnrollmentProfileAssignment
                            {
                                OdataType = "#microsoft.graph.appleEnrollmentProfileAssignment",
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

                            assignment = new AppleEnrollmentProfileAssignment
                            {
                                OdataType = "#microsoft.graph.appleEnrollmentProfileAssignment",
                                Target = target
                            };
                        }

                        assignments.Add(assignment);
                    }

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .AppleUserInitiatedEnrollmentProfiles[id]
                        .Assignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            if (existing.Target is AllLicensedUsersAssignmentTarget)
                            {
                                if (!hasAllUsers)
                                {
                                    assignments.Add(existing);
                                }
                            }
                            else if (existing.Target is AllDevicesAssignmentTarget)
                            {
                                LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Apple BYOD Enrollment profile {id}. This should not exist and will be skipped.", LogLevels.Warning);
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

                    // Post assignments individually (Apple enrollment profiles don't support batch assign)
                    foreach (var assignment in assignments)
                    {
                        // Skip existing assignments that were already posted
                        if (!string.IsNullOrEmpty(assignment.Id))
                        {
                            continue;
                        }

                        try
                        {
                            await client
                                .DeviceManagement
                                .AppleUserInitiatedEnrollmentProfiles[id]
                                .Assignments
                                .PostAsync(assignment);

                            string targetType = assignment.Target switch
                            {
                                AllLicensedUsersAssignmentTarget => "All Users",
                                GroupAssignmentTarget gt => $"group {gt.GroupId}",
                                _ => "unknown target"
                            };

                            string filterInfo = !string.IsNullOrEmpty(SelectedFilterID)
                                ? $" with filter ID {SelectedFilterID} (Type: {deviceAndAppManagementAssignmentFilterType})"
                                : "";

                            LogToFunctionFile(appFunction.Main, $"Assigned {targetType} to profile {id}{filterInfo}.");
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

                    LogToFunctionFile(appFunction.Main, $"Completed assignment process for profile {id}. Total assignments processed: {assignments.Count}");
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An unexpected error occurred while preparing group assignments for profile ID {id}: {ex.Message}", LogLevels.Error);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<AppleUserInitiatedEnrollmentProfile>> SearchForAppleBYODEnrollmentProfiles(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task<List<AppleUserInitiatedEnrollmentProfile>> GetAllAppleBYODEnrollmentProfiles(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleAppleBYODEnrollmentProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIds, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, profileIds, assignments, filter, groups);

        public static Task AssignGroupsToSingleAppleBYODEnrollmentProfile(string profileId, List<string> groupIds, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(profileId, groupIds, destinationGraphServiceClient);

        public static Task DeleteAppleBYODEnrollmentProfile(GraphServiceClient graphServiceClient, string profileID)
            => _helper.DeleteAsync(graphServiceClient, profileID);

        public static Task RenameAppleBYODEnrollmentProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
            => _helper.RenameAsync(graphServiceClient, profileID, newName);

        public static Task<List<CustomContentInfo>> GetAllAppleBYODEnrollmentContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchAppleBYODEnrollmentContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportAppleBYODEnrollmentProfileDataAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.ExportDataAsync(graphServiceClient, profileId);

        public static Task<string?> ImportAppleBYODEnrollmentProfileFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasAppleBYODEnrollmentProfileAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.HasAssignmentsAsync(graphServiceClient, profileId);

        public static Task<List<AssignmentInfo>?> GetAppleBYODAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, profileId);

        public static Task RemoveAllAppleBYODAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, profileId);
    }
}
