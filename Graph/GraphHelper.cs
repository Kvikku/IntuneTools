using IntuneTools.Utilities;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph
{
    /// <summary>
    /// Generic base class for Graph API policy/resource helper operations.
    /// Eliminates duplicated GetAll/Search/Delete/Rename/Content/Export/Import/AssignmentDetails patterns
    /// across all Intune helper classes.
    /// </summary>
    /// <typeparam name="TPolicy">The Graph SDK model type (e.g., DeviceManagementConfigurationPolicy).</typeparam>
    /// <typeparam name="TCollectionResponse">The collection response type from the Graph SDK.</typeparam>
    public abstract class GraphHelper<TPolicy, TCollectionResponse>
        where TPolicy : class, IParsable
        where TCollectionResponse : class, IParsable
    {
        /// <summary>Human-readable resource name for logging (e.g., "settings catalog policies").</summary>
        protected abstract string ResourceName { get; }

        /// <summary>Content type constant string (e.g., ContentTypes.SettingsCatalog).</summary>
        protected abstract string ContentTypeName { get; }

        /// <summary>Platform string for content mapping (e.g., "Windows"). Return null to use TranslatePolicyPlatformName.</summary>
        protected virtual string? FixedPlatform => null;

        // ── Core Graph SDK delegates (must be provided by subclass) ──────────────

        /// <summary>Calls the Graph SDK GetAsync for the collection endpoint with Top=1000.</summary>
        protected abstract Task<TCollectionResponse?> GetCollectionAsync(GraphServiceClient client);

        /// <summary>Calls the Graph SDK GetAsync for the collection with a search filter.</summary>
        protected abstract Task<TCollectionResponse?> SearchCollectionAsync(GraphServiceClient client, string searchQuery);

        /// <summary>Calls the Graph SDK GetAsync for a single item by ID.</summary>
        protected abstract Task<TPolicy?> GetByIdAsync(GraphServiceClient client, string id);

        /// <summary>Calls the Graph SDK DeleteAsync for a single item by ID.</summary>
        protected abstract Task DeleteByIdAsync(GraphServiceClient client, string id);

        /// <summary>Creates a patch object with the name/displayName set, and calls PatchAsync.</summary>
        protected abstract Task PatchNameAsync(GraphServiceClient client, string id, string newName);

        /// <summary>Creates a patch object with the description set, and calls PatchAsync.</summary>
        protected abstract Task PatchDescriptionAsync(GraphServiceClient client, string id, string description);

        // ── Content mapping (must be provided by subclass) ───────────────────────

        /// <summary>Gets the display name from a policy object (Name vs DisplayName).</summary>
        protected abstract string? GetPolicyName(TPolicy policy);

        /// <summary>Gets the ID from a policy object.</summary>
        protected abstract string? GetPolicyId(TPolicy policy);

        /// <summary>Gets the description from a policy object.</summary>
        protected abstract string? GetPolicyDescription(TPolicy policy);

        /// <summary>Gets the platform string from a policy object. Default uses FixedPlatform.</summary>
        protected virtual string? GetPolicyPlatform(TPolicy policy) => FixedPlatform;

        // ── GetAll ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves all items using PageIterator-based pagination.
        /// </summary>
        public async Task<List<TPolicy>> GetAllAsync(GraphServiceClient client)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Retrieving all {ResourceName}.");

                var result = await GetCollectionAsync(client);

                var items = new List<TPolicy>();
                var pageIterator = PageIterator<TPolicy, TCollectionResponse>
                    .CreatePageIterator(client, result, item =>
                    {
                        items.Add(item);
                        return true;
                    });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {items.Count} {ResourceName}.");
                return items;
            }
            catch (Exception ex)
            {
                GraphErrorHandler.HandleException(ex, "retrieving all", ResourceName);
                return new List<TPolicy>();
            }
        }

        // ── Search ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Searches for items using a server-side filter with PageIterator pagination.
        /// </summary>
        public virtual async Task<List<TPolicy>> SearchAsync(GraphServiceClient client, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Searching for {ResourceName}. Search query: {searchQuery}");

                var result = await SearchCollectionAsync(client, searchQuery);

                var items = new List<TPolicy>();
                var pageIterator = PageIterator<TPolicy, TCollectionResponse>
                    .CreatePageIterator(client, result, item =>
                    {
                        items.Add(item);
                        return true;
                    });
                await pageIterator.IterateAsync();

                LogToFunctionFile(appFunction.Main, $"Found {items.Count} {ResourceName}.");
                return items;
            }
            catch (Exception ex)
            {
                GraphErrorHandler.HandleException(ex, "searching for", ResourceName);
                return new List<TPolicy>();
            }
        }

        // ── Delete ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes a single item by ID.
        /// </summary>
        public async Task DeleteAsync(GraphServiceClient client, string id)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(client);
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException($"{ResourceName} ID cannot be null or empty.");

                await DeleteByIdAsync(client, id);
            }
            catch (Exception ex)
            {
                GraphErrorHandler.HandleException(ex, "deleting", ResourceName);
            }
        }

        // ── Rename ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Renames a single item based on the current selectedRenameMode (Prefix/Description/RemovePrefix).
        /// </summary>
        public async Task RenameAsync(GraphServiceClient client, string id, string newName)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(client);
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException($"{ResourceName} ID cannot be null or empty.");
                if (string.IsNullOrWhiteSpace(newName))
                    throw new InvalidOperationException("New name cannot be null or empty.");

                if (selectedRenameMode == "Prefix")
                {
                    var existing = await GetByIdAsync(client, id);
                    if (existing == null)
                    {
                        LogToFunctionFile(appFunction.Main, $"Unable to rename: {ResourceName} with ID {id} was not found.", LogLevels.Warning);
                        return;
                    }

                    var currentName = GetPolicyName(existing) ?? string.Empty;
                    var name = FindPreFixInPolicyName(currentName, newName);
                    await PatchNameAsync(client, id, name);
                    LogToFunctionFile(appFunction.Main, $"Renamed {ResourceName} {id} to {name}");
                }
                else if (selectedRenameMode == "Suffix")
                {
                    // Suffix mode is not yet implemented
                }
                else if (selectedRenameMode == "Description")
                {
                    await PatchDescriptionAsync(client, id, newName);
                    LogToFunctionFile(appFunction.Main, $"Updated description for {ResourceName} {id} to {newName}");
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var existing = await GetByIdAsync(client, id);
                    if (existing == null)
                    {
                        LogToFunctionFile(appFunction.Main, $"Unable to remove prefix: {ResourceName} with ID {id} was not found.", LogLevels.Warning);
                        return;
                    }

                    var currentName = GetPolicyName(existing);
                    if (string.IsNullOrWhiteSpace(currentName))
                    {
                        LogToFunctionFile(appFunction.Main, $"Unable to remove prefix from {ResourceName} {id}: name is null or empty.", LogLevels.Warning);
                        return;
                    }

                    var name = RemovePrefixFromPolicyName(currentName);
                    await PatchNameAsync(client, id, name);
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from {ResourceName} {id}, new name: {name}");
                }
            }
            catch (Exception ex)
            {
                GraphErrorHandler.HandleException(ex, "renaming", ResourceName);
            }
        }

        // ── Content mapping ──────────────────────────────────────────────────────

        /// <summary>
        /// Gets all items as CustomContentInfo list for UI binding.
        /// </summary>
        public async Task<List<CustomContentInfo>> GetAllContentAsync(GraphServiceClient client)
        {
            var policies = await GetAllAsync(client);
            return MapToContent(policies);
        }

        /// <summary>
        /// Searches items and returns as CustomContentInfo list for UI binding.
        /// </summary>
        public async Task<List<CustomContentInfo>> SearchContentAsync(GraphServiceClient client, string searchQuery)
        {
            var policies = await SearchAsync(client, searchQuery);
            return MapToContent(policies);
        }

        private List<CustomContentInfo> MapToContent(List<TPolicy> policies)
        {
            var content = new List<CustomContentInfo>();
            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = GetPolicyName(policy),
                    ContentType = ContentTypeName,
                    ContentPlatform = GetPolicyPlatform(policy),
                    ContentId = GetPolicyId(policy),
                    ContentDescription = GetPolicyDescription(policy)
                });
            }
            return content;
        }

        // ── JSON Export ──────────────────────────────────────────────────────────

        /// <summary>
        /// Exports a single item's full data as a JsonElement for JSON file export.
        /// Override GetByIdForExportAsync to customize the GET request (e.g., add $expand).
        /// </summary>
        public async Task<JsonElement?> ExportDataAsync(GraphServiceClient client, string id)
        {
            try
            {
                var result = await GetByIdForExportAsync(client, id);

                if (result == null)
                {
                    LogToFunctionFile(appFunction.Main, $"{ResourceName} {id} not found for export.", LogLevels.Warning);
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
                GraphErrorHandler.HandleException(ex, "exporting", $"{ResourceName} {id}");
                return null;
            }
        }

        /// <summary>
        /// Gets a single item by ID for export. Override to add $expand or other query params.
        /// Default calls GetByIdAsync.
        /// </summary>
        protected virtual Task<TPolicy?> GetByIdForExportAsync(GraphServiceClient client, string id)
            => GetByIdAsync(client, id);

        // ── JSON Import ──────────────────────────────────────────────────────────

        /// <summary>
        /// Imports a single item from JSON data into the destination tenant.
        /// Must be overridden by subclasses to provide deserialization and POST logic.
        /// </summary>
        public abstract Task<string?> ImportFromJsonDataAsync(GraphServiceClient client, JsonElement policyData);

        // ── Has Assignments ──────────────────────────────────────────────────────

        /// <summary>
        /// Checks if an item has any assignments. Returns null on error.
        /// Override in subclasses that support assignment checking.
        /// </summary>
        public virtual Task<bool?> HasAssignmentsAsync(GraphServiceClient client, string id)
            => Task.FromResult<bool?>(null);

        // ── Get Assignment Details ───────────────────────────────────────────────

        /// <summary>
        /// Gets detailed assignment information for an item.
        /// Override in subclasses that support assignment management.
        /// </summary>
        public virtual Task<List<AssignmentInfo>?> GetAssignmentDetailsAsync(GraphServiceClient client, string id)
            => Task.FromResult<List<AssignmentInfo>?>(null);

        // ── Remove All Assignments ───────────────────────────────────────────────

        /// <summary>
        /// Removes all assignments from an item.
        /// Override in subclasses that support assignment management.
        /// </summary>
        public virtual Task RemoveAllAssignmentsAsync(GraphServiceClient client, string id)
            => Task.CompletedTask;

        // ── Import Multiple ──────────────────────────────────────────────────────

        /// <summary>
        /// Imports multiple items from the source tenant into the destination tenant.
        /// Override in subclasses that support cross-tenant import.
        /// </summary>
        public virtual Task ImportMultipleAsync(
            GraphServiceClient sourceClient,
            GraphServiceClient destinationClient,
            List<string> ids,
            bool assignments,
            bool filter,
            List<string> groups)
            => Task.CompletedTask;

        // ── Assign Groups ────────────────────────────────────────────────────────

        /// <summary>
        /// Assigns groups to a single item.
        /// Override in subclasses that support assignment management.
        /// </summary>
        public virtual Task AssignGroupsAsync(string id, List<string> groupIds, GraphServiceClient client)
            => Task.CompletedTask;
    }
}
