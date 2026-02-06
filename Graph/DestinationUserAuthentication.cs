using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IntuneTools.Graph;
/// <summary>
/// Provides reusable user-interactive authentication against Microsoft Graph (delegated) using MSAL PublicClientApplication.
/// Wraps acquisition logic and returns a GraphServiceClient configured with a token provider that silently refreshes tokens.
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
            "DeviceManagementApps.Read.All",
            "DeviceManagementCloudCA.Read.All",
            "DeviceManagementConfiguration.Read.All",
            "DeviceManagementManagedDevices.Read.All",
            "DeviceManagementRBAC.Read.All",
            "DeviceManagementScripts.Read.All",
            "DeviceManagementServiceConfig.Read.All",
            "Group.ReadWrite.All"
        };

    private const string PublicClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";
    private const string AuthorityOrganizations = "https://login.microsoftonline.com/organizations";
    private static IPublicClientApplication? _pca;
    private static MsalAccessTokenProvider? _tokenProvider;
    private static BaseBearerTokenAuthenticationProvider? _authProvider;
    public static GraphServiceClient? destinationGraphServiceClient;

    public static IAccount? SignedInAccount { get; private set; }
    public static string? TenantId { get; private set; }

    public static async Task<GraphServiceClient> GetGraphClientAsync(string[]? scopes = null)
    {
        scopes ??= DefaultScopes;

        if (_pca == null)
        {
            _pca = PublicClientApplicationBuilder
                .Create(PublicClientId)
                .WithAuthority(AuthorityOrganizations)
                .WithRedirectUri("http://localhost")
                .Build();
        }

        if (_tokenProvider == null)
        {
            AuthenticationResult? result = null;
            var accounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
            try
            {
                result = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync().ConfigureAwait(false);
            }
            catch (MsalUiRequiredException)
            {
                result = await _pca.AcquireTokenInteractive(scopes)
                    .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                    .ExecuteAsync().ConfigureAwait(false);
            }

            SignedInAccount = result.Account;
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var idToken = handler.ReadJwtToken(result.IdToken);
                TenantId = idToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
            }
            catch { }

            _tokenProvider = new MsalAccessTokenProvider(_pca, scopes);
            _authProvider = new BaseBearerTokenAuthenticationProvider(_tokenProvider);
            destinationGraphServiceClient = new GraphServiceClient(_authProvider);
        }

        return destinationGraphServiceClient;
    }

    public static async Task<string> GetAccessTokenAsync(string[] scopes = null)
    {
        _ = await GetGraphClientAsync(scopes).ConfigureAwait(false);
        return await _tokenProvider.GetAuthorizationTokenAsync(new Uri("https://graph.microsoft.com"));
    }

    public static async Task<bool> ClearSessionAsync()
    {
        // Ensure PCA exists
        if (_pca == null)
        {
            _pca = PublicClientApplicationBuilder
                .Create(PublicClientId)
                .WithAuthority(AuthorityOrganizations)
                .WithRedirectUri("http://localhost")
                .Build();
        }

        var accounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
        foreach (var acc in accounts)
        {
            await _pca.RemoveAsync(acc).ConfigureAwait(false);
        }

        // Reset cached state
        SignedInAccount = null;
        TenantId = null;
        _tokenProvider = null;
        _authProvider = null;
        destinationGraphServiceClient = null;

        return true;
    }

    private sealed class MsalAccessTokenProvider : IAccessTokenProvider
    {
        private readonly IPublicClientApplication _pca;
        private readonly string[] _scopes;
        private AuthenticationResult _cached;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public MsalAccessTokenProvider(IPublicClientApplication pca, string[] scopes)
        {
            _pca = pca;
            _scopes = scopes;
            AllowedHostsValidator = new AllowedHostsValidator();
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }

        public async Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object> additionalAuthenticationContext = default,
            CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cached != null && _cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                    return _cached.AccessToken;

                var accounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
                try
                {
                    _cached = await _pca
                        .AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (MsalUiRequiredException)
                {
                    _cached = await _pca
                        .AcquireTokenInteractive(_scopes)
                        .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                return _cached.AccessToken;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
