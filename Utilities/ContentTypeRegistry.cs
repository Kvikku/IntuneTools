using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static IntuneTools.Graph.EntraHelperClasses.GroupHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ApplicationHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
using static IntuneTools.Graph.IntuneHelperClasses.FilterHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.macOSShellScript;
using static IntuneTools.Graph.IntuneHelperClasses.PowerShellScriptsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ProactiveRemediationsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsAutoPilotHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsDriverUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsFeatureUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdatePolicyHandler;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Defines load and search operations for a content type.
    /// </summary>
    /// <param name="ContentType">The content type constant (e.g., ContentTypes.SettingsCatalog)</param>
    /// <param name="DisplayNamePlural">Plural display name for logging (e.g., "settings catalog policies")</param>
    /// <param name="LoadAll">Function to load all content of this type</param>
    /// <param name="Search">Function to search content of this type</param>
    public record ContentTypeOperations(
        string ContentType,
        string DisplayNamePlural,
        Func<GraphServiceClient, Task<IEnumerable<CustomContentInfo>>> LoadAll,
        Func<GraphServiceClient, string, Task<IEnumerable<CustomContentInfo>>> Search
    );

    /// <summary>
    /// Central registry of all content types and their Graph operations.
    /// Adding a new content type only requires adding an entry here.
    /// </summary>
    public static class ContentTypeRegistry
    {
        private static readonly List<ContentTypeOperations> _registry = new()
        {
            new(ContentTypes.SettingsCatalog, "settings catalog policies",
                async client => await GetAllSettingsCatalogContentAsync(client),
                async (client, query) => await SearchSettingsCatalogContentAsync(client, query)),

            new(ContentTypes.DeviceCompliancePolicy, "device compliance policies",
                async client => await GetAllDeviceComplianceContentAsync(client),
                async (client, query) => await SearchDeviceComplianceContentAsync(client, query)),

            new(ContentTypes.DeviceConfigurationPolicy, "device configuration policies",
                async client => await GetAllDeviceConfigurationContentAsync(client),
                async (client, query) => await SearchDeviceConfigurationContentAsync(client, query)),

            new(ContentTypes.AppleBYODEnrollmentProfile, "Apple BYOD enrollment profiles",
                async client => await GetAllAppleBYODEnrollmentContentAsync(client),
                async (client, query) => await SearchAppleBYODEnrollmentContentAsync(client, query)),

            new(ContentTypes.AssignmentFilter, "assignment filters",
                async client => await GetAllAssignmentFilterContentAsync(client),
                async (client, query) => await SearchAssignmentFilterContentAsync(client, query)),

            new(ContentTypes.EntraGroup, "Entra groups",
                async client => await GetAllGroupContentAsync(client),
                async (client, query) => await SearchGroupContentAsync(client, query)),

            new(ContentTypes.PowerShellScript, "PowerShell scripts",
                async client => await GetAllPowerShellScriptContentAsync(client),
                async (client, query) => await SearchPowerShellScriptContentAsync(client, query)),

            new(ContentTypes.ProactiveRemediation, "proactive remediations",
                async client => await GetAllProactiveRemediationContentAsync(client),
                async (client, query) => await SearchProactiveRemediationContentAsync(client, query)),

            new(ContentTypes.MacOSShellScript, "macOS shell scripts",
                async client => await GetAllMacOSShellScriptContentAsync(client),
                async (client, query) => await SearchMacOSShellScriptContentAsync(client, query)),

            new(ContentTypes.WindowsAutoPilotProfile, "Windows AutoPilot profiles",
                async client => await GetAllWindowsAutoPilotContentAsync(client),
                async (client, query) => await SearchWindowsAutoPilotContentAsync(client, query)),

            new(ContentTypes.WindowsDriverUpdate, "Windows driver updates",
                async client => await GetAllWindowsDriverUpdateContentAsync(client),
                async (client, query) => await SearchWindowsDriverUpdateContentAsync(client, query)),

            new(ContentTypes.WindowsFeatureUpdate, "Windows feature updates",
                async client => await GetAllWindowsFeatureUpdateContentAsync(client),
                async (client, query) => await SearchWindowsFeatureUpdateContentAsync(client, query)),

            new(ContentTypes.WindowsQualityUpdatePolicy, "Windows quality update policies",
                async client => await GetAllWindowsQualityUpdatePolicyContentAsync(client),
                async (client, query) => await SearchWindowsQualityUpdatePolicyContentAsync(client, query)),

            new(ContentTypes.WindowsQualityUpdateProfile, "Windows quality update profiles",
                async client => await GetAllWindowsQualityUpdateProfileContentAsync(client),
                async (client, query) => await SearchWindowsQualityUpdateProfileContentAsync(client, query)),

            new(ContentTypes.Application, "applications",
                async client => await GetAllApplicationContentAsync(client),
                async (client, query) => await SearchApplicationContentAsync(client, query)),
        };

        /// <summary>
        /// Gets all registered content type operations.
        /// </summary>
        public static IReadOnlyList<ContentTypeOperations> All => _registry;

        /// <summary>
        /// Gets operations for a specific content type.
        /// </summary>
        public static ContentTypeOperations? Get(string contentType) =>
            _registry.Find(r => string.Equals(r.ContentType, contentType, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets operations for multiple content types.
        /// </summary>
        public static IEnumerable<ContentTypeOperations> GetMany(IEnumerable<string> contentTypes)
        {
            foreach (var contentType in contentTypes)
            {
                var op = Get(contentType);
                if (op != null)
                    yield return op;
            }
        }
    }
}
