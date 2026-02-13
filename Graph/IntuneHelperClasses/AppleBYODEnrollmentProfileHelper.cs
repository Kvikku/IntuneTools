using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class AppleBYODEnrollmentProfileHelper
    {
        public static async Task<List<AppleUserInitiatedEnrollmentProfile>> SearchForAppleBYODEnrollmentProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Searching for Apple BYOD enrollment profile. Search query: {searchQuery}");

                var result = await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(DisplayName,'{searchQuery}')";
                });

                if (result == null || result.Value == null)
                {
                    LogToFunctionFile(appFunction.Main, $"Search returned null or empty result.", LogLevels.Warning);
                    return new List<AppleUserInitiatedEnrollmentProfile>();
                }

                List<AppleUserInitiatedEnrollmentProfile> enrollmentProfiles = new List<AppleUserInitiatedEnrollmentProfile>();
                var pageIterator = PageIterator<AppleUserInitiatedEnrollmentProfile, AppleUserInitiatedEnrollmentProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                {
                    enrollmentProfiles.Add(profile);
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {enrollmentProfiles.Count} policies.");

                return enrollmentProfiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Search returned null or empty result.", LogLevels.Error);
                return new List<AppleUserInitiatedEnrollmentProfile>();
            }
        }

        public static async Task<List<AppleUserInitiatedEnrollmentProfile>> GetAllAppleBYODEnrollmentProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Retrieving all Apple BYOD enrollment prfoiles.");

                var result = await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 999;
                });

                if (result == null || result.Value == null)
                {
                    LogToFunctionFile(appFunction.Main, $"Get all returned null or empty result for policies.", LogLevels.Warning);
                    return new List<AppleUserInitiatedEnrollmentProfile>();
                }

                List<AppleUserInitiatedEnrollmentProfile> enrollmentProfiles = new List<AppleUserInitiatedEnrollmentProfile>();
                var pageIterator = PageIterator<AppleUserInitiatedEnrollmentProfile, AppleUserInitiatedEnrollmentProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                {
                    enrollmentProfiles.Add(profile);
                    return true;
                });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {enrollmentProfiles.Count} policies.");

                return enrollmentProfiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Search returned null or empty result.", LogLevels.Error);
                return new List<AppleUserInitiatedEnrollmentProfile>();
            }
        }

        public static async Task ImportMultipleAppleBYODEnrollmentProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIds, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, " ");
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Importing {profileIds.Count} Apple BYOD enrollment profile(s).");

                foreach (var profileId in profileIds)
                {
                    // FIX: Declare sourceProfile outside the try block to be accessible in catch
                    AppleUserInitiatedEnrollmentProfile? sourceProfile = null;
                    var profileName = string.Empty;

                    try
                    {
                        sourceProfile = await sourceGraphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileId].GetAsync();

                        if (sourceProfile == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping profile ID {profileId}: Not found in source tenant.");
                            continue;
                        }

                        profileName = sourceProfile.DisplayName ?? "Unknown Profile";

                        var newProfile = new AppleUserInitiatedEnrollmentProfile
                        {

                        };

                        // Get the type of the policy with reflection
                        var type = sourceProfile.GetType();

                        // Create a new instance of the same type
                        var newPolicy = Activator.CreateInstance(type);

                        // Copy all settings from the source policy to the new policy
                        foreach (var property in sourceProfile.GetType().GetProperties())
                        {
                            if (property.CanWrite && property.Name != "Id" && property.Name != "CreatedDateTime" && property.Name != "LastModifiedDateTime")
                            {
                                var value = property.GetValue(sourceProfile);
                                if (value != null)
                                {
                                    property.SetValue(newProfile, value);
                                }
                            }
                        }

                        var importedProfile = await destinationGraphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.PostAsync(newProfile);

                        LogToFunctionFile(appFunction.Main, $"Successfully imported {importedProfile.DisplayName}");

                        if (assignments && groups != null && groups.Any())
                        {
                            await AssignGroupsToSingleAppleBYODEnrollmentProfile(importedProfile.Id, groups, destinationGraphServiceClient);
                        }

                        // TODO - delete this code block if not needed

                        //if (importedProfile != null && !string.IsNullOrEmpty(importedProfile.Id))
                        //{
                        //    rtb.AppendText($"Successfully imported {importedProfile.DisplayName}\n");
                        //    WriteToImportStatusFile($"Successfully imported {importedProfile.DisplayName}");

                        //    if (assignments && groups != null && groups.Any())
                        //    {
                        //        await AssignGroupsToSingleAppleBYODEnrollmentProfile(importedProfile.Id, groups, destinationGraphServiceClient, filter);
                        //    }
                        //}
                        //else
                        //{
                        //     rtb.AppendText($"Error importing {sourceProfile.DisplayName}\n");
                        //     WriteToImportStatusFile($"Error importing {sourceProfile.DisplayName} (ID: {profileId}).");
                        //}
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import {profileName}: {ex.Message}", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An unexpected error occurred during the import process: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                LogToFunctionFile(appFunction.Main, $" {DateTime.Now.ToString()} - Import process for Apple BYOD Enrollment profilesW completed.");
            }
        }

        /// <summary>
        /// Assigns groups to a single Apple BYOD Enrollment Profile.
        /// Apple BYOD Enrollment profiles support All Users and regular user groups, but NOT All Devices.
        /// </summary>
        /// <param name="profileId">The ID of the profile to assign groups to.</param>
        /// <param name="groupIds">List of group IDs to assign.</param>
        /// <param name="destinationGraphServiceClient">GraphServiceClient for the destination tenant.</param>
        /// <param name="applyFilter">Whether to apply assignment filters.</param>
        /// <returns>A Task representing the asynchronous assignment operation.</returns>
        public static async Task AssignGroupsToSingleAppleBYODEnrollmentProfile(string profileId, List<string> groupIds, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (profileId == null)
                {
                    throw new ArgumentNullException(nameof(profileId));
                }

                if (groupIds == null)
                {
                    throw new ArgumentNullException(nameof(groupIds));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<AppleEnrollmentProfileAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllUsers = false;

                // Step 1: Add new assignments to request body
                foreach (var groupId in groupIds)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    // Check if this is All Devices - Apple BYOD Enrollment profiles cannot be assigned to All Devices
                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        LogToFunctionFile(appFunction.Main, $"Warning: Apple BYOD Enrollment profiles cannot be assigned to 'All Devices'. Only All Users and user groups are supported. Skipping this assignment.", LogLevels.Warning);
                        continue;
                    }

                    AppleEnrollmentProfileAssignment assignment;

                    // Check if this is All Users - this IS supported
                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignment = new AppleEnrollmentProfileAssignment
                        {
                            OdataType = "#microsoft.graph.appleEnrollmentProfileAssignment",
                            Target = new AllLicensedUsersAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            }
                        };
                    }
                    else
                    {
                        // Regular group assignment
                        assignment = new AppleEnrollmentProfileAssignment
                        {
                            OdataType = "#microsoft.graph.appleEnrollmentProfileAssignment",
                            Target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                GroupId = groupId,
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            }
                        };
                    }

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .AppleUserInitiatedEnrollmentProfiles[profileId]
                    .Assignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        // Check the type of assignment target
                        if (existing.Target is AllLicensedUsersAssignmentTarget)
                        {
                            // Skip if we're already adding All Users
                            if (!hasAllUsers)
                            {
                                assignments.Add(existing);
                            }
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip All Devices assignments - they shouldn't exist but handle gracefully
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Apple BYOD Enrollment profile {profileId}. This should not exist and will be skipped.", LogLevels.Warning);
                            continue;
                        }
                        else if (existing.Target is GroupAssignmentTarget groupTarget)
                        {
                            var existingGroupId = groupTarget.GroupId;

                            // Only add if not already in the new assignments
                            if (!string.IsNullOrWhiteSpace(existingGroupId) && seenGroupIds.Add(existingGroupId))
                            {
                                assignments.Add(existing);
                            }
                        }
                        else
                        {
                            // Include any other assignment types (e.g., exclusions, all users with exclusions, etc.)
                            assignments.Add(existing);
                        }
                    }
                }

                // Step 3: Post assignments individually (Apple enrollment profiles don't support batch assign)
                foreach (var assignment in assignments)
                {
                    // Skip existing assignments that were already posted
                    if (!string.IsNullOrEmpty(assignment.Id))
                    {
                        continue;
                    }

                    try
                    {
                        await destinationGraphServiceClient
                            .DeviceManagement
                            .AppleUserInitiatedEnrollmentProfiles[profileId]
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

                        LogToFunctionFile(appFunction.Main, $"Assigned {targetType} to profile {profileId}{filterInfo}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (ODataError odataError)
                    {
                        LogToFunctionFile(appFunction.Main, $"Graph API error assigning to profile {profileId}: {odataError.Error?.Message}", LogLevels.Error);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error assigning to profile {profileId}: {ex.Message}", LogLevels.Error);
                    }
                }

                LogToFunctionFile(appFunction.Main, $"Completed assignment process for profile {profileId}. Total assignments processed: {assignments.Count}");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An unexpected error occurred while preparing group assignments for profile ID {profileId}: {ex.Message}", LogLevels.Error);
            }
        }

        public static async Task DeleteAppleBYODEnrollmentProfile(GraphServiceClient graphServiceClient, string profileID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (profileID == null)
                {
                    throw new InvalidOperationException("Profile ID cannot be null.");
                }
                await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileID].DeleteAsync();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting Apple BYOD Enrollment profiles", LogLevels.Error);
            }
        }
        public static async Task RenameAppleBYODEnrollmentProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (profileID == null)
                {
                    throw new InvalidOperationException("Profile ID cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                if (selectedRenameMode == "Prefix")
                {
                    // Look up the existing profile
                    var existingProfile = await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingProfile.DisplayName ?? string.Empty, newName);

                    // Create an instance of the specific profile type using reflection
                    var profileType = existingProfile.GetType();
                    var profile = (AppleUserInitiatedEnrollmentProfile?)Activator.CreateInstance(profileType);

                    if (profile == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {profileType.Name}");
                    }

                    // Set the DisplayName on the new instance
                    profile.DisplayName = name;

                    await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Renamed Apple BYOD Enrollment profile with ID {profileID} to '{name}'.");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing profile
                    var existingProfile = await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    // Create an instance of the specific profile type using reflection
                    var profileType = existingProfile.GetType();
                    var profile = (AppleUserInitiatedEnrollmentProfile?)Activator.CreateInstance(profileType);

                    if (profile == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {profileType.Name}");
                    }

                    profile.Description = newName;

                    await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Updated description for Apple BYOD Enrollment profile {profileID} to '{newName}'.");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming Apple BYOD Enrollment profiles", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }
    }
}
