using Microsoft.Identity.Client;
using System.Threading.Tasks;

namespace IntuneTools.Graph;
/// <summary>
/// Provides source-tenant (read-only) authentication against Microsoft Graph.
/// Thin static façade that delegates to the shared <see cref="UserAuthenticationBase"/>.
/// </summary>
public static class SourceUserAuthentication
{
    // Delegated permission scopes (adjust as needed)
    public static readonly string[] DefaultScopes = new[]
    {
            "openid",
            "offline_access",
            "User.Read",
            "Directory.Read.All",
            "Policy.Read.All",
            "AuditLog.Read.All",
            "Reports.Read.All",
            "RoleManagement.Read.All",
            "Application.Read.All",
            "DelegatedPermissionGrant.Read.All",
            "DeviceManagementApps.Read.All",
            "DeviceManagementCloudCA.Read.All",
            "DeviceManagementConfiguration.Read.All",
            "DeviceManagementManagedDevices.Read.All",
            "DeviceManagementRBAC.Read.All",
            "DeviceManagementScripts.Read.All",
            "DeviceManagementServiceConfig.Read.All",
            "Group.Read.All"
        };

    private static readonly UserAuthenticationBase _instance = new(DefaultScopes);

    public static GraphServiceClient? sourceGraphServiceClient
    {
        get => _instance.GraphClient;
        set => _instance.GraphClient = value;
    }

    public static IAccount? SignedInAccount => _instance.SignedInAccount;
    public static string? TenantId => _instance.TenantId;

    public static Task<GraphServiceClient> GetGraphClientAsync(string[]? scopes = null)
        => _instance.GetGraphClientAsync(scopes);

    public static Task<string> GetAccessTokenAsync(string[]? scopes = null)
        => _instance.GetAccessTokenAsync(scopes);

    public static Task<string[]> GetGrantedScopesAsync()
        => _instance.GetGrantedScopesAsync();

    public static Task<bool> ClearSessionAsync()
        => _instance.ClearSessionAsync();
}
