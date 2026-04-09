using IntuneTools.Utilities;
using Microsoft.Graph;
using Microsoft.Kiota.Serialization.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
                LogToFunctionFile(appFunction.Main, "Searching for Windows Quality Update policies. Search query: " + searchQuery);

                // Fetch all first and then filter locally as a safer approach.
                var allPolicies = await GetAllWindowsQualityUpdatePolicies(graphServiceClient);
                // Add null checks for policy and DisplayName
                var filteredPolicies = allPolicies.Where(p => p?.DisplayName != null && p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

                LogToFunctionFile(appFunction.Main, $"Found {filteredPolicies.Count} Windows Quality Update policies matching the search query.");

                return filteredPolicies;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while searching for Windows Quality Update policies", LogLevels.Error);
                return new List<WindowsQualityUpdatePolicy>();
            }
        }

        public static async Task<List<WindowsQualityUpdatePolicy>> GetAllWindowsQualityUpdatePolicies(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all Windows Quality Update policies.");

                var result = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies.GetAsync((requestConfiguration) =>
                {
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
                    LogToFunctionFile(appFunction.Main, "No Windows Quality Update policies found or result was null.", LogLevels.Warning);
                }

                LogToFunctionFile(appFunction.Main, $"Found {policies.Count} Windows Quality Update policies.");

                return policies;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all Windows Quality Update policies", LogLevels.Error);
                return new List<WindowsQualityUpdatePolicy>();
            }
        }
        public static async Task ImportMultipleWindowsQualityUpdatePolicies(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policyIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Importing {policyIDs.Count} Windows Quality Update policies.");

                string profileName = "";

                foreach (var policyId in policyIDs)
                {
                    try
                    {
                        // Fetch the source policy
                        var sourcePolicy = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyId].GetAsync();

                        if (sourcePolicy == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping policy ID {policyId}: Not found in source tenant.");
                            continue;
                        }

                        profileName = sourcePolicy.DisplayName ?? GraphConstants.FallbackDisplayName;

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
                        LogToFunctionFile(appFunction.Main, $"Imported policy: {importedPolicy?.DisplayName ?? "Unnamed Policy"} (ID: {importedPolicy?.Id ?? "Unknown ID"})");

                        // Handle assignments if requested
                        if (assignments && groups != null && groups.Any() && importedPolicy?.Id != null)
                        {
                            await AssignGroupsToSingleWindowsQualityUpdatePolicy(importedPolicy.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        //rtb.AppendText($"This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active\n");
                        LogToFunctionFile(appFunction.Main, $"Failed to import Windows Quality Update policy {profileName}: {ex.Message}", LogLevels.Error);
                        LogToFunctionFile(appFunction.Main, "This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogLevels.Warning);
                    }
                }
                LogToFunctionFile(appFunction.Main, "Windows Quality Update policy import process finished.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred during the import process: {ex.Message}", LogLevels.Error);
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

                LogToFunctionFile(appFunction.Main, $"Assigning {groupIDs.Count} groups to Windows Quality Update policy {policyID}.");

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
                        LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update policies cannot be assigned to 'All Users'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
                        continue;
                    }

                    // Check if this is All Devices - Quality Update policies cannot be assigned to All Devices
                    if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        LogToFunctionFile(appFunction.Main, "Warning: Windows Quality Update policies cannot be assigned to 'All Devices'. Only device groups are supported. Skipping this assignment.", LogLevels.Warning);
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
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Users' assignment on Quality Update policy {policyID}. This should not exist and will be skipped.", LogLevels.Warning);
                            continue;
                        }
                        else if (existing.Target is AllDevicesAssignmentTarget)
                        {
                            // Skip All Devices assignments - they shouldn't exist but handle gracefully
                            LogToFunctionFile(appFunction.Main, $"Warning: Found existing 'All Devices' assignment on Quality Update policy {policyID}. This should not exist and will be skipped.", LogLevels.Warning);
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
                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to Quality Update policy {policyID}.");
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"Error assigning groups to policy {policyID}: {ex.Message}", LogLevels.Error);
                }
            }
            catch (ArgumentNullException argEx)
            {
                LogToFunctionFile(appFunction.Main, $"Argument null exception during group assignment setup: {argEx.Message}", LogLevels.Error);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while preparing assignment for policy {policyID}: {ex.Message}", LogLevels.Warning);
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
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting a Windows Quality Update policy", LogLevels.Error);
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
                    LogToFunctionFile(appFunction.Main, $"Successfully renamed Windows Quality Update policy {policyID} to '{name}'");
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
                    LogToFunctionFile(appFunction.Main, $"Updated description for Windows Quality Update policy {policyID} to '{newName}'");
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingPolicy = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingPolicy.DisplayName);

                    var policy = new WindowsQualityUpdatePolicy
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyID].PatchAsync(policy);
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from Windows Quality Update policy {policyID}, new name: '{name}'");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming Windows Quality Update policy", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllWindowsQualityUpdatePolicyContentAsync(GraphServiceClient graphServiceClient)
        {
            var policies = await GetAllWindowsQualityUpdatePolicies(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Windows Quality Update Policy",
                    ContentPlatform = "Windows",
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchWindowsQualityUpdatePolicyContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var policies = await SearchForWindowsQualityUpdatePolicies(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Windows Quality Update Policy",
                    ContentPlatform = "Windows",
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Exports a Windows Quality Update policy's full data as a JsonElement for JSON file export.
        /// </summary>
        public static async Task<JsonElement?> ExportWindowsQualityUpdatePolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyId].GetAsync();

                if (result == null)
                {
                    LogToFunctionFile(appFunction.Main, $"Windows Quality Update policy {policyId} not found for export.", LogLevels.Warning);
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
                LogToFunctionFile(appFunction.Main, $"Error exporting Windows Quality Update policy {policyId}: {ex.Message}", LogLevels.Error);
                return null;
            }
        }

        /// <summary>
        /// Imports a Windows Quality Update policy from previously exported JSON data into the destination tenant.
        /// </summary>
        public static async Task<string?> ImportWindowsQualityUpdatePolicyFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exportedPolicy = parseNode.GetObjectValue(WindowsQualityUpdatePolicy.CreateFromDiscriminatorValue);

                if (exportedPolicy == null)
                {
                    LogToFunctionFile(appFunction.Main, "Failed to deserialize Windows Quality Update policy data from JSON.", LogLevels.Error);
                    return null;
                }

                var type = exportedPolicy.GetType();
                var newPolicy = new WindowsQualityUpdatePolicy();

                foreach (var property in type.GetProperties())
                {
                    if (property.CanWrite
                        && property.Name != "Id"
                        && property.Name != "CreatedDateTime"
                        && property.Name != "LastModifiedDateTime"
                        && property.Name != "Assignments"
                        && property.Name != "AdditionalData"
                        && property.Name != "BackingStore")
                    {
                        var value = property.GetValue(exportedPolicy);
                        if (value != null)
                        {
                            property.SetValue(newPolicy, value);
                        }
                    }
                }

                newPolicy.OdataType = "#microsoft.graph.windowsQualityUpdatePolicy";

                var imported = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies.PostAsync(newPolicy);

                LogToFunctionFile(appFunction.Main, $"Imported Windows Quality Update policy: {imported?.DisplayName}");
                return imported?.DisplayName;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error importing Windows Quality Update policy from JSON: {ex.Message}", LogLevels.Error);
                LogToFunctionFile(appFunction.Main, "This is most likely due to the feature not being licensed in the destination tenant. Please check that you have a Windows E3 or higher license active", LogLevels.Warning);
                return null;
            }
        }

        /// <summary>
        /// Checks if a Windows quality update policy has any group assignments.
        /// </summary>
        public static async Task<bool?> HasWindowsQualityUpdatePolicyAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyId].Assignments.GetAsync(rc =>
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
        /// Gets detailed assignment information for a Windows Quality Update policy.
        /// </summary>
        public static async Task<List<AssignmentInfo>?> GetWindowsQualityUpdatePolicyAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var details = new List<AssignmentInfo>();
                var result = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyId].Assignments.GetAsync();

                while (result?.Value != null)
                {
                    foreach (var assignment in result.Value)
                    {
                        details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyId]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                return details;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error getting assignment details for Windows Quality Update Policy {policyId}: {ex.Message}", LogLevels.Error);
                return null;
            }
        }

        /// <summary>
        /// Removes all assignments from a Windows Quality Update policy.
        /// </summary>
        public static async Task RemoveAllWindowsQualityUpdatePolicyAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdatePolicies.Item.Assign.AssignPostRequestBody
            {
                Assignments = new List<WindowsQualityUpdatePolicyAssignment>()
            };

            await graphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[policyId].Assign.PostAsync(requestBody);
            LogToFunctionFile(appFunction.Main, $"Removed all assignments from Windows Quality Update Policy {policyId}.");
        }
    }
}
