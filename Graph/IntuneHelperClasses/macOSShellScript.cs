using Microsoft.Graph;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class macOSShellScript
    {
        public static async Task<List<DeviceShellScript>> SearchForShellScriptmacOS(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                // Note: The Graph API for DeviceShellScript might not support filtering by name directly in the same way.
                // This might require fetching all and filtering locally, or adjusting the query if supported.
                // For now, let's assume a similar filter structure, but this might need adjustment.
                var result = await graphServiceClient.DeviceManagement.DeviceShellScripts.GetAsync((requestConfiguration) =>
                {
                    // Filter for macOS platform and name contains search query
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                    requestConfiguration.QueryParameters.Top = 1000; // Adjust as needed
                });

                List<DeviceShellScript> shellScripts = new List<DeviceShellScript>();
                var pageIterator = PageIterator<DeviceShellScript, DeviceShellScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    shellScripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                return shellScripts;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while searching for macOS shell scripts: {ex.Message}", appFunction.Main);
                return new List<DeviceShellScript>();
            }
        }

        public static async Task<List<DeviceShellScript>> GetAllmacOSShellScripts(GraphServiceClient graphServiceClient)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.DeviceShellScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000; // Adjust as needed
                });

                List<DeviceShellScript> shellScripts = new List<DeviceShellScript>();
                var pageIterator = PageIterator<DeviceShellScript, DeviceShellScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    shellScripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                return shellScripts;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while retrieving all macOS shell scripts: {ex.Message}", appFunction.Main);
                return new List<DeviceShellScript>();
            }
        }
        public static async Task ImportMultiplemacOSShellScripts(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scriptIDs, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                AppLogger.Info($"Importing {scriptIDs.Count} macOS shell scripts.", appFunction.Import);

                bool hasFailures = false;
                foreach (var scriptId in scriptIDs)
                {
                    DeviceShellScript? sourceScript = null;
                    try
                    {
                        // Get the full script object, including script content
                        sourceScript = await sourceGraphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].GetAsync();


                        if (sourceScript == null)
                        {
                            AppLogger.Info($"Script with ID {scriptId} not found in source tenant. Skipping.", appFunction.Import);
                            continue;
                        }

                        var newScript = new DeviceShellScript
                        {

                        };

                        foreach (var property in sourceScript.GetType().GetProperties())
                        {
                            var value = property.GetValue(sourceScript);
                            if (value != null && property.CanWrite)
                            {
                                property.SetValue(newScript, value);
                            }
                        }

                        newScript.Id = "";

                        var importResult = await destinationGraphServiceClient.DeviceManagement.DeviceShellScripts.PostAsync(newScript);

                        if (importResult != null)
                        {
                            AppLogger.Info($"Imported '{importResult.DisplayName}' successfully.", appFunction.Import);

                            if (assignments && groups != null && groups.Any())
                            {
                                // Shell script assignments use a different structure
                                await AssignGroupsToSingleShellScriptmacOS(importResult.Id, importResult.DisplayName ?? string.Empty, groups, destinationGraphServiceClient);
                            }
                        }
                        else
                        {
                            AppLogger.Error($"Failed to import '{sourceScript.DisplayName}': import returned null.", appFunction.Import);
                            hasFailures = true;
                        }

                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to import '{sourceScript?.DisplayName ?? scriptId}': {ex.Message}", appFunction.Import);
                        hasFailures = true;
                    }
                }
                if (hasFailures)
                    throw new Exception("One or more macOS shell scripts failed to import. See Import.log for details.");
            }
            catch (Exception)
            {
                throw;
            }
        }


        // Note: Assignment structure for Shell Scripts is different from Configuration Policies
        public static async Task AssignGroupsToSingleShellScriptmacOS(string scriptId, string contentName, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (string.IsNullOrEmpty(scriptId))
                {
                    throw new ArgumentNullException(nameof(scriptId));
                }

                if (groupIDs == null)
                {
                    throw new ArgumentNullException(nameof(groupIDs));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                var assignments = new List<DeviceManagementScriptGroupAssignment>();
                var seenGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hasAllUsers = false;
                var hasAllDevices = false;

                AppLogger.Info($"Assigning {groupIDs.Count} groups to macOS shell script {scriptId}.", appFunction.Assignment);

                // Step 1: Add new assignments to request body
                foreach (var groupId in groupIDs)
                {
                    if (string.IsNullOrWhiteSpace(groupId) || !seenGroupIds.Add(groupId))
                    {
                        continue;
                    }

                    // Check if this is a virtual group (All Users or All Devices)
                    if (groupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllUsers = true;
                        assignments.Add(new DeviceManagementScriptGroupAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                            TargetGroupId = allUsersVirtualGroupID
                        });
                    }
                    else if (groupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllDevices = true;
                        assignments.Add(new DeviceManagementScriptGroupAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                            TargetGroupId = allDevicesVirtualGroupID
                        });
                    }
                    else
                    {
                        // Regular group assignment
                        assignments.Add(new DeviceManagementScriptGroupAssignment
                        {
                            OdataType = "#microsoft.graph.deviceManagementScriptGroupAssignment",
                            TargetGroupId = groupId
                        });
                    }
                }

                // Step 2: Check for existing assignments and add only if not already present
                var existingAssignments = await destinationGraphServiceClient
                    .DeviceManagement
                    .DeviceShellScripts[scriptId]
                    .GroupAssignments
                    .GetAsync();

                if (existingAssignments?.Value != null)
                {
                    foreach (var existing in existingAssignments.Value)
                    {
                        var existingGroupId = existing.TargetGroupId;

                        if (string.IsNullOrWhiteSpace(existingGroupId))
                        {
                            continue;
                        }

                        // Check if this is a virtual group
                        if (existingGroupId.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip if we're already adding All Users
                            if (!hasAllUsers)
                            {
                                assignments.Add(existing);
                            }
                        }
                        else if (existingGroupId.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip if we're already adding All Devices
                            if (!hasAllDevices)
                            {
                                assignments.Add(existing);
                            }
                        }
                        else
                        {
                            // Only add if not already in the new assignments
                            if (seenGroupIds.Add(existingGroupId))
                            {
                                assignments.Add(existing);
                            }
                        }
                    }
                }

                if (!assignments.Any())
                {
                    AppLogger.Info($"No valid group assignments to process for script {scriptId}.", appFunction.Assignment);
                    return;
                }

                // Step 3: Update the script with the assignments
                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceShellScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceManagementScriptGroupAssignments = assignments
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].Assign.PostAsync(requestBody);
                    UpdateTotalTimeSaved(assignments.Count * secondsSavedOnAssignments, appFunction.Assignment);

                    // Note: Filters are not directly supported in the Assign action for shell scripts
                    // They would need to be applied via a separate PATCH operation if supported
                    if (!string.IsNullOrEmpty(SelectedFilterID))
                    {
                        AppLogger.Info($"Filter application requested for script {scriptId}, but direct filter assignment via Assign action is not supported for shell scripts. Manual verification/update might be needed.", appFunction.Assignment);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"An error occurred while assigning groups to macOS shell script: {ex.Message}", appFunction.Assignment);
                    throw;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static async Task DeleteMacosShellScript(GraphServiceClient graphServiceClient, string profileID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (profileID == null)
                {
                    throw new InvalidOperationException("Profile ID cannot be null.");
                }
                await graphServiceClient.DeviceManagement.DeviceShellScripts[profileID].DeleteAsync();
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static async Task RenameMacOSShellScript(GraphServiceClient graphServiceClient, string scriptID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (scriptID == null)
                {
                    throw new InvalidOperationException("Script ID cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                if (selectedRenameMode == "Prefix")
                {
                    // Look up the existing script
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingScript.DisplayName ?? string.Empty, newName);

                    // Create an instance of the specific script type using reflection
                    var scriptType = existingScript.GetType();
                    var script = (DeviceShellScript?)Activator.CreateInstance(scriptType);

                    if (script == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {scriptType.Name}");
                    }

                    // Set the DisplayName on the new instance
                    script.DisplayName = name;

                    await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].PatchAsync(script);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing script
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    // Create an instance of the specific script type using reflection
                    var scriptType = existingScript.GetType();
                    var script = (DeviceShellScript?)Activator.CreateInstance(scriptType);

                    if (script == null)
                    {
                        throw new InvalidOperationException($"Failed to create instance of type {scriptType.Name}");
                    }

                    script.Description = newName;

                    await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].PatchAsync(script);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingScript = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].GetAsync();

                    if (existingScript == null)
                    {
                        throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingScript.DisplayName);

                    var script = new DeviceShellScript
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptID].PatchAsync(script);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllMacOSShellScriptContentAsync(GraphServiceClient graphServiceClient)
        {
            var scripts = await GetAllmacOSShellScripts(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var script in scripts)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "MacOS Shell Script",
                    ContentPlatform = "macOS",
                    ContentId = script.Id,
                    ContentDescription = script.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchMacOSShellScriptContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var scripts = await SearchForShellScriptmacOS(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var script in scripts)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "MacOS Shell Script",
                    ContentPlatform = "macOS",
                    ContentId = script.Id,
                    ContentDescription = script.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Exports a macOS shell script's full data as a JsonElement for JSON file export.
        /// </summary>
        public static async Task<JsonElement?> ExportMacOSShellScriptDataAsync(GraphServiceClient graphServiceClient, string scriptId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].GetAsync();

                if (result == null)
                {
                    AppLogger.Warning($"macOS shell script {scriptId} not found for export.", appFunction.JsonExport);
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
                AppLogger.Error($"Error exporting macOS shell script {scriptId}: {ex.Message}", appFunction.JsonExport);
                return null;
            }
        }

        /// <summary>
        /// Imports a macOS shell script from previously exported JSON data into the destination tenant.
        /// </summary>
        public static async Task<string?> ImportMacOSShellScriptFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exported = parseNode.GetObjectValue(DeviceShellScript.CreateFromDiscriminatorValue);

                if (exported == null)
                {
                    AppLogger.Error("Failed to deserialize macOS shell script data from JSON.", appFunction.Import);
                    return null;
                }

                var newScript = new DeviceShellScript();

                foreach (var property in exported.GetType().GetProperties())
                {
                    var value = property.GetValue(exported);
                    if (value != null && property.CanWrite)
                    {
                        property.SetValue(newScript, value);
                    }
                }

                newScript.Id = "";

                var imported = await graphServiceClient.DeviceManagement.DeviceShellScripts.PostAsync(newScript);

                AppLogger.Info($"Imported macOS shell script: {imported?.DisplayName}", appFunction.Import);
                return imported?.DisplayName;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error importing macOS shell script from JSON: {ex.Message}", appFunction.Import);
                return null;
            }
        }

        /// <summary>
        /// Checks if a macOS shell script has any group assignments.
        /// </summary>
        public static async Task<bool?> HasMacOSShellScriptAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].Assignments.GetAsync(rc =>
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
        /// Gets detailed assignment information for a macOS Shell Script.
        /// </summary>
        public static async Task<List<AssignmentInfo>?> GetMacOSShellScriptAssignmentDetailsAsync(GraphServiceClient graphServiceClient, string scriptId)
        {
            try
            {
                var details = new List<AssignmentInfo>();
                var result = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].Assignments.GetAsync();

                while (result?.Value != null)
                {
                    foreach (var assignment in result.Value)
                    {
                        details.Add(AssignmentInfo.FromTarget(assignment.Id, assignment.Target));
                    }

                    if (string.IsNullOrEmpty(result.OdataNextLink)) break;

                    result = await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptId]
                        .Assignments.WithUrl(result.OdataNextLink).GetAsync();
                }

                return details;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error getting assignment details for macOS Shell Script {scriptId}: {ex.Message}", appFunction.ManageAssignment);
                return null;
            }
        }

        /// <summary>
        /// Removes all assignments from a macOS Shell Script.
        /// </summary>
        public static async Task RemoveAllMacOSShellScriptAssignmentsAsync(GraphServiceClient graphServiceClient, string scriptId)
        {
            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceShellScripts.Item.Assign.AssignPostRequestBody
            {
                DeviceManagementScriptGroupAssignments = new List<DeviceManagementScriptGroupAssignment>()
            };

            await graphServiceClient.DeviceManagement.DeviceShellScripts[scriptId].Assign.PostAsync(requestBody);
            AppLogger.Info($"Removed all assignments from macOS Shell Script {scriptId}.", appFunction.ManageAssignment);
        }
    }
}
