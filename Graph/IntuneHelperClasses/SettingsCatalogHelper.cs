using Microsoft.Graph;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class SettingsCatalogHelper
    {
        private static void ApplySelectedFilter(DeviceAndAppManagementAssignmentTarget target)
        {
            if (target == null)
            {
                return;
            }

            if (IsFilterSelected
                && !string.IsNullOrWhiteSpace(SelectedFilterID)
                && Guid.TryParse(SelectedFilterID, out _)
                && deviceAndAppManagementAssignmentFilterType != DeviceAndAppManagementAssignmentFilterType.None)
            {
                target.DeviceAndAppManagementAssignmentFilterId = SelectedFilterID;
                target.DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType;
                return;
            }

            target.DeviceAndAppManagementAssignmentFilterId = null;
            target.DeviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
        }

        public static async Task<List<DeviceManagementConfigurationPolicy>> SearchForSettingsCatalog(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.ConfigurationPolicies.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(Name,'{searchQuery}')";
                });

                List<DeviceManagementConfigurationPolicy> configurationPolicies = new List<DeviceManagementConfigurationPolicy>();
                var pageIterator = PageIterator<DeviceManagementConfigurationPolicy, DeviceManagementConfigurationPolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    configurationPolicies.Add(policy);
                    return true;
                });
                await pageIterator.IterateAsync();

                return configurationPolicies;
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while searching for settings catalog policies: {ex.Message}", appFunction.Main);
                return new List<DeviceManagementConfigurationPolicy>();
            }
        }

        public static async Task<List<DeviceManagementConfigurationPolicy>> GetAllSettingsCatalogPolicies(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.ConfigurationPolicies.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                List<DeviceManagementConfigurationPolicy> configurationPolicies = new List<DeviceManagementConfigurationPolicy>();
                var pageIterator = PageIterator<DeviceManagementConfigurationPolicy, DeviceManagementConfigurationPolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    configurationPolicies.Add(policy);
                    return true;
                });
                await pageIterator.IterateAsync();

                return configurationPolicies;
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"An error occurred while retrieving all settings catalog policies: {ex.Message}", appFunction.Main);
                return new List<DeviceManagementConfigurationPolicy>();
            }
        }

        public static async Task ImportMultipleSettingsCatalog(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> policies, bool assignments, bool filter, List<string> groups)
        {
            try
            {

                AppLogger.Info($"Importing {policies.Count} settings catalog policies.", appFunction.Import);

                bool hasFailures = false;
                foreach (var policy in policies)
                {
                    var policyName = policy;
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.ConfigurationPolicies[policy].GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Expand = new[] { "settings" };
                        });

                        var newPolicy = new DeviceManagementConfigurationPolicy
                        {
                            Name = result.Name,
                            Description = result.Description,
                            Platforms = result.Platforms,
                            Technologies = result.Technologies,
                            RoleScopeTagIds = result.RoleScopeTagIds,
                            Settings = result.Settings,
                            Assignments = new List<DeviceManagementConfigurationPolicyAssignment>()
                        };

                        policyName = newPolicy.Name ?? policy;

                        var import = await destinationGraphServiceClient.DeviceManagement.ConfigurationPolicies.PostAsync(newPolicy);

                        AppLogger.Info($"Imported '{import.Name}' successfully.", appFunction.Import);

                        if (assignments)
                        {
                            await AssignGroupsToSingleSettingsCatalog(import.Id, import.Name ?? string.Empty, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to import '{policyName}': {ex.Message}", appFunction.Import);
                        hasFailures = true;
                    }
                }
                if (hasFailures)
                    throw new Exception("One or more settings catalog policies failed to import. See Import.log for details.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task AssignGroupsToSingleSettingsCatalog(string policyID, string contentName, List<string> groupID, GraphServiceClient _graphServiceClient)
        {
            try
            {
                if (policyID == null)
                {
                    throw new ArgumentNullException(nameof(policyID));
                }
                if (groupID == null)
                {
                    throw new ArgumentNullException(nameof(groupID));
                }
                if (_graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(_graphServiceClient));
                }

                var assignments = new List<DeviceManagementConfigurationPolicyAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllUsers = false;
                var hasAllDevices = false;

                // Step 1: Add new assignments to request body
                foreach (var group in groupID)
                {
                    if (string.IsNullOrWhiteSpace(group) || !seenGroupIds.Add(group))
                    {
                        continue;
                    }

                    DeviceManagementConfigurationPolicyAssignment assignment;

                    // Check if this is a virtual group (All Users or All Devices)
                    if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        var target = new AllLicensedUsersAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget"
                        };
                        ApplySelectedFilter(target);

                        assignment = new DeviceManagementConfigurationPolicyAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                            Target = target,
                            Source = DeviceAndAppManagementAssignmentSource.Direct
                        };
                    }
                    else if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        var target = new AllDevicesAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.allDevicesAssignmentTarget"
                        };
                        ApplySelectedFilter(target);

                        assignment = new DeviceManagementConfigurationPolicyAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                            Target = target,
                            Source = DeviceAndAppManagementAssignmentSource.Direct
                        };
                    }
                    else
                    {
                        // Regular group assignment
                        var target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            GroupId = group
                        };
                        ApplySelectedFilter(target);

                        assignment = new DeviceManagementConfigurationPolicyAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementConfigurationPolicyAssignment",
                            Id = group,
                            Target = target,
                            Source = DeviceAndAppManagementAssignmentSource.Direct,
                            SourceId = group
                        };
                    }

                    assignments.Add(assignment);
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await _graphServiceClient
                    .DeviceManagement
                    .ConfigurationPolicies[policyID]
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
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
                {
                    Assignments = assignments
                };

                try
                {
                    await _graphServiceClient
                        .DeviceManagement
                        .ConfigurationPolicies[policyID]
                        .Assign
                        .PostAsAssignPostResponseAsync(requestBody);

                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"An error occurred while assigning groups to settings catalog policy: {ex.Message}", appFunction.Assignment);
                    throw;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task DeleteSettingsCatalog(GraphServiceClient graphServiceClient, string policyID)
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
                await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].DeleteAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task RenameSettingsCatalogPolicy(GraphServiceClient graphServiceClient, string policyID, string newName)
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
                    var existingPolicy = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].GetAsync();

                    var name = FindPreFixInPolicyName(existingPolicy.Name, newName);

                    var policy = new DeviceManagementConfigurationPolicy
                    {
                        Name = name
                    };

                    await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].PatchAsync(policy);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    //var existingPolicy = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].GetAsync();


                    var policy = new DeviceManagementConfigurationPolicy
                    {
                        Description = newName
                    };

                    await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].PatchAsync(policy);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingPolicy = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].GetAsync();

                    if (existingPolicy == null)
                    {
                        AppLogger.Warning($"Unable to remove prefix: policy with ID {policyID} was not found.", appFunction.Rename);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(existingPolicy.Name))
                    {
                        AppLogger.Warning($"Unable to remove prefix from policy {policyID}: policy name is null or empty.", appFunction.Rename);
                        return;
                    }
                    var name = ApplyPrefixRemoval(existingPolicy.Name);

                    var policy = new DeviceManagementConfigurationPolicy
                    {
                        Name = name
                    };

                    await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].PatchAsync(policy);
                }
                else if (selectedRenameMode == "RemoveDescription")
                {
                    var policy = new DeviceManagementConfigurationPolicy
                    {
                        Description = string.Empty
                    };

                    await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyID].PatchAsync(policy);
                    AppLogger.Info($"Cleared description for Settings Catalog policy {policyID}", appFunction.Main);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllSettingsCatalogContentAsync(GraphServiceClient graphServiceClient)
        {
            var policies = await GetAllSettingsCatalogPolicies(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.Name,
                    ContentType = "Settings Catalog",
                    ContentPlatform = HelperClass.TranslatePolicyPlatformName(policy.Platforms.ToString()),
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchSettingsCatalogContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var policies = await SearchForSettingsCatalog(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.Name,
                    ContentType = "Settings Catalog",
                    ContentPlatform = HelperClass.TranslatePolicyPlatformName(policy.Platforms.ToString()),
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Exports a settings catalog policy's full data as a JsonElement for JSON file export.
        /// Uses Kiota serialization to preserve OData type annotations and polymorphic settings.
        /// </summary>
        public static async Task<JsonElement?> ExportSettingsCatalogPolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyId].GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Expand = new[] { "settings" };
                });

                if (result == null)
                {
                    AppLogger.Warning($"Policy {policyId} not found for export.", appFunction.JsonExport);
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
                AppLogger.Error($"Error exporting settings catalog policy {policyId}: {ex.Message}", appFunction.JsonExport);
                return null;
            }
        }

        /// <summary>
        /// Imports a settings catalog policy from previously exported JSON data into the destination tenant.
        /// </summary>
        public static async Task<string?> ImportSettingsCatalogFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                // Deserialize the exported data back into a typed policy object
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exportedPolicy = parseNode.GetObjectValue(DeviceManagementConfigurationPolicy.CreateFromDiscriminatorValue);

                if (exportedPolicy == null)
                {
                    AppLogger.Error("Failed to deserialize policy data from JSON.", appFunction.Import);
                    return null;
                }

                // Create a clean policy object for import (exclude read-only properties like Id)
                var newPolicy = new DeviceManagementConfigurationPolicy
                {
                    Name = exportedPolicy.Name,
                    Description = exportedPolicy.Description,
                    Platforms = exportedPolicy.Platforms,
                    Technologies = exportedPolicy.Technologies,
                    RoleScopeTagIds = exportedPolicy.RoleScopeTagIds,
                    Settings = exportedPolicy.Settings,
                    Assignments = new List<DeviceManagementConfigurationPolicyAssignment>()
                };

                var imported = await graphServiceClient.DeviceManagement.ConfigurationPolicies.PostAsync(newPolicy);

                AppLogger.Info($"Imported settings catalog policy: {imported?.Name}", appFunction.Import);
                return imported?.Name;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error importing settings catalog policy from JSON: {ex.Message}", appFunction.Import);
                return null;
            }
        }

        /// <summary>
        /// Checks if a settings catalog policy has any group assignments.
        /// </summary>
        public static async Task<bool?> HasSettingsCatalogAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyId].Assignments.GetAsync(rc =>
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
        /// Gets detailed assignment information for a Settings Catalog policy.
        /// </summary>
        public static async Task<List<AssignmentInfo>?> GetSettingsCatalogAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var details = new List<AssignmentInfo>();
                var result = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyId].Assignments.GetAsync();

                while (result?.Value != null)
                {
                    foreach (var assignment in result.Value)
                    {
                        details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyId]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                return details;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error getting assignment details for Settings Catalog {policyId}: {ex.Message}", appFunction.ManageAssignment);
                return null;
            }
        }

        /// <summary>
        /// Removes all assignments from a Settings Catalog policy.
        /// </summary>
        public static async Task RemoveAllSettingsCatalogAssignmentsAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
            {
                Assignments = new List<DeviceManagementConfigurationPolicyAssignment>()
            };

            await graphServiceClient.DeviceManagement.ConfigurationPolicies[policyId].Assign.PostAsAssignPostResponseAsync(requestBody);
        }
    }
}
