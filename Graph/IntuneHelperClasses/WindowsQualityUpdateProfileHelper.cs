using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsQualityUpdateProfileHelper
    {
        // Expedite policies (not regular quality update policies)
        public static async Task<List<WindowsQualityUpdateProfile>> SearchForWindowsQualityUpdateProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Searching for Windows Quality Update profiles. Search query: " + searchQuery);

                var allProfiles = await GetAllWindowsQualityUpdateProfiles(graphServiceClient);
                var filteredProfiles = allProfiles.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

                LogToFunctionFile(appFunction.Main, $"Found {filteredProfiles.Count} Windows Quality Update profiles matching the search query.");

                return filteredProfiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while searching for Windows Quality Update profiles", LogLevels.Error);
                return new List<WindowsQualityUpdateProfile>();
            }
        }

        public static async Task<List<WindowsQualityUpdateProfile>> GetAllWindowsQualityUpdateProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all Windows Quality Update profiles.");

                var result = await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles.GetAsync((requestConfiguration) =>
                {
                });

                List<WindowsQualityUpdateProfile> profiles = new List<WindowsQualityUpdateProfile>();

                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<WindowsQualityUpdateProfile, WindowsQualityUpdateProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                    {
                        profiles.Add(profile);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                }
                else
                {
                    LogToFunctionFile(appFunction.Main, "No Windows Quality Update profiles found or result was null.");
                }

                LogToFunctionFile(appFunction.Main, $"Found {profiles.Count} Windows Quality Update profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all Windows Quality Update profiles", LogLevels.Error);
                return new List<WindowsQualityUpdateProfile>();
            }
        }
        public static async Task ImportMultipleWindowsQualityUpdateProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Importing {profileIDs.Count} Windows Quality Update profiles.");
                string profileName = "";

                foreach (var profileId in profileIDs)
                {
                    try
                    {
                        var sourceProfile = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileId].GetAsync();

                        if (sourceProfile == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping profile ID {profileId}: Not found in source tenant.");
                            continue;
                        }

                        profileName = sourceProfile.DisplayName ?? "ERROR GETTING NAME";

                        var newPolicy = new WindowsQualityUpdateProfile
                        {
                            // Initialize properties needed for creation. Copy relevant ones from sourcePolicy.
                            // Be careful about read-only properties like Id, CreatedDateTime, LastModifiedDateTime.
                        };

                        // Dynamically copy properties (excluding specific ones)
                        foreach (var property in sourceProfile.GetType().GetProperties())
                        {
                            // Skip read-only or problematic properties
                            if (property.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Equals("createdDateTime", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Equals("lastModifiedDateTime", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Equals("assignments", StringComparison.OrdinalIgnoreCase) || // Assignments are handled separately
                                !property.CanWrite) // Skip properties without a setter
                            {
                                continue;
                            }

                            var value = property.GetValue(sourceProfile);
                            // Check if the property exists on the newPolicy object before setting
                            var destProperty = newPolicy.GetType().GetProperty(property.Name);
                            if (destProperty != null && destProperty.CanWrite)
                            {
                                destProperty.SetValue(newPolicy, value);
                            }
                        }

                        newPolicy.OdataType = "#microsoft.graph.windowsQualityUpdateProfile";

                        var importedProfile = await destinationGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles.PostAsync(newPolicy);

                        LogToFunctionFile(appFunction.Main, $"Imported profile: {importedProfile?.DisplayName ?? "Unnamed Profile"} (ID: {importedProfile?.Id ?? "Unknown ID"})");

                        if (assignments && groups != null && groups.Any() && importedProfile?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsQualityUpdateProfile(importedProfile.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error importing profile {profileName}: {ex.Message}", LogLevels.Error);
                        LogToFunctionFile(appFunction.Main, "There is currently a known bug with importing Windows Quality Update profiles. " +
                                                "This will be fixed in a future release. " +
                                                "For now, please manually assign the groups to the imported profiles.", LogLevels.Warning);
                    }
                }
                LogToFunctionFile(appFunction.Main, "Windows Quality Update profile import process finished.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred during the import process: {ex.Message}", LogLevels.Error);
            }
        }


        /// <summary>
        /// Assigns groups to a single Windows Quality Update Profile (Expedite).
        /// Windows Quality Update profiles can ONLY be assigned to device groups - not All Users or All Devices.
        /// </summary>
        /// <param name="profileID">The ID of the profile to assign groups to.</param>
        /// <param name="groupIDs">List of group IDs to assign.</param>
        /// <param name="destinationGraphServiceClient">GraphServiceClient for the destination tenant.</param>
        /// <param name="applyFilter">Whether to apply assignment filters.</param>
        /// <returns>A Task representing the asynchronous assignment operation.</returns>
        public static async Task AssignGroupsToSingleWindowsQualityUpdateProfile(string profileID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
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

                var assignments = new List<WindowsQualityUpdateProfileAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                LogToFunctionFile(appFunction.Main, $"Assigning {groupIDs.Count} groups to Windows Quality Update profile {profileID}.");

                // Step 1: Add new assignments to request body
                foreach (var groupId in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    // Check if this is All Users - Quality Update profiles cannot be assigned to All Users
                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                        continue;
                    }

                    // Check if this is All Devices - Quality Update profiles cannot be assigned to All Devices
                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
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

                    var assignment = new WindowsQualityUpdateProfileAssignment
                    {
                        OdataType = "#microsoft.graph.windowsQualityUpdateProfileAssignment",
                        Target = assignmentTarget
                    };

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .WindowsQualityUpdateProfiles[profileID]
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
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on Quality Update profile {profileID}. This should not exist and will be skipped.", LogLevels.Warning);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip All Devices assignments - they shouldn't exist but handle gracefully
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Quality Update profile {profileID}. This should not exist and will be skipped.", LogLevels.Warning);
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
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].Assign.PostAsync(requestBody);
                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to Quality Update profile {profileID}.");
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
        public static async Task DeleteWindowsQualityUpdateProfile(GraphServiceClient graphServiceClient, string profileID)
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

                await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].DeleteAsync();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting a Windows Quality Update profile", LogLevels.Error);
            }
        }
        public static async Task RenameWindowsQualityUpdateProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
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
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingProfile.DisplayName ?? string.Empty, newName);

                    var profile = new WindowsQualityUpdateProfile
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Successfully renamed Windows Quality Update profile {profileID} to '{name}'");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing profile
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var profile = new WindowsQualityUpdateProfile
                    {
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Updated description for Windows Quality Update profile {profileID} to '{newName}'");
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingProfile.DisplayName);

                    var profile = new WindowsQualityUpdateProfile
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from Windows Quality Update profile {profileID}, new name: '{name}'");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming Windows Quality Update profile", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllWindowsQualityUpdateProfileContentAsync(GraphServiceClient graphServiceClient)
        {
            var profiles = await GetAllWindowsQualityUpdateProfiles(graphServiceClient);
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
    }
}
