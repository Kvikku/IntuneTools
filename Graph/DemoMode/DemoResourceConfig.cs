namespace IntuneTools.Graph.DemoMode;

/// <summary>
/// Describes one Graph resource collection that <see cref="DemoGraphMessageHandler"/> knows how
/// to fake (list/get/create/patch/delete/assign), so routing stays generic instead of needing
/// bespoke code per Intune content type.
/// </summary>
internal sealed record DemoResourceConfig(
    string? TopLevelSegment,
    string CollectionSegment,
    string DisplayNameProperty,
    string DefaultODataType,
    bool SupportsCreate = true,
    bool SupportsMutation = true,
    bool SupportsAssign = true,
    bool SupportsSingleAssignmentDelete = false);

/// <summary>
/// Static catalog of every Graph resource the app calls, mirroring the endpoints used across
/// Graph/IntuneHelperClasses and Graph/EntraHelperClasses.
/// </summary>
internal static class DemoResourceCatalog
{
    public static readonly IReadOnlyList<DemoResourceConfig> All = new List<DemoResourceConfig>
    {
        new("deviceManagement", "deviceConfigurations", "displayName", "#microsoft.graph.windows10CustomConfiguration"),
        new("deviceManagement", "deviceCompliancePolicies", "displayName", "#microsoft.graph.windows10CompliancePolicy"),
        new("deviceManagement", "configurationPolicies", "name", "#microsoft.graph.deviceManagementConfigurationPolicy"),
        new("deviceManagement", "deviceManagementScripts", "displayName", "#microsoft.graph.deviceManagementScript"),
        new("deviceManagement", "deviceHealthScripts", "displayName", "#microsoft.graph.deviceHealthScript"),
        new("deviceManagement", "deviceShellScripts", "displayName", "#microsoft.graph.deviceShellScript", SupportsSingleAssignmentDelete: true),
        new("deviceManagement", "appleUserInitiatedEnrollmentProfiles", "displayName", "#microsoft.graph.appleUserInitiatedEnrollmentProfile", SupportsSingleAssignmentDelete: true),
        new("deviceManagement", "windowsAutopilotDeploymentProfiles", "displayName", "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile", SupportsSingleAssignmentDelete: true),
        new("deviceManagement", "windowsDriverUpdateProfiles", "displayName", "#microsoft.graph.windowsDriverUpdateProfile"),
        new("deviceManagement", "windowsFeatureUpdateProfiles", "displayName", "#microsoft.graph.windowsFeatureUpdateProfile"),
        new("deviceManagement", "windowsQualityUpdatePolicies", "displayName", "#microsoft.graph.windowsQualityUpdatePolicy"),
        new("deviceManagement", "windowsQualityUpdateProfiles", "displayName", "#microsoft.graph.windowsQualityUpdateProfile"),
        new("deviceManagement", "assignmentFilters", "displayName", "#microsoft.graph.deviceAndAppManagementAssignmentFilter", SupportsAssign: false),
        new("deviceManagement", "auditEvents", "displayName", "#microsoft.graph.auditEvent", SupportsCreate: false, SupportsMutation: false, SupportsAssign: false),
        new("deviceAppManagement", "mobileApps", "displayName", "#microsoft.graph.win32LobApp", SupportsCreate: false),
        new(null, "groups", "displayName", "#microsoft.graph.group", SupportsAssign: false),
        new(null, "organization", "displayName", "#microsoft.graph.organization", SupportsCreate: false, SupportsMutation: false, SupportsAssign: false),
    };

    public static DemoResourceConfig? Find(string? topLevel, string collection) =>
        All.FirstOrDefault(c =>
            string.Equals(c.TopLevelSegment, topLevel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.CollectionSegment, collection, StringComparison.OrdinalIgnoreCase));
}
