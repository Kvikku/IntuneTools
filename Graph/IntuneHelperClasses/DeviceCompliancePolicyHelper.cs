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
    public class DeviceCompliancePolicyHelper
    {
        public static async Task<List<DeviceCompliancePolicy>> GetAllDeviceCompliancePolicies(GraphServiceClient graphServiceClient)
        {
            // This method retrieves all the device compliance policies from Intune and returns them as a list of DeviceManagementCompliancePolicy objects
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all device compliance policies.");

                var result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                if (result == null)
                {
                    throw new InvalidOperationException("The result from the Graph API is null.");
                }

                List<DeviceCompliancePolicy> compliancePolicies = new List<DeviceCompliancePolicy>();
                // Iterate through the pages of results
                var pageIterator = PageIterator<DeviceCompliancePolicy, DeviceCompliancePolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    compliancePolicies.Add(policy);
                    return true;
                });
                // start the iteration
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {compliancePolicies.Count} device compliance policies.");

                // return the list of policies
                return compliancePolicies;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                LogToFunctionFile(appFunction.Main, "ODataError occurred", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, me.Message, LogLevels.Warning);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An unexpected error occurred", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Warning);
            }

            // Return an empty list if an exception occurs
            return new List<DeviceCompliancePolicy>();
        }

        public static async Task<List<DeviceCompliancePolicy>> SearchForDeviceCompliancePolicies(GraphServiceClient graphServiceClient, string searchQuery)
        {
            // This method searches the Intune device compliance policies for a specific query and returns the results as a list of DeviceManagementCompliancePolicy objects
            try
            {
                LogToFunctionFile(appFunction.Main, "Searching for device compliance policies. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies.GetAsync();


                List<DeviceCompliancePolicy> compliancePolicies = new List<DeviceCompliancePolicy>();
                // Iterate through the pages of results
                var pageIterator = PageIterator<DeviceCompliancePolicy, DeviceCompliancePolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    compliancePolicies.Add(policy);
                    return true;
                });
                // start the iteration
                await pageIterator.IterateAsync();


                LogToFunctionFile(appFunction.Main, $"Found {compliancePolicies.Count} device compliance policies.");

                // Filter the collected policies based on the searchQuery - Graph API does not allow for server side filtering 
                var filteredPolicies = compliancePolicies
                    .Where(policy => policy.DisplayName != null && policy.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                LogToFunctionFile(appFunction.Main, $"Filtered policies count: {filteredPolicies.Count}");


                // return the list of policies
                return filteredPolicies;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                LogToFunctionFile(appFunction.Main, "ODataError occurred", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, me.Message, LogLevels.Warning);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An unexpected error occurred", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Warning);
            }

            // Return an empty list if an exception occurs
            return new List<DeviceCompliancePolicy>();
        }

        public static async Task ImportMultipleDeviceCompliancePolicies(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policies, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, " ");
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Importing {policies.Count} Device Compliance policies.");

                foreach (var policy in policies)
                {
                    var policyName = string.Empty;
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[policy].GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Expand = new string[] { "scheduledActionsForRule($expand=scheduledActionConfigurations)" };
                        });

                        policyName = result.DisplayName;

                        // Get the type of the policy with reflection
                        var type = result.GetType();

                        // Create a new instance of the same type
                        var newPolicy = Activator.CreateInstance(type);

                        // Copy all settings from the source policy to the new policy
                        // Copy all properties except server-generated ones and ScheduledActionsForRule
                        // (ScheduledActionsForRule is rebuilt separately to strip server-generated IDs)
                        foreach (var property in result.GetType().GetProperties())
                        {
                            if (property.CanWrite
                                && property.Name != "Id"
                                && property.Name != "CreatedDateTime"
                                && property.Name != "LastModifiedDateTime"
                                && property.Name != "ScheduledActionsForRule")
                            {
                                var value = property.GetValue(result);
                                if (value != null)
                                {
                                    property.SetValue(newPolicy, value);
                                }
                            }
                        }

                        // Cast the new policy to DeviceCompliancePolicy
                        var deviceCompliancePolicy = newPolicy as DeviceCompliancePolicy;

                        // Rebuild ScheduledActionsForRule with clean objects (no server-generated IDs).
                        // The Graph API requires exactly one rule with exactly one Block action.
                        // Collect all action configs from all source rules into a single rule.
                        var allConfigs = new List<DeviceComplianceActionItem>();
                        if (result.ScheduledActionsForRule != null)
                        {
                            foreach (var action in result.ScheduledActionsForRule)
                            {
                                if (action.ScheduledActionConfigurations != null)
                                {
                                    foreach (var config in action.ScheduledActionConfigurations)
                                    {
                                        allConfigs.Add(new DeviceComplianceActionItem
                                        {
                                            ActionType = config.ActionType,
                                            GracePeriodHours = config.GracePeriodHours,
                                            NotificationMessageCCList = config.NotificationMessageCCList ?? new List<string>(),
                                            NotificationTemplateId = config.NotificationTemplateId ?? ""
                                        });
                                    }
                                }
                            }
                        }

                        // Ensure exactly one Block action exists
                        var blockActions = allConfigs.Where(c => c.ActionType == DeviceComplianceActionType.Block).ToList();
                        var nonBlockActions = allConfigs.Where(c => c.ActionType != DeviceComplianceActionType.Block).ToList();

                        var finalConfigs = new List<DeviceComplianceActionItem>();
                        if (blockActions.Count > 0)
                        {
                            finalConfigs.Add(blockActions.First());
                        }
                        else
                        {
                            finalConfigs.Add(new DeviceComplianceActionItem
                            {
                                ActionType = DeviceComplianceActionType.Block,
                                GracePeriodHours = 0,
                                NotificationMessageCCList = new List<string>(),
                                NotificationTemplateId = ""
                            });
                        }
                        finalConfigs.AddRange(nonBlockActions);

                        deviceCompliancePolicy.ScheduledActionsForRule = new List<DeviceComplianceScheduledActionForRule>
                        {
                            new DeviceComplianceScheduledActionForRule
                            {
                                RuleName = "PasswordRequired",
                                ScheduledActionConfigurations = finalConfigs
                            }
                        };


                        var import = await destinationGraphServiceClient.DeviceManagement.DeviceCompliancePolicies.PostAsync(deviceCompliancePolicy);
                        LogToFunctionFile(appFunction.Main, $"Successfully imported {import.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSingleDeviceCompliance(import.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import {policyName}\n", LogLevels.Error);
                        LogToFunctionFile(appFunction.Main, $"Failed to import {policyName}: {ex.Message}", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An unexpected error occurred during the import process: {ex.Message}", LogLevels.Error);
                LogToFunctionFile(appFunction.Main, "An unexpected error occurred during the import process. Please check the log file for more information.", LogLevels.Error);
            }
            finally
            {
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Finished importing Device Compliance policies.");
            }
        }

        public static async Task AssignGroupsToSingleDeviceCompliance(string policyID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (policyID == null)
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

                var assignments = new List<DeviceCompliancePolicyAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllUsers = false;
                var hasAllDevices = false;

                // Step 1: Add new assignments to request body
                foreach (var group in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(group) || !seenGroupIds.Add(group))
                    {
                        continue;
                    }

                    DeviceCompliancePolicyAssignment assignment;

                    // Check if this is a virtual group (All Users or All Devices)
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignment = new DeviceCompliancePolicyAssignment
                        {
                            Target = new AllLicensedUsersAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            }
                        };
                    }
                    else if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        assignment = new DeviceCompliancePolicyAssignment
                        {
                            Target = new AllDevicesAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allDevicesAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            }
                        };
                    }
                    else
                    {
                        // Regular group assignment
                        assignment = new DeviceCompliancePolicyAssignment
                        {
                            Target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType,
                                GroupId = group
                            }
                        };
                    }

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .DeviceCompliancePolicies[policyID]
                    .Assignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        // Check the type of assignment target
                        if (existing.Target is AllLicensedUsersAssignmentTarget)
                        {
                            // Skip if we're already adding All Users
                            if (!hasAllUsers)
                            {
                                assignments.Add(existing);
                            }
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
                            // Include any other assignment types (e.g., exclusions, all users with exclusions, etc.)
                            assignments.Add(existing);
                        }
                    }
                }

                // Step 3: Update the policy with the request body
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceCompliancePolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].Assign.PostAsync(requestBody);
                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to policy {policyID} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to device compliance policy", LogLevels.Warning);
                    LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to device compliance policy", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static string TranslateComplianceODataTypeToPlatform(string odatatype)
        {
            string platform = "Unknown";

            if (string.IsNullOrEmpty(odatatype))
            {
                return platform;
            }

            switch (odatatype.ToLower())
            {
                case "#microsoft.graph.ioscompliancepolicy":
                    platform = "iOS";
                    break;
                case "#microsoft.graph.windows10compliancepolicy":
                    platform = "Windows";
                    break;
                case "#microsoft.graph.macoscompliancepolicy":
                    platform = "macOS";
                    break;
                case "#microsoft.graph.androidworkprofilecompliancepolicy":
                    platform = "Android Work Profile";
                    break;
                case "#microsoft.graph.androiddeviceownercompliancepolicy":
                    platform = "Android Device Owner";
                    break;
                default:
                    platform = "Unknown";
                    break;
            }

            return platform;
        }

        public static async Task DeleteDeviceCompliancePolicy(GraphServiceClient graphServiceClient, string policyID)
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
                await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].DeleteAsync();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting settings catalog policies", LogLevels.Error);
            }
        }

        public static async Task RenameDeviceCompliancePolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
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
                    // Look up the existing policy to determine its specific type
                    var existingPolicy = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingPolicy.DisplayName ?? string.Empty, newName);

                    // Create an instance of the specific policy type using reflection
                    var policyType = existingPolicy.GetType();
                    var policy = (DeviceCompliancePolicy?)Activator.CreateInstance(policyType);

                    if (policy == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {policyType.Name}");
                    }

                    // Set the DisplayName on the new instance
                    policy.DisplayName = name;

                    await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].PatchAsync(policy);
                    LogToFunctionFile(appFunction.Main, $"Renamed device compliance policy {policyID} to '{name}'", LogLevels.Info);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing policy to determine its specific type
                    var existingPolicy = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    // Create an instance of the specific policy type using reflection
                    var policyType = existingPolicy.GetType();
                    var policy = (DeviceCompliancePolicy?)Activator.CreateInstance(policyType);

                    if (policy == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {policyType.Name}");
                    }

                    policy.Description = newName;

                    await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].PatchAsync(policy);
                    LogToFunctionFile(appFunction.Main, $"Updated description for {policyID} to {newName}");
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingPolicy = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingPolicy.DisplayName);

                    var policyType = existingPolicy.GetType();
                    var policy = (DeviceCompliancePolicy?)Activator.CreateInstance(policyType);

                    if (policy == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {policyType.Name}");
                    }

                    policy.DisplayName = name;

                    await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].PatchAsync(policy);
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from device compliance policy {policyID}, new name: '{name}'");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming device compliance policies", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllDeviceComplianceContentAsync(GraphServiceClient graphServiceClient)
        {
            var policies = await GetAllDeviceCompliancePolicies(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Device Compliance Policy",
                    ContentPlatform = HelperClass.TranslatePolicyPlatformName(policy.OdataType?.ToString() ?? string.Empty),
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchDeviceComplianceContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var policies = await SearchForDeviceCompliancePolicies(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Device Compliance Policy",
                    ContentPlatform = HelperClass.TranslatePolicyPlatformName(policy.OdataType?.ToString() ?? string.Empty),
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Exports a device compliance policy's full data as a JsonElement for JSON file export.
        /// Uses Kiota serialization to preserve OData type annotations and polymorphic types.
        /// </summary>
        public static async Task<JsonElement?> ExportDeviceCompliancePolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyId].GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Expand = new[] { "scheduledActionsForRule($expand=scheduledActionConfigurations)" };
                });

                if (result == null)
                {
                    LogToFunctionFile(appFunction.Main, $"Device compliance policy {policyId} not found for export.", LogLevels.Warning);
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
                LogToFunctionFile(appFunction.Main, $"Error exporting device compliance policy {policyId}: {ex.Message}", LogLevels.Error);
                return null;
            }
        }

        /// <summary>
        /// Imports a device compliance policy from previously exported JSON data into the destination tenant.
        /// </summary>
        public static async Task<string?> ImportDeviceComplianceFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exportedPolicy = parseNode.GetObjectValue(DeviceCompliancePolicy.CreateFromDiscriminatorValue);

                if (exportedPolicy == null)
                {
                    LogToFunctionFile(appFunction.Main, "Failed to deserialize device compliance policy data from JSON.", LogLevels.Error);
                    return null;
                }

                // Use reflection to create a clean copy of the specific derived type
                var type = exportedPolicy.GetType();
                var newPolicy = (DeviceCompliancePolicy)Activator.CreateInstance(type)!;

                // Copy all properties except server-generated ones and ScheduledActionsForRule
                // (ScheduledActionsForRule is rebuilt separately to strip server-generated IDs)
                foreach (var property in type.GetProperties())
                {
                    if (property.CanWrite
                        && property.Name != "Id"
                        && property.Name != "CreatedDateTime"
                        && property.Name != "LastModifiedDateTime"
                        && property.Name != "ScheduledActionsForRule")
                    {
                        var value = property.GetValue(exportedPolicy);
                        if (value != null)
                        {
                            property.SetValue(newPolicy, value);
                        }
                    }
                }

                // Rebuild ScheduledActionsForRule with clean objects (no server-generated IDs).
                // The Graph API requires exactly one rule with exactly one Block action.
                // Collect all action configs from all source rules into a single rule.
                var allConfigs = new List<DeviceComplianceActionItem>();
                if (exportedPolicy.ScheduledActionsForRule != null)
                {
                    foreach (var action in exportedPolicy.ScheduledActionsForRule)
                    {
                        if (action.ScheduledActionConfigurations != null)
                        {
                            foreach (var config in action.ScheduledActionConfigurations)
                            {
                                allConfigs.Add(new DeviceComplianceActionItem
                                {
                                    ActionType = config.ActionType,
                                    GracePeriodHours = config.GracePeriodHours,
                                    NotificationMessageCCList = config.NotificationMessageCCList ?? new List<string>(),
                                    NotificationTemplateId = config.NotificationTemplateId ?? ""
                                });
                            }
                        }
                    }
                }

                // Ensure exactly one Block action exists
                var blockActions = allConfigs.Where(c => c.ActionType == DeviceComplianceActionType.Block).ToList();
                var nonBlockActions = allConfigs.Where(c => c.ActionType != DeviceComplianceActionType.Block).ToList();

                var finalConfigs = new List<DeviceComplianceActionItem>();
                if (blockActions.Count > 0)
                {
                    finalConfigs.Add(blockActions.First());
                }
                else
                {
                    finalConfigs.Add(new DeviceComplianceActionItem
                    {
                        ActionType = DeviceComplianceActionType.Block,
                        GracePeriodHours = 0,
                        NotificationMessageCCList = new List<string>(),
                        NotificationTemplateId = ""
                    });
                }
                finalConfigs.AddRange(nonBlockActions);

                newPolicy.ScheduledActionsForRule = new List<DeviceComplianceScheduledActionForRule>
                {
                    new DeviceComplianceScheduledActionForRule
                    {
                        RuleName = "PasswordRequired",
                        ScheduledActionConfigurations = finalConfigs
                    }
                };

                var imported = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies.PostAsync(newPolicy);

                LogToFunctionFile(appFunction.Main, $"Imported device compliance policy: {imported?.DisplayName}");
                return imported?.DisplayName;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error importing device compliance policy from JSON: {ex.Message}", LogLevels.Error);
                return null;
            }
        }

        /// <summary>
        /// Checks if a device compliance policy has any group assignments.
        /// </summary>
        public static async Task<bool?> HasDeviceCompliancePolicyAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyId].Assignments.GetAsync(rc =>
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
        /// Gets detailed assignment information for a Device Compliance policy.
        /// </summary>
        public static async Task<List<AssignmentInfo>?> GetDeviceComplianceAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var details = new List<AssignmentInfo>();
                var result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyId].Assignments.GetAsync();

                while (result?.Value != null)
                {
                    foreach (var assignment in result.Value)
                    {
                        details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyId]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                return details;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error getting assignment details for Device Compliance {policyId}: {ex.Message}", LogLevels.Error);
                return null;
            }
        }

        /// <summary>
        /// Removes all assignments from a Device Compliance policy.
        /// </summary>
        public static async Task RemoveAllDeviceComplianceAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceCompliancePolicies.Item.Assign.AssignPostRequestBody
            {
                Assignments = new List<DeviceCompliancePolicyAssignment>()
            };

            await graphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyId].Assign.PostAsync(requestBody);
            LogToFunctionFile(appFunction.Main, $"Removed all assignments from Device Compliance policy {policyId}.");
        }
    }

}
