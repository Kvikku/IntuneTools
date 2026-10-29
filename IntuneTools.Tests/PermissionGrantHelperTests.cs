using System.Net;
using System.Text.Json.Nodes;
using IntuneTools.Graph;
using static IntuneTools.Graph.EntraHelperClasses.PermissionGrantHelper;

namespace IntuneTools.Tests;

/// <summary>
/// The "Also granted, but not used" report: reading the tenant's consent grant for this app,
/// and diffing it against the scopes this version requests.
/// </summary>
public class PermissionGrantHelperTests
{
    private const string AppClientId = "app-client-id";
    private const string ServicePrincipalId = "sp-1";

    [Fact]
    public void FindUnnecessaryScopes_CompleteConsent_ReportsNothing()
    {
        var required = GetRelevantScopes(SourceUserAuthentication.DefaultScopes);

        Assert.Empty(FindUnnecessaryScopes(required, required));
    }

    [Fact]
    public void FindUnnecessaryScopes_LeftoverConsentFromOlderVersion_IsListedSorted()
    {
        var required = GetRelevantScopes(SourceUserAuthentication.DefaultScopes);
        var consented = required.Concat(new[] { "Organization.Read.All", "DeviceManagementConfiguration.Read.All" });

        var extras = FindUnnecessaryScopes(consented, required);

        Assert.Equal(new[] { "DeviceManagementConfiguration.Read.All", "Organization.Read.All" }, extras);
    }

    [Fact]
    public void FindUnnecessaryScopes_OidcScopesAreNotReported()
    {
        var extras = FindUnnecessaryScopes(new[] { "openid", "offline_access", "profile", "email", "User.Read" }, new[] { "User.Read" });

        Assert.Empty(extras);
    }

    [Fact]
    public void FindUnnecessaryScopes_IgnoresCase()
    {
        var extras = FindUnnecessaryScopes(new[] { "user.read" }, new[] { "User.Read" });

        Assert.Empty(extras);
    }

    [Fact]
    public async Task GetConsentedAppScopes_CombinesEveryGrantForTheAppsServicePrincipal()
    {
        var graph = new DemoGraphTestClient
        {
            Intercept = request => Path(request) switch
            {
                "/beta/servicePrincipals" => ServicePrincipalFound(),
                "/beta/oauth2PermissionGrants" => Grants("User.Read Group.ReadWrite.All", "  Device.ReadWrite.All  "),
                _ => null
            }
        };

        var scopes = await GetConsentedAppScopesAsync(graph.Client, AppClientId);

        Assert.NotNull(scopes);
        Assert.Equal(new[] { "Device.ReadWrite.All", "Group.ReadWrite.All", "User.Read" }, scopes.OrderBy(s => s));
        Assert.Contains(graph.Requests, r => r.Uri.AbsolutePath == "/beta/servicePrincipals" && Uri.UnescapeDataString(r.Uri.Query).Contains(AppClientId));
        Assert.Contains(graph.Requests, r => r.Uri.AbsolutePath == "/beta/oauth2PermissionGrants" && Uri.UnescapeDataString(r.Uri.Query).Contains(ServicePrincipalId));
    }

    [Fact]
    public async Task GetConsentedAppScopes_ServicePrincipalLookupForbidden_ReturnsNull()
    {
        var graph = new DemoGraphTestClient
        {
            Intercept = request => Path(request) == "/beta/servicePrincipals" ? DemoGraphTestClient.Forbidden() : null
        };

        Assert.Null(await GetConsentedAppScopesAsync(graph.Client, AppClientId));
    }

    [Fact]
    public async Task GetConsentedAppScopes_GrantLookupForbidden_ReturnsNull()
    {
        var graph = new DemoGraphTestClient
        {
            Intercept = request => Path(request) switch
            {
                "/beta/servicePrincipals" => ServicePrincipalFound(),
                "/beta/oauth2PermissionGrants" => DemoGraphTestClient.Forbidden(),
                _ => null
            }
        };

        Assert.Null(await GetConsentedAppScopesAsync(graph.Client, AppClientId));
    }

    [Fact]
    public async Task GetConsentedAppScopes_ServicePrincipalNotInTenant_ReturnsNull()
    {
        var graph = new DemoGraphTestClient
        {
            Intercept = request => Path(request) == "/beta/servicePrincipals"
                ? DemoGraphTestClient.Json(HttpStatusCode.OK, new JsonObject { ["value"] = new JsonArray() })
                : null
        };

        Assert.Null(await GetConsentedAppScopesAsync(graph.Client, AppClientId));
    }

    private static string Path(HttpRequestMessage request) => request.RequestUri!.AbsolutePath;

    private static HttpResponseMessage ServicePrincipalFound() => DemoGraphTestClient.Json(HttpStatusCode.OK, new JsonObject
    {
        ["value"] = new JsonArray(new JsonObject { ["id"] = ServicePrincipalId })
    });

    private static HttpResponseMessage Grants(params string[] scopes) => DemoGraphTestClient.Json(HttpStatusCode.OK, new JsonObject
    {
        ["value"] = new JsonArray(scopes.Select((scope, i) => (JsonNode)new JsonObject
        {
            ["id"] = $"grant-{i}",
            ["clientId"] = ServicePrincipalId,
            ["consentType"] = "AllPrincipals",
            ["scope"] = scope
        }).ToArray())
    });
}
