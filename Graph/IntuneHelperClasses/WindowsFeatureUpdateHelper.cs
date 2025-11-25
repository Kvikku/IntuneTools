using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsFeatureUpdateHelper
    {
        public static async Task<List<WindowsFeatureUpdateProfile>> SearchForWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile("Searching for Windows Feature Update profiles. Search query: " + searchQuery);

                // Note: The Graph API for WindowsFeatureUpdateProfile might not support filtering by name directly in the same way.
                // Adjust the query or filter locally if needed. This example assumes direct filtering is possible or fetches all and filters locally.
                // Let's fetch all first and then filter locally as a safer approach.
                var allProfiles = await GetAllWindowsFeatureUpdateProfiles(graphServiceClient);
                // Add null checks for profile and DisplayName
                var filteredProfiles = allProfiles.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

                WriteToImportStatusFile($"Found {filteredProfiles.Count} Windows Feature Update profiles matching the search query.");

                return filteredProfiles;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while searching for Windows Feature Update profiles",LogType.Error);
                return new List<WindowsFeatureUpdateProfile>();
            }
        }

        public static async Task<List<WindowsFeatureUpdateProfile>> GetAllWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile("Retrieving all Windows Feature Update profiles.");

                var result = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles.GetAsync((requestConfiguration) =>
                {
                    //requestConfiguration.QueryParameters.Top = 1000; // Adjust as needed
                });

                List<WindowsFeatureUpdateProfile> profiles = new List<WindowsFeatureUpdateProfile>();

                // Add null check for result before creating iterator
                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<WindowsFeatureUpdateProfile, WindowsFeatureUpdateProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                    {
                        profiles.Add(profile);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                }
                else
                {
                    WriteToImportStatusFile("No Windows Feature Update profiles found or result was null.",LogType.Warning);
                }

                WriteToImportStatusFile($"Found {profiles.Count} Windows Feature Update profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all Windows Feature Update profiles",LogType.Error);
                return new List<WindowsFeatureUpdateProfile>();
            }
        }
        public static async Task ImportMultipleWindowsFeatureUpdateProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {profileIDs.Count} Windows Feature Update profiles.");


                // Note: Filters are not supported for feature updates yet
                //if (filter)
                //{
                //    rtb.AppendText("Filters will be added (if applicable).\n");
                //    WriteToImportStatusFile("Filters will be added (if applicable).");
                //}

                string profileName = "";

                foreach (var profileId in profileIDs)
                {
                    try
                    {
                        // Fetch the source profile
                        var sourceProfile = await sourceGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileId].GetAsync();

                        if (sourceProfile == null)
                        {
                            WriteToImportStatusFile($"Skipping profile ID {profileId}: Not found in source tenant.");
                            continue;
                        }

                        profileName = sourceProfile.DisplayName ?? "Unnamed Profile";

                        // Create the new profile object for the destination tenant
                        var newProfile = new WindowsFeatureUpdateProfile
                        {
                        };


                        foreach (var property in sourceProfile.GetType().GetProperties())
                        {
                            if (property.Name.Equals("createdDateTime", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Equals("lastModifiedDateTime", StringComparison.OrdinalIgnoreCase))
                            {
                                continue; // Skip these properties
                            }

                            var value = property.GetValue(sourceProfile);
                            if (value != null && property.CanWrite)
                            {
                                property.SetValue(newProfile, value);
                            }
                        }


                        newProfile.Id = "";
                        newProfile.OdataType = "#microsoft.graph.windowsFeatureUpdateProfile";

                        // Create the profile in the destination tenant

                        var importedProfile = await destinationGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles.PostAsync(newProfile);

                        // Add null check for importedProfile and DisplayName
                        WriteToImportStatusFile($"Imported profile: {importedProfile?.DisplayName ?? "Unnamed Profile"} (ID: {importedProfile?.Id ?? "Unknown ID"})");

                        // Handle assignments if requested
                        if (assignments && groups != null && groups.Any() && importedProfile?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsFeatureUpdateProfile(importedProfile.Id, groups, destinationGraphServiceClient); // Pass filter flag if needed for assignment logic
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToImportStatusFile($"Failed to import Windows Feature Update profile {profileName}: {ex.Message}", LogType.Error);
                        WriteToImportStatusFile("This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active",LogType.Warning);
                    }
                }
                WriteToImportStatusFile("Windows Feature Update profile import process finished.");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred during the import process: {ex.Message}",LogType.Error);
            }
        }

        /// <summary>
        /// Assigns groups to a single Windows Feature Update Profile.
        /// Windows Feature Update profiles can ONLY be assigned to device groups - not All Users or All Devices.
        /// </summary>
        /// <param name="profileID">The ID of the profile to assign groups to.</param>
        /// <param name="groupIDs">List of group IDs to assign.</param>
        /// <param name="destinationGraphServiceClient">GraphServiceClient for the destination tenant.</param>
        /// <param name="applyFilter">Whether to apply assignment filters.</param>
        /// <returns>A Task representing the asynchronous assignment operation.</returns>
        public static async Task AssignGroupsToSingleWindowsFeatureUpdateProfile(string profileID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (string.IsNullOrEmpty(profileID))
                {
                    throw new ArgumentNullException(nameof(profileID));
                }

                if (groupIDs == null)
                {
                    throw new ArgumentNullException(nameof(groupIDs));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<WindowsFeatureUpdateProfileAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                WriteToImportStatusFile($"Assigning {groupIDs.Count} groups to Windows Feature Update profile {profileID}.");

                // Step 1: Add new assignments to request body
                foreach (var groupId in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    // Check if this is All Users - Feature Update profiles cannot be assigned to All Users
                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteToImportStatusFile($"Warning: Windows Feature Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogType.Warning);
                        continue;
                    }

                    // Check if this is All Devices - Feature Update profiles cannot be assigned to All Devices
                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteToImportStatusFile($"Warning: Windows Feature Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogType.Warning);
                        continue;
                    }

                    // Regular group assignment (device groups only)
                    var assignmentTarget = new GroupAssignmentTarget
                    {
                        OdataType = "#microsoft.graph.groupAssignmentTarget",
                        GroupId = groupId,
                        DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                        DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                    };

                    var assignment = new WindowsFeatureUpdateProfileAssignment
                    {
                        OdataType = "#microsoft.graph.windowsFeatureUpdateProfileAssignment",
                        Target = assignmentTarget
                    };

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .WindowsFeatureUpdateProfiles[profileID]
                    .Assignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        // Check the type of assignment target
                        if (existing.Target is AllLicensedUsersAssignmentTarget)
                        {
                            // Skip All Users assignments - they shouldn't exist but handle gracefully
                            WriteToImportStatusFile($"Warning: Found existing 'All Users' assignment on Feature Update profile {profileID}. This should not exist and will be skipped.", LogType.Warning);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip All Devices assignments - they shouldn't exist but handle gracefully
                            WriteToImportStatusFile($"Warning: Found existing 'All Devices' assignment on Feature Update profile {profileID}. This should not exist and will be skipped.", LogType.Warning);
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
                            // Include any other assignment types (e.g., exclusions, etc.)
                            assignments.Add(existing);
                        }
                    }
                }

                // Step 3: Update the profile with the assignments
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsFeatureUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile($"Assigned {assignments.Count} assignments to Feature Update profile {profileID}");
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile($"Error assigning groups to profile {profileID}: {ex.Message}", LogType.Error);
                }
            }
            catch (ArgumentNullException argEx)
            {
                WriteToImportStatusFile($"Argument null exception during group assignment setup: {argEx.Message}", LogType.Error);
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred while preparing assignment for profile {profileID}: {ex.Message}", LogType.Warning);
            }
        }
        public static async Task DeleteWindowsFeatureUpdateProfile(GraphServiceClient graphServiceClient, string profileID)
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

                await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].DeleteAsync();
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while deleting a Windows Feature Update profile",LogType.Error);
            }
        }
        public static async Task RenameWindowsFeatureUpdateProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
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
                var existingProfile = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].GetAsync();

                if (existingProfile == null)
                {
                    throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingProfile.DisplayName, newName);

                var profile = new WindowsFeatureUpdateProfile
                {
                    DisplayName = name,
                };

                await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].PatchAsync(profile);
                WriteToImportStatusFile($"Renamed Windows Feature Update profile '{existingProfile.DisplayName}' to '{name}' (ID: {profileID})");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming Windows Feature Update profile", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
