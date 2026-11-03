using InToolz.Graph;
using static InToolz.Graph.EntraHelperClasses.PermissionGrantHelper;

namespace InToolz.Tests;

/// <summary>
/// The scope lists each tenant requests, and the missing-scope count the post-sign-in permission
/// check reports. Guards the 1.8 scope change: <c>Device.ReadWrite.All</c> was added to the
/// source tenant only, for Entra device deletion.
/// </summary>
public class PermissionScopeTests
{
    /// <summary>
    /// What a tenant consented to under 1.7.0.0 — the scopes both tenants requested in that
    /// release, minus openid/offline_access.
    /// </summary>
    private static readonly string[] ScopesConsentedUnder17 =
    {
        "User.Read",
        "Directory.Read.All",
        "Policy.Read.All",
        "AuditLog.Read.All",
        "Reports.Read.All",
        "RoleManagement.Read.All",
        "Application.Read.All",
        "DelegatedPermissionGrant.Read.All",
        "DeviceManagementApps.ReadWrite.All",
        "DeviceManagementCloudCA.ReadWrite.All",
        "DeviceManagementConfiguration.ReadWrite.All",
        "DeviceManagementManagedDevices.ReadWrite.All",
        "DeviceManagementRBAC.ReadWrite.All",
        "DeviceManagementScripts.ReadWrite.All",
        "DeviceManagementServiceConfig.ReadWrite.All",
        "Group.ReadWrite.All"
    };

    [Fact]
    public void SourceScopes_Requests17PermissionsIncludingDeviceReadWriteAll()
    {
        var relevant = GetRelevantScopes(SourceUserAuthentication.DefaultScopes);

        Assert.Equal(17, relevant.Count);
        Assert.Contains("Device.ReadWrite.All", relevant);
    }

    [Fact]
    public void DestinationScopes_Requests16PermissionsWithoutDeviceReadWriteAll()
    {
        var relevant = GetRelevantScopes(DestinationUserAuthentication.DefaultScopes);

        Assert.Equal(16, relevant.Count);
        Assert.DoesNotContain("Device.ReadWrite.All", relevant, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DestinationScopes_DifferFromSourceOnlyByDeviceReadWriteAll()
    {
        var source = GetRelevantScopes(SourceUserAuthentication.DefaultScopes);
        var destination = GetRelevantScopes(DestinationUserAuthentication.DefaultScopes);

        Assert.Equal(source.Where(s => s != "Device.ReadWrite.All"), destination);
    }

    [Fact]
    public void ReturningUserFrom17_SourceIsMissingOnlyDeviceReadWriteAll()
    {
        var relevant = GetRelevantScopes(SourceUserAuthentication.DefaultScopes);

        var missing = FindMissingScopes(relevant, ScopesConsentedUnder17);

        Assert.Equal(new[] { "Device.ReadWrite.All" }, missing);
    }

    [Fact]
    public void ReturningUserFrom17_DestinationIsMissingNothing()
    {
        var relevant = GetRelevantScopes(DestinationUserAuthentication.DefaultScopes);

        var missing = FindMissingScopes(relevant, ScopesConsentedUnder17);

        Assert.Empty(missing);
    }

    [Fact]
    public void GetRelevantScopes_DropsOidcScopesAndSorts()
    {
        var relevant = GetRelevantScopes(new[] { "openid", "offline_access", "profile", "email", "User.Read", "Group.ReadWrite.All" });

        Assert.Equal(new[] { "Group.ReadWrite.All", "User.Read" }, relevant);
    }

    [Fact]
    public void FindMissingScopes_IgnoresCase()
    {
        var missing = FindMissingScopes(new[] { "Device.ReadWrite.All", "User.Read" }, new[] { "device.readwrite.all", "USER.READ" });

        Assert.Empty(missing);
    }
}
