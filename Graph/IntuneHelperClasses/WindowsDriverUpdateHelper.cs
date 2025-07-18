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
    public class WindowsDriverUpdateHelper
    {
        /// <summary>
        /// Searches for Windows Driver Update Profiles matching the specified search query.
        /// </summary>
        /// <param name="graphServiceClient">The GraphServiceClient instance for Microsoft Graph calls.</param>
        /// <param name="searchQuery">The string to search for in profile DisplayName.</param>
        /// <returns>A list of WindowsDriverUpdateProfile objects that match the search criteria.</returns>
        public static async Task<List<WindowsDriverUpdateProfile>> SearchForDriverProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToImportStatusFile("Searching for Windows Driver Update Profiles. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles.GetAsync();

                List<WindowsDriverUpdateProfile> driverProfiles = new List<WindowsDriverUpdateProfile>();

                if (result?.Value != null) // Check if result and Value are not null
                {
                    // Use PageIterator to handle paginated results
                    var pageIterator = PageIterator<WindowsDriverUpdateProfile, WindowsDriverUpdateProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                    {
                        if (!string.IsNullOrEmpty(profile.DisplayName) && profile.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                        {
                            driverProfiles.Add(profile);
                        }
                        return true;
                    });
                    await pageIterator.IterateAsync();

                    LogToImportStatusFile($"Found {driverProfiles.Count} Windows Driver Update Profiles matching the search query.");
                }
                else
                {
                    LogToImportStatusFile("No Windows Driver Update Profiles found matching the search query or the result was null.",LogLevels.Error);
                }


                return driverProfiles;
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while searching for Windows Driver Update Profiles",LogLevels.Error);
                return new List<WindowsDriverUpdateProfile>();
            }
        }

        /// <summary>
        /// Retrieves all Windows Driver Update Profiles.
        /// </summary>
        /// <param name="graphServiceClient">The GraphServiceClient instance for Microsoft Graph calls.</param>
        /// <returns>A list of all WindowsDriverUpdateProfile objects.</returns>
        public static async Task<List<WindowsDriverUpdateProfile>> GetAllDriverProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToImportStatusFile("Retrieving all Windows Driver Update Profiles.");

                var result = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles.GetAsync();

                List<WindowsDriverUpdateProfile> driverProfiles = new List<WindowsDriverUpdateProfile>();

                if (result?.Value != null) // Check if result and Value are not null
                {
                    var pageIterator = PageIterator<WindowsDriverUpdateProfile, WindowsDriverUpdateProfileCollectionResponse>.CreatePageIterator(graphServiceClient, result, (profile) =>
                    {
                        driverProfiles.Add(profile);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                    LogToImportStatusFile($"Found {driverProfiles.Count} Windows Driver Update Profiles.");
                }
                else
                {
                    LogToImportStatusFile("No Windows Driver Update Profiles found or the result was null.");
                }

                return driverProfiles;
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while retrieving all Windows Driver Update Profiles", LogLevels.Error);
                return new List<WindowsDriverUpdateProfile>();
            }
        }

        /// <summary>
        /// Imports multiple Windows Driver Update Profiles from source to destination tenant.
        /// </summary>
        /// <param name="sourceGraphServiceClient">GraphServiceClient for source tenant.</param>
        /// <param name="destinationGraphServiceClient">GraphServiceClient for destination tenant.</param>
        /// <param name="profileIds">List of profile IDs to import.</param>
        /// <param name="assignments">Whether to import assignments after creating profiles.</param>
        /// <param name="filter">Whether to apply an assignment filter.</param>
        /// <param name="groups">List of group IDs for assignment.</param>
        /// <returns>A Task representing the asynchronous import operation.</returns>
        public static async Task ImportMultipleDriverProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> profileIds,bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {profileIds.Count} Windows Driver Update Profiles.");
                foreach (var profileId in profileIds)
                {
                    var profileName = "";
                    try
                    {
                        // Get the source profile
                        var sourceProfile = await sourceGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileId].GetAsync();

                        if (sourceProfile == null)
                        {
                            WriteToImportStatusFile($"Profile with ID {profileId} not found in source tenant."); // Removed LogType
                            continue;
                        }

                        profileName = sourceProfile.DisplayName ?? "Unknown Profile"; // Handle potential null DisplayName

                        // Create the new profile object for the destination tenant
                        // Map relevant properties. Check API documentation for required/allowed properties.
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
                        WriteToImportStatusFile($"Imported profile: {importResult?.DisplayName ?? "Unknown"}"); // Added null check

                        if (assignments && importResult?.Id != null)
                        {
                            // Assign groups using the specific method for Driver Update Profiles
                            await AssignGroupsToSingleDriverProfile(importResult.Id, groups, destinationGraphServiceClient, filter); // Pass filter status
                        }
                    }
                    catch (Exception ex)
                    {
                        //rtb.AppendText($"This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active\n");
                        WriteToImportStatusFile($"Failed to import Windows Driver Update policy {profileName}: {ex.Message}",LogType.Error);
                        WriteToImportStatusFile("This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active",LogType.Warning);
                    }
                }
                WriteToImportStatusFile("Windows Driver Update policy import process finished.");
            }

            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred during the driver profile import process",LogLevels.Warning);
            }
        }


        // Note: Assignment structure for Driver Update Profiles differs from Settings Catalog.
        /// <summary>
        /// Assigns groups to a single Windows Driver Update Profile.
        /// </summary>
        /// <param name="profileID">The ID of the profile to assign groups to.</param>
        /// <param name="groupIDs">List of group IDs to assign.</param>
        /// <param name="destinationGraphServiceClient">GraphServiceClient for the destination tenant.</param>
        /// <param name="useFilter">Whether to apply assignment filters.</param>
        /// <returns>A Task representing the asynchronous assignment operation.</returns>
        public static async Task AssignGroupsToSingleDriverProfile(string profileID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient, bool useFilter)
        {
            try
            {
                if (string.IsNullOrEmpty(profileID))
                {
                    throw new ArgumentNullException(nameof(profileID));
                }

                if (groupIDs == null || !groupIDs.Any())
                {
                    LogToImportStatusFile($"No groups provided for assignment to profile {profileID}."); // Removed LogType
                    return; // Nothing to assign
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                LogToImportStatusFile($"Assigning {groupIDs.Count} groups to driver profile {profileID}. Filter enabled: {useFilter}");


                var assignments = new List<WindowsDriverUpdateProfileAssignment>();

                foreach (var groupId in groupIDs)
                {
                    var assignment = new WindowsDriverUpdateProfileAssignment
                    {
                        OdataType = "#microsoft.graph.windowsDriverUpdateProfileAssignment",
                        Target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = groupId,
                            // Apply filter information if 'useFilter' is true and a filter is selected
                            DeviceAndAppManagementAssignmentFilterId = useFilter ? SelectedFilterID : null, // Use SelectedFilterID from GlobalVariables
                            DeviceAndAppManagementAssignmentFilterType = useFilter ? deviceAndAppManagementAssignmentFilterType : Microsoft.Graph.Beta.Models.DeviceAndAppManagementAssignmentFilterType.None // Use type from GlobalVariables
                        }
                        // Source and SourceId might not be applicable/required for driver profile assignments directly via POST /assign
                    };
                    assignments.Add(assignment);
                }


                // The endpoint for assigning driver profiles is different
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsDriverUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };


                try
                {
                    // Use the correct endpoint and method for driver profile assignment
                    await destinationGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile($"Successfully initiated assignment of {groupIDs.Count} groups to profile {profileID}. Filter: {SelectedFilterID ?? "None"}");
                }
                catch (ServiceException svcex)
                {
                    // More specific error handling for Graph API calls
                    // Extracting the error message might require inspecting the exception details further
                    string errorMessage = svcex.Message; // Basic message
                                                         // Consider logging svcex.ToString() for more details if needed
                    WriteToImportStatusFile($"Graph API error assigning groups to profile {profileID}: {errorMessage}"); // Removed LogType
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile($"Error assigning groups to profile {profileID}: {ex.Message}"); // Removed LogType
                }
            }
            catch (ArgumentNullException argEx)
            {
                WriteToImportStatusFile("Argument null exception during group assignment setup.",LogType.Error);
            }
            catch (Exception ex)
            {
                // Catch unexpected errors during the setup phase
                WriteToImportStatusFile("An unexpected error occurred while preparing group assignments for a driver profile.",LogType.Error);
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

                WriteToImportStatusFile($"Attempting to delete Windows Driver Update Profile with ID: {profileID}");
                await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].DeleteAsync();
                WriteToImportStatusFile($"Successfully deleted Windows Driver Update Profile with ID: {profileID}");
            }
            catch (ServiceException svcex) when (svcex.ResponseStatusCode == (int)System.Net.HttpStatusCode.NotFound) // Corrected comparison
            {
                // Handle case where the profile doesn't exist (might have been deleted already)
                WriteToImportStatusFile($"Windows Driver Update Profile with ID {profileID} not found. It might have already been deleted."); // Removed LogType
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred while deleting Windows Driver Update Profile with ID: {profileID}",LogType.Error);
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

                // Look up the existing profile
                var existingProfile = await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].GetAsync();

                if (existingProfile == null)
                {
                    throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingProfile.DisplayName, newName);

                var profile = new WindowsDriverUpdateProfile
                {
                    DisplayName = name,
                };

                await graphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[profileID].PatchAsync(profile);
                WriteToImportStatusFile($"Successfully renamed Windows Driver Update Profile from '{existingProfile.DisplayName}' to '{name}'");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming Windows Driver Update Profile", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
