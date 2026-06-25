using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class FilterHelperClass
    {

        private const string PolicyType = "Assignment Filter";

        public static async Task<List<DeviceAndAppManagementAssignmentFilter>> SearchForAssignmentFilters(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                // Assignment filters don't have a direct filter on DisplayName in the same way,
                // so we get all and filter locally, or adjust if a specific filter query is needed.
                // For simplicity, getting all and filtering locally for now.
                // A server-side filter might look like: $"contains(displayName,'{searchQuery}')" if supported.
                // Let's try the filter first.
                var result = await graphServiceClient.DeviceManagement.AssignmentFilters.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                    requestConfiguration.QueryParameters.Top = 999; // Ensure we get enough results if filtering client-side later
                });


                if (result == null || result.Value == null)
                    return new List<DeviceAndAppManagementAssignmentFilter>();

                List<DeviceAndAppManagementAssignmentFilter> assignmentFilters = new List<DeviceAndAppManagementAssignmentFilter>();
                var pageIterator = PageIterator<DeviceAndAppManagementAssignmentFilter, DeviceAndAppManagementAssignmentFilterCollectionResponse>.CreatePageIterator(graphServiceClient, result, (filter) =>
                {
                    assignmentFilters.Add(filter);
                    return true; // Continue iterating
                });
                await pageIterator.IterateAsync();


                // If server-side filter doesn't work as expected, filter client-side:
                // assignmentFilters = assignmentFilters.Where(f => f.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();


                return assignmentFilters;
            }
            catch (ODataError odataError) when (odataError.ResponseStatusCode == 400) // Handle potential filter query issues
            {
                AppLogger.Error($"Server-side filtering might not be supported or the query is invalid for {PolicyType}. Trying client-side filtering. Error: {odataError.Error?.Message}", appFunction.Main);
                // Fallback: Get all and filter client-side
                var allFilters = await GetAllAssignmentFilters(graphServiceClient);
                return allFilters.Where(f => f.DisplayName != null && f.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"An error occurred while searching for {PolicyType} policies: {ex.Message}", appFunction.Main);
                return new List<DeviceAndAppManagementAssignmentFilter>();
            }
        }

        public static async Task<List<DeviceAndAppManagementAssignmentFilter>> GetAllAssignmentFilters(GraphServiceClient graphServiceClient)
        {
            // Method to get the assignment filters for a policy
            // Create a new instance of the GraphServiceClient class

            // Create a list to store the assignment filters in
            List<DeviceAndAppManagementAssignmentFilter> assignmentFilters = new List<DeviceAndAppManagementAssignmentFilter>();

            try
            {
                // Look up the assignment filters
                var result = await graphServiceClient.DeviceManagement.AssignmentFilters.GetAsync();

                if (result != null && result.Value != null)
                {
                    // Create a page iterator
                    var pageIterator = PageIterator<DeviceAndAppManagementAssignmentFilter, DeviceAndAppManagementAssignmentFilterCollectionResponse>.CreatePageIterator(
                        graphServiceClient,
                        result,
                        (filter) =>
                        {
                            assignmentFilters.Add(filter);
                            return true;
                        });

                    // Iterate through the pages
                    await pageIterator.IterateAsync();
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log the error)
                AppLogger.Error($"An error occurred while getting assignment filters: {ex.Message}", appFunction.Main);
            }

            // Add filter name and ID to the dictionary
            foreach (var filter in assignmentFilters)
            {
                if (filter.DisplayName != null && !filterNameAndID.ContainsKey(filter.DisplayName))
                {
                    filterNameAndID.Add(filter.DisplayName, filter.Id);
                }
            }

            return assignmentFilters;
        }

        public static async Task ImportMultipleAssignmentFilters(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> filterIds)
        {
            try
            {
                AppLogger.Info($"Importing {filterIds.Count} Assignment filters.", appFunction.Import);

                bool hasFailures = false;
                foreach (var filterId in filterIds)
                {
                    DeviceAndAppManagementAssignmentFilter? sourceFilter = null;
                    var filterName = filterId;
                    try
                    {
                        sourceFilter = await sourceGraphServiceClient.DeviceManagement.AssignmentFilters[filterId].GetAsync();

                        if (sourceFilter == null)
                        {
                            AppLogger.Info($"Skipping filter ID {filterId}: Not found in source tenant.", appFunction.Import);
                            continue;
                        }

                        filterName = sourceFilter.DisplayName ?? "Unnamed Filter";

                        // Create the new filter object based on the source
                        var newFilter = new DeviceAndAppManagementAssignmentFilter
                        {
                        };


                        // Copy the display name and description
                        newFilter.DisplayName = sourceFilter.DisplayName;
                        newFilter.Description = sourceFilter.Description;
                        newFilter.Platform = sourceFilter.Platform; // Assuming Platform is a property of the filter
                        newFilter.Rule = sourceFilter.Rule; // Assuming Rule is a property of the filter
                        newFilter.OdataType = "#microsoft.graph.deviceAndAppManagementAssignmentFilter";



                        var importedFilter = await destinationGraphServiceClient.DeviceManagement.AssignmentFilters.PostAsync(newFilter);

                        AppLogger.Info($"Imported '{importedFilter.DisplayName}' successfully.", appFunction.Import);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to import '{filterName}': {ex.Message}", appFunction.Import);
                        hasFailures = true;
                    }
                }
                if (hasFailures)
                    throw new Exception("One or more assignment filters failed to import. See Import.log for details.");
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static async Task<bool> DeleteAssignmentFilter(GraphServiceClient graphServiceClient, string filterID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (filterID == null)
                {
                    throw new InvalidOperationException("Filter ID cannot be null.");
                }

                var result = await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].GetAsync();

                await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].DeleteAsync();
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static async Task RenameAssignmentFilter(GraphServiceClient graphServiceClient, string filterID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (filterID == null)
                {
                    throw new InvalidOperationException("Filter ID cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                if (selectedRenameMode == "Prefix")
                {
                    // Look up the existing filter
                    var existingFilter = await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].GetAsync();

                    if (existingFilter == null)
                    {
                        throw new InvalidOperationException($"Filter with ID '{filterID}' not found.");
                    }

                    var name = FindPreFixInPolicyName(existingFilter.DisplayName ?? string.Empty, newName);

                    var filter = new DeviceAndAppManagementAssignmentFilter
                    {
                        DisplayName = name,
                    };

                    await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].PatchAsync(filter);
                }
                else if (selectedRenameMode == "Suffix")
                {

                }
                else if (selectedRenameMode == "Description")
                {
                    // Look up the existing filter
                    var existingFilter = await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].GetAsync();

                    if (existingFilter == null)
                    {
                        throw new InvalidOperationException($"Filter with ID '{filterID}' not found.");
                    }

                    var filter = new DeviceAndAppManagementAssignmentFilter
                    {
                        Description = newName,
                    };

                    await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].PatchAsync(filter);
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existingFilter = await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].GetAsync();

                    if (existingFilter == null)
                    {
                        throw new InvalidOperationException($"Filter with ID '{filterID}' not found.");
                    }

                    var name = RemovePrefixFromPolicyName(existingFilter.DisplayName);

                    var filter = new DeviceAndAppManagementAssignmentFilter
                    {
                        DisplayName = name
                    };

                    await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].PatchAsync(filter);
                }
                else if (selectedRenameMode == "RemoveDescription")
                {
                    var filter = new DeviceAndAppManagementAssignmentFilter
                    {
                        Description = string.Empty
                    };

                    await graphServiceClient.DeviceManagement.AssignmentFilters[filterID].PatchAsync(filter);
                    AppLogger.Info($"Cleared description for filter {filterID}", appFunction.Main);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllAssignmentFilterContentAsync(GraphServiceClient graphServiceClient)
        {
            var filters = await GetAllAssignmentFilters(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var filter in filters)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = filter.DisplayName,
                    ContentType = PolicyType,
                    ContentPlatform = HelperClass.TranslatePolicyPlatformName(filter.Platform?.ToString() ?? string.Empty),
                    ContentId = filter.Id,
                    ContentDescription = filter.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchAssignmentFilterContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var filters = await SearchForAssignmentFilters(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var filter in filters)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = filter.DisplayName,
                    ContentType = PolicyType,
                    ContentPlatform = HelperClass.TranslatePolicyPlatformName(filter.Platform?.ToString() ?? string.Empty),
                    ContentId = filter.Id,
                    ContentDescription = filter.Description
                });
            }

            return content;
        }

        /// <summary>
        /// Exports an assignment filter's full data as a JsonElement for JSON file export.
        /// </summary>
        public static async Task<JsonElement?> ExportAssignmentFilterDataAsync(GraphServiceClient graphServiceClient, string filterId)
        {
            try
            {
                var result = await graphServiceClient.DeviceManagement.AssignmentFilters[filterId].GetAsync();

                if (result == null)
                {
                    AppLogger.Warning($"Assignment filter {filterId} not found for export.", appFunction.JsonExport);
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
                AppLogger.Error($"Error exporting assignment filter {filterId}: {ex.Message}", appFunction.JsonExport);
                return null;
            }
        }

        /// <summary>
        /// Imports an assignment filter from previously exported JSON data into the destination tenant.
        /// </summary>
        public static async Task<string?> ImportAssignmentFilterFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                var json = policyData.GetRawText();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var parseNode = new JsonParseNode(JsonDocument.Parse(stream).RootElement);
                var exported = parseNode.GetObjectValue(DeviceAndAppManagementAssignmentFilter.CreateFromDiscriminatorValue);

                if (exported == null)
                {
                    AppLogger.Error("Failed to deserialize assignment filter data from JSON.", appFunction.Import);
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

                var imported = await graphServiceClient.DeviceManagement.AssignmentFilters.PostAsync(newFilter);

                AppLogger.Info($"Imported assignment filter: {imported?.DisplayName}", appFunction.Import);
                return imported?.DisplayName;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error importing assignment filter from JSON: {ex.Message}", appFunction.Import);
                return null;
            }
        }
    }
}
