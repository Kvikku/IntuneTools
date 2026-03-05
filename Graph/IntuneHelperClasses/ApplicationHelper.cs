using IntuneTools.Pages;
using IntuneTools.Utilities;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ApplicationHelper
    {
        public static async Task<List<MobileApp>> GetAllMobileApps(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, "Retrieving all Mobile Apps.");

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
                    LogToFunctionFile(appFunction.Main, $"Found {mobileApps.Count} Mobile Apps.");
                }
                else
                {
                    LogToFunctionFile(appFunction.Main, "No Mobile Apps found or the result was null.");
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
            catch (Exception)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while retrieving all Mobile Apps", LogLevels.Error);
                return new List<MobileApp>();
            }
        }

        public static async Task<List<MobileApp>> SearchMobileApps(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Searching for Mobile Apps containing '{searchQuery}'.");

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
                    LogToFunctionFile(appFunction.Main, $"Found {mobileApps.Count} Mobile Apps matching '{searchQuery}'.");
                }
                else
                {
                    LogToFunctionFile(appFunction.Main, $"No Mobile Apps found matching '{searchQuery}' or the result was null.");
                }

                return mobileApps;
            }
            catch (Exception)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while searching for Mobile Apps with query '{searchQuery}'", LogLevels.Error);
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
                LogToFunctionFile(appFunction.Main, "The selected app type is not supported for deployment yet. Skipping");
                return;
            }

            try
            {
                await AssignGroupsToApplication(appInfo.Value.ContentId, groups, graphServiceClient, assignmentSettings);
            }
            catch (Exception)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while preparing application of type '{appInfo.Value.ContentPlatform}' for assignment", LogLevels.Error);
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

        public static async Task AssignGroupsToApplication(string appId, List<string> groupIds, GraphServiceClient graphServiceClient, MobileAppAssignmentSettings? assignmentSettings = null)
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
                        LogToFunctionFile(appFunction.Main, "Assignment settings for 'Available' intent to 'All Devices virtual group' is not supported.");
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

                LogToFunctionFile(appFunction.Main, $"Assigned {assignments.Count} assignments to application {appId} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while assigning groups to application", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
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
                    LogToFunctionFile(appFunction.Main, $"Rate limited (429). Retrying in {delaySeconds} seconds... (Attempt {attempt + 1}/{maxRetries})", LogLevels.Warning);
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
                    LogToFunctionFile(appFunction.Main, $"Rename/description updates are not supported for app type '{existingApp.OdataType ?? "Unknown"}'.", LogLevels.Warning);
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
                    LogToFunctionFile(appFunction.Main, $"Renamed application {appId} to '{name}'");
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
                    LogToFunctionFile(appFunction.Main, $"Updated description for application {appId} to '{newName}'");
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
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from application {appId}, new name: '{name}'");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming applications", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
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
