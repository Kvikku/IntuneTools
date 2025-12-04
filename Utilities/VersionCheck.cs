using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IntuneTools.Utilities
{
    public static class VersionCheck
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

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
        /// <returns>The latest version string (e.g., "v1.2.3"). Throws on HTTP/network or parse errors.</returns>
        public static async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken = default)
        {
            const string url = "https://api.github.com/repos/kvikku/IntuneTools/releases/latest";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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
        /// </summary>
        public static async Task<VersionStatus> CheckAsync(CancellationToken cancellationToken = default)
        {
            var current = GetCurrentVersionString();
            string latest;

            try
            {
                latest = await GetLatestVersionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // On failure, report no update with unknown latest.
                return new VersionStatus
                {
                    CurrentVersion = current,
                    LatestVersion = "unknown",
                    IsUpdateAvailable = false
                };
            }

            var isNewer = IsLatestNewer(latest, current);

            return new VersionStatus
            {
                CurrentVersion = current,
                LatestVersion = latest,
                IsUpdateAvailable = isNewer
            };
        }

        private static bool IsLatestNewer(string latestTag, string currentTag)
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

            // Fallback: if parse fails, do a string compare which is imperfect but safe.
            return string.Compare(latestNorm, currentNorm, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static string GetCurrentVersionString()
        {
            // Prefer informational version if present, otherwise assembly version.
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                // e.g., "1.2.3+commit" -> "1.2.3"
                var plusIdx = info.IndexOf('+');
                var clean = plusIdx >= 0 ? info[..plusIdx] : info;
                return clean.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? clean : $"v{clean}";
            }

            var version = asm.GetName().Version;
            var v = version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
            return $"v{v}";
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IntuneTools", "1.0"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(compatible; +https://github.com/Kvikku/IntuneTools)"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }
    }
}
