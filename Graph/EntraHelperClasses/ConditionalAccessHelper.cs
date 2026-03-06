using IntuneTools.Utilities;
using Microsoft.Kiota.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Graph.EntraHelperClasses
{
    /// <summary>
    /// Helper class for Conditional Access policy operations.
    /// Uses raw HTTP requests via the Graph SDK's RequestAdapter because
    /// the typed ConditionalAccessPolicy model is not available in this SDK version.
    /// </summary>
    public class ConditionalAccessHelper
    {
        private const string PolicyType = "Conditional Access Policy";
        private const string BaseUrl = "https://graph.microsoft.com/beta/identity/conditionalAccess/policies";

        /// <summary>
        /// Lightweight record representing a Conditional Access policy.
        /// </summary>
        public record ConditionalAccessPolicyInfo(string Id, string DisplayName, string? Description);

        private static async Task<JsonElement?> SendGraphGetAsync(GraphServiceClient graphServiceClient, string url)
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.GET,
                URI = new Uri(url),
            };
            using var stream = await graphServiceClient.RequestAdapter.SendPrimitiveAsync<Stream>(requestInfo);
            if (stream == null) return null;
            var doc = await JsonDocument.ParseAsync(stream);
            return doc.RootElement.Clone();
        }

        private static async Task SendGraphPatchAsync(GraphServiceClient graphServiceClient, string url, string jsonBody)
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.PATCH,
                URI = new Uri(url),
            };
            requestInfo.Headers.Add("Content-Type", "application/json");
            requestInfo.SetStreamContent(new MemoryStream(Encoding.UTF8.GetBytes(jsonBody)), "application/json");
            await graphServiceClient.RequestAdapter.SendPrimitiveAsync<Stream>(requestInfo);
        }

        private static async Task SendGraphDeleteAsync(GraphServiceClient graphServiceClient, string url)
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.DELETE,
                URI = new Uri(url),
            };
            await graphServiceClient.RequestAdapter.SendPrimitiveAsync<Stream>(requestInfo);
        }

        private static async Task<JsonElement?> SendGraphPostAsync(GraphServiceClient graphServiceClient, string url, string jsonBody)
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.POST,
                URI = new Uri(url),
            };
            requestInfo.Headers.Add("Content-Type", "application/json");
            requestInfo.SetStreamContent(new MemoryStream(Encoding.UTF8.GetBytes(jsonBody)), "application/json");
            using var stream = await graphServiceClient.RequestAdapter.SendPrimitiveAsync<Stream>(requestInfo);
            if (stream == null) return null;
            var doc = await JsonDocument.ParseAsync(stream);
            return doc.RootElement.Clone();
        }

        private static List<ConditionalAccessPolicyInfo> ParsePoliciesFromJson(JsonElement root)
        {
            var policies = new List<ConditionalAccessPolicyInfo>();

            if (root.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    var displayName = item.TryGetProperty("displayName", out var nameProp) ? nameProp.GetString() : null;
                    var description = item.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(displayName))
                    {
                        policies.Add(new ConditionalAccessPolicyInfo(id, displayName, description));
                    }
                }
            }

            return policies;
        }

        public static async Task<List<ConditionalAccessPolicyInfo>> GetAllConditionalAccessPolicies(GraphServiceClient graphServiceClient)
        {
            var allPolicies = new List<ConditionalAccessPolicyInfo>();

            try
            {
                LogToFunctionFile(appFunction.Main, "Getting all Conditional Access policies in the tenant");

                var nextUrl = BaseUrl;
                while (!string.IsNullOrEmpty(nextUrl))
                {
                    var root = await SendGraphGetAsync(graphServiceClient, nextUrl);
                    if (root == null) break;

                    allPolicies.AddRange(ParsePoliciesFromJson(root.Value));

                    nextUrl = root.Value.TryGetProperty("@odata.nextLink", out var nextLink)
                        ? nextLink.GetString()
                        : null;
                }

                LogToFunctionFile(appFunction.Main, $"Found {allPolicies.Count} Conditional Access policies in the tenant");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while getting Conditional Access policies: {ex.Message}", LogLevels.Error);
            }

            return allPolicies;
        }

        public static async Task<List<ConditionalAccessPolicyInfo>> SearchForConditionalAccessPolicies(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                LogToFunctionFile(appFunction.Main, $"Searching for Conditional Access policies. Search query: {searchQuery}");

                var allPolicies = await GetAllConditionalAccessPolicies(graphServiceClient);

                var filtered = allPolicies
                    .Where(p => p.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                LogToFunctionFile(appFunction.Main, $"Found {filtered.Count} Conditional Access policies matching the search query.");
                return filtered;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while searching for Conditional Access policies: {ex.Message}", LogLevels.Error);
                return new List<ConditionalAccessPolicyInfo>();
            }
        }

        public static async Task<string?> GetConditionalAccessPolicyDisplayName(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var root = await SendGraphGetAsync(graphServiceClient, $"{BaseUrl}/{policyId}?$select=displayName");
                return root?.TryGetProperty("displayName", out var name) == true ? name.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task RenameConditionalAccessPolicy(GraphServiceClient graphServiceClient, string policyId, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                    throw new ArgumentNullException(nameof(graphServiceClient));
                if (string.IsNullOrEmpty(policyId))
                    throw new InvalidOperationException("Policy ID cannot be null or empty.");

                var policyUrl = $"{BaseUrl}/{policyId}";

                if (selectedRenameMode == "Prefix")
                {
                    var root = await SendGraphGetAsync(graphServiceClient, $"{policyUrl}?$select=displayName");
                    var currentName = root?.TryGetProperty("displayName", out var nameProp) == true ? nameProp.GetString() : null;

                    if (string.IsNullOrEmpty(currentName))
                        throw new InvalidOperationException($"Conditional Access policy with ID '{policyId}' not found.");

                    var name = FindPreFixInPolicyName(currentName, newName);
                    var body = JsonSerializer.Serialize(new { displayName = name });
                    await SendGraphPatchAsync(graphServiceClient, policyUrl, body);
                    LogToFunctionFile(appFunction.Main, $"Successfully renamed Conditional Access policy '{policyId}' to '{name}'");
                }
                else if (selectedRenameMode == "Description")
                {
                    var body = JsonSerializer.Serialize(new { description = newName });
                    await SendGraphPatchAsync(graphServiceClient, policyUrl, body);
                    LogToFunctionFile(appFunction.Main, $"Updated description for Conditional Access policy {policyId} to '{newName}'");
                }
                else if (selectedRenameMode == "RemovePrefix")
                {
                    var root = await SendGraphGetAsync(graphServiceClient, $"{policyUrl}?$select=displayName");
                    var currentName = root?.TryGetProperty("displayName", out var nameProp) == true ? nameProp.GetString() : null;

                    if (string.IsNullOrEmpty(currentName))
                        throw new InvalidOperationException($"Conditional Access policy with ID '{policyId}' not found.");

                    var name = RemovePrefixFromPolicyName(currentName);
                    var body = JsonSerializer.Serialize(new { displayName = name });
                    await SendGraphPatchAsync(graphServiceClient, policyUrl, body);
                    LogToFunctionFile(appFunction.Main, $"Removed prefix from Conditional Access policy {policyId}, new name: '{name}'");
                }
            }
            catch (ApiException apiEx)
            {
                LogToFunctionFile(appFunction.Main, $"API error renaming Conditional Access policy: {apiEx.Message}", LogLevels.Error);
                if (apiEx.ResponseStatusCode == 403)
                    LogToFunctionFile(appFunction.Main, "Please ensure the app has Policy.ReadWrite.ConditionalAccess permissions.", LogLevels.Warning);
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, "An error occurred while renaming Conditional Access policy", LogLevels.Warning);
                LogToFunctionFile(appFunction.Main, ex.Message, LogLevels.Error);
            }
        }

        public static async Task<bool> DeleteConditionalAccessPolicy(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                if (graphServiceClient == null)
                    throw new ArgumentNullException(nameof(graphServiceClient));
                if (string.IsNullOrEmpty(policyId))
                    throw new InvalidOperationException("Policy ID cannot be null or empty.");

                await SendGraphDeleteAsync(graphServiceClient, $"{BaseUrl}/{policyId}");
                return true;
            }
            catch (ApiException apiEx)
            {
                LogToFunctionFile(appFunction.Main, $"API error deleting Conditional Access policy: {apiEx.Message}", LogLevels.Error);
                if (apiEx.ResponseStatusCode == 403)
                    LogToFunctionFile(appFunction.Main, "Please ensure the app has Policy.ReadWrite.ConditionalAccess permissions.", LogLevels.Warning);
                return false;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"An error occurred while deleting Conditional Access policy: {ex.Message}", LogLevels.Error);
                return false;
            }
        }

        /// <summary>
        /// Exports the full JSON data for a single Conditional Access policy.
        /// </summary>
        public static async Task<JsonElement?> ExportConditionalAccessPolicyDataAsync(GraphServiceClient graphServiceClient, string policyId)
        {
            try
            {
                var root = await SendGraphGetAsync(graphServiceClient, $"{BaseUrl}/{policyId}");
                if (root == null)
                {
                    LogToFunctionFile(appFunction.Main, $"Conditional Access policy {policyId} not found for export.", LogLevels.Warning);
                    return null;
                }
                return root;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error exporting Conditional Access policy {policyId}: {ex.Message}", LogLevels.Error);
                return null;
            }
        }

        /// <summary>
        /// Imports a Conditional Access policy from previously exported JSON data.
        /// SAFETY: The policy state is always forced to "disabled" regardless of the source state.
        /// </summary>
        public static async Task<string?> ImportConditionalAccessPolicyFromJsonDataAsync(GraphServiceClient graphServiceClient, JsonElement policyData)
        {
            try
            {
                // Parse the exported JSON and build a clean object for import
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(policyData.GetRawText());
                if (dict == null)
                {
                    LogToFunctionFile(appFunction.Main, "Failed to deserialize Conditional Access policy data from JSON.", LogLevels.Error);
                    return null;
                }

                // Remove read-only / server-generated properties
                dict.Remove("id");
                dict.Remove("createdDateTime");
                dict.Remove("modifiedDateTime");
                dict.Remove("templateId");

                // CRITICAL: Force state to disabled — never import a CA policy that is enabled or report-only
                dict["state"] = JsonSerializer.SerializeToElement("disabled");

                var body = JsonSerializer.Serialize(dict);
                var result = await SendGraphPostAsync(graphServiceClient, BaseUrl, body);

                var displayName = result?.TryGetProperty("displayName", out var nameProp) == true
                    ? nameProp.GetString()
                    : "Unknown";

                LogToFunctionFile(appFunction.Main, $"Imported Conditional Access policy: {displayName} (state forced to disabled)");
                return displayName;
            }
            catch (ApiException apiEx)
            {
                LogToFunctionFile(appFunction.Main, $"API error importing Conditional Access policy: {apiEx.Message}", LogLevels.Error);
                if (apiEx.ResponseStatusCode == 403)
                    LogToFunctionFile(appFunction.Main, "Please ensure the app has Policy.ReadWrite.ConditionalAccess permissions.", LogLevels.Warning);
                return null;
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error importing Conditional Access policy from JSON: {ex.Message}", LogLevels.Error);
                return null;
            }
        }

        public static async Task<List<CustomContentInfo>> GetAllConditionalAccessContentAsync(GraphServiceClient graphServiceClient)
        {
            var policies = await GetAllConditionalAccessPolicies(graphServiceClient);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = PolicyType,
                    ContentPlatform = "Entra ID",
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }

        public static async Task<List<CustomContentInfo>> SearchConditionalAccessContentAsync(GraphServiceClient graphServiceClient, string searchQuery)
        {
            var policies = await SearchForConditionalAccessPolicies(graphServiceClient, searchQuery);
            var content = new List<CustomContentInfo>();

            foreach (var policy in policies)
            {
                content.Add(new CustomContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = PolicyType,
                    ContentPlatform = "Entra ID",
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }

            return content;
        }
    }
}
