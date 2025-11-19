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
    public class WindowsAutoPilotHelper
    {
        public static async Task<List<WindowsAutopilotDeploymentProfile>> SearchForWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToImportStatusFile("Searching for Windows AutoPilot profiles. Search query: " + searchQuery);

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

                LogToImportStatusFile($"Found {profiles.Count} Windows AutoPilot profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while searching for Windows AutoPilot profiles",LogLevels.Error);
                return new List<WindowsAutopilotDeploymentProfile>();
            }
        }

        public static async Task<List<WindowsAutopilotDeploymentProfile>> GetAllWindowsAutoPilotProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToImportStatusFile("Retrieving all Windows AutoPilot profiles.");

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

                LogToImportStatusFile($"Found {profiles.Count} Windows AutoPilot profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while retrieving all Windows AutoPilot profiles",LogLevels.Error);
                return new List<WindowsAutopilotDeploymentProfile>();
            }
        }
        public static async Task ImportMultipleWindowsAutoPilotProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient,List<string> profiles, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {profiles.Count} Windows AutoPilot profiles.");
                foreach (var profile in profiles)
                {
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profile].GetAsync();

                        // Check what Autopilot profile it is



                        if (result.OdataType.Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
                        {
                            WriteToImportStatusFile($"Hybrid Autopilot profiles are currently bugged in Graph API/C# SDK. Please handle manually for now.",LogType.Warning);

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
                            WriteToImportStatusFile($"Imported profile: {requestBody.DisplayName}");
                            if (assignments)
                            {
                                await AssignGroupsToSingleWindowsAutoPilotProfile(import.Id, groups, destinationGraphServiceClient);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToImportStatusFile($"Error importing profile {profile}", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred during the import process", LogLevels.Error);
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
                        WriteToImportStatusFile($"Warning: AutoPilot profiles cannot be assigned to 'All Users'. Skipping this assignment.", LogType.Warning);
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
                            WriteToImportStatusFile($"Warning: Found existing 'All Users' assignment on AutoPilot profile {profileID}. This should not exist and will be skipped.", LogType.Warning);
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

                        WriteToImportStatusFile($"Assigned {targetType} to AutoPilot profile {profileID}.");
                    }
                    catch (Exception ex)
                    {
                        LogToImportStatusFile($"Error assigning to profile {profileID}: {ex.Message}", LogLevels.Error);
                    }
                }

                WriteToImportStatusFile($"Assigned {successCount} of {assignments.Count} assignments to AutoPilot profile {profileID}.");
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while assigning groups to a single Windows AutoPilot profile", LogLevels.Warning);
                LogToImportStatusFile(ex.Message, LogLevels.Error);
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
                WriteToImportStatusFile("An error occurred while attempting to delete Autopilot profile assignments", LogType.Error);
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while attempting to delete Autopilot profile assignments",LogType.Error);
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
                WriteToImportStatusFile("An error occurred while deleting Windows Autopilot profiles",LogType.Error);
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

                // Look up the existing profile
                var existingProfile = await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].GetAsync();

                if (existingProfile == null)
                {
                    throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingProfile.DisplayName, newName);

                var profile = new WindowsAutopilotDeploymentProfile
                {
                    DisplayName = name,
                };

                await graphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].PatchAsync(profile);
                WriteToImportStatusFile($"Renamed Windows Autopilot profile '{existingProfile.DisplayName}' to '{name}'", LogType.Info);
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming Windows Autopilot profiles", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
