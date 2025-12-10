using IntuneTools.Pages;
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
    public class ApplicationHelper
    {
        public static async Task<List<MobileApp>> GetAllMobileApps(GraphServiceClient graphServiceClient)
        {
            try
            {
                LogToImportStatusFile("Retrieving all Mobile Apps.");

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
                    LogToImportStatusFile($"Found {mobileApps.Count} Mobile Apps.");
                }
                else
                {
                    LogToImportStatusFile("No Mobile Apps found or the result was null.");
                }

                return mobileApps;
            }
            catch (Exception)
            {
                LogToImportStatusFile("An error occurred while retrieving all Mobile Apps", LogLevels.Error);
                return new List<MobileApp>();
            }
        }

        public static async Task<List<MobileApp>> SearchMobileApps(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToImportStatusFile($"Searching for Mobile Apps containing '{searchQuery}'.");

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
                    LogToImportStatusFile($"Found {mobileApps.Count} Mobile Apps matching '{searchQuery}'.");
                }
                else
                {
                    LogToImportStatusFile($"No Mobile Apps found matching '{searchQuery}' or the result was null.");
                }

                return mobileApps;
            }
            catch (Exception)
            {
                LogToImportStatusFile($"An error occurred while searching for Mobile Apps with query '{searchQuery}'", LogLevels.Error);
                return new List<MobileApp>();
            }
        }

        public static async Task PrepareApplicationForAssignment(KeyValuePair<string, AssignmentInfo> appInfo, List<string> groups , GraphServiceClient graphServiceClient)
        {
            // This method can be expanded based on specific preparation steps needed for different app types

            // Get the application type
            var appType = TranslateODataTypeFromApplicationType(appInfo.Value.Platform);

            try
            {
                await AssignGroupsToApplication(appInfo.Value.Id, groups, graphServiceClient);
            }
            catch (Exception)
            {
                LogToImportStatusFile($"An error occurred while preparing application of type '{appInfo.Value.Platform}' for assignment", LogLevels.Error);
            }
        }



        public static async Task AssignGroupsToApplication(string appId, List<string> groupIds, GraphServiceClient graphServiceClient)
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
                            Intent = _selectedInstallIntent
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
                            Intent = _selectedInstallIntent
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
                            Intent = _selectedInstallIntent
                        };
                    }

                    assignments.Add(assignment);
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

                try
                {
                    await graphServiceClient
                        .DeviceAppManagement
                        .MobileApps[appId]
                        .Assign
                        .PostAsync(requestBody);

                    LogToImportStatusFile($"Assigned {assignments.Count} assignments to application {appId} with filter type {deviceAndAppManagementAssignmentFilterType}.");
                }
                catch (Exception ex)
                {
                    LogToImportStatusFile("An error occurred while assigning groups to application", LogLevels.Warning);
                    LogToImportStatusFile(ex.Message, LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An error occurred while assigning groups to application", LogLevels.Warning);
                LogToImportStatusFile(ex.Message, LogLevels.Error);
            }
        }
    }
}
