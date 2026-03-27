using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IntuneTools.Graph;
/// <summary>
/// Encapsulates reusable user-interactive authentication against Microsoft Graph (delegated) using MSAL PublicClientApplication.
/// Wraps acquisition logic and returns a GraphServiceClient configured with a token provider that silently refreshes tokens.
/// Used by <see cref="SourceUserAuthentication"/> and <see cref="DestinationUserAuthentication"/> to avoid code duplication.
/// </summary>
internal sealed class UserAuthenticationBase
{
    private const string PublicClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";
    private const string AuthorityOrganizations = "https://login.microsoftonline.com/organizations";

    private readonly string[] _defaultScopes;
    private IPublicClientApplication? _pca;
    private MsalAccessTokenProvider? _tokenProvider;
    private BaseBearerTokenAuthenticationProvider? _authProvider;

    public GraphServiceClient? GraphClient { get; internal set; }
    public IAccount? SignedInAccount { get; private set; }
    public string? TenantId { get; private set; }

    public UserAuthenticationBase(string[] defaultScopes)
    {
        _defaultScopes = defaultScopes;
    }

    public async Task<GraphServiceClient> GetGraphClientAsync(string[]? scopes = null)
    {
        scopes ??= _defaultScopes;

        EnsurePcaInitialized();

        if (_tokenProvider == null)
        {
            AuthenticationResult? result = null;
            var accounts = await _pca!.GetAccountsAsync().ConfigureAwait(false);
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
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Failed to parse tenant ID from JWT: {ex.Message}", LogLevels.Warning);
            }

            _tokenProvider = new MsalAccessTokenProvider(_pca, scopes);
            _authProvider = new BaseBearerTokenAuthenticationProvider(_tokenProvider);
            GraphClient = new GraphServiceClient(_authProvider);
        }
        else if (GraphClient == null)
        {
            // Re-create GraphClient if it was cleared externally while _tokenProvider remained set.
            _authProvider ??= new BaseBearerTokenAuthenticationProvider(_tokenProvider);
            GraphClient = new GraphServiceClient(_authProvider);
        }

        return GraphClient;
    }

    public async Task<string> GetAccessTokenAsync(string[]? scopes = null)
    {
        _ = await GetGraphClientAsync(scopes).ConfigureAwait(false);
        return await _tokenProvider!.GetAuthorizationTokenAsync(new Uri("https://graph.microsoft.com"));
    }

    /// <summary>
    /// Gets the granted permission scopes from the current access token.
    /// </summary>
    /// <returns>Array of granted scope strings, or empty array if not authenticated.</returns>
    public async Task<string[]> GetGrantedScopesAsync()
    {
        if (_tokenProvider == null)
            return Array.Empty<string>();

        try
        {
            var token = await GetAccessTokenAsync();
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var scopes = jwt.Claims.FirstOrDefault(c => c.Type == "scp")?.Value;
            return scopes?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            LogToFunctionFile(appFunction.Main, $"Failed to read granted scopes: {ex.Message}", LogLevels.Warning);
            return Array.Empty<string>();
        }
    }

    public async Task<bool> ClearSessionAsync()
    {
        EnsurePcaInitialized();

        var accounts = await _pca!.GetAccountsAsync().ConfigureAwait(false);
        foreach (var acc in accounts)
        {
            await _pca.RemoveAsync(acc).ConfigureAwait(false);
        }

        // Reset cached state
        SignedInAccount = null;
        TenantId = null;
        _tokenProvider = null;
        _authProvider = null;
        GraphClient = null;

        return true;
    }

    private void EnsurePcaInitialized()
    {
        _pca ??= PublicClientApplicationBuilder
            .Create(PublicClientId)
            .WithAuthority(AuthorityOrganizations)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    private sealed class MsalAccessTokenProvider : IAccessTokenProvider
    {
        private readonly IPublicClientApplication _pca;
        private readonly string[] _scopes;
        private AuthenticationResult? _cached;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public MsalAccessTokenProvider(IPublicClientApplication pca, string[] scopes)
        {
            _pca = pca;
            _scopes = scopes;
            AllowedHostsValidator = new AllowedHostsValidator();
            AllowedHostsValidator.SetAllowedHosts(new List<string> { "graph.microsoft.com" });
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }

        public async Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = default,
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
