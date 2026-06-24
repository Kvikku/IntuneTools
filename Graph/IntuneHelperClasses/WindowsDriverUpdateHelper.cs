using Microsoft.Graph;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsDriverUpdateHelper
    {
        /// <summary>
        /// Searches for Windows Driver Update Profiles matching the specified search query.
        /// </summary>
        public static async Task<List<WindowsDriverUpdateProfile>> SearchForDriverProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles.GetAsync();

                List<WindowsDriverUpdateProfile> driverProfiles = new List<WindowsDriverUpdateProfile>();

                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<WindowsDriverUpdateProfile, WindowsDriverUpdateProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                    {
                        if (!string.IsNullOrEmpty(profile.DisplayName) && profile.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                        {
                            driverProfiles.Add(profile);
                        }
                        return true;
                    });
                    await pageIterator.IterateAsync();

                }


                return driverProfiles;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while searching for Windows Driver Update Profiles: {ex.Message}", appFunction.Main);
                return new List<WindowsDriverUpdateProfile>();
            }
        }

        /// <summary>
        /// Retrieves all Windows Driver Update Profiles.
        /// </summary>
        public static async Task<List<WindowsDriverUpdateProfile>> GetAllDriverProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles.GetAsync();

                List<WindowsDriverUpdateProfile> driverProfiles = new List<WindowsDriverUpdateProfile>();

                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<WindowsDriverUpdateProfile, WindowsDriverUpdateProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                    {
                        driverProfiles.Add(profile);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                }

                return driverProfiles;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while retrieving all Windows Driver Update Profiles: {ex.Message}", appFunction.Main);
                return new List<WindowsDriverUpdateProfile>();
            }
        }

        /// <summary>
        /// Imports multiple Windows Driver Update Profiles from source to destination tenant.
        /// </summary>
        public static async Task ImportMultipleDriverProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIds, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                AppLogger.Info($"Importing {profileIds.Count} Windows Driver Update Profiles.", appFunction.Import);
                bool hasFailures = false;
                foreach (var profileId in profileIds)
                {
                    var profileName = profileId;
                    try
                    {
                        var sourceProfile = await sourceGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileId].GetAsync();

                        if (sourceProfile == null)
                        {
                            AppLogger.Info($"Profile with ID {profileId} not found in source tenant.", appFunction.Import);
                            continue;
                        }

                        profileName = sourceProfile.DisplayName ?? "Unknown Profile";

                        var newProfile = new WindowsDriverUpdateProfile
                        {
                            OdataType = "#microsoft.graph.windowsDriverUpdateProfile",
                            DisplayName = sourceProfile.DisplayName,
                            Description = sourceProfile.Description,
                            ApprovalType = sourceProfile.ApprovalType,
                            RoleScopeTagIds = sourceProfile.RoleScopeTagIds,
                            DeploymentDeferralInDays = sourceProfile.DeploymentDeferralInDays
                        };


                        var importResult = await destinationGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles.PostAsync(newProfile);
                        AppLogger.Info($"Imported '{importResult?.DisplayName ?? "Unknown"}' successfully.", appFunction.Import);

                        if (assignments && importResult?.Id != null)
                        {
                            await AssignGroupsToSingleDriverProfile(importResult.Id, importResult.DisplayName ?? string.Empty, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to import '{profileName}': {ex.Message}", appFunction.Import);
                        AppLogger.Warning("This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active.", appFunction.Import);
                        hasFailures = true;
                    }
                }
                if (hasFailures)
                    throw new Exception("One or more Windows Driver Update profiles failed to import. See Import.log for details.");
            }
            catch (Exception)
            {
                throw;
            }
        }


        // Note: Assignment structure for Driver Update Profiles differs from Settings Catalog.
        /// <summary>
        /// Assigns groups to a single Windows Driver Update Profile.
        /// Windows Driver Update profiles can ONLY be assigned to device groups - not All Users or All Devices.
        /// </summary>
        public static async Task AssignGroupsToSingleDriverProfile(string profileID, string contentName, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
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

                var assignments = new List<WindowsDriverUpdateProfileAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                AppLogger.Info($"Assigning {groupIDs.Count} groups to driver profile {profileID}.", appFunction.Assignment);

                // Step 1: Add new assignments to request body
                foreach (var groupId in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.Warning("Warning: Windows Driver Update profiles cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", appFunction.Assignment);
                        continue;
                    }

                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.Warning("Warning: Windows Driver Update profiles cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", appFunction.Assignment);
                        continue;
                    }

                    var assignment = new WindowsDriverUpdateProfileAssignment
                    {
                        OdataType = "#microsoft.graph.windowsDriverUpdateProfileAssignment",
                        Target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = groupId,
                            DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                            DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                        }
                    };

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .WindowsDriverUpdateProfiles[profileID]
                    .Assignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        if (existing.Target is AllLicensedUsersAssignmentTarget)
                        {
                            AppLogger.Warning($"Warning: Found existing 'All Users' assignment on Driver Update profile {profileID}. This should not exist and will be skipped.", appFunction.Assignment);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            AppLogger.Warning($"Warning: Found existing 'All Devices' assignment on Driver Update profile {profileID}. This should not exist and will be skipped.", appFunction.Assignment);
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

                // Step 3: Update the profile with the assignments
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsDriverUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].Assign.PostAsync(requestBody);
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (ServiceException svcex)
                {
                    AppLogger.Error($"Graph API error assigning groups to profile {profileID}: {svcex.Message}", appFunction.Assignment);
                    throw;
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
        public static async Task DeleteDriverProfile(GraphServiceClient graphServiceClient, string profileID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (string.IsNullOrEmpty(profileID))
                {
                    throw new ArgumentNullException(nameof(profileID), "Profile ID cannot be null or empty.");
                }

                await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].DeleteAsync();
            }
            catch (ServiceException svcex) when (svcex.ResponseStatusCode == (int)System.Net.HttpStatusCode.NotFound)
            {
                AppLogger.Warning($"Windows Driver Update Profile with ID '{profileID}' not found — may have already been deleted.", appFunction.Delete);
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static async Task RenameDriverProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (string.IsNullOrEmpty(profileID))
                {
                    throw new ArgumentNullException(nameof(profileID), "Profile ID cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                if (selectedRenameMode == "Prefix")
                {
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingProfile.DisplayName ?? string.Empty, newName);

                    var profile = new WindowsDriverUpdateProfile
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].PatchAsync(profile);
                    AppLogger.Info($"Successfully renamed Windows Driver Update Profile from '{existingProfile.DisplayName}' to '{name}'", appFunction.Rename);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var profile = new WindowsDriverUpdateProfile
                    {
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].PatchAsync(profile);
                    AppLogger.Info($"Updated description for Windows Driver Update Profile {profileID} to '{newName}'", appFunction.Rename);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingProfile.DisplayName);

                    var profile = new WindowsDriverUpdateProfile
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].PatchAsync(profile);
                    AppLogger.Info($"Removed prefix from Windows Driver Update Profile {profileID}, new name: '{name}'", appFunction.Rename);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while renaming Windows Driver Update Profile: {ex.Message}", appFunction.Rename);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllWindowsDriverUpdateContentAsync(GraphServiceClient graphServiceClient)
        {
            var profiles = await GetAllDriverProfiles(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var profile in profiles)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows Driver Update",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchWindowsDriverUpdateContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var profiles = await SearchForDriverProfiles(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var profile in profiles)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows Driver Update",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Exports a Windows Driver Update profile's full data as a JsonElement for JSON file export.
        /// </summary>
        public static async Task<JsonElement?> ExportWindowsDriverUpdatePolicyDataAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileId].GetAsync();

                if (result == null)
                {
                    AppLogger.Warning($"Windows Driver Update profile {profileId} not found for export.", appFunction.JsonExport);
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
                AppLogger.Error($"Error exporting Windows Driver Update profile {profileId}: {ex.Message}", appFunction.JsonExport);
                return null;
            }
        }

        /// <summary>
        /// Imports a Windows Driver Update profile from previously exported JSON data into the destination tenant.
        /// </summary>
        public static async Task<string?> ImportWindowsDriverUpdateFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exportedProfile = parseNode.GetObjectValue(WindowsDriverUpdateProfile.CreateFromDiscriminatorValue);

                if (exportedProfile == null)
                {
                    AppLogger.Error("Failed to deserialize Windows Driver Update profile data from JSON.", appFunction.Import);
                    return null;
                }

                var newProfile = new WindowsDriverUpdateProfile
                {
                    OdataType = "#microsoft.graph.windowsDriverUpdateProfile",
                    DisplayName = exportedProfile.DisplayName,
                    Description = exportedProfile.Description,
                    ApprovalType = exportedProfile.ApprovalType,
                    RoleScopeTagIds = exportedProfile.RoleScopeTagIds,
                    DeploymentDeferralInDays = exportedProfile.DeploymentDeferralInDays
                };

                var imported = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles.PostAsync(newProfile);

                AppLogger.Info($"Imported Windows Driver Update profile: {imported?.DisplayName}", appFunction.Import);
                return imported?.DisplayName;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error importing Windows Driver Update profile from JSON: {ex.Message}", appFunction.Import);
                AppLogger.Warning($"This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", appFunction.Import);
                return null;
            }
        }

        /// <summary>
        /// Checks if a Windows driver update profile has any group assignments.
        /// </summary>
        public static async Task<bool?> HasWindowsDriverUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileId].Assignments.GetAsync(rc =>
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
        /// Gets detailed assignment information for a Windows Driver Update profile.
        /// </summary>
        public static async Task<List<AssignmentInfo>?> GetWindowsDriverUpdateAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var details = new List<AssignmentInfo>();
                var result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileId].Assignments.GetAsync();

                while (result?.Value != null)
                {
                    foreach (var assignment in result.Value)
                    {
                        details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileId]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                return details;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error getting assignment details for Windows Driver Update {profileId}: {ex.Message}", appFunction.ManageAssignment);
                return null;
            }
        }

        /// <summary>
        /// Removes all assignments from a Windows Driver Update profile.
        /// </summary>
        public static async Task RemoveAllWindowsDriverUpdateAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsDriverUpdateProfiles.Item.Assign.AssignPostRequestBody
            {
                Assignments = new List<WindowsDriverUpdateProfileAssignment>()
            };

            await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileId].Assign.PostAsync(requestBody);
            AppLogger.Info($"Removed all assignments from Windows Driver Update profile {profileId}.", appFunction.ManageAssignment);
        }
    }
}
