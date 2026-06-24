using Microsoft.Graph;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsFeatureUpdateHelper
    {
        public static async Task<List<WindowsFeatureUpdateProfile>> SearchForWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                var allProfiles = await GetAllWindowsFeatureUpdateProfiles(graphServiceClient);
                var filteredProfiles = allProfiles.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
                return filteredProfiles;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while searching for Windows Feature Update profiles: {ex.Message}", appFunction.Main);
                return new List<WindowsFeatureUpdateProfile>();
            }
        }

        public static async Task<List<WindowsFeatureUpdateProfile>> GetAllWindowsFeatureUpdateProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles.GetAsync((requestConfiguration) =>
                {
                    //requestConfiguration.QueryParameters.Top = 1000; // Adjust as needed
                });

                List<WindowsFeatureUpdateProfile> profiles = new List<WindowsFeatureUpdateProfile>();

                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<WindowsFeatureUpdateProfile, WindowsFeatureUpdateProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                    {
                        profiles.Add(profile);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                }
                return profiles;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while retrieving all Windows Feature Update profiles: {ex.Message}", appFunction.Main);
                return new List<WindowsFeatureUpdateProfile>();
            }
        }
        public static async Task ImportMultipleWindowsFeatureUpdateProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                AppLogger.Info($"Importing {profileIDs.Count} Windows Feature Update profiles.", appFunction.Import);

                string profileName = "";

                foreach (var profileId in profileIDs)
                {
                    try
                    {
                        var sourceProfile = await sourceGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileId].GetAsync();

                        if (sourceProfile == null)
                        {
                            AppLogger.Info($"Skipping profile ID {profileId}: Not found in source tenant.", appFunction.Import);
                            continue;
                        }

                        profileName = sourceProfile.DisplayName ?? "Unnamed Profile";

                        var newProfile = new WindowsFeatureUpdateProfile
                        {
                        };


                        foreach (var property in sourceProfile.GetType().GetProperties())
                        {
                            if (property.Name.Equals("createdDateTime", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Equals("lastModifiedDateTime", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var value = property.GetValue(sourceProfile);
                            if (value != null && property.CanWrite)
                            {
                                property.SetValue(newProfile, value);
                            }
                        }


                        newProfile.Id = "";
                        newProfile.OdataType = "#microsoft.graph.windowsFeatureUpdateProfile";

                        var importedProfile = await destinationGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles.PostAsync(newProfile);

                        AppLogger.Info($"Imported profile: {importedProfile?.DisplayName ?? "Unnamed Profile"} (ID: {importedProfile?.Id ?? "Unknown ID"})", appFunction.Import);

                        if (assignments && groups != null && groups.Any() && importedProfile?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsFeatureUpdateProfile(importedProfile.Id, importedProfile.DisplayName ?? string.Empty, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to import Windows Feature Update profile {profileName}: {ex.Message}", appFunction.Import);
                        AppLogger.Warning($"This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", appFunction.Import);
                    }
                }
                AppLogger.Info("Windows Feature Update profile import process finished.", appFunction.Import);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred during the import process: {ex.Message}", appFunction.Import);
            }
        }

        /// <summary>
        /// Assigns groups to a single Windows Feature Update Profile.
        /// Windows Feature Update profiles can ONLY be assigned to device groups - not All Users or All Devices.
        /// </summary>
        public static async Task AssignGroupsToSingleWindowsFeatureUpdateProfile(string profileID, string contentName, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
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

                AppLogger.Info($"Assigning {groupIDs.Count} groups to Windows Feature Update profile {profileID}.", appFunction.Assignment);

                foreach (var groupId in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.Warning("Warning: Windows Feature Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", appFunction.Assignment);
                        continue;
                    }

                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.Warning("Warning: Windows Feature Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", appFunction.Assignment);
                        continue;
                    }

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

                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .WindowsFeatureUpdateProfiles[profileID]
                    .Assignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        if (existing.Target is AllLicensedUsersAssignmentTarget)
                        {
                            AppLogger.Warning($"Warning: Found existing 'All Users' assignment on Feature Update profile {profileID}. This should not exist and will be skipped.", appFunction.Assignment);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            AppLogger.Warning($"Warning: Found existing 'All Devices' assignment on Feature Update profile {profileID}. This should not exist and will be skipped.", appFunction.Assignment);
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
                    await destinationGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].Assign.PostAsync(requestBody);
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Error assigning groups to profile {profileID}: {ex.Message}", appFunction.Assignment);
                    throw;
                }
            }
            catch (ArgumentNullException argEx)
            {
                AppLogger.Error($"Argument null exception during group assignment setup: {argEx.Message}", appFunction.Assignment);
                throw;
            }
            catch (Exception)
            {
                throw;
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
                AppLogger.Error($"An error occurred while deleting a Windows Feature Update profile: {ex.Message}", appFunction.Delete);
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
                    AppLogger.Info($"Renamed Windows Feature Update profile '{existingProfile.DisplayName}' to '{name}' (ID: {profileID})", appFunction.Rename);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
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
                    AppLogger.Info($"Updated description for Windows Feature Update profile {profileID} to '{newName}'", appFunction.Rename);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingProfile.DisplayName);

                    var profile = new WindowsFeatureUpdateProfile
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileID].PatchAsync(profile);
                    AppLogger.Info($"Removed prefix from Windows Feature Update profile {profileID}, new name: '{name}'", appFunction.Rename);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while renaming Windows Feature Update profile: {ex.Message}", appFunction.Rename);
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

        /// <summary>
        /// Exports a Windows Feature Update profile's full data as a JsonElement for JSON file export.
        /// </summary>
        public static async Task<JsonElement?> ExportWindowsFeatureUpdatePolicyDataAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileId].GetAsync();

                if (result == null)
                {
                    AppLogger.Warning($"Windows Feature Update profile {profileId} not found for export.", appFunction.JsonExport);
                    return null;
                }

                using var writer = new JsonSerializationWriter();
                writer.WriteObjectValue(null, result);
                using var stream = writer.GetSerializedContent();
                var doc = await JsonDocument.ParseAsync(stream);
                return doc.RootElement.Clone();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error exporting Windows Feature Update profile {profileId}: {ex.Message}", appFunction.JsonExport);
                return null;
            }
        }

        /// <summary>
        /// Imports a Windows Feature Update profile from previously exported JSON data into the destination tenant.
        /// </summary>
        public static async Task<string?> ImportWindowsFeatureUpdateFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exportedProfile = parseNode.GetObjectValue(WindowsFeatureUpdateProfile.CreateFromDiscriminatorValue);

                if (exportedProfile == null)
                {
                    AppLogger.Error("Failed to deserialize Windows Feature Update profile data from JSON.", appFunction.Import);
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

                var imported = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles.PostAsync(newProfile);

                AppLogger.Info($"Imported Windows Feature Update profile: {imported?.DisplayName}", appFunction.Import);
                return imported?.DisplayName;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error importing Windows Feature Update profile from JSON: {ex.Message}", appFunction.Import);
                AppLogger.Warning($"This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", appFunction.Import);
                return null;
            }
        }

        /// <summary>
        /// Checks if a Windows feature update profile has any group assignments.
        /// </summary>
        public static async Task<bool?> HasWindowsFeatureUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileId].Assignments.GetAsync(rc =>
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

        /// <summary>
        /// Gets detailed assignment information for a Windows Feature Update profile.
        /// </summary>
        public static async Task<List<AssignmentInfo>?> GetWindowsFeatureUpdateAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var details = new List<AssignmentInfo>();
                var result = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileId].Assignments.GetAsync();

                while (result?.Value != null)
                {
                    foreach (var assignment in result.Value)
                    {
                        details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileId]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                return details;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error getting assignment details for Windows Feature Update {profileId}: {ex.Message}", appFunction.ManageAssignment);
                return null;
            }
        }

        /// <summary>
        /// Removes all assignments from a Windows Feature Update profile.
        /// </summary>
        public static async Task RemoveAllWindowsFeatureUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsFeatureUpdateProfiles.Item.Assign.AssignPostRequestBody
            {
                Assignments = new List<WindowsFeatureUpdateProfileAssignment>()
            };

            await graphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[profileId].Assign.PostAsync(requestBody);
            AppLogger.Info($"Removed all assignments from Windows Feature Update profile {profileId}.", appFunction.ManageAssignment);
        }
    }
}
