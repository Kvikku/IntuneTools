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
    public class WindowsQualityUpdateProfileHelper
    {
        // Expedite policies (not regular quality update policies)
        public static async Task<List<WindowsQualityUpdateProfile>> SearchForWindowsQualityUpdateProfiles(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile("Searching for Windows Quality Update profiles. Search query: " + searchQuery);

                var allProfiles = await GetAllWindowsQualityUpdateProfiles(graphServiceClient);
                var filteredProfiles = allProfiles.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

                WriteToImportStatusFile($"Found {filteredProfiles.Count} Windows Quality Update profiles matching the search query.");

                return filteredProfiles;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while searching for Windows Quality Update profiles",LogType.Error);
                return new List<WindowsQualityUpdateProfile>();
            }
        }

        public static async Task<List<WindowsQualityUpdateProfile>> GetAllWindowsQualityUpdateProfiles(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile("Retrieving all Windows Quality Update profiles.");

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
                    WriteToImportStatusFile("No Windows Quality Update profiles found or result was null.");
                }

                WriteToImportStatusFile($"Found {profiles.Count} Windows Quality Update profiles.");

                return profiles;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all Windows Quality Update profiles",LogType.Error);
                return new List<WindowsQualityUpdateProfile>();
            }
        }
        public static async Task ImportMultipleWindowsQualityUpdateProfiles(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient,List<string> profileIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {profileIDs.Count} Windows Quality Update profiles.");
                string profileName = "";

                foreach (var profileId in profileIDs)
                {
                    try
                    {
                        var sourceProfile = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileId].GetAsync();

                        if (sourceProfile == null)
                        {
                            WriteToImportStatusFile($"Skipping profile ID {profileId}: Not found in source tenant.");
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

                        WriteToImportStatusFile($"Imported profile: {importedProfile?.DisplayName ?? "Unnamed Profile"} (ID: {importedProfile?.Id ?? "Unknown ID"})");

                        if (assignments && groups != null && groups.Any() && importedProfile?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsQualityUpdateProfile(importedProfile.Id, groups, destinationGraphServiceClient, filter);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToImportStatusFile($"Error importing profile {profileName}: {ex.Message}",LogType.Error);
                        WriteToImportStatusFile("There is currently a known bug with importing Windows Quality Update profiles. " +
                                                "This will be fixed in a future release. " +
                                                "For now, please manually assign the groups to the imported profiles.", LogType.Warning);
                    }
                }
                WriteToImportStatusFile("Windows Quality Update profile import process finished.");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred during the import process: {ex.Message}",LogType.Error);
            }
        }


        public static async Task AssignGroupsToSingleWindowsQualityUpdateProfile(string profileID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient, bool applyFilter)
        {
            try
            {
                if (string.IsNullOrEmpty(profileID))
                {
                    throw new ArgumentNullException(nameof(profileID));
                }

                if (groupIDs == null || !groupIDs.Any())
                {
                    WriteToImportStatusFile($"No groups provided for assignment to profile {profileID}. Skipping assignment.");
                    return;
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                WriteToImportStatusFile($"Assigning {groupIDs.Count} groups to Windows Quality Update profile {profileID}. Apply filter: {applyFilter}");

                List<WindowsQualityUpdateProfileAssignment> assignments = new List<WindowsQualityUpdateProfileAssignment>();

                foreach (var groupId in groupIDs)
                {
                    var assignmentTarget = new GroupAssignmentTarget
                    {
                        OdataType = "#microsoft.graph.groupAssignmentTarget",
                        GroupId = groupId,
                        DeviceAndAppManagementAssignmentFilterId = applyFilter ? SelectedFilterID : null,
                        DeviceAndAppManagementAssignmentFilterType = applyFilter ? deviceAndAppManagementAssignmentFilterType : Microsoft.Graph.Beta.Models.DeviceAndAppManagementAssignmentFilterType.None,
                    };

                    var assignment = new WindowsQualityUpdateProfileAssignment
                    {
                        OdataType = "#microsoft.graph.windowsQualityUpdateProfileAssignment",
                        Target = assignmentTarget,
                    };
                    assignments.Add(assignment);
                }

                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdateProfiles.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile($"Successfully assigned {groupIDs.Count} groups to profile {profileID}. Filter applied: {applyFilter}");
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile($"Error assigning groups to profile {profileID}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred while preparing assignment for profile {profileID}: {ex.Message}");
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
                WriteToImportStatusFile("An error occurred while deleting a Windows Quality Update profile",LogType.Error);
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

                // Look up the existing profile
                var existingProfile = await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].GetAsync();

                if (existingProfile == null)
                {
                    throw new InvalidOperationException($"Profile with ID '{profileID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingProfile.DisplayName, newName);

                var profile = new WindowsQualityUpdateProfile
                {
                    DisplayName = name,
                };

                await graphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[profileID].PatchAsync(profile);
                WriteToImportStatusFile($"Successfully renamed Windows Quality Update profile {profileID} to '{name}'");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming Windows Quality Update profile", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
