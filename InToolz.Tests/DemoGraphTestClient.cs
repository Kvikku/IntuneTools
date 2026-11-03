using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using IntuneTools.Graph.DemoMode;
using Microsoft.Graph.Beta;
using Microsoft.Kiota.Abstractions.Authentication;

namespace IntuneTools.Tests;

/// <summary>
/// A real <see cref="GraphServiceClient"/> backed by Demo Mode's in-memory store, so tests run the
/// app's helper classes and the Graph SDK's own serialization end to end without a tenant.
/// Every request is recorded, and <see cref="Intercept"/> can answer one before it reaches the
/// store — to fake a 403, or an endpoint Demo Mode doesn't implement.
/// </summary>
internal sealed class DemoGraphTestClient
{
    public DemoDataStore Store { get; }
    public GraphServiceClient Client { get; }
    public List<(HttpMethod Method, Uri Uri)> Requests { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage?>? Intercept { get; set; }

    public DemoGraphTestClient(DemoDataStore? store = null)
    {
        Store = store ?? new DemoDataStore("Test Tenant");
        var handler = new RecordingHandler(this) { InnerHandler = new DemoGraphMessageHandler(Store) };
        Client = new GraphServiceClient(new HttpClient(handler), new AnonymousAuthenticationProvider());
    }

    public string AddManagedDevice(string deviceName, int? daysSinceSync)
    {
        var id = Guid.NewGuid().ToString();
        Store.Add("managedDevices", new JsonObject
        {
            ["id"] = id,
            ["@odata.type"] = "#microsoft.graph.managedDevice",
            ["deviceName"] = deviceName,
            ["operatingSystem"] = "Windows",
            ["osVersion"] = "10.0.22631",
            ["lastSyncDateTime"] = daysSinceSync.HasValue ? DateTimeOffset.UtcNow.AddDays(-daysSinceSync.Value).ToString("o") : null,
            ["userPrincipalName"] = $"{deviceName.ToLowerInvariant()}@contoso.test",
            ["complianceState"] = "compliant",
            ["serialNumber"] = $"SN-{deviceName}"
        });
        return id;
    }

    public string AddEntraDevice(string displayName, int? daysSinceSignIn)
    {
        var id = Guid.NewGuid().ToString();
        Store.Add("devices", new JsonObject
        {
            ["id"] = id,
            ["@odata.type"] = "#microsoft.graph.device",
            ["displayName"] = displayName,
            ["operatingSystem"] = "Windows",
            ["operatingSystemVersion"] = "10.0.22631",
            ["trustType"] = "AzureAd",
            ["accountEnabled"] = true,
            ["approximateLastSignInDateTime"] = daysSinceSignIn.HasValue ? DateTimeOffset.UtcNow.AddDays(-daysSinceSignIn.Value).ToString("o") : null
        });
        return id;
    }

    public static HttpResponseMessage Json(HttpStatusCode status, JsonNode body) => new(status)
    {
        Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
    };

    /// <summary>
    /// What Graph returns when the signed-in user's token lacks the scope a call needs.
    /// </summary>
    public static HttpResponseMessage Forbidden() => Json(HttpStatusCode.Forbidden, new JsonObject
    {
        ["error"] = new JsonObject
        {
            ["code"] = "Authorization_RequestDenied",
            ["message"] = "Insufficient privileges to complete the operation."
        }
    });

    private sealed class RecordingHandler : DelegatingHandler
    {
        private readonly DemoGraphTestClient _owner;

        public RecordingHandler(DemoGraphTestClient owner)
        {
            _owner = owner;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _owner.Requests.Add((request.Method, request.RequestUri!));

            var intercepted = _owner.Intercept?.Invoke(request);
            return intercepted != null ? Task.FromResult(intercepted) : base.SendAsync(request, cancellationToken);
        }
    }
}
