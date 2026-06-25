using Microsoft.Graph;

namespace IntuneTools.Graph.EntraHelperClasses
{
    public class GroupHelperClass
    {
        public static async Task<List<Group>> GetAllGroups(GraphServiceClient graphServiceClient)
        {
            // This method gets all the groups in the tenant and returns them as a list of Group objects

            // Replace instances so any in-flight reader keeps its reference to the old dict
            groupNameAndID.Clear();
            groupIDAndName = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            try
            {
                var result = await graphServiceClient.Groups.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Count = true;
                    requestConfiguration.QueryParameters.Filter = "not(groupTypes/any(g:g eq 'Unified'))";
                    requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                });

                List<Group> groups = new List<Group>();

                // Iterate through the pages of results
                var pageIterator = PageIterator<Group, GroupCollectionResponse>.CreatePageIterator(graphServiceClient, result, (group) =>
                {
                    groups.Add(group);
                    return true;
                });
                // start the iteration
                await pageIterator.IterateAsync();

                // Add virtual groups for All Users and All Devices
                var allUsersGroup = new Group
                {
                    Id = allUsersVirtualGroupID,
                    DisplayName = "All Users",
                    Description = "Virtual group representing all licensed users",
                    OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget"
                };

                var allDevicesGroup = new Group
                {
                    Id = allDevicesVirtualGroupID,
                    DisplayName = "All Devices",
                    Description = "Virtual group representing all devices",
                    OdataType = "#microsoft.graph.allDevicesAssignmentTarget"
                };

                // Insert virtual groups at the beginning of the list for easier access
                groups.Insert(0, allUsersGroup);
                groups.Insert(1, allDevicesGroup);

                // Populate the groupNameAndID dictionary with group names and IDs
                foreach (var group in groups)
                {
                    if (!string.IsNullOrEmpty(group.DisplayName) && !string.IsNullOrEmpty(group.Id))
                    {
                        groupNameAndID[group.DisplayName] = group.Id;
                        groupIDAndName[group.Id] = group.DisplayName;
                    }
                }

                // return the list of groups
                return groups;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                // Log the error message
                AppLogger.Warning($"ODataError retrieving all groups: {me.Message}", appFunction.Main);
                return null;
            }
        }

        public static async Task<List<Group>> SearchForGroups(GraphServiceClient graphServiceClient, string searchQuery)
        {
            // This method searches for groups in the tenant based on a search query and returns the results as a list of Group objects

            // Replace instances so any in-flight reader keeps its reference to the old dict
            groupNameAndID.Clear();
            groupIDAndName = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            try
            {
                var result = await graphServiceClient.Groups.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Search = "\"displayName:" + searchQuery + "\"";
                    requestConfiguration.QueryParameters.Filter = "not(groupTypes/any(g:g eq 'Unified'))";
                    requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                });
                List<Group> groups = new List<Group>();

                // Iterate through the pages of results
                var pageIterator = PageIterator<Group, GroupCollectionResponse>.CreatePageIterator(graphServiceClient, result, (group) =>
                {
                    groups.Add(group);
                    return true;
                });
                // start the iteration
                await pageIterator.IterateAsync();

                // Populate the groupNameAndID dictionary with group names and IDs
                foreach (var group in groups)
                {
                    if (!string.IsNullOrEmpty(group.DisplayName) && !string.IsNullOrEmpty(group.Id))
                    {
                        groupNameAndID[group.DisplayName] = group.Id;
                        groupIDAndName[group.Id] = group.DisplayName;
                    }
                }

                // return the list of groups
                return groups;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                // Log the error message
                AppLogger.Warning($"ODataError searching for groups: {me.Message}", appFunction.Main);
                return null;
            }
        }

        public static async Task ImportMultipleGroups(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> groupIds)
        {
            // This method imports multiple groups from the source tenant to the destination tenant
            const string ItemType = "Group"; // Define item type for logging/messages

            // Basic null checks for arguments
            if (sourceGraphServiceClient == null || destinationGraphServiceClient == null || groupIds == null)
            {
                AppLogger.Info("ImportMultipleGroups called with null arguments.", appFunction.Import);
                return;
            }


            try
            {
                AppLogger.Info($"Importing {groupIds.Count} Security groups.", appFunction.Import);

                bool hasFailures = false;
                foreach (var groupId in groupIds)
                {
                    Group? sourceGroup = null;
                    var groupName = groupId;
                    try
                    {
                        // Get the group from the source tenant
                        // Select specific properties to potentially reduce payload size and avoid issues with read-only properties
                        sourceGroup = await sourceGraphServiceClient.Groups[groupId].GetAsync();

                        groupName = sourceGroup.DisplayName ?? "Unnamed Group"; // Use DisplayName or default to "Unnamed Group"

                        if (sourceGroup == null)
                        {
                            AppLogger.Info($"Skipping {ItemType} ID {groupId}: Not found in source tenant.", appFunction.Import);
                            continue;
                        }



                        // Optional: Check if a group with the same name already exists in the destination tenant
                        // Uncomment the following code if you want to check for existing groups by name
                        //var existingGroups = await destinationGraphServiceClient.Groups.GetAsync(q =>
                        //{
                        //    q.QueryParameters.Filter = $"displayName eq '{sourceGroup.DisplayName?.Replace("'", "''")}'"; // Handle potential apostrophes in name
                        //    q.QueryParameters.Select = new string[] { "id", "displayName" }; // Only need ID and name for check
                        //    q.Headers.Add("ConsistencyLevel", "eventual"); // Required for advanced filters like displayName
                        //    q.QueryParameters.Count = true; // Request count
                        //});

                        //if (existingGroups?.Value?.Count > 0)
                        //{
                        //    LogToImportStatusFile($"Skipping {ItemType} '{sourceGroup.DisplayName}' (ID: {groupId}): Name conflict in destination.");
                        //    continue;
                        //}


                        // Create the new group object based on the source
                        var newGroup = new Group
                        {
                            DisplayName = sourceGroup.DisplayName,
                            //Description = sourceGroup.Description ?? $"Imported from source group {sourceGroup.DisplayName}", // Provide default if null
                            MailEnabled = sourceGroup.MailEnabled ?? false, // Default to false if null
                            SecurityEnabled = sourceGroup.SecurityEnabled ?? true, // Default to true if null
                            MailNickname = $"group_{Guid.NewGuid().ToString().Substring(0, 8)}", // Needs a unique mail nickname
                            // Visibility = sourceGroup.Visibility, // Copy visibility if needed (e.g., for M365 groups, though we filtered them out earlier)
                            OdataType = "#microsoft.graph.group",
                            MembershipRuleProcessingState = sourceGroup.MembershipRuleProcessingState, // Copy if applicable

                        };

                        // Handle dynamic group properties
                        if (sourceGroup.GroupTypes != null && sourceGroup.GroupTypes.Contains("DynamicMembership"))
                        {
                            if (string.IsNullOrWhiteSpace(sourceGroup.MembershipRule))
                            {
                                AppLogger.Info($"Skipping Dynamic {ItemType} '{sourceGroup.DisplayName}' (ID: {groupId}): Missing membership rule.", appFunction.Import);
                                continue; // Cannot create dynamic group without a rule
                            }
                            newGroup.GroupTypes = new List<string> { "DynamicMembership" };
                            newGroup.MembershipRule = sourceGroup.MembershipRule;
                            // MembershipRuleProcessingState is read-only and set by the system
                        }
                        else
                        {
                            // Ensure assigned groups are explicitly marked if needed, though usually default
                            newGroup.GroupTypes = new List<string>(); // Ensure it's not dynamic if source wasn't
                        }


                        // Create the group in the destination tenant
                        var importedGroup = await destinationGraphServiceClient.Groups.PostAsync(newGroup);
                        AppLogger.Info($"Imported '{groupName}' successfully.", appFunction.Import);

                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to import '{groupName}': {ex.Message}", appFunction.Import);
                        hasFailures = true;
                    }
                }
                if (hasFailures)
                    throw new Exception("One or more groups failed to import. See Import.log for details.");
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                AppLogger.Info($"{DateTime.Now.ToString()} - Finished importing Security groups.", appFunction.Import);
            }
        }
        public static async Task DeleteSecurityGroup(GraphServiceClient graphServiceClient, string groupId)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (groupId == null)
                {
                    throw new InvalidOperationException("Group ID cannot be null.");
                }



                await graphServiceClient.Groups[groupId].DeleteAsync();
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError odataError)
            {
                if (string.Equals(odataError?.Error?.Message, "Insufficient privileges to complete the operation.", StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.Error("Insufficient privileges to delete the security group.", appFunction.Delete);
                    AppLogger.Warning("Please double check that the Microsoft Graph command line tools app has permissions to delete security groups.", appFunction.Delete);
                }
                else
                {
                    AppLogger.Error("An OData error occurred while deleting a security group. Check the permissions and try again.", appFunction.Delete);
                }
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static async Task RenameGroup(GraphServiceClient graphServiceClient, string groupID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (groupID == null)
                {
                    throw new InvalidOperationException("Group ID cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                if (selectedRenameMode == "Prefix")
                {
                    // Look up the existing group
                    var existingGroup = await graphServiceClient.Groups[groupID].GetAsync();

                    if (existingGroup == null)
                    {
                        throw new InvalidOperationException($"Group with ID '{groupID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingGroup.DisplayName ?? string.Empty, newName);

                    var group = new Group
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.Groups[groupID].PatchAsync(group);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing group
                    var existingGroup = await graphServiceClient.Groups[groupID].GetAsync();

                    if (existingGroup == null)
                    {
                        throw new InvalidOperationException($"Group with ID '{groupID}' not found.");
                    }

                    var group = new Group
                    {
                        Description = newName,
                    };

                    await graphServiceClient.Groups[groupID].PatchAsync(group);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingGroup = await graphServiceClient.Groups[groupID].GetAsync();

                    if (existingGroup == null)
                    {
                        throw new InvalidOperationException($"Group with ID '{groupID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingGroup.DisplayName);

                    var group = new Group
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.Groups[groupID].PatchAsync(group);
                }
                else if (selectedRenameMode == "RemoveDescription")
                {
                    var group = new Group
                    {
                        Description = string.Empty
                    };

                    await graphServiceClient.Groups[groupID].PatchAsync(group);
                    AppLogger.Info($"Cleared description for group {groupID}", appFunction.Main);
                }
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError odataError)
            {
                if (string.Equals(odataError?.Error?.Message, "Insufficient privileges to complete the operation.", StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.Error("Insufficient privileges to rename the group.", appFunction.Rename);
                    AppLogger.Warning("Please double check that the Microsoft Graph command line tools app has permissions to rename groups.", appFunction.Rename);
                }
                else
                {
                    AppLogger.Error("An OData error occurred while renaming the group. Check the permissions and try again.", appFunction.Rename);
                }
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllGroupContentAsync(GraphServiceClient graphServiceClient)
        {
            var groups = await GetAllGroups(graphServiceClient) ?? new List<Group>();
            var content = new List<CustomContentInfo>();

            foreach (var group in groups)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = group.DisplayName,
                    ContentType = "Entra Group",
                    ContentPlatform = "Entra group",
                    ContentId = group.Id,
                    ContentDescription = group.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchGroupContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var groups = await SearchForGroups(graphServiceClient, searchQuery) ?? new List<Group>();
            var content = new List<CustomContentInfo>();

            foreach (var group in groups)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = group.DisplayName,
                    ContentType = "Entra Group",
                    ContentPlatform = "Entra group",
                    ContentId = group.Id,
                    ContentDescription = group.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Resolves a list of group IDs to their display names.
        /// Checks the session cache first; falls back to individual Graph API calls for any misses.
        /// Falls back to the raw ID if resolution fails.
        /// </summary>
        public static async Task<Dictionary<string, string>> ResolveGroupNamesAsync(
            GraphServiceClient graphServiceClient,
            IEnumerable<string> groupIds)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var misses = new List<string>();

            foreach (var id in groupIds)
            {
                if (groupIDAndName.TryGetValue(id, out var cached))
                    result[id] = cached;
                else
                    misses.Add(id);
            }

            foreach (var id in misses)
            {
                try
                {
                    var group = await graphServiceClient.Groups[id].GetAsync(config =>
                        config.QueryParameters.Select = new[] { "displayName" });

                    if (group?.DisplayName != null)
                    {
                        result[id] = group.DisplayName;
                        groupIDAndName[id] = group.DisplayName;
                    }
                    else
                    {
                        result[id] = id; // fall back to ID without caching — allows retry next session
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"Failed to resolve group name for ID '{id}': {ex.Message}", appFunction.Main);
                    result[id] = id;
                }
            }

            return result;
        }
    }
}
