using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace IntuneTools.Graph.DemoMode;

/// <summary>
/// Terminal <see cref="HttpMessageHandler"/> for Demo Mode. Instead of calling
/// graph.microsoft.com, it routes each request against an in-memory <see cref="DemoDataStore"/>
/// and returns hand-built JSON that mirrors the shapes the app's Kiota-generated Graph SDK calls
/// expect (list envelopes, @odata.type discriminators, 204s for patch/delete/assign).
/// </summary>
internal sealed class DemoGraphMessageHandler : HttpMessageHandler
{
    private static readonly Regex ContainsFilterRegex = new(@"contains\(([\w./]+),\s*'(.*)'\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly DemoDataStore _store;

    public DemoGraphMessageHandler(DemoDataStore store)
    {
        _store = store;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content != null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return Route(request, body);
        }
        catch (Exception ex)
        {
            return JsonResponse(HttpStatusCode.InternalServerError, new JsonObject
            {
                ["error"] = new JsonObject { ["code"] = "DemoModeError", ["message"] = ex.Message }
            });
        }
    }

    private HttpResponseMessage Route(HttpRequestMessage request, string? body)
    {
        var segments = GetPathSegments(request.RequestUri!);
        if (segments.Length == 0) return NotFound();

        string? topLevel;
        string collection;
        string[] rest;

        if (segments[0] is "deviceManagement" or "deviceAppManagement")
        {
            if (segments.Length < 2) return NotFound();
            topLevel = segments[0];
            collection = segments[1];
            rest = segments[2..];
        }
        else
        {
            topLevel = null;
            collection = segments[0];
            rest = segments[1..];
        }

        var config = DemoResourceCatalog.Find(topLevel, collection);
        if (config == null) return NotFound();

        return HandleResource(config, rest, request, body);
    }

    private HttpResponseMessage HandleResource(DemoResourceConfig config, string[] rest, HttpRequestMessage request, string? body)
    {
        var method = request.Method;

        if (rest.Length == 0)
        {
            if (method == HttpMethod.Get) return HandleList(config, request);
            if (method == HttpMethod.Post && config.SupportsCreate) return HandleCreate(config, body);
        }
        else if (rest.Length == 1)
        {
            var id = Uri.UnescapeDataString(rest[0]);
            if (method == HttpMethod.Get) return HandleGetById(config, id);
            if (method == HttpMethod.Patch && config.SupportsMutation) return HandlePatch(config, id, body);
            if (method == HttpMethod.Delete && config.SupportsMutation) return HandleDelete(config, id);
        }
        else if (rest.Length == 2 && string.Equals(rest[1], "assignments", StringComparison.OrdinalIgnoreCase))
        {
            var id = Uri.UnescapeDataString(rest[0]);
            if (method == HttpMethod.Get) return HandleListAssignments(config, id, request);
        }
        else if (rest.Length == 2 && string.Equals(rest[1], "members", StringComparison.OrdinalIgnoreCase))
        {
            if (method == HttpMethod.Get) return JsonResponse(HttpStatusCode.OK, new JsonObject { ["value"] = new JsonArray() });
        }
        else if (rest.Length == 2 && string.Equals(rest[1], "assign", StringComparison.OrdinalIgnoreCase))
        {
            var id = Uri.UnescapeDataString(rest[0]);
            if (method == HttpMethod.Post && config.SupportsAssign) return HandleAssign(config, id, body);
        }
        else if (rest.Length == 3 && string.Equals(rest[1], "assignments", StringComparison.OrdinalIgnoreCase) && config.SupportsSingleAssignmentDelete)
        {
            var id = Uri.UnescapeDataString(rest[0]);
            var assignmentId = Uri.UnescapeDataString(rest[2]);
            if (method == HttpMethod.Delete) return HandleDeleteAssignment(config, id, assignmentId);
        }

        return NotFound();
    }

    private HttpResponseMessage HandleList(DemoResourceConfig config, HttpRequestMessage request)
    {
        var query = ParseQuery(request.RequestUri!.Query);
        IEnumerable<JsonNode?> items = _store.GetCollection(config.CollectionSegment);

        if (query.TryGetValue("$filter", out var filter) && !string.IsNullOrWhiteSpace(filter))
        {
            var parsed = ParseContainsFilter(filter);
            var field = parsed?.Field ?? config.DisplayNameProperty;
            var term = parsed?.Term;
            if (!string.IsNullOrEmpty(term))
            {
                items = items.Where(n => n is JsonObject o && (o[field]?.ToString() ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }
        else if (query.TryGetValue("$search", out var search) && !string.IsNullOrWhiteSpace(search))
        {
            var term = ParseSearchTerm(search);
            if (!string.IsNullOrEmpty(term))
            {
                items = items.Where(n => n is JsonObject o && (o[config.DisplayNameProperty]?.ToString() ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (query.TryGetValue("$top", out var topStr) && int.TryParse(topStr, out var top) && top >= 0)
        {
            items = items.Take(top);
        }

        var value = new JsonArray(items.Select(n => n!.DeepClone()).ToArray());
        return JsonResponse(HttpStatusCode.OK, new JsonObject { ["value"] = value });
    }

    private HttpResponseMessage HandleGetById(DemoResourceConfig config, string id)
    {
        var item = _store.GetById(config.CollectionSegment, id);
        return item == null ? NotFoundError() : JsonResponse(HttpStatusCode.OK, item);
    }

    private HttpResponseMessage HandleCreate(DemoResourceConfig config, string? body)
    {
        var node = (!string.IsNullOrWhiteSpace(body) ? JsonNode.Parse(body) as JsonObject : null) ?? new JsonObject();
        node["id"] = Guid.NewGuid().ToString();
        node["createdDateTime"] = DateTimeOffset.UtcNow.ToString("o");
        node["lastModifiedDateTime"] = DateTimeOffset.UtcNow.ToString("o");
        node["@odata.type"] ??= config.DefaultODataType;

        _store.Add(config.CollectionSegment, node);
        return JsonResponse(HttpStatusCode.Created, node);
    }

    private HttpResponseMessage HandlePatch(DemoResourceConfig config, string id, string? body)
    {
        var existing = _store.GetById(config.CollectionSegment, id);
        if (existing == null) return NotFoundError();

        if (!string.IsNullOrWhiteSpace(body) && JsonNode.Parse(body) is JsonObject patch)
        {
            foreach (var (key, value) in patch)
            {
                existing[key] = value?.DeepClone();
            }
        }
        existing["lastModifiedDateTime"] = DateTimeOffset.UtcNow.ToString("o");

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleDelete(DemoResourceConfig config, string id)
    {
        _store.Remove(config.CollectionSegment, id);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleListAssignments(DemoResourceConfig config, string id, HttpRequestMessage request)
    {
        var query = ParseQuery(request.RequestUri!.Query);
        IEnumerable<JsonNode?> items = _store.GetAssignments(config.CollectionSegment, id);

        if (query.TryGetValue("$top", out var topStr) && int.TryParse(topStr, out var top) && top >= 0)
        {
            items = items.Take(top);
        }

        var value = new JsonArray(items.Select(n => n!.DeepClone()).ToArray());
        return JsonResponse(HttpStatusCode.OK, new JsonObject { ["value"] = value });
    }

    private HttpResponseMessage HandleAssign(DemoResourceConfig config, string id, string? body)
    {
        var newAssignments = new JsonArray();

        if (!string.IsNullOrWhiteSpace(body) && JsonNode.Parse(body) is JsonObject obj)
        {
            var assignmentsNode = (obj["assignments"] ?? obj["mobileAppAssignments"]) as JsonArray;
            if (assignmentsNode != null)
            {
                foreach (var entry in assignmentsNode)
                {
                    if (entry is not JsonObject assignmentObj) continue;
                    var clone = (JsonObject)assignmentObj.DeepClone();
                    clone["id"] ??= Guid.NewGuid().ToString();
                    newAssignments.Add(clone);
                }
            }
        }

        _store.SetAssignments(config.CollectionSegment, id, newAssignments);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage HandleDeleteAssignment(DemoResourceConfig config, string id, string assignmentId)
    {
        var list = _store.GetAssignments(config.CollectionSegment, id);
        var match = list.OfType<JsonObject>().FirstOrDefault(o => (string?)o["id"] == assignmentId);
        if (match != null) list.Remove(match);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static string[] GetPathSegments(Uri uri)
    {
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && (parts[0] == "beta" || parts[0] == "v1.0"))
        {
            parts = parts[1..];
        }
        return parts;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) continue;
            var key = Uri.UnescapeDataString(pair[..idx]);
            var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
            result[key] = value;
        }
        return result;
    }

    private static (string Field, string Term)? ParseContainsFilter(string filter)
    {
        var match = ContainsFilterRegex.Match(filter);
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : null;
    }

    private static string ParseSearchTerm(string search)
    {
        var trimmed = search.Trim('"');
        var idx = trimmed.IndexOf(':');
        return idx >= 0 ? trimmed[(idx + 1)..] : trimmed;
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, JsonNode body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

    private static HttpResponseMessage NotFoundError() => JsonResponse(HttpStatusCode.NotFound, new JsonObject
    {
        ["error"] = new JsonObject { ["code"] = "ItemNotFound", ["message"] = "Demo item not found." }
    });
}
