using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsAutoPilotHelper
    {
        public static async Task<List<WindowsAutopilotDeploymentProfile>> SearchForWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

                List<WindowsAutopilotDeploymentProfile> profiles = new List<WindowsAutopilotDeploymentProfile>();
                var pageIterator = PageIterator<WindowsAutopilotDeploymentProfile, WindowsAutopilotDeploymentProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                {
                    profiles.Add(profile);
                    return true;
                });
                await pageIterator.IterateAsync();

                return profiles;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while searching for Windows AutoPilot profiles: {ex.Message}", appFunction.Main);
                return new List<WindowsAutopilotDeploymentProfile>();
            }
        }

        public static async Task<List<WindowsAutopilotDeploymentProfile>> GetAllWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                List<WindowsAutopilotDeploymentProfile> profiles = new List<WindowsAutopilotDeploymentProfile>();
                var pageIterator = PageIterator<WindowsAutopilotDeploymentProfile, WindowsAutopilotDeploymentProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                {
                    profiles.Add(profile);
                    return true;
                });
                await pageIterator.IterateAsync();

                return profiles;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while retrieving all Windows AutoPilot profiles: {ex.Message}", appFunction.Main);
                return new List<WindowsAutopilotDeploymentProfile>();
            }
        }
        public static async Task ImportMultipleWindowsAutoPilotProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profiles, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                AppLogger.Info($"Importing {profiles.Count} Windows AutoPilot profiles.", appFunction.Import);
                foreach (var profile in profiles)
                {
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profile].GetAsync();

                        // Check what Autopilot profile it is



                        if (result.OdataType.Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
                        {
                            AppLogger.Warning("Hybrid Autopilot profiles are currently bugged in Graph API/C# SDK. Please handle manually for now.", appFunction.Import);

                            //var requestBody = new ActiveDirectoryWindowsAutopilotDeploymentProfile()
                            //{

                            //};

                            //foreach (var property in result.GetType().GetProperties())
                            //{
                            //    var value = property.GetValue(result);
                            //    if (value != null && property.CanWrite)
                            //    {
                            //        property.SetValue(requestBody, value);
                            //    }
                            //}

                            //requestBody.Id = "";

                            //await destinationGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.PostAsync(requestBody);
                            //rtb.AppendText($"Imported profile: {requestBody.DisplayName}\n");
                            //WriteToImportStatusFile($"Imported profile: {requestBody.DisplayName}");

                            //if (assignments)
                            //{
                            //    await AssignGroupsToSingleWindowsAutoPilotProfile(requestBody.Id, groups, destinationGraphServiceClient);
                            //}

                        }

                        else if (result.OdataType.Contains("azureAD"))
                        {
                            var requestBody = new WindowsAutopilotDeploymentProfile
                            {
                            };

                            foreach (var property in result.GetType().GetProperties())
                            {
                                var value = property.GetValue(result);
                                if (value != null && property.CanWrite)
                                {
                                    property.SetValue(requestBody, value);
                                }
                            }
                            var import = await destinationGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.PostAsync(requestBody);
                            AppLogger.Info($"Imported profile: {requestBody.DisplayName}", appFunction.Import);
                            if (assignments)
                            {
                                await AssignGroupsToSingleWindowsAutoPilotProfile(import.Id, requestBody.DisplayName ?? string.Empty, groups, destinationGraphServiceClient);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Error importing profile {profile}: {ex.Message}", appFunction.Import);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred during the import process: {ex.Message}", appFunction.Import);
            }
        }

        public static async Task AssignGroupsToSingleWindowsAutoPilotProfile(string profileID, string contentName, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (profileID == null)
                {
                    throw new ArgumentNullException(nameof(profileID));
                }

                if (groupID == null)
                {
                    throw new ArgumentNullException(nameof(groupID));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<WindowsAutopilotDeploymentProfileAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllDevices = false;

                // Step 1: Add new assignments to list
                foreach (var group in groupID)
                {
                    if (string.IsNullOrWhiteSpace(group) || !seenGroupIds.Add(group))
                    {
                        continue;
                    }

                    // Check if this is All Users - AutoPilot profiles cannot be assigned to All Users
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.Warning("Warning: AutoPilot profiles cannot be assigned to 'All Users'. Skipping this assignment.", appFunction.Assignment);
                        continue;
                    }

                    WindowsAutopilotDeploymentProfileAssignment assignment;

                    // Check if this is All Devices
                    if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        assignment = new WindowsAutopilotDeploymentProfileAssignment
                        {
                            Source = DeviceAndAppManagementAssignmentSource.Direct,
                            SourceId = profileID,
                            Target = new AllDevicesAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allDevicesAssignmentTarget"
                            }
                        };
                    }
                    else
                    {
                        // Regular group assignment
                        assignment = new WindowsAutopilotDeploymentProfileAssignment
                        {
                            Source = DeviceAndAppManagementAssignmentSource.Direct,
                            SourceId = profileID,
                            Target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                GroupId = group
                            }
                        };
                    }

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .WindowsAutopilotDeploymentProfiles[profileID]
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
                            AppLogger.Warning($"Warning: Found existing 'All Users' assignment on AutoPilot profile {profileID}. This should not exist and will be skipped.", appFunction.Assignment);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip if we're already adding All Devices
                            if (!hasAllDevices)
                            {
                                assignments.Add(existing);
                            }
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

                // Step 3: Post assignments individually (AutoPilot profiles require individual posts)
                int successCount = 0;
                bool hasFailures = false;
                foreach (var assignment in assignments)
                {
                    // Skip existing assignments that were already posted
                    if (!string.IsNullOrEmpty(assignment.Id))
                    {
                        successCount++;
                        continue;
                    }

                    try
                    {
                        await destinationGraphServiceClient
                            .DeviceManagement
                            .WindowsAutopilotDeploymentProfiles[profileID]
                            .Assignments
                            .PostAsync(assignment);

                        successCount++;

                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Error assigning '{contentName}': {ex.Message}", appFunction.Assignment);
                        hasFailures = true;
                    }
                }

                AppLogger.Info($"Assigned '{contentName}' to {successCount} group(s).", appFunction.Assignment);
                if (hasFailures)
                    throw new Exception($"One or more group assignments failed for '{contentName}'. See log for details.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task DeleteWindowsAutoPilotProfileAssignments(GraphServiceClient graphServiceClient, string profileID)
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

                // Get the assignments for the profile

                var result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].Assignments.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                // If the result is not null and has assignments, delete them

                if (result != null && result.Value != null && result.Value.Count > 0)
                {
                    foreach (var assignment in result.Value)
                    {
                        await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].Assignments[assignment.Id].DeleteAsync();
                    }
                }

            }
            catch (ODataError error)
            {
                AppLogger.Error("An error occurred while attempting to delete Autopilot profile assignments", appFunction.Delete);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while attempting to delete Autopilot profile assignments: {ex.Message}", appFunction.Delete);
            }
        }

        public static async Task<bool?> CheckIfAutoPilotProfileHasAssignments(GraphServiceClient graphServiceClient, string profileID)
        {
            try
            {
                // Check if the GraphServiceClient and profileID are not null
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (profileID == null)
                {
                    throw new InvalidOperationException("Profile ID cannot be null.");
                }

                // Get the assignments for the profile
                var result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].Assignments.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                // If the result is not null and has assignments, return true
                if (result != null && result.Value != null && result.Value.Count > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (ODataError error)
            {
                AppLogger.Error($"OData error checking AutoPilot profile assignments: {error.Error?.Message}", appFunction.Main);
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error checking AutoPilot profile assignments: {ex.Message}", appFunction.Main);
                return null;
            }
        }
        public static async Task DeleteWindowsAutopilotProfile(GraphServiceClient graphServiceClient, string profileID)
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



                await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].DeleteAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while deleting Windows Autopilot profiles: {ex.Message}", appFunction.Delete);
            }
        }

        public static async Task RenameWindowsAutoPilotProfile(GraphServiceClient graphServiceClient, string profileID, string newName)
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
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingProfile.DisplayName ?? string.Empty, newName);

                    // after existingProfile is retrieved
                    if (existingProfile.OdataType?.Contains("activeDirectory", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var profile = new ActiveDirectoryWindowsAutopilotDeploymentProfile
                        {
                            OdataType = existingProfile.OdataType,
                            DisplayName = name
                        };
                        await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].PatchAsync(profile);
                    }
                    else
                    {
                        var profile = new AzureADWindowsAutopilotDeploymentProfile
                        {
                            OdataType = existingProfile.OdataType ?? "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile",
                            DisplayName = name
                        };
                        await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].PatchAsync(profile);
                    }

                    AppLogger.Info($"Renamed Windows Autopilot profile '{existingProfile.DisplayName}' to '{name}'", appFunction.Rename);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing profile
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    if (existingProfile.OdataType?.Contains("activeDirectory", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        AppLogger.Warning("Active Directory Autopilot profiles is not supported yet. Skipping.", appFunction.Rename);
                        return;
                    }

                    var profile = new AzureADWindowsAutopilotDeploymentProfile
                    {
                        OdataType = existingProfile.OdataType ?? "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile",
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].PatchAsync(profile);
                    AppLogger.Info($"Updated description for Windows Autopilot profile {profileID} to '{newName}'", appFunction.Rename);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingProfile = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].GetAsync();

                    if (existingProfile == null)
                    {
                        throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                    }

                    if (existingProfile.OdataType?.Contains("activeDirectory", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        AppLogger.Warning("Active Directory Autopilot profiles is not supported yet. Skipping.", appFunction.Rename);
                        return;
                    }

                    var name = RemovePrefixFromPolicyName(existingProfile.DisplayName);

                    var profile = new AzureADWindowsAutopilotDeploymentProfile
                    {
                        OdataType = existingProfile.OdataType ?? "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile",
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].PatchAsync(profile);
                    AppLogger.Info($"Removed prefix from Windows Autopilot profile {profileID}, new name: '{name}'", appFunction.Rename);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while renaming Windows Autopilot profiles: {ex.Message}", appFunction.Rename);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllWindowsAutoPilotContentAsync(GraphServiceClient graphServiceClient)
        {
            var profiles = await GetAllWindowsAutoPilotProfiles(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var profile in profiles)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows AutoPilot Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchWindowsAutoPilotContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var profiles = await SearchForWindowsAutoPilotProfiles(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var profile in profiles)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows AutoPilot Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Exports a Windows AutoPilot deployment profile's full data as a JsonElement for JSON file export.
        /// </summary>
        public static async Task<JsonElement?> ExportWindowsAutoPilotProfileDataAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileId].GetAsync();

                if (result == null)
                {
                    AppLogger.Warning($"Windows AutoPilot profile {profileId} not found for export.", appFunction.JsonExport);
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
                AppLogger.Error($"Error exporting Windows AutoPilot profile {profileId}: {ex.Message}", appFunction.JsonExport);
                return null;
            }
        }

        /// <summary>
        /// Imports a Windows AutoPilot deployment profile from previously exported JSON data into the destination tenant.
        /// Note: Hybrid Azure AD join profiles are not supported via Graph API and will be skipped.
        /// </summary>
        public static async Task<string?> ImportWindowsAutoPilotProfileFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exported = parseNode.GetObjectValue(WindowsAutopilotDeploymentProfile.CreateFromDiscriminatorValue);

                if (exported == null)
                {
                    AppLogger.Error("Failed to deserialize Windows AutoPilot profile data from JSON.", appFunction.Import);
                    return null;
                }

                // Hybrid AD join profiles are not supported via Graph API
                if (exported.OdataType != null && exported.OdataType.Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.Warning($"Skipping Hybrid Azure AD join AutoPilot profile '{exported.DisplayName}' - not supported via Graph API.", appFunction.Import);
                    return null;
                }

                var newProfile = new WindowsAutopilotDeploymentProfile();

                foreach (var property in exported.GetType().GetProperties())
                {
                    var value = property.GetValue(exported);
                    if (value != null && property.CanWrite)
                    {
                        property.SetValue(newProfile, value);
                    }
                }

                newProfile.Id = "";

                var imported = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.PostAsync(newProfile);

                AppLogger.Info($"Imported Windows AutoPilot profile: {imported?.DisplayName}", appFunction.Import);
                return imported?.DisplayName;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error importing Windows AutoPilot profile from JSON: {ex.Message}", appFunction.Import);
                return null;
            }
        }

        /// <summary>
        /// Gets detailed assignment information for a Windows AutoPilot Deployment Profile.
        /// </summary>
        public static async Task<List<AssignmentInfo>?> GetWindowsAutoPilotAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            try
            {
                var details = new List<AssignmentInfo>();
                var result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileId].Assignments.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

                while (result?.Value != null)
                {
                    foreach (var assignment in result.Value)
                    {
                        details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileId]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                return details;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error getting assignment details for Windows AutoPilot Profile {profileId}: {ex.Message}", appFunction.ManageAssignment);
                return null;
            }
        }

        /// <summary>
        /// Removes all assignments from a Windows AutoPilot Deployment Profile.
        /// Uses individual DELETE calls since AutoPilot does not support batch assignment removal.
        /// </summary>
        public static async Task RemoveAllWindowsAutoPilotAssignmentsAsync(GraphServiceClient graphServiceClient, string profileId)
        {
            var result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileId].Assignments.GetAsync(rc =>
            {
                rc.QueryParameters.Top = 1000;
            });

            while (result?.Value != null && result.Value.Count > 0)
            {
                foreach (var assignment in result.Value)
                {
                    await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileId].Assignments[assignment.Id].DeleteAsync();
                }

                if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                result = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileId]
                    .Assignments.WithUrl(result.OdataNextLink).GetAsync();
            }

            AppLogger.Info($"Removed all assignments from Windows AutoPilot Profile {profileId}.", appFunction.ManageAssignment);
        }
    }
}
