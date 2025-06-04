using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static IntuneTools.Utilities.HelperClass;

namespace IntuneTools.Utilities
{
    public class SourceTenantGraphClient
    {
        public static string sourceAuthority = $"https://login.microsoftonline.com/{sourceTenantID}";
        public static string sourceClientID { get; set; }
        public static string sourceTenantID { get; set; }

        public static string redirectUri = "http://localhost";  // Use a valid redirect URI

        public static string[] sourceScope = new string[] { "https://graph.microsoft.com/.default" };

        public static GraphServiceClient sourceGraphServiceClient;

        public static string? sourceAccessToken;
        public static DateTimeOffset sourceTokenExpirationTime;

        public static GraphServiceClient CreateGraphServiceClient()
        {
            Console.WriteLine("Creating graph object");
            var authenticationProvider = new BaseBearerTokenAuthenticationProvider(new SourceTokenProvider());
            return new GraphServiceClient(authenticationProvider);
        }

        public static async Task<GraphServiceClient> GetSourceGraphClient()
        {
            try
            {
                // Check if the token is still valid
                if (sourceTokenExpirationTime > DateTimeOffset.UtcNow)
                {
                    Log("Token is still valid. Using existing token");
                    var authprovider = new BaseBearerTokenAuthenticationProvider(new SourceTokenProvider());
                    return new GraphServiceClient(authprovider);
                }

                var app = PublicClientApplicationBuilder
                    .Create(sourceClientID)
                    .WithAuthority(new Uri(sourceAuthority))
                    .WithRedirectUri(redirectUri)
                    .Build();

                var accounts = await app.GetAccountsAsync();
                AuthenticationResult result;

                if (!accounts.Any())
                {
                    result = await app.AcquireTokenInteractive(sourceScope)
                        .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                        .WithExtraScopesToConsent(sourceScope) // Add this line to consent to all scopes
                        .ExecuteAsync();
                }
                else
                {
                    try
                    {
                        result = await app.AcquireTokenSilent(sourceScope, accounts.FirstOrDefault())
                            .ExecuteAsync();
                    }
                    catch (MsalUiRequiredException)
                    {
                        result = await app.AcquireTokenInteractive(sourceScope)
                            .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                            .WithExtraScopesToConsent(sourceScope) // Add this line to consent to all scopes
                            .ExecuteAsync();
                    }
                }

                sourceAccessToken = result.AccessToken;
                sourceTokenExpirationTime = result.ExpiresOn;

                var authenticationProvider = new BaseBearerTokenAuthenticationProvider(new SourceTokenProvider());
                return new GraphServiceClient(authenticationProvider);
            }
            catch (Exception ex)
            {
                Log($"Error acquiring token: {ex.Message}");
                throw;
            }
        }

        public class SourceTokenProvider : IAccessTokenProvider
        {
            public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = default,
                CancellationToken cancellationToken = default)
            {
                var token = "";

                // check if the token is still valid
                if (sourceTokenExpirationTime > DateTimeOffset.UtcNow)
                {
                    Log("Token is still valid. Using existing token");
                    return sourceAccessToken;
                }
                else
                {
                    Log("Token is expired. Must acquire new token");

                    var app = PublicClientApplicationBuilder
                       .Create(sourceClientID)
                       .WithAuthority(new Uri(sourceAuthority))
                       .WithRedirectUri(redirectUri)
                       .Build();

                    var accounts = await app.GetAccountsAsync();

                    AuthenticationResult result;
                    try
                    {
                        result = await app.AcquireTokenSilent(sourceScope, accounts.FirstOrDefault())
                            .ExecuteAsync();
                    }
                    catch (MsalUiRequiredException)
                    {
                        try
                        {
                            result = await app.AcquireTokenInteractive(sourceScope)
                            .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                            .ExecuteAsync();
                        }
                        catch (Microsoft.Identity.Client.MsalServiceException me)
                        {
                            Log($"Error acquiring token interactively: {me.Message}");
                            throw;
                        }
                    }

                    token = result.AccessToken;
                    sourceAccessToken = result.AccessToken;
                    sourceTokenExpirationTime = result.ExpiresOn;

                    return token;
                }
            }

            public AllowedHostsValidator AllowedHostsValidator { get; }
        }
    }
}