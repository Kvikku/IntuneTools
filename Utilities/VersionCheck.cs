using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IntuneTools.Utilities
{
    public static class VersionCheck
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(2);
        private static readonly SemaphoreSlim _cacheSemaphore = new(1, 1);
        private static VersionStatus? _cachedStatus;
        private static DateTime _cacheExpiry = DateTime.MinValue;

        public sealed class VersionStatus
        {
            public string CurrentVersion { get; init; } = string.Empty;
            public string LatestVersion { get; init; } = string.Empty;
            public bool IsUpdateAvailable { get; init; }
        }

        /// <summary>
        /// Gets the latest release version string from the GitHub API.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The latest version string (e.g., "1.2.0.0"). Throws on HTTP/network or parse errors.</returns>
        private static async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken = default)
        {
            const string url = "https://api.github.com/repos/kvikku/IntuneTools/releases/latest";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;

            if (root.TryGetProperty("tag_name", out var tagName) && tagName.ValueKind == JsonValueKind.String)
            {
                return tagName.GetString()!;
            }

            if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            {
                return name.GetString()!;
            }

            throw new InvalidOperationException("Latest release JSON does not contain 'tag_name' or 'name'.");
        }

        /// <summary>
        /// Checks the running app's version against GitHub's latest release.
        /// Results are cached for 15 minutes to avoid unnecessary API calls.
        /// </summary>
        public static async Task<VersionStatus> CheckAsync(CancellationToken cancellationToken = default)
        {
            await _cacheSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cachedStatus is not null && DateTime.UtcNow < _cacheExpiry)
                {
                    return _cachedStatus;
                }

                var current = GetCurrentVersionString();
                string latest;

                try
                {
                    latest = await GetLatestVersionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // On failure, report no update with unknown latest.
                    // Cache failures briefly (2 min) to avoid hanging on every navigation during outages.
                    var failureStatus = new VersionStatus
                    {
                        CurrentVersion = current,
                        LatestVersion = "unknown",
                        IsUpdateAvailable = false
                    };
                    _cachedStatus = failureStatus;
                    _cacheExpiry = DateTime.UtcNow + FailureCacheDuration;
                    return failureStatus;
                }

                var isNewer = IsLatestNewer(latest, current);

                var status = new VersionStatus
                {
                    CurrentVersion = current,
                    LatestVersion = latest,
                    IsUpdateAvailable = isNewer
                };

                _cachedStatus = status;
                _cacheExpiry = DateTime.UtcNow + CacheDuration;

                return status;
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }

        internal static bool IsLatestNewer(string latestTag, string currentTag)
        {
            // Normalize tags like "v1.2.3" -> "1.2.3"
            static string Normalize(string v)
            {
                if (string.IsNullOrWhiteSpace(v)) return "0.0.0";
                v = v.Trim();
                if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                {
                    v = v[1..];
                }
                return v;
            }

            var latestNorm = Normalize(latestTag);
            var currentNorm = Normalize(currentTag);

            if (Version.TryParse(latestNorm, out var latest) && Version.TryParse(currentNorm, out var current))
            {
                return latest > current;
            }

            // If either version string can't be parsed, we can't reliably compare.
            // Assume no update rather than risk a false positive from string comparison.
            return false;
        }

        internal static string GetCurrentVersionString()
        {
            return appVersion;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IntuneTools", "1.0"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(compatible; +https://github.com/Kvikku/IntuneTools)"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }
    }
}
