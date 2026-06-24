using IntuneTools.Pages;
using Microsoft.Graph;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ApplicationHelper
    {
        public static async Task<List<MobileApp>> GetAllMobileApps(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.DeviceAppManagement.MobileApps.GetAsync();

                List<MobileApp> mobileApps = new List<MobileApp>();

                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<MobileApp, MobileAppCollectionResponse>.CreatePageIterator(graphServiceClient, result, (app) =>
                    {
                        mobileApps.Add(app);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                }



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
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while retrieving all Mobile Apps: {ex.Message}", appFunction.Main);
                return new List<MobileApp>();
            }
        }

        public static async Task<List<MobileApp>> SearchMobileApps(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                var result = await graphServiceClient.DeviceAppManagement.MobileApps.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName, '{searchQuery}')";
                });

                List<MobileApp> mobileApps = new List<MobileApp>();

                if (result?.Value != null)
                {
                    var pageIterator = PageIterator<MobileApp, MobileAppCollectionResponse>.CreatePageIterator(graphServiceClient, result, (app) =>
                    {
                        mobileApps.Add(app);
                        return true;
                    });
                    await pageIterator.IterateAsync();
                }

                return mobileApps;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while searching for Mobile Apps with query '{searchQuery}': {ex.Message}", appFunction.Main);
                return new List<MobileApp>();
            }
        }

        public static async Task PrepareApplicationForAssignment(KeyValuePair<string, CustomContentInfo> appInfo, List<string> groups, GraphServiceClient graphServiceClient)
        {
            // Get the application type
            var appType = TranslateODataTypeFromApplicationType(appInfo.Value.ContentType);

            // Prepare the app options based on the application type
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
                _ => (MobileAppAssignmentSettings?)null // Marker for unsupported
            };

            // Check if app type is unsupported (not in the switch cases above)
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
                AppLogger.Info("The selected app type is not supported for deployment yet. Skipping", appFunction.Main);
                return;
            }

            try
            {
                await AssignGroupsToApplication(appInfo.Value.ContentId, appInfo.Value.ContentName, groups, graphServiceClient, assignmentSettings);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while preparing application of type '{appInfo.Value.ContentPlatform}' for assignment: {ex.Message}", appFunction.Main);
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

        public static async Task AssignGroupsToApplication(string appId, string contentName, List<string> groupIds, GraphServiceClient graphServiceClient, MobileAppAssignmentSettings? assignmentSettings = null)
        {
            try
            {
                if (string.IsNullOrEmpty(appId))
                {
                    throw new ArgumentNullException(nameof(appId));
                }
                if (groupIds == null)
                {
                    throw new ArgumentNullException(nameof(groupIds));
                }
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                var assignments = new List<MobileAppAssignment>();
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

                    MobileAppAssignment assignment;

                    // Check if this is a virtual group (All Users or All Devices)
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignment = new MobileAppAssignment
                        {
                            OdataType = "#microsoft.graph.mobileAppAssignment",
                            Target = new AllLicensedUsersAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            },
                            Intent = _selectedAppDeploymentIntent,
                            Settings = assignmentSettings
                        };
                    }
                    else if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        assignment = new MobileAppAssignment
                        {
                            OdataType = "#microsoft.graph.mobileAppAssignment",
                            Target = new AllDevicesAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.allDevicesAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                            },
                            Intent = _selectedAppDeploymentIntent,
                            Settings = assignmentSettings
                        };
                    }
                    else
                    {
                        // Regular group assignment
                        assignment = new MobileAppAssignment
                        {
                            OdataType = "#microsoft.graph.mobileAppAssignment",
                            Target = new GroupAssignmentTarget
                            {
                                OdataType = "#microsoft.graph.groupAssignmentTarget",
                                DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                                DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType,
                                GroupId = group
                            },
                            Intent = _selectedAppDeploymentIntent,
                            Settings = assignmentSettings
                        };
                    }

                    assignments.Add(assignment);
                }

                // Cleanup for known issues with certain assignment settings
                foreach (var assignment in assignments)
                {
                    // iOS VPP App specific cleanup
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
                        // Not supported
                        AppLogger.Info("Assignment settings for 'Available' intent to 'All Devices virtual group' is not supported.", appFunction.Assignment);
                    }
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await graphServiceClient
                    .DeviceAppManagement
                    .MobileApps[appId]
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
                var requestBody = new Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.Assign.AssignPostRequestBody
                {
                    MobileAppAssignments = assignments
                };

                await ExecuteWithRetryAsync(async () =>
                {
                    await graphServiceClient
                        .DeviceAppManagement
                        .MobileApps[appId]
                        .Assign
                        .PostAsync(requestBody);
                }, maxRetries: 5, baseDelaySeconds: 2);

                UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while assigning groups to application: {ex.Message}", appFunction.Assignment);
                throw;
            }
        }

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

                    // Calculate delay with exponential backoff
                    var delaySeconds = baseDelaySeconds * Math.Pow(2, attempt);
                    AppLogger.Warning($"Rate limited (429). Retrying in {delaySeconds} seconds... (Attempt {attempt + 1}/{maxRetries})", appFunction.Main);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }

        public static async Task RenameApplication(GraphServiceClient graphServiceClient, string appId, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (string.IsNullOrWhiteSpace(appId))
                {
                    throw new ArgumentNullException(nameof(appId));
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                var existingApp = await graphServiceClient.DeviceAppManagement.MobileApps[appId].GetAsync();

                if (existingApp == null)
                {
                    throw new InvalidOperationException($"Application with ID '{appId}' not found.");
                }

                var supportedRenameTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "#microsoft.graph.win32LobApp",
                    "#microsoft.graph.winGetApp",
                    "#microsoft.graph.webApp",
                    "#microsoft.graph.windowsWebApp"
                    // TODO - Add more supported types as needed, currently only types that support renaming are included here
                };

                if (string.IsNullOrWhiteSpace(existingApp.OdataType) || !supportedRenameTypes.Contains(existingApp.OdataType))
                {
                    AppLogger.Warning($"Rename/description updates are not supported for app type '{existingApp.OdataType ?? "Unknown"}'.", appFunction.Rename);
                    return;
                }

                if (selectedRenameMode == "Prefix")
                {
                    var name = FindPreFixInPolicyName(existingApp.DisplayName ?? string.Empty, newName);

                    var app = new MobileApp
                    {
                        OdataType = existingApp.OdataType,
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceAppManagement.MobileApps[appId].PatchAsync(app);
                    AppLogger.Info($"Renamed application {appId} to '{name}'", appFunction.Rename);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    var app = new MobileApp
                    {
                        OdataType = existingApp.OdataType,
                        Description = newName,
                    };

                    await graphServiceClient.DeviceAppManagement.MobileApps[appId].PatchAsync(app);
                    AppLogger.Info($"Updated description for application {appId} to '{newName}'", appFunction.Rename);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var name = RemovePrefixFromPolicyName(existingApp.DisplayName);

                    var app = new MobileApp
                    {
                        OdataType = existingApp.OdataType,
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceAppManagement.MobileApps[appId].PatchAsync(app);
                    AppLogger.Info($"Removed prefix from application {appId}, new name: '{name}'", appFunction.Rename);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while renaming applications: {ex.Message}", appFunction.Rename);
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllApplicationContentAsync(GraphServiceClient graphServiceClient)
        {
            var apps = await GetAllMobileApps(graphServiceClient);
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

        /// <summary>
        /// Deletes a mobile application by ID. Throws if the deletion fails so callers
        /// (e.g. CleanupPage's bulk-delete loop) can count the failure as an error
        /// instead of treating it as a silent skip.
        /// </summary>
        public static async Task DeleteApplication(GraphServiceClient graphServiceClient, string appId)
        {
            if (graphServiceClient == null)
            {
                throw new ArgumentNullException(nameof(graphServiceClient));
            }

            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new InvalidOperationException("Application ID cannot be null or empty.");
            }

            try
            {
                await graphServiceClient.DeviceAppManagement.MobileApps[appId].DeleteAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while deleting application '{appId}': {ex.Message}", appFunction.Delete);
                throw;
            }
        }

        /// <summary>
        /// Checks if a mobile application has any assignments.
        /// </summary>
        public static async Task<bool?> HasApplicationAssignmentsAsync(GraphServiceClient graphServiceClient, string appId)
        {
            try
            {
                var result = await graphServiceClient.DeviceAppManagement.MobileApps[appId].Assignments.GetAsync(rc =>
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

        public static async Task<List<CustomContentInfo>> SearchApplicationContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var apps = await SearchMobileApps(graphServiceClient, searchQuery);
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
    }
}
