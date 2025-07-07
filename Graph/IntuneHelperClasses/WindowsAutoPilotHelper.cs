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

                List<WindowsAutopilotDeploymentProfileAssignment> assignments = new List<WindowsAutopilotDeploymentProfileAssignment>();

                foreach (var group in groupID)
                {
                    var assignment = new WindowsAutopilotDeploymentProfileAssignment
                    {
                        Id = profileID + "_" + group + "_0",
                        Source = DeviceAndAppManagementAssignmentSource.Direct,
                        SourceId = profileID,
                        Target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = group,
                        },
                    };
                    assignments.Add(assignment);

                    await destinationGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].Assignments.PostAsync(assignment);
                }

                //var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsAutopilotDeploymentProfiles.Item.Assign.AssignPostRequestBody
                //{
                //    AdditionalData = new Dictionary<string, object>
                //    {
                //        { "Assignments", assignments }
                //    },
                //};


                try
                {
                    //await destinationGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[profileID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile("Assigned groups to profile " + profileID + " with filter type" + deviceAndAppManagementAssignmentFilterType.ToString());
                }
                catch (Exception ex)
                {
                    LogToImportStatusFile($"Error assigning groups to profile {profileID}",LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while assigning groups to a single Windows AutoPilot profile", LogLevels.Error);
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
    }
}
