using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class DeviceConfigurationHelper
    {
        private class Helper : GraphHelper<Microsoft.Graph.Beta.Models.DeviceConfiguration, DeviceConfigurationCollectionResponse>
        {
            protected override string ResourceName => "device configuration policies";
            protected override string ContentTypeName => "Device Configuration Policy";

            protected override string? GetPolicyPlatform(Microsoft.Graph.Beta.Models.DeviceConfiguration policy)
                => HelperClass.TranslatePolicyPlatformName(policy.OdataType?.ToString() ?? string.Empty);

            protected override string? GetPolicyName(Microsoft.Graph.Beta.Models.DeviceConfiguration policy) => policy.DisplayName;
            protected override string? GetPolicyId(Microsoft.Graph.Beta.Models.DeviceConfiguration policy) => policy.Id;
            protected override string? GetPolicyDescription(Microsoft.Graph.Beta.Models.DeviceConfiguration policy) => policy.Description;

            protected override Task<DeviceConfigurationCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.DeviceConfigurations.GetAsync(rc =>
                {
                    rc.QueryParameters.Top = 1000;
                });

            protected override Task<DeviceConfigurationCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.DeviceConfigurations.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

            protected override Task<Microsoft.Graph.Beta.Models.DeviceConfiguration?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceConfigurations[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.DeviceConfigurations[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var existing = await GetByIdAsync(client, id);
                if (existing == null) return;

                var policyType = existing.GetType();
                var policy = (Microsoft.Graph.Beta.Models.DeviceConfiguration?)Activator.CreateInstance(policyType);
                if (policy == null) return;

                policy.DisplayName = newName;
                await client.DeviceManagement.DeviceConfigurations[id].PatchAsync(policy);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var existing = await GetByIdAsync(client, id);
                if (existing == null) return;

                var policyType = existing.GetType();
                var policy = (Microsoft.Graph.Beta.Models.DeviceConfiguration?)Activator.CreateInstance(policyType);
                if (policy == null) return;

                policy.Description = description;
                await client.DeviceManagement.DeviceConfigurations[id].PatchAsync(policy);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exportedPolicy = GraphImportHelper.DeserializeFromJson(policyData, DeviceConfiguration.CreateFromDiscriminatorValue);

                    if (exportedPolicy == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize device configuration data from JSON.", LogLevels.Error);
                        return null;
                    }

                    // Skip iOS Device Features — known Graph SDK bug
                    if (exportedPolicy.OdataType != null &&
                        exportedPolicy.OdataType.Equals("#microsoft.graph.iosDeviceFeaturesConfiguration", StringComparison.OrdinalIgnoreCase))
                    {
                        LogToFunctionFile(appFunction.Main, $"Skipped '{exportedPolicy.DisplayName}': iOS Device Feature template is currently bugged in Graph SDK.", LogLevels.Warning);
                        return null;
                    }

                    var type = exportedPolicy.GetType();
                    if (type.IsAbstract)
                    {
                        LogToFunctionFile(appFunction.Main, $"Skipped '{exportedPolicy.DisplayName}': abstract type {type.Name} cannot be imported.", LogLevels.Warning);
                        return null;
                    }

                    var newPolicy = (DeviceConfiguration)Activator.CreateInstance(type)!;
                    GraphImportHelper.CopyProperties(exportedPolicy, newPolicy, new[] { "Version", "AdditionalData", "BackingStore" });

                    // Clear navigation / read-only collections that Graph rejects on POST
                    newPolicy.Assignments = new List<DeviceConfigurationAssignment>();
                    newPolicy.GroupAssignments = new List<DeviceConfigurationGroupAssignment>();
                    newPolicy.DeviceStatuses = new List<DeviceConfigurationDeviceStatus>();
                    newPolicy.DeviceSettingStateSummaries = new List<SettingStateDeviceSummary>();
                    newPolicy.UserStatuses = new List<DeviceConfigurationUserStatus>();

                    // Special case for Windows 10 General Configuration
                    if (newPolicy is Windows10GeneralConfiguration windows10Config)
                    {
                        windows10Config.PrivacyAccessControls = windows10Config.PrivacyAccessControls ?? new List<WindowsPrivacyDataAccessControlItem>();
                    }

                    var imported = await client.DeviceManagement.DeviceConfigurations.PostAsync(newPolicy);

                    LogToFunctionFile(appFunction.Main, $"Imported device configuration: {imported?.DisplayName}");
                    return imported?.DisplayName;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "importing from JSON", ResourceName);
                    return null;
                }
            }

            public override async Task<bool?> HasAssignmentsAsync(GraphServiceClient client, string id)
            {
                try
                {
                    var result = await client.DeviceManagement.DeviceConfigurations[id].Assignments.GetAsync(rc =>
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

            public override async Task<List<AssignmentInfo>?> GetAssignmentDetailsAsync(GraphServiceClient client, string id)
            {
                try
                {
                    var details = new List<AssignmentInfo>();
                    var result = await client.DeviceManagement.DeviceConfigurations[id].Assignments.GetAsync();

                    while (result?.Value != null)
                    {
                        foreach (var assignment in result.Value)
                        {
                            details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                        }

                        if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                        result = await client.DeviceManagement.DeviceConfigurations[id]
                            .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                    }

                    return details;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "getting assignment details for", $"Device Configuration {id}");
                    return null;
                }
            }

            public override async Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            {
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceConfigurations.Item.Assign.AssignPostRequestBody
                {
                    Assignments = new List<DeviceConfigurationAssignment>()
                };

                await client.DeviceManagement.DeviceConfigurations[id].Assign.PostAsAssignPostResponseAsync(requestBody);
                LogToFunctionFile(appFunction.Main, $"Removed all assignments from Device Configuration policy {id}.");
            }

            public override async Task ImportMultipleAsync(
                GraphServiceClient sourceClient,
                GraphServiceClient destinationClient,
                List<string> ids,
                bool assignments,
                bool filter,
                List<string> groups)
            {
                await GraphImportHelper.ImportBatchAsync(ids, ResourceName, async id =>
                {
                    var policyName = "";
                    try
                    {
                        var originalConfig = await sourceClient.DeviceManagement.DeviceConfigurations[id].GetAsync();

                        if (originalConfig == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping config ID {id}: Not found in source tenant.");
                            return;
                        }

                        if (originalConfig.OdataType != null &&
                            originalConfig.OdataType.Equals("#microsoft.graph.iosDeviceFeaturesConfiguration", StringComparison.OrdinalIgnoreCase))
                        {
                            LogToFunctionFile(appFunction.Main, originalConfig.DisplayName + " failed to import. iOS Device Feature template is currently bugged in graph SDK. Handle manually until this is resolved", LogLevels.Error);
                            return;
                        }

                        var typeOfPolicy = originalConfig.GetType();

                        if (typeOfPolicy.IsAbstract)
                        {
                            return;
                        }

                        var deviceConfiguration = GraphImportHelper.CloneForImport<Microsoft.Graph.Beta.Models.DeviceConfiguration>(originalConfig);

                        deviceConfiguration.Assignments = deviceConfiguration.Assignments ?? new List<DeviceConfigurationAssignment>();
                        deviceConfiguration.GroupAssignments = deviceConfiguration.GroupAssignments ?? new List<DeviceConfigurationGroupAssignment>();
                        deviceConfiguration.DeviceStatuses = deviceConfiguration.DeviceStatuses ?? new List<DeviceConfigurationDeviceStatus>();
                        deviceConfiguration.DeviceSettingStateSummaries = deviceConfiguration.DeviceSettingStateSummaries ?? new List<SettingStateDeviceSummary>();
                        deviceConfiguration.UserStatuses = deviceConfiguration.UserStatuses ?? new List<DeviceConfigurationUserStatus>();

                        // Special case for Windows 10 General Configuration policies
                        if (deviceConfiguration.OdataType != null &&
                            deviceConfiguration.OdataType.Equals("#microsoft.graph.windows10GeneralConfiguration", StringComparison.OrdinalIgnoreCase))
                        {
                            if (deviceConfiguration is Windows10GeneralConfiguration windows10Config)
                            {
                                windows10Config.PrivacyAccessControls = windows10Config.PrivacyAccessControls ?? new List<WindowsPrivacyDataAccessControlItem>();
                            }
                        }

                        policyName = deviceConfiguration.DisplayName;

                        var import = await destinationClient.DeviceManagement.DeviceConfigurations.PostAsync(deviceConfiguration);

                        LogToFunctionFile(appFunction.Main, $"Successfully imported {import.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSingleDeviceConfiguration(import.Id, groups, destinationClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import {policyName}\n", LogLevels.Error);
                    }
                });
            }

            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<DeviceConfigurationAssignment>();

                    var buildResult = GraphAssignmentHelper.BuildAssignments<DeviceConfigurationAssignment>(
                        groupIds,
                        (target, groupId) =>
                        {
                            return new DeviceConfigurationAssignment
                            {
                                OdataType = "#microsoft.graph.deviceConfigurationAssignment",
                                Target = target
                            };
                        },
                        assignments);

                    // Merge existing assignments
                    var existingAssignments = await client
                        .DeviceManagement
                        .DeviceConfigurations[id]
                        .Assignments
                        .GetAsync();

                    GraphAssignmentHelper.MergeExistingAssignments(
                        existingAssignments?.Value,
                        assignments,
                        buildResult,
                        a => a.Target);

                    var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceConfigurations.Item.Assign.AssignPostRequestBody
                    {
                        Assignments = assignments
                    };

                    try
                    {
                        await client
                            .DeviceManagement
                            .DeviceConfigurations[id]
                            .Assign
                            .PostAsAssignPostResponseAsync(requestBody);

                        LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to device configuration {id} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                        UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to device configuration policy: {ex.Message}", LogLevels.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to a single device configuration policy: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<Microsoft.Graph.Beta.Models.DeviceConfiguration>> SearchForDeviceConfigurations(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task<List<Microsoft.Graph.Beta.Models.DeviceConfiguration>> GetAllDeviceConfigurations(GraphServiceClient graphServiceClient)
            => _helper.GetAllAsync(graphServiceClient);

        public static Task ImportMultipleDeviceConfigurations(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> configurationIds, bool assignments, bool filter, List<string> groups)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, configurationIds, assignments, filter, groups);

        public static Task AssignGroupsToSingleDeviceConfiguration(string configId, List<string> groupIds, GraphServiceClient destinationGraphServiceClient)
            => _helper.AssignGroupsAsync(configId, groupIds, destinationGraphServiceClient);

        public static Task DeleteDeviceConfigurationPolicy(GraphServiceClient graphServiceClient, string policyID)
            => _helper.DeleteAsync(graphServiceClient, policyID);

        public static Task RenameDeviceConfigurationPolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
            => _helper.RenameAsync(graphServiceClient, policyID, newName);

        public static Task<List<CustomContentInfo>> GetAllDeviceConfigurationContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchDeviceConfigurationContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportDeviceConfigurationPolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
            => _helper.ExportDataAsync(graphServiceClient, policyId);

        public static Task<string?> ImportDeviceConfigurationFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);

        public static Task<bool?> HasDeviceConfigurationAssignmentsAsync(GraphServiceClient graphServiceClient, string configId)
            => _helper.HasAssignmentsAsync(graphServiceClient, configId);

        public static Task<List<AssignmentInfo>?> GetDeviceConfigurationAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string configId)
            => _helper.GetAssignmentDetailsAsync(graphServiceClient, configId);

        public static Task RemoveAllDeviceConfigurationAssignmentsAsync(GraphServiceClient graphServiceClient, string configId)
            => _helper.RemoveAllAssignmentsAsync(graphServiceClient, configId);
    }
}
