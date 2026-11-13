using static InToolz.Graph.EntraHelperClasses.GroupHelperClass;
using static InToolz.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static InToolz.Graph.IntuneHelperClasses.ApplicationHelper;
using static InToolz.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static InToolz.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
using static InToolz.Graph.IntuneHelperClasses.FilterHelperClass;
using static InToolz.Graph.IntuneHelperClasses.macOSShellScript;
using static InToolz.Graph.IntuneHelperClasses.PowerShellScriptsHelper;
using static InToolz.Graph.IntuneHelperClasses.ProactiveRemediationsHelper;
using static InToolz.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsAutoPilotHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsDriverUpdateHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsFeatureUpdateHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsQualityUpdatePolicyHandler;
using static InToolz.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;

namespace InToolz.Utilities
{
    /// <summary>
    /// Defines an assignment operation for a specific content type.
    /// </summary>
    /// <param name="TypeKey">The ContentTypes constant identifying this content type.</param>
    /// <param name="AssignAsync">Async function that performs the assignment operation.</param>
    public record AssignTypeDefinition(
        string TypeKey,
        Func<string, string, List<string>, GraphServiceClient, Task> AssignAsync);

    /// <summary>
    /// Defines a delete operation for a specific content type.
    /// </summary>
    /// <param name="TypeKey">Content type identifier (e.g., ContentTypes.SettingsCatalog).</param>
    /// <param name="DisplayName">Human-readable name for logging.</param>
    /// <param name="DeleteAsync">Async function that deletes a single item by ID. Returns true if deleted, false if skipped.</param>
    public record DeleteTypeDefinition(
        string TypeKey,
        string DisplayName,
        Func<string, Task<bool>> DeleteAsync);

    /// <summary>
    /// Defines a rename operation for a specific content type.
    /// </summary>
    /// <param name="TypeKey">Content type identifier (e.g., ContentTypes.SettingsCatalog).</param>
    /// <param name="DisplayName">Human-readable name for logging.</param>
    /// <param name="RenameAsync">Async action that renames a single item by ID.</param>
    /// <param name="GetDisplayNameAsync">Optional async function to get item's display name for logging.</param>
    public record RenameTypeDefinition(
        string TypeKey,
        string DisplayName,
        Func<string, string, Task> RenameAsync,
        Func<string, Task<string?>>? GetDisplayNameAsync = null);

    /// <summary>
    /// Defines an import operation for a specific content type.
    /// </summary>
    /// <param name="TypeKey">The ContentTypes constant identifying this content type.</param>
    /// <param name="DisplayName">Human-readable name for logging.</param>
    /// <param name="ImportAsync">Async function that performs the import operation.</param>
    public record ImportTypeDefinition(
        string TypeKey,
        string DisplayName,
        Func<List<string>, List<string>, Task> ImportAsync);

    /// <summary>
    /// Defines a bulk "remove all assignments" operation for a specific content type.
    /// </summary>
    public record RemoveAssignmentDefinition(
        string TypeKey,
        string DisplayName,
        Func<GraphServiceClient, string, Task> RemoveAsync);

    /// <summary>
    /// Central registries of per-content-type Graph operations (assign/delete/rename/import/JSON
    /// export-import), shared by AssignmentPage, CleanupPage, RenamingPage, ImportPage, and
    /// JsonPage. These were originally five near-identical private methods, one hand-maintained
    /// copy of the content-type list per page — moved here so there's one place to add a new
    /// content type's operations, and so they're unit-testable (they were previously private
    /// instance methods on a Page, unreachable without a UI host).
    ///
    /// Every entry is keyed by a <see cref="ContentTypes"/> constant, never a hand-typed literal —
    /// a hand-typed "MacOS Shell Script" literal (wrong casing vs. the real
    /// <see cref="ContentTypes.MacOSShellScript"/> constant) previously made macOS Shell Script
    /// assignment silently no-op from AssignmentPage.
    /// </summary>
    public static class GraphOperationRegistries
    {
        /// <summary>
        /// Gets the registry of all assignment type definitions.
        /// </summary>
        public static IEnumerable<AssignTypeDefinition> GetAssignTypeRegistry()
        {
            yield return new AssignTypeDefinition(
                ContentTypes.DeviceCompliancePolicy,
                async (id, name, groups, client) => await AssignGroupsToSingleDeviceCompliance(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.SettingsCatalog,
                async (id, name, groups, client) => await AssignGroupsToSingleSettingsCatalog(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.DeviceConfigurationPolicy,
                async (id, name, groups, client) => await AssignGroupsToSingleDeviceConfiguration(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.MacOSShellScript,
                async (id, name, groups, client) => await AssignGroupsToSingleShellScriptmacOS(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.PowerShellScript,
                async (id, name, groups, client) => await AssignGroupsToSinglePowerShellScript(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.ProactiveRemediation,
                async (id, name, groups, client) => await AssignGroupsToSingleProactiveRemediation(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.WindowsAutoPilotProfile,
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsAutoPilotProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.WindowsDriverUpdate,
                async (id, name, groups, client) => await AssignGroupsToSingleDriverProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.WindowsFeatureUpdate,
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsFeatureUpdateProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.WindowsQualityUpdatePolicy,
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsQualityUpdatePolicy(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.WindowsQualityUpdateProfile,
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsQualityUpdateProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                ContentTypes.AppleBYODEnrollmentProfile,
                async (id, name, groups, client) => await AssignGroupsToSingleAppleBYODEnrollmentProfile(id, name, groups, client));
        }

        /// <summary>
        /// Returns the delete registry with all content types and their delete operations.
        /// AutoPilot profile deletion needs a confirmation dialog (assigned devices may need
        /// their assignment cleared first) and page-local logging, so it stays on CleanupPage —
        /// <paramref name="deleteAutoPilotProfileAsync"/> is CleanupPage's own handler, injected
        /// rather than duplicated here.
        /// </summary>
        public static IEnumerable<DeleteTypeDefinition> GetDeleteTypeRegistry(
            GraphServiceClient client,
            Func<string, Task<bool>> deleteAutoPilotProfileAsync) =>
        [
            new(ContentTypes.SettingsCatalog, "Settings Catalog",
                async id => { await DeleteSettingsCatalog(client, id); return true; }),

            new(ContentTypes.DeviceCompliancePolicy, "Device Compliance Policy",
                async id => { await DeleteDeviceCompliancePolicy(client, id); return true; }),

            new(ContentTypes.DeviceConfigurationPolicy, "Device Configuration Policy",
                async id => { await DeleteDeviceConfigurationPolicy(client, id); return true; }),

            new(ContentTypes.AppleBYODEnrollmentProfile, "Apple BYOD Enrollment Profile",
                async id => { await DeleteAppleBYODEnrollmentProfile(client, id); return true; }),

            new(ContentTypes.AssignmentFilter, "Assignment Filter",
                async id => { await DeleteAssignmentFilter(client, id); return true; }),

            new(ContentTypes.EntraGroup, "Entra Group",
                async id => { await DeleteSecurityGroup(client, id); return true; }),

            new(ContentTypes.PowerShellScript, "PowerShell Script",
                async id => { await DeletePowerShellScript(client, id); return true; }),

            new(ContentTypes.ProactiveRemediation, "Proactive Remediation",
                async id => { await DeleteProactiveRemediationScript(client, id); return true; }),

            new(ContentTypes.MacOSShellScript, "macOS Shell Script",
                async id => { await DeleteMacosShellScript(client, id); return true; }),

            new(ContentTypes.WindowsAutoPilotProfile, "Windows AutoPilot Profile",
                deleteAutoPilotProfileAsync),

            new(ContentTypes.WindowsDriverUpdate, "Windows Driver Update",
                async id => { await DeleteDriverProfile(client, id); return true; }),

            new(ContentTypes.WindowsFeatureUpdate, "Windows Feature Update",
                async id => { await DeleteWindowsFeatureUpdateProfile(client, id); return true; }),

            new(ContentTypes.WindowsQualityUpdatePolicy, "Windows Quality Update Policy",
                async id => { await DeleteWindowsQualityUpdatePolicy(client, id); return true; }),

            new(ContentTypes.WindowsQualityUpdateProfile, "Windows Quality Update Profile",
                async id => { await DeleteWindowsQualityUpdateProfile(client, id); return true; }),

            new(ContentTypes.Application, "Application",
                async id => { await DeleteApplication(client, id); return true; }),
        ];

        /// <summary>
        /// Returns the rename registry with all content types and their rename operations.
        /// </summary>
        public static IEnumerable<RenameTypeDefinition> GetRenameTypeRegistry(GraphServiceClient client) =>
        [
            new(ContentTypes.SettingsCatalog, "Settings Catalog",
                async (id, p) => await RenameSettingsCatalogPolicy(client, id, p),
                async id => (await client.DeviceManagement.ConfigurationPolicies[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "name" }))?.Name),

            new(ContentTypes.DeviceCompliancePolicy, "Device Compliance Policy",
                async (id, p) => await RenameDeviceCompliancePolicy(client, id, p),
                async id => (await client.DeviceManagement.DeviceCompliancePolicies[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.DeviceConfigurationPolicy, "Device Configuration Policy",
                async (id, p) => await RenameDeviceConfigurationPolicy(client, id, p),
                async id => (await client.DeviceManagement.DeviceConfigurations[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.AppleBYODEnrollmentProfile, "Apple BYOD Enrollment Profile",
                async (id, p) => await RenameAppleBYODEnrollmentProfile(client, id, p),
                async id => (await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.MacOSShellScript, "macOS Shell Script",
                async (id, p) => await RenameMacOSShellScript(client, id, p),
                async id => (await client.DeviceManagement.DeviceShellScripts[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.PowerShellScript, "PowerShell Script",
                async (id, p) => await RenamePowerShellScript(client, id, p),
                async id => (await client.DeviceManagement.DeviceManagementScripts[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.ProactiveRemediation, "Proactive Remediation",
                async (id, p) => await RenameProactiveRemediation(client, id, p),
                async id => (await client.DeviceManagement.DeviceHealthScripts[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.WindowsAutoPilotProfile, "Windows AutoPilot Profile",
                async (id, p) => await RenameWindowsAutoPilotProfile(client, id, p),
                async id => (await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.WindowsDriverUpdate, "Windows Driver Update",
                async (id, p) => await RenameDriverProfile(client, id, p),
                async id => (await client.DeviceManagement.WindowsDriverUpdateProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.WindowsFeatureUpdate, "Windows Feature Update",
                async (id, p) => await RenameWindowsFeatureUpdateProfile(client, id, p),
                async id => (await client.DeviceManagement.WindowsFeatureUpdateProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.WindowsQualityUpdatePolicy, "Windows Quality Update Policy",
                async (id, p) => await RenameWindowsQualityUpdatePolicy(client, id, p),
                async id => (await client.DeviceManagement.WindowsQualityUpdatePolicies[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.WindowsQualityUpdateProfile, "Windows Quality Update Profile",
                async (id, p) => await RenameWindowsQualityUpdateProfile(client, id, p),
                async id => (await client.DeviceManagement.WindowsQualityUpdateProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.AssignmentFilter, "Assignment Filter",
                async (id, p) => await RenameAssignmentFilter(client, id, p),
                async id => (await client.DeviceManagement.AssignmentFilters[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.EntraGroup, "Entra Group",
                async (id, p) => await RenameGroup(client, id, p),
                async id => (await client.Groups[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName),

            new(ContentTypes.Application, "Application",
                async (id, p) => await RenameApplication(client, id, p)),
        ];

        /// <summary>
        /// Gets the registry of all import type definitions.
        /// </summary>
        public static IEnumerable<ImportTypeDefinition> GetImportTypeRegistry(
            bool isGroupSelected,
            bool isFilterSelected,
            List<string> groupIds,
            GraphServiceClient sourceClient,
            GraphServiceClient destinationClient)
        {
            yield return new ImportTypeDefinition(
                ContentTypes.EntraGroup,
                "Entra Groups",
                async (ids, _) => await ImportMultipleGroups(sourceClient, destinationClient, ids));

            yield return new ImportTypeDefinition(
                ContentTypes.SettingsCatalog,
                "Settings Catalog policies",
                async (ids, grpIds) => await ImportMultipleSettingsCatalog(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.DeviceCompliancePolicy,
                "Device Compliance policies",
                async (ids, grpIds) => await ImportMultipleDeviceCompliancePolicies(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.DeviceConfigurationPolicy,
                "Device Configuration policies",
                async (ids, grpIds) => await ImportMultipleDeviceConfigurations(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.AppleBYODEnrollmentProfile,
                "Apple BYOD Enrollment Profiles",
                async (ids, grpIds) => await ImportMultipleAppleBYODEnrollmentProfiles(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.AssignmentFilter,
                "Assignment Filters",
                async (ids, _) => await ImportMultipleAssignmentFilters(sourceClient, destinationClient, ids));

            yield return new ImportTypeDefinition(
                ContentTypes.PowerShellScript,
                "PowerShell Scripts",
                async (ids, grpIds) => await ImportMultiplePowerShellScripts(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.ProactiveRemediation,
                "Proactive Remediations",
                async (ids, grpIds) => await ImportMultipleProactiveRemediations(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.MacOSShellScript,
                "macOS Shell Scripts",
                async (ids, grpIds) => await ImportMultiplemacOSShellScripts(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsAutoPilotProfile,
                "Windows AutoPilot Profiles",
                async (ids, grpIds) => await ImportMultipleWindowsAutoPilotProfiles(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsDriverUpdate,
                "Windows Driver Updates",
                async (ids, grpIds) => await ImportMultipleDriverProfiles(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsFeatureUpdate,
                "Windows Feature Updates",
                async (ids, grpIds) => await ImportMultipleWindowsFeatureUpdateProfiles(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsQualityUpdatePolicy,
                "Windows Quality Update Policies",
                async (ids, grpIds) => await ImportMultipleWindowsQualityUpdatePolicies(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsQualityUpdateProfile,
                "Windows Quality Update Profiles",
                async (ids, grpIds) => await ImportMultipleWindowsQualityUpdateProfiles(sourceClient, destinationClient, ids, isGroupSelected, isFilterSelected, grpIds));
        }

        /// <summary>
        /// Registry of JSON export/import delegates per content type. Adding a new content type
        /// only requires adding an entry here (plus the helper methods themselves).
        /// </summary>
        public static readonly Dictionary<string, (
            Func<GraphServiceClient, string, Task<JsonElement?>> Export,
            Func<GraphServiceClient, JsonElement, Task<string?>> Import
        )> JsonContentTypeOperations = new(StringComparer.OrdinalIgnoreCase)
        {
            { ContentTypes.SettingsCatalog, (ExportSettingsCatalogPolicyDataAsync, ImportSettingsCatalogFromJsonDataAsync) },
            { ContentTypes.DeviceCompliancePolicy, (ExportDeviceCompliancePolicyDataAsync, ImportDeviceComplianceFromJsonDataAsync) },
            { ContentTypes.DeviceConfigurationPolicy, (ExportDeviceConfigurationPolicyDataAsync, ImportDeviceConfigurationFromJsonDataAsync) },
            { ContentTypes.WindowsDriverUpdate, (ExportWindowsDriverUpdatePolicyDataAsync, ImportWindowsDriverUpdateFromJsonDataAsync) },
            { ContentTypes.WindowsFeatureUpdate, (ExportWindowsFeatureUpdatePolicyDataAsync, ImportWindowsFeatureUpdateFromJsonDataAsync) },
            { ContentTypes.WindowsQualityUpdateProfile, (ExportWindowsQualityUpdateProfileDataAsync, ImportWindowsQualityUpdateProfileFromJsonDataAsync) },
            { ContentTypes.WindowsQualityUpdatePolicy, (ExportWindowsQualityUpdatePolicyDataAsync, ImportWindowsQualityUpdatePolicyFromJsonDataAsync) },
            { ContentTypes.PowerShellScript, (ExportPowerShellScriptDataAsync, ImportPowerShellScriptFromJsonDataAsync) },
            { ContentTypes.ProactiveRemediation, (ExportProactiveRemediationDataAsync, ImportProactiveRemediationFromJsonDataAsync) },
            { ContentTypes.MacOSShellScript, (ExportMacOSShellScriptDataAsync, ImportMacOSShellScriptFromJsonDataAsync) },
            { ContentTypes.WindowsAutoPilotProfile, (ExportWindowsAutoPilotProfileDataAsync, ImportWindowsAutoPilotProfileFromJsonDataAsync) },
            { ContentTypes.AppleBYODEnrollmentProfile, (ExportAppleBYODEnrollmentProfileDataAsync, ImportAppleBYODEnrollmentProfileFromJsonDataAsync) },
            { ContentTypes.AssignmentFilter, (ExportAssignmentFilterDataAsync, ImportAssignmentFilterFromJsonDataAsync) },
            { ContentTypes.EntraGroup, (ExportEntraGroupDataAsync, ImportEntraGroupFromJsonDataAsync) },
        };

        /// <summary>
        /// Registry mapping content types to their assignment detail retrieval functions.
        /// Shared by RightClickMenu's "View assignments" item and ManageAssignmentsPage's own
        /// assignment-viewer flow — these two were previously byte-for-byte duplicate private
        /// dictionaries.
        /// </summary>
        public static Dictionary<string, Func<GraphServiceClient, string, Task<List<AssignmentInfo>?>>> GetAssignmentDetailsRegistry() => new(StringComparer.OrdinalIgnoreCase)
        {
            [ContentTypes.SettingsCatalog] = GetSettingsCatalogAssignmentDetailsAsync,
            [ContentTypes.DeviceCompliancePolicy] = GetDeviceComplianceAssignmentDetailsAsync,
            [ContentTypes.DeviceConfigurationPolicy] = GetDeviceConfigurationAssignmentDetailsAsync,
            [ContentTypes.AppleBYODEnrollmentProfile] = GetAppleBYODAssignmentDetailsAsync,
            [ContentTypes.PowerShellScript] = GetPowerShellScriptAssignmentDetailsAsync,
            [ContentTypes.ProactiveRemediation] = GetProactiveRemediationAssignmentDetailsAsync,
            [ContentTypes.MacOSShellScript] = GetMacOSShellScriptAssignmentDetailsAsync,
            [ContentTypes.WindowsAutoPilotProfile] = GetWindowsAutoPilotAssignmentDetailsAsync,
            [ContentTypes.WindowsDriverUpdate] = GetWindowsDriverUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsFeatureUpdate] = GetWindowsFeatureUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdatePolicy] = GetWindowsQualityUpdatePolicyAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdateProfile] = GetWindowsQualityUpdateProfileAssignmentDetailsAsync,
        };

        /// <summary>
        /// Registry mapping content types to their bulk "remove all assignments" functions.
        /// </summary>
        public static IEnumerable<RemoveAssignmentDefinition> GetRemoveAssignmentRegistry() =>
        [
            new(ContentTypes.SettingsCatalog, "Settings Catalog",
                async (client, id) => await RemoveAllSettingsCatalogAssignmentsAsync(client, id)),

            new(ContentTypes.DeviceCompliancePolicy, "Device Compliance Policy",
                async (client, id) => await RemoveAllDeviceComplianceAssignmentsAsync(client, id)),

            new(ContentTypes.DeviceConfigurationPolicy, "Device Configuration Policy",
                async (client, id) => await RemoveAllDeviceConfigurationAssignmentsAsync(client, id)),

            new(ContentTypes.AppleBYODEnrollmentProfile, "Apple BYOD Enrollment Profile",
                async (client, id) => await RemoveAllAppleBYODAssignmentsAsync(client, id)),

            new(ContentTypes.PowerShellScript, "PowerShell Script",
                async (client, id) => await RemoveAllPowerShellScriptAssignmentsAsync(client, id)),

            new(ContentTypes.ProactiveRemediation, "Proactive Remediation",
                async (client, id) => await RemoveAllProactiveRemediationAssignmentsAsync(client, id)),

            new(ContentTypes.MacOSShellScript, "macOS Shell Script",
                async (client, id) => await RemoveAllMacOSShellScriptAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsAutoPilotProfile, "Windows AutoPilot Profile",
                async (client, id) => await RemoveAllWindowsAutoPilotAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsDriverUpdate, "Windows Driver Update",
                async (client, id) => await RemoveAllWindowsDriverUpdateAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsFeatureUpdate, "Windows Feature Update",
                async (client, id) => await RemoveAllWindowsFeatureUpdateAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsQualityUpdatePolicy, "Windows Quality Update Policy",
                async (client, id) => await RemoveAllWindowsQualityUpdatePolicyAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsQualityUpdateProfile, "Windows Quality Update Profile",
                async (client, id) => await RemoveAllWindowsQualityUpdateProfileAssignmentsAsync(client, id)),
        ];

        /// <summary>
        /// Registry mapping content types to single-assignment removal functions. Each function
        /// removes one specific assignment (by ID) from a content item.
        /// </summary>
        public static Dictionary<string, Func<GraphServiceClient, string, string, Task>> GetRemoveSingleAssignmentRegistry() => new(StringComparer.OrdinalIgnoreCase)
        {
            [ContentTypes.SettingsCatalog] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.ConfigurationPolicies[id].Assignments.GetAsync();
                await client.DeviceManagement.ConfigurationPolicies[id].Assign.PostAsAssignPostResponseAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.ConfigurationPolicies.Item.Assign.AssignPostRequestBody
                    { Assignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.DeviceCompliancePolicy] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.DeviceCompliancePolicies[id].Assignments.GetAsync();
                await client.DeviceManagement.DeviceCompliancePolicies[id].Assign.PostAsAssignPostResponseAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.DeviceCompliancePolicies.Item.Assign.AssignPostRequestBody
                    { Assignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.DeviceConfigurationPolicy] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.DeviceConfigurations[id].Assignments.GetAsync();
                await client.DeviceManagement.DeviceConfigurations[id].Assign.PostAsAssignPostResponseAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.DeviceConfigurations.Item.Assign.AssignPostRequestBody
                    { Assignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.AppleBYODEnrollmentProfile] = async (client, id, assignmentId) =>
            {
                await client.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].Assignments[assignmentId].DeleteAsync();
            },
            [ContentTypes.PowerShellScript] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.DeviceManagementScripts[id].Assignments.GetAsync();
                await client.DeviceManagement.DeviceManagementScripts[id].Assign.PostAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.DeviceManagementScripts.Item.Assign.AssignPostRequestBody
                    { DeviceManagementScriptAssignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.ProactiveRemediation] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.DeviceHealthScripts[id].Assignments.GetAsync();
                await client.DeviceManagement.DeviceHealthScripts[id].Assign.PostAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.DeviceHealthScripts.Item.Assign.AssignPostRequestBody
                    { DeviceHealthScriptAssignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.MacOSShellScript] = async (client, id, assignmentId) =>
            {
                await client.DeviceManagement.DeviceShellScripts[id].Assignments[assignmentId].DeleteAsync();
            },
            [ContentTypes.WindowsAutoPilotProfile] = async (client, id, assignmentId) =>
            {
                await client.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].Assignments[assignmentId].DeleteAsync();
            },
            [ContentTypes.WindowsDriverUpdate] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.WindowsDriverUpdateProfiles[id].Assignments.GetAsync();
                await client.DeviceManagement.WindowsDriverUpdateProfiles[id].Assign.PostAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.WindowsDriverUpdateProfiles.Item.Assign.AssignPostRequestBody
                    { Assignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.WindowsFeatureUpdate] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].Assignments.GetAsync();
                await client.DeviceManagement.WindowsFeatureUpdateProfiles[id].Assign.PostAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.WindowsFeatureUpdateProfiles.Item.Assign.AssignPostRequestBody
                    { Assignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.WindowsQualityUpdatePolicy] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.WindowsQualityUpdatePolicies[id].Assignments.GetAsync();
                await client.DeviceManagement.WindowsQualityUpdatePolicies[id].Assign.PostAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdatePolicies.Item.Assign.AssignPostRequestBody
                    { Assignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
            [ContentTypes.WindowsQualityUpdateProfile] = async (client, id, assignmentId) =>
            {
                var all = await client.DeviceManagement.WindowsQualityUpdateProfiles[id].Assignments.GetAsync();
                await client.DeviceManagement.WindowsQualityUpdateProfiles[id].Assign.PostAsync(
                    new Microsoft.Graph.Beta.DeviceManagement.WindowsQualityUpdateProfiles.Item.Assign.AssignPostRequestBody
                    { Assignments = all?.Value?.Where(a => a.Id != assignmentId).ToList() });
            },
        };
    }
}
