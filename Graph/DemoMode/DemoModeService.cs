using System.Net.Http;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace IntuneTools.Graph.DemoMode;

/// <summary>
/// Entry point for enabling Demo Mode: builds a <see cref="GraphServiceClient"/> wired to an
/// in-memory fake Graph backend instead of a live tenant, so every page can be exercised without
/// signing in anywhere. Source and destination get independent stores, like two real tenants.
/// </summary>
internal static class DemoModeService
{
    public static (GraphServiceClient Client, string TenantName) CreateSourceTenant()
    {
        var store = DemoDataStore.CreateSourceSeed();
        return (BuildClient(store), store.TenantDisplayName);
    }

    public static (GraphServiceClient Client, string TenantName) CreateDestinationTenant()
    {
        var store = DemoDataStore.CreateDestinationSeed();
        return (BuildClient(store), store.TenantDisplayName);
    }

    private static GraphServiceClient BuildClient(DemoDataStore store)
    {
        var httpClient = new HttpClient(new DemoGraphMessageHandler(store));
        return new GraphServiceClient(httpClient, new NoOpAuthenticationProvider());
    }

    /// <summary>
    /// Demo Mode never sends real requests, so no Authorization header is needed.
    /// </summary>
    private sealed class NoOpAuthenticationProvider : IAuthenticationProvider
    {
        public Task AuthenticateRequestAsync(
            RequestInformation request,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
