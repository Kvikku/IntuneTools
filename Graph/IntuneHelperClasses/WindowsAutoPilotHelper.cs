using IntuneTools.Utilities;
using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsAutoPilotHelper
    {
        public static async Task<List<WindowsAutopilotDeploymentProfile>> SearchForWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Searching for Windows AutoPilot profiles. Search query: " + searchQuery);

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

                LogToFunctionFile(appFunction.Main, $"Found {profiles.Count} Windows AutoPilot profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while searching for Windows AutoPilot profiles", LogLevels.Error);
                return new List<WindowsAutopilotDeploymentProfile>();
            }
        }

        public static async Task<List<WindowsAutopilotDeploymentProfile>> GetAllWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all Windows AutoPilot profiles.");

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

                LogToFunctionFile(appFunction.Main, $"Found {profiles.Count} Windows AutoPilot profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all Windows AutoPilot profiles", LogLevels.Error);
                return new List<WindowsAutopilotDeploymentProfile>();
            }
        }
        public static async Task ImportMultipleWindowsAutoPilotProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profiles, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Importing {profiles.Count} Windows AutoPilot profiles.");
                foreach (var profile in profiles)
                {
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profile].GetAsync();

                        // Check what Autopilot profile it is



                        if (result.OdataType.Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, "Hybrid Autopilot profiles are currently bugged in Graph API/C# SDK. Please handle manually for now.", LogLevels.Warning);

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
                            LogToFunctionFile(appFunction.Main, $"Imported profile: {requestBody.DisplayName}");
                            if (assignments)
                            {
                                await AssignGroupsToSingleWindowsAutoPilotProfile(import.Id, groups, destinationGraphServiceClient);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error importing profile {profile}", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred during the import process", LogLevels.Error);
            }
        }

        public static async Task AssignGroupsToSingleWindowsAutoPilotProfile(string profileID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
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
                        LogToFunctionFile(appFunction.Main, "Warning: AutoPilot profiles cannot be assigned to 'All Users'. Skipping this assignment.", LogLevels.Warning);
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
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on AutoPilot profile {profileID}. This should not exist and will be skipped.", LogLevels.Warning);
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

                        string targetType = assignment.Target switch
                        {
                            AllDevicesAssignmentTarget => "All Devices",
                            GroupAssignmentTarget gt => $"group {gt.GroupId}",
                            _ => "unknown target"
                        };

                        LogToFunctionFile(appFunction.Main, $"Assigned {targetType} to AutoPilot profile {profileID}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Error assigning to profile {profileID}: {ex.Message}", LogLevels.Error);
                    }
                }

                LogToFunctionFile(appFunction.Main, $"Assigned {successCount} of {assignments.Count} assignments to AutoPilot profile {profileID}.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to a single Windows AutoPilot profile", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
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
                LogToFunctionFile(appFunction.Main, "An error occurred while attempting to delete Autopilot profile assignments", LogLevels.Error);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while attempting to delete Autopilot profile assignments", LogLevels.Error);
            }
        }

        public static async Task<bool> CheckIfAutoPilotProfileHasAssignments(GraphServiceClient graphServiceClient, string profileID)
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

                return false;
            }
            catch (Exception ex)
            {

                return false;
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
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting Windows Autopilot profiles", LogLevels.Error);
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

                    LogToFunctionFile(appFunction.Main, $"Renamed Windows Autopilot profile '{existingProfile.DisplayName}' to '{name}'", LogLevels.Info);
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
                        LogToFunctionFile(appFunction.Main, "Active Directory Autopilot profiles is not supported yet. Skipping.", LogLevels.Warning);
                        return;
                    }

                    var profile = new AzureADWindowsAutopilotDeploymentProfile
                    {
                        OdataType = existingProfile.OdataType ?? "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile",
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].PatchAsync(profile);
                    LogToFunctionFile(appFunction.Main, $"Updated description for Windows Autopilot profile {profileID} to '{newName}'", LogLevels.Info);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming Windows Autopilot profiles", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
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
    }
}
