using System.Net.Http;
using System.Runtime.CompilerServices;
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
    /// <summary>
    /// Every client this service has handed out, so callers can ask whether a given tenant is
    /// backed by sample data. Keyed on the client itself rather than a global "demo mode is on"
    /// flag, which would drift the moment one tenant is signed back in to a live tenant while
    /// the other stays on sample data.
    /// </summary>
    private static readonly ConditionalWeakTable<GraphServiceClient, DemoDataStore> DemoClients = new();

    /// <summary>
    /// True when <paramref name="client"/> was created by Demo Mode and therefore talks to an
    /// in-memory store instead of graph.microsoft.com.
    /// </summary>
    public static bool IsDemoClient(GraphServiceClient? client) =>
        client != null && DemoClients.TryGetValue(client, out _);

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
        var client = new GraphServiceClient(httpClient, new NoOpAuthenticationProvider());
        DemoClients.Add(client, store);
        return client;
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
