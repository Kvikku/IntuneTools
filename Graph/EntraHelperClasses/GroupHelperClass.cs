using IntuneTools.Utilities;
using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.EntraHelperClasses
{
    public class GroupHelperClass
    {
        private class Helper : GraphHelper<Group, GroupCollectionResponse>
        {
            protected override string ResourceName => "security groups";
            protected override string ContentTypeName => "Entra Group";
            protected override string? FixedPlatform => "Entra group";

            protected override string? GetPolicyName(Group policy) => policy.DisplayName;
            protected override string? GetPolicyId(Group policy) => policy.Id;
            protected override string? GetPolicyDescription(Group policy) => policy.Description;

            protected override Task<GroupCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.Groups.GetAsync(rc =>
                {
                    rc.QueryParameters.Count = true;
                    rc.QueryParameters.Filter = "not(groupTypes/any(g:g eq 'Unified'))";
                    rc.Headers.Add("ConsistencyLevel", "eventual");
                });

            protected override Task<GroupCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.Groups.GetAsync(rc =>
                {
                    rc.QueryParameters.Search = "\"displayName:" + searchQuery + "\"";
                    rc.QueryParameters.Filter = "not(groupTypes/any(g:g eq 'Unified'))";
                    rc.Headers.Add("ConsistencyLevel", "eventual");
                });

            protected override Task<Group?> GetByIdAsync(GraphServiceClient client, string id)
                => client.Groups[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.Groups[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var group = new Group { DisplayName = newName };
                await client.Groups[id].PatchAsync(group);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var group = new Group { Description = description };
                await client.Groups[id].PatchAsync(group);
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                // Groups are imported via ImportMultipleAsync; JSON import is not used for groups
                return null;
            }

            /// <summary>
            /// Gets all groups and injects virtual groups (All Users, All Devices) at the beginning.
            /// Populates the groupNameAndID dictionary.
            /// </summary>
            public async Task<List<Group>> GetAllWithVirtualGroupsAsync(GraphServiceClient client)
            {
                groupNameAndID.Clear();

                var groups = await GetAllAsync(client);

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

                groups.Insert(0, allUsersGroup);
                groups.Insert(1, allDevicesGroup);

                LogToFunctionFile(appFunction.Main, $"Added virtual groups. Total groups: {groups.Count}");

                // Populate the groupNameAndID dictionary
                foreach (var group in groups)
                {
                    if (!string.IsNullOrEmpty(group.DisplayName) && !string.IsNullOrEmpty(group.Id))
                    {
                        groupNameAndID[group.DisplayName] = group.Id;
                    }
                }

                return groups;
            }

            /// <summary>
            /// Searches for groups and populates the groupNameAndID dictionary.
            /// </summary>
            public async Task<List<Group>> SearchWithDictionaryAsync(GraphServiceClient client, string searchQuery)
            {
                groupNameAndID.Clear();

                var groups = await SearchAsync(client, searchQuery);

                foreach (var group in groups)
                {
                    if (!string.IsNullOrEmpty(group.DisplayName) && !string.IsNullOrEmpty(group.Id))
                    {
                        groupNameAndID[group.DisplayName] = group.Id;
                    }
                }

                return groups;
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
                    var groupName = string.Empty;
                    try
                    {
                        var sourceGroup = await sourceClient.Groups[id].GetAsync();

                        if (sourceGroup == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping group ID {id}: Not found in source tenant.");
                            return;
                        }

                        groupName = sourceGroup.DisplayName ?? "Unnamed Group";

                        var newGroup = new Group
                        {
                            DisplayName = sourceGroup.DisplayName,
                            MailEnabled = sourceGroup.MailEnabled ?? false,
                            SecurityEnabled = sourceGroup.SecurityEnabled ?? true,
                            MailNickname = $"group_{Guid.NewGuid().ToString().Substring(0, 8)}",
                            OdataType = "#microsoft.graph.group",
                            MembershipRuleProcessingState = sourceGroup.MembershipRuleProcessingState,
                        };

                        // Handle dynamic group properties
                        if (sourceGroup.GroupTypes != null && sourceGroup.GroupTypes.Contains("DynamicMembership"))
                        {
                            if (string.IsNullOrWhiteSpace(sourceGroup.MembershipRule))
                            {
                                LogToFunctionFile(appFunction.Main, $"Skipping Dynamic group '{sourceGroup.DisplayName}' (ID: {id}): Missing membership rule.");
                                return;
                            }
                            newGroup.GroupTypes = new List<string> { "DynamicMembership" };
                            newGroup.MembershipRule = sourceGroup.MembershipRule;
                        }
                        else
                        {
                            newGroup.GroupTypes = new List<string>();
                        }

                        await destinationClient.Groups.PostAsync(newGroup);
                        LogToFunctionFile(appFunction.Main, $"Successfully imported {groupName}");
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import {groupName}: {ex.Message}", LogLevels.Error);
                    }
                });
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<Group>> GetAllGroups(GraphServiceClient graphServiceClient)
            => _helper.GetAllWithVirtualGroupsAsync(graphServiceClient);

        public static Task<List<Group>> SearchForGroups(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchWithDictionaryAsync(graphServiceClient, searchQuery);

        public static Task ImportMultipleGroups(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> groupIds)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, groupIds, false, false, new List<string>());

        public static async Task DeleteSecurityGroup(GraphServiceClient graphServiceClient, string groupId)
        {
            try
            {
                await _helper.DeleteAsync(graphServiceClient, groupId);
            }
            catch (ODataError odataError)
            {
                if (string.Equals(odataError?.Error?.Message, "Insufficient privileges to complete the operation.", StringComparison.OrdinalIgnoreCase))
                {
                    LogToFunctionFile(appFunction.Main, "Insufficient privileges to delete the security group.", LogLevels.Error);
                    LogToFunctionFile(appFunction.Main, "Please double check that the Microsoft Graph command line tools app has permissions to delete security groups.", LogLevels.Warning);
                }
                else
                {
                    LogToFunctionFile(appFunction.Main, "An OData error occurred while deleting a security group. Check the permissions and try again.", LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while deleting a security group: {ex.Message}", LogLevels.Error);
            }
        }

        public static Task RenameGroup(GraphServiceClient graphServiceClient, string groupID, string newName)
            => _helper.RenameAsync(graphServiceClient, groupID, newName);

        public static Task<List<CustomContentInfo>> GetAllGroupContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchGroupContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);
    }
}
