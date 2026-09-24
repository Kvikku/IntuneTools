namespace IntuneTools.Utilities
{
    /// <summary>
    /// Persists lightweight, per-page UI preferences — selected content-type filters and
    /// the last search query — across app sessions, so users aren't forced to re-tick the
    /// same checkboxes or retype the same query every time they open a page.
    ///
    /// Deliberately avoids Windows.Storage.ApplicationData: InToolz can run unpackaged
    /// (see the GitHub Releases build, and IntuneTools.csproj has EnableMsixTooling=false —
    /// packaging is a separate wrapper project). ApplicationData.Current throws for processes
    /// without package identity, so a plain JSON file next to the app's log folder is used
    /// instead, matching the pattern HelperClass/Variables already use for the log directory.
    /// </summary>
    public static class PageStatePersistence
    {
        private sealed class PageState
        {
            public List<string> ContentTypes { get; set; } = new();
            public string LastSearchQuery { get; set; } = string.Empty;
        }

        private static readonly string SettingsFilePath =
            Path.Combine(appDataPath, appFolderName, "pagestate.json");

        private static readonly object SyncRoot = new();
        private static Dictionary<string, PageState>? _cache;

        private static Dictionary<string, PageState> LoadAll()
        {
            if (_cache != null) return _cache;

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    _cache = JsonSerializer.Deserialize<Dictionary<string, PageState>>(json)
                             ?? new Dictionary<string, PageState>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    _cache = new Dictionary<string, PageState>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Corrupt or unreadable settings file — start fresh rather than blocking the app.
                _cache = new Dictionary<string, PageState>(StringComparer.OrdinalIgnoreCase);
            }

            return _cache;
        }

        private static void SaveAll(Dictionary<string, PageState> state)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Best-effort persistence — a write failure (locked file, permissions, etc.)
                // should never break the UI.
            }
        }

        /// <summary>
        /// Saves the set of selected content-type keys for the given page.
        /// </summary>
        public static void SaveContentTypeSelection(string pageKey, IEnumerable<string> selectedTypeKeys)
        {
            lock (SyncRoot)
            {
                var all = LoadAll();
                if (!all.TryGetValue(pageKey, out var state))
                {
                    state = new PageState();
                    all[pageKey] = state;
                }
                state.ContentTypes = selectedTypeKeys.Distinct().ToList();
                SaveAll(all);
            }
        }

        /// <summary>
        /// Loads the previously saved content-type selection for the given page.
        /// Returns null if nothing has been saved yet, so callers can fall back to their
        /// own default (e.g. "select all").
        /// </summary>
        public static HashSet<string>? LoadContentTypeSelection(string pageKey)
        {
            lock (SyncRoot)
            {
                var all = LoadAll();
                return all.TryGetValue(pageKey, out var state)
                    ? new HashSet<string>(state.ContentTypes)
                    : null;
            }
        }

        /// <summary>
        /// Saves the last search query text entered on the given page.
        /// </summary>
        public static void SaveLastSearchQuery(string pageKey, string query)
        {
            lock (SyncRoot)
            {
                var all = LoadAll();
                if (!all.TryGetValue(pageKey, out var state))
                {
                    state = new PageState();
                    all[pageKey] = state;
                }
                state.LastSearchQuery = query ?? string.Empty;
                SaveAll(all);
            }
        }

        /// <summary>
        /// Loads the last search query text for the given page, or an empty string if none
        /// was saved yet.
        /// </summary>
        public static string LoadLastSearchQuery(string pageKey)
        {
            lock (SyncRoot)
            {
                var all = LoadAll();
                return all.TryGetValue(pageKey, out var state) ? state.LastSearchQuery : string.Empty;
            }
        }
    }
}
