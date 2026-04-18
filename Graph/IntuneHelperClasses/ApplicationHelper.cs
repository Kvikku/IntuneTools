using IntuneTools.Pages;
using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ApplicationHelper
    {
        private class Helper : GraphHelper<MobileApp, MobileAppCollectionResponse>
        {
            protected override string ResourceName => "mobile applications";
            protected override string ContentTypeName => "Application";

            protected override string? GetPolicyName(MobileApp policy) => policy.DisplayName;
            protected override string? GetPolicyId(MobileApp policy) => policy.Id;
            protected override string? GetPolicyDescription(MobileApp policy) => policy.Description;

            protected override string? GetPolicyPlatform(MobileApp policy)
                => HelperClass.TranslatePolicyPlatformName(policy.OdataType);

            /// <summary>
            /// Applications use TranslateApplicationType for the content type instead of a fixed string.
            /// Override MapToContent behavior by overriding GetAllContentAsync/SearchContentAsync.
            /// </summary>
            private List<CustomContentInfo> MapAppsToContent(List<MobileApp> apps)
            {
                var content = new List<CustomContentInfo>();
                foreach (var app in apps)
                {
                    content.Add(new CustomContentInfo
                    {
                        ContentName = app.DisplayName,
                        ContentType = HelperClass.TranslateApplicationType(app.OdataType),
                        ContentPlatform = HelperClass.TranslatePolicyPlatformName(app.OdataType),
                        ContentId = app.Id,
                        ContentDescription = app.Description
                    });
                }
                return content;
            }

            public async Task<List<CustomContentInfo>> GetAllAppContentAsync(GraphServiceClient client)
            {
                var apps = await GetAllAppsAsync(client);
                return MapAppsToContent(apps);
            }

            public async Task<List<CustomContentInfo>> SearchAppContentAsync(GraphServiceClient client, string searchQuery)
            {
                var apps = await SearchAsync(client, searchQuery);
                return MapAppsToContent(apps);
            }

            protected override Task<MobileAppCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceAppManagement.MobileApps.GetAsync();

            protected override Task<MobileAppCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceAppManagement.MobileApps.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(displayName, '{searchQuery}')";
                });

            protected override Task<MobileApp?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceAppManagement.MobileApps[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceAppManagement.MobileApps[id].DeleteAsync();

            private async Task PatchNameWithTypeAsync(GraphServiceClient client, string id, string newName, string odataType)
            {
                var app = new MobileApp
                {
                    OdataType = odataType,
                    DisplayName = newName,
                };
                await client.DeviceAppManagement.MobileApps[id].PatchAsync(app);
            }

            private async Task PatchDescriptionWithTypeAsync(GraphServiceClient client, string id, string description, string odataType)
            {
                var app = new MobileApp
                {
                    OdataType = odataType,
                    Description = description,
                };
                await client.DeviceAppManagement.MobileApps[id].PatchAsync(app);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                // Applications are not imported via JSON; they use cross-tenant import
                return null;
            }

            /// <summary>
            /// Gets all apps and filters out excluded OData types.
            /// </summary>
            public async Task<List<MobileApp>> GetAllAppsAsync(GraphServiceClient client)
            {
                var mobileApps = await GetAllAsync(client);

                // Filter out unwanted ODataTypes
                var excludedODataTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Add the ODataTypes to be excluded here
                };

                if (excludedODataTypes.Count > 0)
                {
                    mobileApps = mobileApps
                        .Where(app => !string.IsNullOrEmpty(app.OdataType) && !excludedODataTypes.Contains(app.OdataType))
                        .ToList();
                }

                return mobileApps;
            }

            /// <summary>
            /// Renames an application with type checking for supported rename types.
            /// Handles all rename modes directly using a single GET to avoid duplicate fetches.
            /// </summary>
            public async Task RenameAppAsync(GraphServiceClient client, string id, string newName)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(client);
                    if (string.IsNullOrWhiteSpace(id))
                        throw new ArgumentNullException(nameof(id));
                    if (string.IsNullOrWhiteSpace(newName))
                        throw new InvalidOperationException("New name cannot be null or empty.");

                    var existingApp = await GetByIdAsync(client, id);

                    if (existingApp == null)
                    {
                        LogToFunctionFile(appFunction.Main, $"Unable to rename: {ResourceName} with ID {id} was not found.", LogLevels.Warning);
                        return;
                    }

                    var supportedRenameTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "#microsoft.graph.win32LobApp",
                        "#microsoft.graph.winGetApp",
                        "#microsoft.graph.webApp",
                        "#microsoft.graph.windowsWebApp"
                    };

                    if (string.IsNullOrWhiteSpace(existingApp.OdataType) || !supportedRenameTypes.Contains(existingApp.OdataType))
                    {
                        LogToFunctionFile(appFunction.Main, $"Rename/description updates are not supported for app type '{existingApp.OdataType ?? "Unknown"}'.", LogLevels.Warning);
                        return;
                    }

                    var odataType = existingApp.OdataType;

                    if (selectedRenameMode == "Prefix")
                    {
                        var currentName = GetPolicyName(existingApp) ?? string.Empty;
                        var name = FindPreFixInPolicyName(currentName, newName);
                        await PatchNameWithTypeAsync(client, id, name, odataType);
                        LogToFunctionFile(appFunction.Main, $"Renamed {ResourceName} {id} to {name}");
                    }
                    else if (selectedRenameMode == "Description")
                    {
                        await PatchDescriptionWithTypeAsync(client, id, newName, odataType);
                        LogToFunctionFile(appFunction.Main, $"Updated description for {ResourceName} {id} to {newName}");
                    }
                    else if (selectedRenameMode == "RemovePrefix")
                    {
                        var currentName = GetPolicyName(existingApp);
                        if (string.IsNullOrWhiteSpace(currentName))
                        {
                            LogToFunctionFile(appFunction.Main, $"Unable to remove prefix from {ResourceName} {id}: name is null or empty.", LogLevels.Warning);
                            return;
                        }
                        var name = RemovePrefixFromPolicyName(currentName);
                        await PatchNameWithTypeAsync(client, id, name, odataType);
                        LogToFunctionFile(appFunction.Main, $"Removed prefix from {ResourceName} {id}, new name: {name}");
                    }
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "renaming", ResourceName);
                }
            }

            /// <summary>
            /// MobileApp assignments use MobileAppAssignment with Settings and Intent.
            /// Uses ExecuteWithRetryAsync for rate limiting.
            /// </summary>
            public override async Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(id);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    // This is called from PrepareApplicationForAssignment with null settings for types
                    // that don't require specific settings
                    await AssignGroupsWithSettingsAsync(id, groupIds, client, null);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to application: {ex.Message}", LogLevels.Warning);
                }
            }

            /// <summary>
            /// Assigns groups to an application with MobileAppAssignment-specific settings and intent.
            /// </summary>
            public async Task AssignGroupsWithSettingsAsync(string appId, List<string> groupIds, GraphServiceClient client, MobileAppAssignmentSettings? assignmentSettings)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(appId);
                    ArgumentNullException.ThrowIfNull(groupIds);
                    ArgumentNullException.ThrowIfNull(client);

                    var assignments = new List<MobileAppAssignment>();
                    var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var hasAllUsers = false;
                    var hasAllDevices = false;

                    // Step 1: Build new assignments with Settings and Intent
                    foreach (var group in groupIds)
                    {
                        if (string.IsNullOrWhiteSpace(group) || !seenGroupIds.Add(group))
                            continue;

                        DeviceAndAppManagementAssignmentTarget target;

                        if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            hasAllUsers = true;
                            target = new AllLicensedUsersAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget"
                            };
                        }
                        else if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            hasAllDevices = true;
                            target = new AllDevicesAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allDevicesAssignmentTarget"
                            };
                        }
                        else
                        {
                            target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                GroupId = group
                            };
                        }

                        GraphAssignmentHelper.ApplySelectedFilter(target);

                        var assignment = new MobileAppAssignment
                        {
                            OdataType = "#microsoft.graph.mobileAppAssignment",
                            Target = target,
                            Intent = _selectedAppDeploymentIntent,
                            Settings = assignmentSettings
                        };

                        assignments.Add(assignment);
                    }

                    // Cleanup for known issues with certain assignment settings
                    foreach (var assignment in assignments)
                    {
                        if (assignment.Settings is IosVppAppAssignmentSettings vppSettings)
                        {
                            switch (assignment.Intent)
                            {
                                case InstallIntent.Available:
                                    vppSettings.IsRemovable = null;
                                    break;
                                case InstallIntent.Uninstall:
                                    vppSettings.IsRemovable = null;
                                    vppSettings.PreventAutoAppUpdate = null;
                                    vppSettings.PreventManagedAppBackup = null;
                                    vppSettings.UninstallOnDeviceRemoval = null;
                                    break;
                            }
                        }
                        if (assignment.Intent == InstallIntent.Available || assignment.Target.OdataType == "#microsoft.graph.allDevicesAssignmentTarget")
                        {
                            LogToFunctionFile(appFunction.Main, "Assignment settings for 'Available' intent to 'All Devices virtual group' is not supported.");
                        }
                    }

                    // Step 2: Merge existing assignments
                    var existingAssignments = await client
                        .DeviceAppManagement
                        .MobileApps[appId]
                        .Assignments
                        .GetAsync();

                    if (existingAssignments?.Value != null)
                    {
                        foreach (var existing in existingAssignments.Value)
                        {
                            if (existing.Target is AllLicensedUsersAssignmentTarget)
                            {
                                if (!hasAllUsers)
                                    assignments.Add(existing);
                            }
                            else if (existing.Target is AllDevicesAssignmentTarget)
                            {
                                if (!hasAllDevices)
                                    assignments.Add(existing);
                            }
                            else if (existing.Target is GroupAssignmentTarget groupTarget)
                            {
                                var existingGroupId = groupTarget.GroupId;
                                if (!string.IsNullOrWhiteSpace(existingGroupId) && seenGroupIds.Add(existingGroupId))
                                    assignments.Add(existing);
                            }
                            else
                            {
                                assignments.Add(existing);
                            }
                        }
                    }

                    // Step 3: Post with retry for rate limiting
                    var requestBody = new Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.Assign.AssignPostRequestBody
                    {
                        MobileAppAssignments = assignments
                    };

                    await ExecuteWithRetryAsync(async () =>
                    {
                        await client
                            .DeviceAppManagement
                            .MobileApps[appId]
                            .Assign
                            .PostAsync(requestBody);
                    }, maxRetries: 5, baseDelaySeconds: 2);

                    LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to application {appId} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    LogToFunctionFile(appFunction.Main, $"An error occurred while assigning groups to application: {ex.Message}", LogLevels.Warning);
                }
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<MobileApp>> GetAllMobileApps(GraphServiceClient graphServiceClient)
            => _helper.GetAllAppsAsync(graphServiceClient);

        public static Task<List<MobileApp>> SearchMobileApps(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static async Task PrepareApplicationForAssignment(KeyValuePair<string, CustomContentInfo> appInfo, List<string> groups, GraphServiceClient graphServiceClient)
        {
            var appType = TranslateODataTypeFromApplicationType(appInfo.Value.ContentType);

            MobileAppAssignmentSettings? assignmentSettings = appType switch
            {
                "#microsoft.graph.win32LobApp" => CreateWin32LobAppAssignmentSettings(),
                "#microsoft.graph.androidManagedStoreApp" => CreateAndroidManagedStoreAppAssignmentSettings(),
                "#microsoft.graph.iosVppApp" => iOSAppDeploymentSettings,
                "#microsoft.graph.winGetApp" => CreateWinGetAppAssignmentSettings(),
                "#microsoft.graph.androidManagedStoreWebApp" or
                "#microsoft.graph.windowsWebApp" or
                "#microsoft.graph.webApp" or
                "#microsoft.graph.windowsMicrosoftEdgeApp" or
                "#microsoft.graph.officeSuiteApp" or
                "#microsoft.graph.macOSOfficeSuiteApp" or
                "#microsoft.graph.macOSWebClip" or
                "#microsoft.graph.macOSMicrosoftEdgeApp" or
                "#microsoft.graph.macOSMicrosoftDefenderApp" or
                "#microsoft.graph.iosiPadOSWebClip" => null,
                _ => (MobileAppAssignmentSettings?)null
            };

            var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "#microsoft.graph.win32LobApp",
                "#microsoft.graph.androidManagedStoreApp",
                "#microsoft.graph.iosVppApp",
                "#microsoft.graph.winGetApp",
                "#microsoft.graph.androidManagedStoreWebApp",
                "#microsoft.graph.windowsWebApp",
                "#microsoft.graph.webApp",
                "#microsoft.graph.windowsMicrosoftEdgeApp",
                "#microsoft.graph.officeSuiteApp",
                "#microsoft.graph.macOSOfficeSuiteApp",
                "#microsoft.graph.macOSWebClip",
                "#microsoft.graph.macOSMicrosoftEdgeApp",
                "#microsoft.graph.macOSMicrosoftDefenderApp",
                "#microsoft.graph.iosiPadOSWebClip"
            };

            if (!supportedTypes.Contains(appType))
            {
                LogToFunctionFile(appFunction.Main, "The selected app type is not supported for deployment yet. Skipping");
                return;
            }

            try
            {
                await AssignGroupsToApplication(appInfo.Value.ContentId, groups, graphServiceClient, assignmentSettings);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while preparing application of type '{appInfo.Value.ContentPlatform}' for assignment: {ex.Message}", LogLevels.Error);
            }
        }

        public static Win32LobAppAssignmentSettings CreateWin32LobAppAssignmentSettings()
        {
            return new Win32LobAppAssignmentSettings
            {
                OdataType = "#microsoft.graph.win32LobAppAssignmentSettings",
                Notifications = win32LobAppNotification,
                DeliveryOptimizationPriority = win32LobAppDeliveryOptimizationPriority
            };
        }

        public static AndroidManagedStoreAppAssignmentSettings CreateAndroidManagedStoreAppAssignmentSettings()
        {
            return new AndroidManagedStoreAppAssignmentSettings
            {
                OdataType = "#microsoft.graph.androidManagedStoreAppAssignmentSettings",
                AutoUpdateMode = _androidManagedStoreAutoUpdateMode
            };
        }

        public static IosVppAppAssignmentSettings CreateiOSVppAppAssignmentSettings(bool useDeviceLicensing, bool uninstallOnDeviceRemoval, bool isRemovable, bool preventManagedAppBackup, bool preventAutoAppUpdate)
        {
            return new IosVppAppAssignmentSettings
            {
                OdataType = "#microsoft.graph.iosVppAppAssignmentSettings",
                UseDeviceLicensing = useDeviceLicensing,
                UninstallOnDeviceRemoval = uninstallOnDeviceRemoval,
                IsRemovable = isRemovable,
                PreventManagedAppBackup = preventManagedAppBackup,
                PreventAutoAppUpdate = preventAutoAppUpdate
            };
        }

        public static WinGetAppAssignmentSettings CreateWinGetAppAssignmentSettings()
        {
            return new WinGetAppAssignmentSettings
            {
                OdataType = "#microsoft.graph.winGetAppAssignmentSettings",
                Notifications = (WinGetAppNotification)win32LobAppNotification,
            };
        }

        public static Task AssignGroupsToApplication(string appId, List<string> groupIds, GraphServiceClient graphServiceClient, MobileAppAssignmentSettings? assignmentSettings = null)
            => _helper.AssignGroupsWithSettingsAsync(appId, groupIds, graphServiceClient, assignmentSettings);

        public static Task RenameApplication(GraphServiceClient graphServiceClient, string appId, string newName)
            => _helper.RenameAppAsync(graphServiceClient, appId, newName);

        public static Task<List<CustomContentInfo>> GetAllApplicationContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllAppContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchApplicationContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAppContentAsync(graphServiceClient, searchQuery);

        private static async Task ExecuteWithRetryAsync(Func<Task> action, int maxRetries = 5, int baseDelaySeconds = 2)
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await action();
                    return;
                }
                catch (Microsoft.Graph.ServiceException ex) when (ex.ResponseStatusCode == 429)
                {
                    if (attempt == maxRetries)
                    {
                        throw;
                    }

                    var delaySeconds = baseDelaySeconds * Math.Pow(2, attempt);
                    LogToFunctionFile(appFunction.Main, $"Rate limited (429). Retrying in {delaySeconds} seconds... (Attempt {attempt + 1}/{maxRetries})", LogLevels.Warning);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }
    }
}
