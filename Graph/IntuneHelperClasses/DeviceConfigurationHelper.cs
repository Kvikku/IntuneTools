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
    public class DeviceConfigurationHelper
    {
        public static async Task<List<Microsoft.Graph.Beta.Models.DeviceConfiguration>> SearchForDeviceConfigurations(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile("Searching for device configuration policies. Search query: " + searchQuery);

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

                WriteToImportStatusFile($"Found {deviceConfigurations.Count} device configuration policies.");
                return deviceConfigurations;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while searching for device configuration policies", LogType.Error);
                return new List<Microsoft.Graph.Beta.Models.DeviceConfiguration>();
            }
        }

        public static async Task<List<Microsoft.Graph.Beta.Models.DeviceConfiguration>> GetAllDeviceConfigurations(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile("Retrieving all device configuration policies.");

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

                WriteToImportStatusFile($"Found {deviceConfigurations.Count} device configuration policies.");
                return deviceConfigurations;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all device configuration policies", LogType.Error);
                return new List<Microsoft.Graph.Beta.Models.DeviceConfiguration>();
            }
        }

        public static async Task ImportMultipleDeviceConfigurations(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient,List<string> configurationIds, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile(" ");
                WriteToImportStatusFile($"{DateTime.Now.ToString()} - Importing {configurationIds.Count} Device Configuration profiles.");

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
                            WriteToImportStatusFile(originalConfig.DisplayName + " failed to import. iOS Device Feature template is currently bugged in graph SDK. Handle manually until this is resolved" + LogType.Error);
                            Log($"Failed to import {originalConfig.DisplayName}. iOS Device Feature template is currently bugged in C# Graph SDK. Handle manually until this is resolved", LogLevels.Error);
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

                        WriteToImportStatusFile($"Successfully imported {import.DisplayName}");

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

                        WriteToImportStatusFile($"Failed to import {policyName}\n", LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An unexpected error occurred during the import process: {ex.Message}", LogType.Error);
            }
            finally
            {
                WriteToImportStatusFile($"{DateTime.Now.ToString()} - Finished importing Device Configuration profiles.");
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

                foreach (var group in groupIds)
                {
                    // Adjust filter or assignment definitions as needed
                    assignments.Add(new DeviceConfigurationAssignment
                    {
                        OdataType = "#microsoft.graph.deviceConfigurationAssignment",
                        Target = new DeviceAndAppManagementAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            AdditionalData = new Dictionary<string, object>
                            {
                                { "groupId", group }
                            },
                            DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType,
                            DeviceAndAppManagementAssignmentFilterId = SelectedFilterID
                        }
                    });
                }

                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceConfigurations.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    var result = await destinationGraphServiceClient.DeviceManagement.DeviceConfigurations[configId].Assign.PostAsAssignPostResponseAsync(requestBody);

                    WriteToImportStatusFile("Assigned groups to device configuration " + configId);
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile($"Error assigning groups to device configuration {configId}", LogType.Error);
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while assigning groups to a single device configuration policy",LogType.Error);
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
                WriteToImportStatusFile($"An error occurred while deleting the device configuration policy: {ex.Message}", LogType.Error);
            }
        }
    }
}
