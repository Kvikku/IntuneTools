using Microsoft.Graph;
using Microsoft.Graph.Beta.Models.ODataErrors;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneTools.Graph
{
    /// <summary>
    /// Reduces boilerplate for Graph API list/search operations that use PageIterator.
    /// Every Graph helper had near-identical GetAll/Search methods with the same
    /// request → PageIterator → collect → log → catch pattern; this helper captures it once.
    /// </summary>
    public static class GraphPageIteratorHelper
    {
        /// <summary>
        /// Executes a paginated Graph API request and collects all items into a list.
        /// Handles the PageIterator pattern, logging, and standard error handling.
        /// </summary>
        /// <typeparam name="TItem">The entity type (e.g., DeviceCompliancePolicy).</typeparam>
        /// <typeparam name="TCollectionResponse">The collection response type (e.g., DeviceCompliancePolicyCollectionResponse).</typeparam>
        /// <param name="client">The Graph service client.</param>
        /// <param name="requestAsync">Async function that makes the initial Graph API call and returns the collection response.</param>
        /// <param name="operationDescription">Description for logging (e.g., "device compliance policies").</param>
        /// <returns>A list of all collected items, or an empty list if an error occurs.</returns>
        public static async Task<List<TItem>> GetAllAsync<TItem, TCollectionResponse>(
            GraphServiceClient client,
            Func<Task<TCollectionResponse?>> requestAsync,
            string operationDescription)
            where TItem : class
            where TCollectionResponse : class, new()
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Retrieving all {operationDescription}.");

                var result = await requestAsync();
                if (result == null)
                {
                    LogToFunctionFile(appFunction.Main, $"Graph API returned null for {operationDescription}.", LogLevels.Warning);
                    return new List<TItem>();
                }

                var items = new List<TItem>();
                var pageIterator = PageIterator<TItem, TCollectionResponse>.CreatePageIterator(
                    client, result, item =>
                    {
                        items.Add(item);
                        return true;
                    });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {items.Count} {operationDescription}.");
                return items;
            }
            catch (ODataError oe)
            {
                LogToFunctionFile(appFunction.Main, $"ODataError retrieving {operationDescription}: {oe.Message}", LogLevels.Warning);
                return new List<TItem>();
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error retrieving {operationDescription}: {ex.Message}", LogLevels.Warning);
                return new List<TItem>();
            }
        }

        /// <summary>
        /// Executes a paginated Graph API request and filters results client-side.
        /// Use when the Graph endpoint doesn't support server-side $filter.
        /// </summary>
        /// <typeparam name="TItem">The entity type.</typeparam>
        /// <typeparam name="TCollectionResponse">The collection response type.</typeparam>
        /// <param name="client">The Graph service client.</param>
        /// <param name="requestAsync">Async function that makes the initial Graph API call.</param>
        /// <param name="predicate">Client-side filter predicate applied to each collected item.</param>
        /// <param name="operationDescription">Description for logging.</param>
        /// <returns>A filtered list of items, or an empty list if an error occurs.</returns>
        public static async Task<List<TItem>> SearchAsync<TItem, TCollectionResponse>(
            GraphServiceClient client,
            Func<Task<TCollectionResponse?>> requestAsync,
            Func<TItem, bool> predicate,
            string operationDescription)
            where TItem : class
            where TCollectionResponse : class, new()
        {
            var all = await GetAllAsync<TItem, TCollectionResponse>(client, requestAsync, operationDescription);
            var filtered = all.FindAll(item => predicate(item));
            LogToFunctionFile(appFunction.Main, $"Filtered to {filtered.Count} {operationDescription}.");
            return filtered;
        }
    }
}
