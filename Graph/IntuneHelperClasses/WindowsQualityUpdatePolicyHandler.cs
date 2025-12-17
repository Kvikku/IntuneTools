using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class WindowsQualityUpdatePolicyHandler
    {
        // For Windows Quality Updates (Not expedite policy)

        public static async Task<List<WindowsQualityUpdatePolicy>> SearchForWindowsQualityUpdatePolicies(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile("Searching for Windows Quality Update policies. Search query: " + searchQuery);

                // Fetch all first and then filter locally as a safer approach.
                var allPolicies = await GetAllWindowsQualityUpdatePolicies(graphServiceClient);
                // Add null checks for policy and DisplayName
                var filteredPolicies = allPolicies.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

                WriteToImportStatusFile($"Found {filteredPolicies.Count} Windows Quality Update policies matching the search query.");

                return filteredPolicies;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while searching for Windows Quality Update policies", LogType.Error);
                return new List<WindowsQualityUpdatePolicy>();
            }
        }

        public static async Task<List<WindowsQualityUpdatePolicy>> GetAllWindowsQualityUpdatePolicies(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile("Retrieving all Windows Quality Update policies.");

                var result = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies.GetAsync((requestConfiguration) =>
                {
                    //requestConfiguration.QueryParameters.Top = 1000; // Adjust as needed
                });

                List<WindowsQualityUpdatePolicy> policies = new List<WindowsQualityUpdatePolicy>();

                // Add null check for result before creating iterator
                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<WindowsQualityUpdatePolicy, WindowsQualityUpdatePolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                    {
                        policies.Add(policy);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                }
                else
                {
                    WriteToImportStatusFile("No Windows Quality Update policies found or result was null.", LogType.Warning);
                }

                WriteToImportStatusFile($"Found {policies.Count} Windows Quality Update policies.");

                return policies;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all Windows Quality Update policies", LogType.Error);
                return new List<WindowsQualityUpdatePolicy>();
            }
        }
        public static async Task ImportMultipleWindowsQualityUpdatePolicies(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policyIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {policyIDs.Count} Windows Quality Update policies.");

                string profileName = "";

                foreach (var policyId in policyIDs)
                {
                    try
                    {
                        // Fetch the source policy
                        var sourcePolicy = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyId].GetAsync();

                        if (sourcePolicy == null)
                        {
                            WriteToImportStatusFile($"Skipping policy ID {policyId}: Not found in source tenant.");
                            continue;
                        }

                        profileName = sourcePolicy.DisplayName ?? "ERROR GETTING NAME";

                        // Create the new policy object for the destination tenant
                        var newPolicy = new WindowsQualityUpdatePolicy
                        {
                            // Initialize properties needed for creation. Copy relevant ones from sourcePolicy.
                            // Be careful about read-only properties like Id, CreatedDateTime, LastModifiedDateTime.
                        };

                        // Dynamically copy properties (excluding specific ones)
                        foreach (var property in sourcePolicy.GetType().GetProperties())
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

                            var value = property.GetValue(sourcePolicy);
                            // Check if the property exists on the newPolicy object before setting
                            var destProperty = newPolicy.GetType().GetProperty(property.Name);
                            if (destProperty != null && destProperty.CanWrite)
                            {
                                destProperty.SetValue(newPolicy, value);
                            }
                        }

                        // Ensure OdataType is set correctly
                        newPolicy.OdataType = "#microsoft.graph.windowsQualityUpdatePolicy";

                        // Create the policy in the destination tenant
                        var importedPolicy = await destinationGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies.PostAsync(newPolicy);

                        // Add null check for importedPolicy and DisplayName
                        WriteToImportStatusFile($"Imported policy: {importedPolicy?.DisplayName ?? "Unnamed Policy"} (ID: {importedPolicy?.Id ?? "Unknown ID"})");

                        // Handle assignments if requested
                        if (assignments && groups != null && groups.Any() && importedPolicy?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsQualityUpdatePolicy(importedPolicy.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        //rtb.AppendText($"This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active\n");
                        WriteToImportStatusFile($"Failed to import Windows Quality Update policy {profileName}: {ex.Message}", LogType.Error);
                        WriteToImportStatusFile("This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogType.Warning);
                    }
                }
                WriteToImportStatusFile("Windows Quality Update policy import process finished.");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred during the import process: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// Assigns groups to a single Windows Quality Update Policy.
        /// Windows Quality Update policies can ONLY be assigned to device groups - not All Users or All Devices.
        /// </summary>
        /// <param name="policyID">The ID of the policy to assign groups to.</param>
        /// <param name="groupIDs">List of group IDs to assign.</param>
        /// <param name="destinationGraphServiceClient">GraphServiceClient for the destination tenant.</param>
        /// <param name="applyFilter">Whether to apply assignment filters.</param>
        /// <returns>A Task representing the asynchronous assignment operation.</returns>
        public static async Task AssignGroupsToSingleWindowsQualityUpdatePolicy(string policyID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (string.IsNullOrEmpty(policyID))
                {
                    throw new ArgumentNullException(nameof(policyID));
                }

                if (groupIDs == null)
                {
                    throw new ArgumentNullException(nameof(groupIDs));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<WindowsQualityUpdatePolicyAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                WriteToImportStatusFile($"Assigning {groupIDs.Count} groups to Windows Quality Update policy {policyID}.");

                // Step 1: Add new assignments to request body
                foreach (var groupId in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    // Check if this is All Users - Quality Update policies cannot be assigned to All Users
                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteToImportStatusFile($"Warning: Windows Quality Update policies cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogType.Warning);
                        continue;
                    }

                    // Check if this is All Devices - Quality Update policies cannot be assigned to All Devices
                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteToImportStatusFile($"Warning: Windows Quality Update policies cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogType.Warning);
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

                    var assignment = new WindowsQualityUpdatePolicyAssignment
                    {
                        OdataType = "#microsoft.graph.windowsQualityUpdatePolicyAssignment",
                        Target = assignmentTarget
                    };

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .WindowsQualityUpdatePolicies[policyID]
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
                            WriteToImportStatusFile($"Warning: Found existing 'All Users' assignment on Quality Update policy {policyID}. This should not exist and will be skipped.", LogType.Warning);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip All Devices assignments - they shouldn't exist but handle gracefully
                            WriteToImportStatusFile($"Warning: Found existing 'All Devices' assignment on Quality Update policy {policyID}. This should not exist and will be skipped.", LogType.Warning);
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

                // Step 3: Update the policy with the assignments
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdatePolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile($"Assigned {assignments.Count} assignments to Quality Update policy {policyID}.");
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile($"Error assigning groups to policy {policyID}: {ex.Message}", LogType.Error);
                }
            }
            catch (ArgumentNullException argEx)
            {
                WriteToImportStatusFile($"Argument null exception during group assignment setup: {argEx.Message}", LogType.Error);
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred while preparing assignment for policy {policyID}: {ex.Message}", LogType.Warning);
            }
        }
        public static async Task DeleteWindowsQualityUpdatePolicy(GraphServiceClient graphServiceClient, string policyID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (policyID == null)
                {
                    throw new InvalidOperationException("Policy ID cannot be null.");
                }

                await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].DeleteAsync();
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while deleting a Windows Quality Update policy", LogType.Error);
            }
        }

        public static async Task RenameWindowsQualityUpdatePolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (policyID == null)
                {
                    throw new InvalidOperationException("Policy ID cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                if (selectedRenameMode == "Prefix")
                {
                    // Look up the existing policy
                    var existingPolicy = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingPolicy.DisplayName ?? string.Empty, newName);

                    var policy = new WindowsQualityUpdatePolicy
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].PatchAsync(policy);
                    WriteToImportStatusFile($"Successfully renamed Windows Quality Update policy {policyID} to '{name}'");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing policy
                    var existingPolicy = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    var policy = new WindowsQualityUpdatePolicy
                    {
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].PatchAsync(policy);
                    WriteToImportStatusFile($"Updated description for Windows Quality Update policy {policyID} to '{newName}'");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming Windows Quality Update policy", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
