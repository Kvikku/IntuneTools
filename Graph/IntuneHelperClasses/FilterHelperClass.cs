using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class FilterHelperClass
    {

        private const string PolicyType = "Assignment Filter";

        public static async Task<List<DeviceAndAppManagementAssignmentFilter>> SearchForAssignmentFilters(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile($"Searching for {PolicyType} policies. Search query: {searchQuery}");

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
                    WriteToImportStatusFile($"Search returned null or empty result for {PolicyType} policies.");
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


                WriteToImportStatusFile($"Found {assignmentFilters.Count} {PolicyType} policies matching the search query.");

                return assignmentFilters;
            }
            catch (ODataError odataError) when (odataError.ResponseStatusCode == 400) // Handle potential filter query issues
            {
                WriteToImportStatusFile($"Server-side filtering might not be supported or the query is invalid for {PolicyType}. Trying client-side filtering. Error: {odataError.Error?.Message}",LogType.Error);
                // Fallback: Get all and filter client-side
                var allFilters = await GetAllAssignmentFilters(graphServiceClient);
                return allFilters.Where(f => f.DisplayName != null && f.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An error occurred while searching for {PolicyType} policies",LogType.Error);
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
                WriteToImportStatusFile($"An error occurred while getting assignment filters: {ex.Message}", LogType.Error);
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
                WriteToImportStatusFile(" ");
                WriteToImportStatusFile($"{DateTime.Now.ToString()} - Importing {filterIds.Count} Assignment filters.");


                foreach (var filterId in filterIds)
                {
                    DeviceAndAppManagementAssignmentFilter? sourceFilter = null;
                    var filterName = string.Empty;
                    try
                    {
                        sourceFilter = await sourceGraphServiceClient.DeviceManagement.AssignmentFilters[filterId].GetAsync();

                        if (sourceFilter == null)
                        {
                            WriteToImportStatusFile($"Skipping filter ID {filterId}: Not found in source tenant.");
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

                        WriteToImportStatusFile($"Successfully imported {importedFilter.DisplayName}\n");
                    }
                    catch (Exception ex)
                    {
                        WriteToImportStatusFile($"Failed to import {filterName}\n", LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An unexpected error occurred during the import process: {ex.Message}", LogType.Error);
            }
            finally
            {
                WriteToImportStatusFile($"{DateTime.Now.ToString()} - Finished importing {filterIds.Count} Assignment filters.");
            }
        }
    }
}
