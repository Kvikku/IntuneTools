using IntuneTools.Utilities;
using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class FilterHelperClass
    {
        private class Helper : GraphHelper<DeviceAndAppManagementAssignmentFilter, DeviceAndAppManagementAssignmentFilterCollectionResponse>
        {
            protected override string ResourceName => "assignment filters";
            protected override string ContentTypeName => "Assignment Filter";

            protected override string? GetPolicyPlatform(DeviceAndAppManagementAssignmentFilter policy)
                => HelperClass.TranslatePolicyPlatformName(policy.Platform?.ToString() ?? string.Empty);

            protected override string? GetPolicyName(DeviceAndAppManagementAssignmentFilter policy) => policy.DisplayName;
            protected override string? GetPolicyId(DeviceAndAppManagementAssignmentFilter policy) => policy.Id;
            protected override string? GetPolicyDescription(DeviceAndAppManagementAssignmentFilter policy) => policy.Description;

            protected override Task<DeviceAndAppManagementAssignmentFilterCollectionResponse?> GetCollectionAsync(GraphServiceClient client)
                => client.DeviceManagement.AssignmentFilters.GetAsync();

            protected override Task<DeviceAndAppManagementAssignmentFilterCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery)
                => client.DeviceManagement.AssignmentFilters.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                    rc.QueryParameters.Top = 999;
                });

            protected override Task<DeviceAndAppManagementAssignmentFilter?> GetByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.AssignmentFilters[id].GetAsync();

            protected override Task DeleteByIdAsync(GraphServiceClient client, string id)
                => client.DeviceManagement.AssignmentFilters[id].DeleteAsync();

            protected override async Task PatchNameAsync(GraphServiceClient client, string id, string newName)
            {
                var filter = new DeviceAndAppManagementAssignmentFilter { DisplayName = newName };
                await client.DeviceManagement.AssignmentFilters[id].PatchAsync(filter);
            }

            protected override async Task PatchDescriptionAsync(GraphServiceClient client, string id, string description)
            {
                var filter = new DeviceAndAppManagementAssignmentFilter { Description = description };
                await client.DeviceManagement.AssignmentFilters[id].PatchAsync(filter);
            }

            /// <summary>
            /// Searches with server-side filter, falling back to client-side filtering on ODataError 400.
            /// </summary>
            public override async Task<List<DeviceAndAppManagementAssignmentFilter>> SearchAsync(GraphServiceClient client, string searchQuery)
            {
                try
                {
                    return await base.SearchAsync(client, searchQuery);
                }
                catch (ODataError odataError) when (odataError.ResponseStatusCode == 400)
                {
                    LogToFunctionFile(appFunction.Main, $"Server-side filtering not supported for {ResourceName}. Falling back to client-side filtering. Error: {odataError.Error?.Message}", LogLevels.Warning);
                    var allFilters = await GetAllAsync(client);
                    return allFilters.Where(f => f.DisplayName != null && f.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            public override async Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData)
            {
                try
                {
                    var exported = GraphImportHelper.DeserializeFromJson(policyData, DeviceAndAppManagementAssignmentFilter.CreateFromDiscriminatorValue);

                    if (exported == null)
                    {
                        LogToFunctionFile(appFunction.Main, "Failed to deserialize assignment filter data from JSON.", LogLevels.Error);
                        return null;
                    }

                    var newFilter = new DeviceAndAppManagementAssignmentFilter
                    {
                        OdataType = "#microsoft.graph.deviceAndAppManagementAssignmentFilter",
                        DisplayName = exported.DisplayName,
                        Description = exported.Description,
                        Platform = exported.Platform,
                        Rule = exported.Rule,
                    };

                    var imported = await client.DeviceManagement.AssignmentFilters.PostAsync(newFilter);

                    LogToFunctionFile(appFunction.Main, $"Imported assignment filter: {imported?.DisplayName}");
                    return imported?.DisplayName;
                }
                catch (Exception ex)
                {
                    GraphErrorHandler.HandleException(ex, "importing from JSON", ResourceName);
                    return null;
                }
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
                    var filterName = string.Empty;
                    try
                    {
                        var sourceFilter = await sourceClient.DeviceManagement.AssignmentFilters[id].GetAsync();

                        if (sourceFilter == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping filter ID {id}: Not found in source tenant.");
                            return;
                        }

                        filterName = sourceFilter.DisplayName ?? "Unnamed Filter";

                        var newFilter = new DeviceAndAppManagementAssignmentFilter
                        {
                            OdataType = "#microsoft.graph.deviceAndAppManagementAssignmentFilter",
                            DisplayName = sourceFilter.DisplayName,
                            Description = sourceFilter.Description,
                            Platform = sourceFilter.Platform,
                            Rule = sourceFilter.Rule,
                        };

                        var importedFilter = await destinationClient.DeviceManagement.AssignmentFilters.PostAsync(newFilter);

                        LogToFunctionFile(appFunction.Main, $"Successfully imported {importedFilter.DisplayName}");
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import {filterName}: {ex.Message}", LogLevels.Error);
                    }
                });
            }

            /// <summary>
            /// Populates the filterNameAndID dictionary after retrieving all filters.
            /// </summary>
            public async Task<List<DeviceAndAppManagementAssignmentFilter>> GetAllWithDictionaryAsync(GraphServiceClient client)
            {
                var filters = await GetAllAsync(client);

                foreach (var filter in filters)
                {
                    if (filter.DisplayName != null && !filterNameAndID.ContainsKey(filter.DisplayName))
                    {
                        filterNameAndID.Add(filter.DisplayName, filter.Id);
                    }
                }

                return filters;
            }
        }

        private static readonly Helper _helper = new();

        // ── Public static methods (signatures preserved for existing consumers) ──

        public static Task<List<DeviceAndAppManagementAssignmentFilter>> SearchForAssignmentFilters(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchAsync(graphServiceClient, searchQuery);

        public static Task<List<DeviceAndAppManagementAssignmentFilter>> GetAllAssignmentFilters(GraphServiceClient graphServiceClient)
            => _helper.GetAllWithDictionaryAsync(graphServiceClient);

        public static Task ImportMultipleAssignmentFilters(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> filterIds)
            => _helper.ImportMultipleAsync(sourceGraphServiceClient, destinationGraphServiceClient, filterIds, false, false, new List<string>());

        public static async Task<bool> DeleteAssignmentFilter(GraphServiceClient graphServiceClient, string filterID)
        {
            try
            {
                await _helper.DeleteAsync(graphServiceClient, filterID);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static Task RenameAssignmentFilter(GraphServiceClient graphServiceClient, string filterID, string newName)
            => _helper.RenameAsync(graphServiceClient, filterID, newName);

        public static Task<List<CustomContentInfo>> GetAllAssignmentFilterContentAsync(GraphServiceClient graphServiceClient)
            => _helper.GetAllContentAsync(graphServiceClient);

        public static Task<List<CustomContentInfo>> SearchAssignmentFilterContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
            => _helper.SearchContentAsync(graphServiceClient, searchQuery);

        public static Task<JsonElement?> ExportAssignmentFilterDataAsync(GraphServiceClient graphServiceClient, string filterId)
            => _helper.ExportDataAsync(graphServiceClient, filterId);

        public static Task<string?> ImportAssignmentFilterFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
            => _helper.ImportFromJsonDataAsync(graphServiceClient, policyData);
    }
}
