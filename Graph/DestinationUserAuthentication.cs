using Microsoft.Identity.Client;

namespace IntuneTools.Graph;
/// <summary>
/// Provides destination-tenant (read-write) authentication against Microsoft Graph.
/// Thin static façade that delegates to the shared <see cref="UserAuthenticationBase"/>.
/// </summary>
public static class DestinationUserAuthentication
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
            "DeviceManagementApps.ReadWrite.All",
            "DeviceManagementCloudCA.ReadWrite.All",
            "DeviceManagementConfiguration.ReadWrite.All",
            "DeviceManagementManagedDevices.ReadWrite.All",
            "DeviceManagementRBAC.ReadWrite.All",
            "DeviceManagementScripts.ReadWrite.All",
            "DeviceManagementServiceConfig.ReadWrite.All",
            "Group.ReadWrite.All"
        };

    private static readonly object _swapLock = new();

    internal static UserAuthenticationBase _instance = new(DefaultScopes);

    public static GraphServiceClient? destinationGraphServiceClient
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

    /// <summary>
    /// Swaps the underlying auth instances between source and destination facades.
    /// After this call, all facade members (GraphClient, SignedInAccount, TenantId,
    /// GetAccessTokenAsync, GetGrantedScopesAsync, etc.) reflect the swapped tenant.
    /// Thread-safe via lock; typically called from the UI thread only.
    /// </summary>
    public static void SwapAuthInstances()
    {
        lock (_swapLock)
        {
            (SourceUserAuthentication._instance, _instance) =
                (_instance, SourceUserAuthentication._instance);
        }
    }
}
