using IntuneTools.Utilities;
using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class FilterHelperClass
    {

        private const string PolicyType = "Assignment Filter";

        public static async Task<List<DeviceAndAppManagementAssignmentFilter>> SearchForAssignmentFilters(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Searching for {PolicyType} policies. Search query: {searchQuery}");

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
                {
                    LogToFunctionFile(appFunction.Main, $"Search returned null or empty result for {PolicyType} policies.");
                    return new List<DeviceAndAppManagementAssignmentFilter>();
                }

                List<DeviceAndAppManagementAssignmentFilter> assignmentFilters = new List<DeviceAndAppManagementAssignmentFilter>();
                var pageIterator = PageIterator<DeviceAndAppManagementAssignmentFilter, DeviceAndAppManagementAssignmentFilterCollectionResponse>.CreatePageIterator(graphServiceClient, result, (filter) =>
                {
                    assignmentFilters.Add(filter);
                    return true; // Continue iterating
                });
                await pageIterator.IterateAsync();


                // If server-side filter doesn't work as expected, filter client-side:
                // assignmentFilters = assignmentFilters.Where(f => f.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();


                LogToFunctionFile(appFunction.Main, $"Found {assignmentFilters.Count} {PolicyType} policies matching the search query.");

                return assignmentFilters;
            }
            catch (ODataError odataError) when (odataError.ResponseStatusCode == 400) // Handle potential filter query issues
            {
                LogToFunctionFile(appFunction.Main, $"Server-side filtering might not be supported or the query is invalid for {PolicyType}. Trying client-side filtering. Error: {odataError.Error?.Message}", LogLevels.Error);
                // Fallback: Get all and filter client-side
                var allFilters = await GetAllAssignmentFilters(graphServiceClient);
                return allFilters.Where(f => f.DisplayName != null && f.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while searching for {PolicyType} policies", LogLevels.Error);
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
                LogToFunctionFile(appFunction.Main, $"An error occurred while getting assignment filters: {ex.Message}", LogLevels.Error);
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
                LogToFunctionFile(appFunction.Main, " ");
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Importing {filterIds.Count} Assignment filters.");


                foreach (var filterId in filterIds)
                {
                    DeviceAndAppManagementAssignmentFilter? sourceFilter = null;
                    var filterName = string.Empty;
                    try
                    {
                        sourceFilter = await sourceGraphServiceClient.DeviceManagement.AssignmentFilters[filterId].GetAsync();

                        if (sourceFilter == null)
                        {
                            LogToFunctionFile(appFunction.Main, $"Skipping filter ID {filterId}: Not found in source tenant.");
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

                        LogToFunctionFile(appFunction.Main, $"Successfully imported {importedFilter.DisplayName}\n");
                    }
                    catch (Exception ex)
                    {
                        LogToFunctionFile(appFunction.Main, $"Failed to import {filterName}\n", LogLevels.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An unexpected error occurred during the import process: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                LogToFunctionFile(appFunction.Main, $"{DateTime.Now.ToString()} - Finished importing {filterIds.Count} Assignment filters.");
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
            catch (ODataError odataError)
            {
                return false;
            }
            catch (Exception ex)
            {
                return false;
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
                    LogToFunctionFile(appFunction.Main, $"Successfully renamed filter with ID '{filterID}' to '{name}'.", LogLevels.Info);
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
                    LogToFunctionFile(appFunction.Main, $"Updated description for filter {filterID} to '{newName}'.", LogLevels.Info);
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
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from filter {filterID}, new name: '{name}'", LogLevels.Info);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming assignment filter", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
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
    }
}
