using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class AppleBYODEnrollmentProfileHelper
    {
        public static async Task<List<AppleUserInitiatedEnrollmentProfile>> SearchForAppleBYODEnrollmentProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile($"Searching for Apple BYOD enrollment profile. Search query: {searchQuery}");

                var result = await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(DisplayName,'{searchQuery}')";
                });

                if (result == null || result.Value == null)
                {
                    WriteToImportStatusFile($"Search returned null or empty result.",LogType.Warning);
                    return new List<AppleUserInitiatedEnrollmentProfile>();
                }

                List<AppleUserInitiatedEnrollmentProfile> enrollmentProfiles = new List<AppleUserInitiatedEnrollmentProfile>();
                var pageIterator = PageIterator<AppleUserInitiatedEnrollmentProfile, AppleUserInitiatedEnrollmentProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                {
                    enrollmentProfiles.Add(profile);
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {enrollmentProfiles.Count} policies.");

                return enrollmentProfiles;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Search returned null or empty result.", LogType.Error);
                return new List<AppleUserInitiatedEnrollmentProfile>();
            }
        }

        public static async Task<List<AppleUserInitiatedEnrollmentProfile>> GetAllAppleBYODEnrollmentProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile($"Retrieving all Apple BYOD enrollment prfoiles.");

                var result = await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 999;
                });

                if (result == null || result.Value == null)
                {
                    WriteToImportStatusFile($"Get all returned null or empty result for policies.",LogType.Warning);
                    return new List<AppleUserInitiatedEnrollmentProfile>();
                }

                List<AppleUserInitiatedEnrollmentProfile> enrollmentProfiles = new List<AppleUserInitiatedEnrollmentProfile>();
                var pageIterator = PageIterator<AppleUserInitiatedEnrollmentProfile, AppleUserInitiatedEnrollmentProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                {
                    enrollmentProfiles.Add(profile);
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {enrollmentProfiles.Count} policies.");

                return enrollmentProfiles;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Search returned null or empty result.", LogType.Error);
                return new List<AppleUserInitiatedEnrollmentProfile>();
            }
        }

        public static async Task ImportMultipleAppleBYODEnrollmentProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient,List<string> profileIds, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile(" ");
                WriteToImportStatusFile($"{DateTime.Now.ToString()} - Importing {profileIds.Count} Apple BYOD enrollment profile(s).");

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
                            WriteToImportStatusFile($"Skipping profile ID {profileId}: Not found in source tenant.");
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

                        WriteToImportStatusFile($"Successfully imported {importedProfile.DisplayName}");

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
                        WriteToImportStatusFile($"Failed to import {profileName}: {ex.Message}", LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An unexpected error occurred during the import process: {ex.Message}", LogType.Error);
            }
            finally
            {
                WriteToImportStatusFile($" {DateTime.Now.ToString()} - Import process for Apple BYOD Enrollment profilesW completed.");
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
                        WriteToImportStatusFile($"Warning: Apple BYOD Enrollment profiles cannot be assigned to 'All Devices'. Only All Users and user groups are supported. Skipping this assignment.", LogType.Warning);
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
                                DeviceAndAppManagementAssignmentFilterId =  SelectedFilterID,
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
                            WriteToImportStatusFile($"Warning: Found existing 'All Devices' assignment on Apple BYOD Enrollment profile {profileId}. This should not exist and will be skipped.", LogType.Warning);
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

                        WriteToImportStatusFile($"Assigned {targetType} to profile {profileId}{filterInfo}.");
                    }
                    catch (ODataError odataError)
                    {
                        WriteToImportStatusFile($"Graph API error assigning to profile {profileId}: {odataError.Error?.Message}", LogType.Error);
                    }
                    catch (Exception ex)
                    {
                        WriteToImportStatusFile($"Error assigning to profile {profileId}: {ex.Message}", LogType.Error);
                    }
                }

                WriteToImportStatusFile($"Completed assignment process for profile {profileId}. Total assignments processed: {assignments.Count}");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An unexpected error occurred while preparing group assignments for profile ID {profileId}: {ex.Message}", LogType.Error);
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
                WriteToImportStatusFile("An error occurred while deleting Apple BYOD Enrollment profiles",LogType.Error);
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

                // Look up the existing profile
                var existingProfile = await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileID].GetAsync();

                if (existingProfile == null)
                {
                    throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingProfile.DisplayName, newName);

                // Create an instance of the specific profile type using reflection
                var profileType = existingProfile.GetType();
                var profile = (AppleUserInitiatedEnrollmentProfile)Activator.CreateInstance(profileType);

                // Set the DisplayName on the new instance
                profile.DisplayName = name;

                await graphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileID].PatchAsync(profile);
                WriteToImportStatusFile($"Successfully renamed Apple BYOD Enrollment profile with ID {profileID} to '{name}'.");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming Apple BYOD Enrollment profiles", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
