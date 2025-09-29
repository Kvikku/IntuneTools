using Microsoft.Graph.Beta;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace IntuneTools.Graph
{
    public static class NativeAuthentication
    {
        // Delegated permission scopes (adjust as needed)
        public static readonly string[] DefaultScopes = new[]
        {
            "User.Read",
            "DeviceManagementApps.ReadWrite.All",
            "DeviceManagementCloudCA.ReadWrite.All",
            "DeviceManagementConfiguration.ReadWrite.All",
            "DeviceManagementManagedDevices.ReadWrite.All",
            "DeviceManagementRBAC.ReadWrite.All",
            "DeviceManagementScripts.ReadWrite.All",
            "DeviceManagementServiceConfig.ReadWrite.All"
        };

        private const string PublicClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";
        private const string AuthorityOrganizations = "https://login.microsoftonline.com/organizations";
        private static IPublicClientApplication? _pca;
        private static MsalAccessTokenProvider? _tokenProvider;
        private static BaseBearerTokenAuthenticationProvider? _authProvider;
        private static GraphServiceClient? _graphClient;

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
                _graphClient = new GraphServiceClient(_authProvider);
            }

            return _graphClient;
        }

        public static async Task<string> GetAccessTokenAsync(string[] scopes = null)
        {
            _ = await GetGraphClientAsync(scopes).ConfigureAwait(false);
            return await _tokenProvider.GetAuthorizationTokenAsync(new Uri("https://graph.microsoft.com"));
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
}
