using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsFeatureUpdateHelper
    {
        public static async Task<List<WindowsFeatureUpdateProfile>> SearchForWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Searching for Windows Feature Update profiles. Search query: " + searchQuery);

                // Note: The Graph API for WindowsFeatureUpdateProfile might not support filtering by name directly in the same way.
                // Adjust the query or filter locally if needed. This example assumes direct filtering is possible or fetches all and filters locally.
                // Let's fetch all first and then filter locally as a safer approach.
                var allProfiles = await GetAllWindowsFeatureUpdateProfiles(graphServiceClient);
                // Add null checks for profile and DisplayName
                var filteredProfiles = allProfiles.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

                LogToFunctionFile(appFunction.Main, $"Found {filteredProfiles.Count} Windows Feature Update profiles matching the search query.");

                return filteredProfiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while searching for Windows Feature Update profiles", LogLevels.Error);
                return new List<WindowsFeatureUpdateProfile>();
            }
        }

        public static async Task<List<WindowsFeatureUpdateProfile>> GetAllWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all Windows Feature Update profiles.");

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
                    LogToFunctionFile(appFunction.Main, "No Windows Feature Update profiles found or result was null.", LogLevels.Warning);
                }

                LogToFunctionFile(appFunction.Main, $"Found {profiles.Count} Windows Feature Update profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all Windows Feature Update profiles", LogLevels.Error);
                return new List<WindowsFeatureUpdateProfile>();
            }
        }
        public static async Task ImportMultipleWindowsFeatureUpdateProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Importing {profileIDs.Count} Windows Feature Update profiles.");


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
                            LogToFunctionFile(appFunction.Main, $"Skipping profile ID {profileId}: Not found in source tenant.");
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
                        LogToFunctionFile(appFunction.Main, $"Imported profile: {importedProfile?.DisplayName ?? "Unnamed Profile"} (ID: {importedProfile?.Id ?? "Unknown ID"})");

                        // Handle assignments if requested
                        if (assignments && groups != null && groups.Any() && importedProfile?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsFeatureUpdateProfile(importedProfile.Id, groups, destinationGraphServiceClient); // Pass filter flag if needed for assignment logic
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import Windows Feature Update profile {profileName}: {ex.Message}", LogLevels.Error);
                        LogToFunctionFile(appFunction.Main, "This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogLevels.Warning);
                    }
                }
                LogToFunctionFile(appFunction.Main, "Windows Feature Update profile import process finished.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred during the import process: {ex.Message}", LogLevels.Error);
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

                LogToFunctionFile(appFunction.Main, $"Assigning {groupIDs.Count} groups to Windows Feature Update profile {profileID}.");

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
                        LogToFunctionFile(appFunction.Main, "Warning: Windows Feature Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                        continue;
                    }

                    // Check if this is All Devices - Feature Update profiles cannot be assigned to All Devices
                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        LogToFunctionFile(appFunction.Main, "Warning: Windows Feature Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
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
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on Feature Update profile {profileID}. This should not exist and will be skipped.", LogLevels.Warning);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip All Devices assignments - they shouldn't exist but handle gracefully
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Feature Update profile {profileID}. This should not exist and will be skipped.", LogLevels.Warning);
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
                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to Feature Update profile {profileID}");
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"Error assigning groups to profile {profileID}: {ex.Message}", LogLevels.Error);
                }
            }
            catch (ArgumentNullException argEx)
            {
                LogToFunctionFile(appFunction.Main, $"Argument null exception during group assignment setup: {argEx.Message}", LogLevels.Error);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while preparing assignment for profile {profileID}: {ex.Message}", LogLevels.Warning);
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
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting a Windows Feature Update profile", LogLevels.Error);
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

                if (selectedRenameMode == "Prefix")
                {
                    // Look up the existing profile
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingProfile.DisplayName ?? string.Empty, newName);

                    var profile = new WindowsFeatureUpdateProfile
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Renamed Windows Feature Update profile '{existingProfile.DisplayName}' to '{name}' (ID: {profileID})");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing profile
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var profile = new WindowsFeatureUpdateProfile
                    {
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Updated description for Windows Feature Update profile {profileID} to '{newName}'");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming Windows Feature Update profile", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllWindowsFeatureUpdateContentAsync(GraphServiceClient graphServiceClient)
        {
            var profiles = await GetAllWindowsFeatureUpdateProfiles(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var profile in profiles)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows Feature Update",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchWindowsFeatureUpdateContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var profiles = await SearchForWindowsFeatureUpdateProfiles(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var profile in profiles)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows Feature Update",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }

            return content;
        }
    }
}
