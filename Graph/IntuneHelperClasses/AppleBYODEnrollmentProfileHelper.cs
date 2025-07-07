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
                            await AssignGroupsToSingleAppleBYODEnrollmentProfile(importedProfile.Id, groups, destinationGraphServiceClient, filter);
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

        public static async Task AssignGroupsToSingleAppleBYODEnrollmentProfile(string profileId, List<string> groupIds, GraphServiceClient destinationGraphServiceClient, bool applyFilter)
        {
            try
            {
                if (groupIds == null || !groupIds.Any())
                {
                    WriteToImportStatusFile($"No group IDs provided for assignment to profile {profileId}. Skipping assignment.");
                    return;
                }
                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                WriteToImportStatusFile($"Assigning {groupIds.Count} groups to profile ID: {profileId}. Apply filter: {applyFilter}");

                foreach (var groupId in groupIds)
                {
                    var assignment = new AppleEnrollmentProfileAssignment
                    {
                        OdataType = "#microsoft.graph.appleEnrollmentProfileAssignment",
                        Target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = groupId,
                            DeviceAndAppManagementAssignmentFilterId = applyFilter ? SelectedFilterID : null,
                            DeviceAndAppManagementAssignmentFilterType = applyFilter ? deviceAndAppManagementAssignmentFilterType : DeviceAndAppManagementAssignmentFilterType.None
                        }
                    };

                    try
                    {
                        await destinationGraphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[profileId].Assignments.PostAsync(assignment);

                        string filterInfo = applyFilter && !string.IsNullOrEmpty(SelectedFilterID) ? $" with filter ID {SelectedFilterID} (Type: {deviceAndAppManagementAssignmentFilterType})" : "";
                        WriteToImportStatusFile($"Assigned group {groupId} to profile {profileId}{filterInfo}.");
                    }
                    catch (ODataError odataError)
                    {
                        WriteToImportStatusFile($"Graph API error assigning group {groupId} to profile {profileId}: {odataError.Error?.Message}",LogType.Error);
                    }
                    catch (Exception ex)
                    {
                        WriteToImportStatusFile($"Error assigning group {groupId} to profile {profileId}: {ex.Message}",LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An unexpected error occurred while preparing group assignments for profile ID {profileId}: {ex.Message}", LogType.Error);
            }
        }
    }
}
