using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class DeviceConfigurationHelper
    {
        public static async Task<List<Microsoft.Graph.Beta.Models.DeviceConfiguration>> SearchForDeviceConfigurations(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Searching for device configuration policies. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.DeviceConfigurations
                    .GetAsync(requestConfiguration =>
                    {
                        // Filter by device configuration displayName
                        requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                    });

                List<Microsoft.Graph.Beta.Models.DeviceConfiguration> deviceConfigurations = new();
                var pageIterator = PageIterator<Microsoft.Graph.Beta.Models.DeviceConfiguration, DeviceConfigurationCollectionResponse>
                    .CreatePageIterator(graphServiceClient, result, (config) =>
                    {
                        deviceConfigurations.Add(config);
                        return true;
                    });

                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {deviceConfigurations.Count} device configuration policies.");
                return deviceConfigurations;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while searching for device configuration policies", LogLevels.Error);
                return new List<Microsoft.Graph.Beta.Models.DeviceConfiguration>();
            }
        }

        public static async Task<List<Microsoft.Graph.Beta.Models.DeviceConfiguration>> GetAllDeviceConfigurations(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all device configuration policies.");

                var result = await graphServiceClient.DeviceManagement.DeviceConfigurations
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Top = 1000;
                    });

                List<Microsoft.Graph.Beta.Models.DeviceConfiguration> deviceConfigurations = new();
                var pageIterator = PageIterator<Microsoft.Graph.Beta.Models.DeviceConfiguration, DeviceConfigurationCollectionResponse>
                    .CreatePageIterator(graphServiceClient, result, (config) =>
                    {
                        deviceConfigurations.Add(config);
                        return true;
                    });

                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {deviceConfigurations.Count} device configuration policies.");
                return deviceConfigurations;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all device configuration policies", LogLevels.Error);
                return new List<Microsoft.Graph.Beta.Models.DeviceConfiguration>();
            }
        }

        public static async Task ImportMultipleDeviceConfigurations(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> configurationIds, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, " ");
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Importing {configurationIds.Count} Device Configuration profiles.");

                foreach (var configId in configurationIds)
                {
                    var policyName = "";
                    try
                    {
                        var originalConfig = await sourceGraphServiceClient.DeviceManagement.DeviceConfigurations[configId].GetAsync(requestConfiguration =>
                        {
                            // Expand settings if needed
                            //requestConfiguration.QueryParameters.Expand = new[] { "settings" };
                        });

                        if (originalConfig.OdataType.Equals("#microsoft.graph.iosDeviceFeaturesConfiguration"))
                        {
                            //MessageBox.Show("iOS Device Feature template is currently bugged in graph SDK. Handle manually until this is resolved");
                            //rtb.AppendText("iOS Device Feature template is currently bugged in graph SDK. Handle manually until this is resolved");
                            LogToFunctionFile(appFunction.Main, originalConfig.DisplayName + " failed to import. iOS Device Feature template is currently bugged in graph SDK. Handle manually until this is resolved", LogLevels.Error);
                            continue;
                        }

                        // get the type of the policy object
                        var typeOfPolicy = originalConfig.GetType();

                        if (typeOfPolicy.IsAbstract)
                        {
                            return;
                        }

                        // create a new instance of the policy object
                        var testRequestBody = Activator.CreateInstance(typeOfPolicy);



                        // copy all the properties from the original policy
                        foreach (var property in typeOfPolicy.GetProperties())
                        {
                            if (property.CanWrite)
                            {
                                var value = property.GetValue(originalConfig);

                                property.SetValue(testRequestBody, value);
                            }
                        }

                        // cast the object to a DeviceConfiguration (this is necessary for the PostAsync method)
                        var deviceConfiguration = testRequestBody as Microsoft.Graph.Beta.Models.DeviceConfiguration;

                        deviceConfiguration.Assignments = deviceConfiguration.Assignments ?? new List<DeviceConfigurationAssignment>();
                        deviceConfiguration.GroupAssignments = deviceConfiguration.GroupAssignments ?? new List<DeviceConfigurationGroupAssignment>();
                        deviceConfiguration.DeviceStatuses = deviceConfiguration.DeviceStatuses ?? new List<DeviceConfigurationDeviceStatus>();
                        deviceConfiguration.DeviceSettingStateSummaries = deviceConfiguration.DeviceSettingStateSummaries ?? new List<SettingStateDeviceSummary>();
                        deviceConfiguration.UserStatuses = deviceConfiguration.UserStatuses ?? new List<DeviceConfigurationUserStatus>();



                        // Special case for Windows 10 General Configuration policies
                        if (deviceConfiguration.OdataType != null &&
                            deviceConfiguration.OdataType.Equals("#microsoft.graph.windows10GeneralConfiguration", StringComparison.OrdinalIgnoreCase))
                        {
                            // Windows 10 General Configuration policies are not supported for import.
                            // Ensure the PrivacyAccessControls property exists and is accessible.
                            if (deviceConfiguration is Windows10GeneralConfiguration windows10Config)
                            {
                                windows10Config.PrivacyAccessControls = windows10Config.PrivacyAccessControls ?? new List<WindowsPrivacyDataAccessControlItem>();
                            }
                        }

                        policyName = deviceConfiguration.DisplayName;

                        var import = await destinationGraphServiceClient.DeviceManagement.DeviceConfigurations.PostAsync(deviceConfiguration);

                        LogToFunctionFile(appFunction.Main, $"Successfully imported {import.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSingleDeviceConfiguration(import.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        //HandleException(ex, $"Error importing device configuration {configId}",false);
                        // Change color of the error output text to red and then reset it for the next text entry
                        //rtb.AppendText(ex.Message + Environment.NewLine);

                        LogToFunctionFile(appFunction.Main, $"Failed to import {policyName}\n", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An unexpected error occurred during the import process: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Finished importing Device Configuration profiles.");
            }
        }

        public static async Task AssignGroupsToSingleDeviceConfiguration(string configId, List<string> groupIds, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (configId == null)
                {
                    throw new ArgumentNullException(nameof(configId));
                }

                if (groupIds == null)
                {
                    throw new ArgumentNullException(nameof(groupIds));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<DeviceConfigurationAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllUsers = false;
                var hasAllDevices = false;

                // Step 1: Add new assignments to request body
                foreach (var group in groupIds)
                {
                    if (string.IsNullOrWhiteSpace(group) || !seenGroupIds.Add(group))
                    {
                        continue;
                    }

                    DeviceConfigurationAssignment assignment;

                    // Check if this is a virtual group (All Users or All Devices)
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignment = new DeviceConfigurationAssignment
                        {
                            OdataType = "#microsoft.graph.deviceConfigurationAssignment",
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
                        assignment = new DeviceConfigurationAssignment
                        {
                            OdataType = "#microsoft.graph.deviceConfigurationAssignment",
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
                        assignment = new DeviceConfigurationAssignment
                        {
                            OdataType = "#microsoft.graph.deviceConfigurationAssignment",
                            Target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                GroupId = group,
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            }
                        };
                    }

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .DeviceConfigurations[configId]
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
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceConfigurations.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    var result = await destinationGraphServiceClient.DeviceManagement.DeviceConfigurations[configId].Assign.PostAsAssignPostResponseAsync(requestBody);

                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to device configuration {configId} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to device configuration policy", LogLevels.Warning);
                    LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to a single device configuration policy", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }
        public static async Task DeleteDeviceConfigurationPolicy(GraphServiceClient graphServiceClient, string policyID)
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
                await graphServiceClient.DeviceManagement.DeviceConfigurations[policyID].DeleteAsync();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while deleting the device configuration policy: {ex.Message}", LogLevels.Error);
            }
        }
        public static async Task RenameDeviceConfigurationPolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
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
                    var existingPolicy = await graphServiceClient.DeviceManagement.DeviceConfigurations[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingPolicy.DisplayName ?? string.Empty, newName);

                    // Create an instance of the specific policy type using reflection
                    var policyType = existingPolicy.GetType();
                    var policy = (DeviceConfiguration?)Activator.CreateInstance(policyType);

                    if (policy == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {policyType.Name}");
                    }

                    // Set the DisplayName on the new instance
                    policy.DisplayName = name;

                    await graphServiceClient.DeviceManagement.DeviceConfigurations[policyID].PatchAsync(policy);
                    LogToFunctionFile(appFunction.Main, $"Renamed device configuration policy {policyID} to {name}");
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing policy to determine its specific type
                    var existingPolicy = await graphServiceClient.DeviceManagement.DeviceConfigurations[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        throw new InvalidOperationException($"Policy with ID '{policyID}' not found.");
                    }

                    // Create an instance of the specific policy type using reflection
                    var policyType = existingPolicy.GetType();
                    var policy = (DeviceConfiguration?)Activator.CreateInstance(policyType);

                    if (policy == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {policyType.Name}");
                    }

                    policy.Description = newName;

                    await graphServiceClient.DeviceManagement.DeviceConfigurations[policyID].PatchAsync(policy);
                    LogToFunctionFile(appFunction.Main, $"Updated description for device configuration policy {policyID} to {newName}");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming device configuration policies", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }
    }
}
