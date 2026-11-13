namespace InToolz.Utilities
{
    /// <summary>String keys shared between the dashboard's stat tiles and whatever writes a cached scan result.</summary>
    public static class ScanKeys
    {
        public const string Unassigned = "Unassigned";
        public const string Duplicates = "Duplicates";
        public const string StaleDevices = "StaleDevices";
    }

    public sealed record DashboardStat(int Count, DateTime ScannedAtUtc);

    /// <summary>
    /// Caches the last known result of each Cleanup scan (unassigned items, duplicates,
    /// stale devices) so the dashboard can show "last scanned Xh ago" instead of forcing
    /// an expensive full-tenant scan on every navigation. Nothing writes to this cache
    /// yet — CleanupPage starts feeding it once its three scans are restyled — so every
    /// tile shows "not yet scanned" until then. Follows the same unpackaged-safe JSON
    /// file pattern as <see cref="PageStatePersistence"/>.
    /// </summary>
    public static class DashboardStatsCache
    {
        private sealed class CacheState
        {
            public Dictionary<string, DashboardStat> Stats { get; set; } = new();
        }

        private static readonly string SettingsFilePath =
            Path.Combine(appDataPath, appFolderName, "dashboardstats.json");

        private static readonly object SyncRoot = new();
        private static CacheState? _cache;

        private static CacheState Load()
        {
            if (_cache != null) return _cache;

            try
            {
                _cache = File.Exists(SettingsFilePath)
                    ? JsonSerializer.Deserialize<CacheState>(File.ReadAllText(SettingsFilePath)) ?? new CacheState()
                    : new CacheState();
            }
            catch
            {
                // Corrupt or unreadable settings file — start fresh rather than blocking the dashboard.
                _cache = new CacheState();
            }

            return _cache;
        }

        private static void Save(CacheState state)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Best-effort persistence — a write failure should never break the dashboard.
            }
        }

        public static void SaveResult(string scanKey, int count)
        {
            lock (SyncRoot)
            {
                var state = Load();
                state.Stats[scanKey] = new DashboardStat(count, DateTime.UtcNow);
                Save(state);
            }
        }

        /// <summary>Returns null when this scan has never been run, so callers can show a "not yet scanned" state.</summary>
        public static DashboardStat? GetResult(string scanKey)
        {
            lock (SyncRoot)
            {
                return Load().Stats.TryGetValue(scanKey, out var stat) ? stat : null;
            }
        }
    }
}
