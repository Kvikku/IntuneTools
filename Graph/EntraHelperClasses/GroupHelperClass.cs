using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph.EntraHelperClasses
{
    public class GroupHelperClass
    {
        public static async Task<List<Group>> GetAllGroups(GraphServiceClient graphServiceClient)
        {
            // This method gets all the groups in the tenant and returns them as a list of Group objects

            // clear the dictionary
            groupNameAndID.Clear();

            try
            {
                LogToImportStatusFile("Getting all groups in the tenant");
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
                LogToImportStatusFile($"Found {groups.Count} groups in the tenant");

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

                LogToImportStatusFile($"Added virtual groups. Total groups: {groups.Count}");

                // Populate the groupNameAndID dictionary with group names and IDs
                foreach (var group in groups)
                {
                    if (!string.IsNullOrEmpty(group.DisplayName) && !string.IsNullOrEmpty(group.Id))
                    {
                        groupNameAndID[group.DisplayName] = group.Id;
                    }
                }

                // return the list of groups
                return groups;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                // Log the error message
                LogToImportStatusFile($"ODataError: {me.Message}");
                return null;
            }
        }

        public static async Task<List<Group>> SearchForGroups(GraphServiceClient graphServiceClient, string searchQuery)
        {
            // This method searches for groups in the tenant based on a search query and returns the results as a list of Group objects

            // clear the dictionary
            groupNameAndID.Clear();

            try
            {
                LogToImportStatusFile("Searching for groups. Search query: " + searchQuery);

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
                LogToImportStatusFile($"Found {groups.Count} groups in the tenant");

                // Populate the groupNameAndID dictionary with group names and IDs
                foreach (var group in groups)
                {
                    if (!string.IsNullOrEmpty(group.DisplayName) && !string.IsNullOrEmpty(group.Id))
                    {
                        groupNameAndID[group.DisplayName] = group.Id;
                    }
                }

                // return the list of groups
                return groups;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                // Log the error message
                LogToImportStatusFile($"ODataError: {me.Message}");
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
                LogToImportStatusFile("ImportMultipleGroups called with null arguments.");
                return;
            }


            try
            {
                Console.WriteLine($"{DateTime.Now.ToString()} - Importing {groupIds.Count} Security groups.\n");
                LogToFunctionFile(appFunction.Main, " ");
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Importing {groupIds.Count} Security groups.");
                LogToFunctionFile(appFunction.Main, " ");
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Importing {groupIds.Count} Security groups.");


                foreach (var groupId in groupIds)
                {
                    Group? sourceGroup = null;
                    var groupName = ""; // Initialize group name for logging
                    try
                    {
                        // Get the group from the source tenant
                        // Select specific properties to potentially reduce payload size and avoid issues with read-only properties
                        sourceGroup = await sourceGraphServiceClient.Groups[groupId].GetAsync();

                        groupName = sourceGroup.DisplayName ?? "Unnamed Group"; // Use DisplayName or default to "Unnamed Group"

                        if (sourceGroup == null)
                        {
                            LogToImportStatusFile($"Skipping {ItemType} ID {groupId}: Not found in source tenant.");
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
                                LogToImportStatusFile($"Skipping Dynamic {ItemType} '{sourceGroup.DisplayName}' (ID: {groupId}): Missing membership rule.");
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
                        LogToImportStatusFile($"Successfully imported {groupName}");

                    }
                    catch (Exception ex)
                    {
                        LogToImportStatusFile($"Failed to import {groupName}\n", LogLevels.Error);
                        LogToFunctionFile(appFunction.Main, $"Failed to import {groupName}: {ex.Message}", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToImportStatusFile($"An unexpected error occurred during the import process: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                LogToImportStatusFile($"{DateTime.Now.ToString()} - Finished importing Security groups.");
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
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while deleting a security group", LogLevels.Error);
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
                    LogToFunctionFile(appFunction.Main, $"Successfully renamed group {groupID} to '{name}'");
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
                    LogToFunctionFile(appFunction.Main, $"Updated description for group {groupID} to '{newName}'");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming group", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }
    }
}
