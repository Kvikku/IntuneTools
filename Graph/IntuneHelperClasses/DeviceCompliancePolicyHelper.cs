using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
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
                            requestConfiguration.QueryParameters.Expand = new string[] { "scheduledActionsForRule" };
                        });

                        //var rules = await sourceGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[policy].ScheduledActionsForRule.GetAsync();

                        policyName = result.DisplayName;

                        // Get the type of the policy with reflection
                        var type = result.GetType();

                        // Create a new instance of the same type
                        var newPolicy = Activator.CreateInstance(type);

                        // Copy all settings from the source policy to the new policy
                        foreach (var property in result.GetType().GetProperties())
                        {
                            if (property.CanWrite && property.Name != "Id" && property.Name != "CreatedDateTime" && property.Name != "LastModifiedDateTime")
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


                        // new device compliance scheduled action rule

                        // Note - this manual test works. Need to copy the scheduled actions for rule

                        var testRule = new DeviceComplianceScheduledActionForRule
                        {
                            RuleName = "Test Rule",
                            ScheduledActionConfigurations = new List<DeviceComplianceActionItem>()
                            {
                                new DeviceComplianceActionItem
                                {
                                    ActionType = DeviceComplianceActionType.Block,
                                    GracePeriodHours = 8,
                                    NotificationMessageCCList = new List<string>(),
                                    NotificationTemplateId = ""
                                }
                            }
                        };

                        deviceCompliancePolicy.ScheduledActionsForRule = new List<DeviceComplianceScheduledActionForRule>
                        {
                            testRule
                        };


                        //// Ensure the ScheduledActionsForRule is copied
                        //if (result.ScheduledActionsForRule != null)
                        //{
                        //    deviceCompliancePolicy.ScheduledActionsForRule = new List<DeviceComplianceScheduledActionForRule>();
                        //    foreach (var action in result.ScheduledActionsForRule)
                        //    {
                        //        var newAction = new DeviceComplianceScheduledActionForRule
                        //        {
                        //            RuleName = action.RuleName,
                        //            ScheduledActionConfigurations = action.ScheduledActionConfigurations

                        //        };
                        //        deviceCompliancePolicy.ScheduledActionsForRule.Add(newAction);
                        //    }
                        //}


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
    }

}
