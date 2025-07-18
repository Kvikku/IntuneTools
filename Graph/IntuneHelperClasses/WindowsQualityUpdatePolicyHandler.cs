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
                WriteToImportStatusFile("An error occurred while searching for Windows Quality Update policies",LogType.Error);
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
                    WriteToImportStatusFile("No Windows Quality Update policies found or result was null.",LogType.Warning);
                }

                WriteToImportStatusFile($"Found {policies.Count} Windows Quality Update policies.");

                return policies;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all Windows Quality Update policies",LogType.Error);
                return new List<WindowsQualityUpdatePolicy>();
            }
        }
        public static async Task ImportMultipleWindowsQualityUpdatePolicies(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient,List<string> policyIDs, bool assignments, bool filter, List<string> groups)
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
                            await AssignGroupsToSingleWindowsQualityUpdatePolicy(importedPolicy.Id, groups, destinationGraphServiceClient, filter);
                        }
                    }
                    catch (Exception ex)
                    {
                        //rtb.AppendText($"This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active\n");
                        WriteToImportStatusFile($"Failed to import Windows Quality Update policy {profileName}: {ex.Message}",LogType.Error);
                        WriteToImportStatusFile("This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active",LogType.Warning);
                    }
                }
                WriteToImportStatusFile("Windows Quality Update policy import process finished.");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred during the import process: {ex.Message}", LogType.Error);
            }
        }

        public static async Task AssignGroupsToSingleWindowsQualityUpdatePolicy(string policyID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient, bool applyFilter)
        {
            try
            {
                if (string.IsNullOrEmpty(policyID))
                {
                    throw new ArgumentNullException(nameof(policyID));
                }

                if (groupIDs == null || !groupIDs.Any())
                {
                    WriteToImportStatusFile($"No groups provided for assignment to policy {policyID}. Skipping assignment.");
                    return; // Nothing to assign
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                WriteToImportStatusFile($"Assigning {groupIDs.Count} groups to Windows Quality Update policy {policyID}. Apply filter: {applyFilter}");

                List<WindowsQualityUpdatePolicyAssignment> assignments = new List<WindowsQualityUpdatePolicyAssignment>();

                foreach (var groupId in groupIDs)
                {
                    var assignmentTarget = new GroupAssignmentTarget
                    {
                        OdataType = "#microsoft.graph.groupAssignmentTarget",
                        GroupId = groupId,
                        // Apply filters if applicable and supported for Quality Update Policy assignments
                        DeviceAndAppManagementAssignmentFilterId = applyFilter ? SelectedFilterID : null,
                        DeviceAndAppManagementAssignmentFilterType = applyFilter ? deviceAndAppManagementAssignmentFilterType : Microsoft.Graph.Beta.Models.DeviceAndAppManagementAssignmentFilterType.None,
                    };

                    var assignment = new WindowsQualityUpdatePolicyAssignment
                    {
                        OdataType = "#microsoft.graph.windowsQualityUpdatePolicyAssignment",
                        Target = assignmentTarget,
                        // Source and SourceId might not be applicable/required here. Check documentation.
                    };
                    assignments.Add(assignment);
                }

                // The request body structure for assigning Quality Update Policies might differ.
                // Check the Graph API documentation for the correct structure.
                // Assuming it's similar to Feature Updates for now.
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdatePolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                    // Other properties specific to Quality Update Policy assignment might be needed here.
                };

                try
                {
                    // The Assign action might return void or a specific response type. Adjust accordingly.
                    await destinationGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile($"Successfully assigned {groupIDs.Count} groups to policy {policyID}. Filter applied: {applyFilter}");
                }
                catch (Exception ex)
                {
                    // Log specific error for this assignment attempt
                    WriteToImportStatusFile($"Error assigning groups to policy {policyID}: {ex.Message}", LogType.Error);
                    // Decide if you want to re-throw or just log
                }
            }
            catch (Exception ex)
            {
                // Catch argument null exceptions or other setup errors
                WriteToImportStatusFile($"An error occurred while preparing assignment for policy {policyID}: {ex.Message}", LogType.Error);
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
                WriteToImportStatusFile("An error occurred while deleting a Windows Quality Update policy",LogType.Error);
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

                // Look up the existing policy
                var existingPolicy = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].GetAsync();

                if (existingPolicy == null)
                {
                    throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingPolicy.DisplayName, newName);

                var policy = new WindowsQualityUpdatePolicy
                {
                    DisplayName = name,
                };

                await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].PatchAsync(policy);
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming Windows Quality Update policy", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
