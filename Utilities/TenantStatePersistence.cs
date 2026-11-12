namespace InToolz.Utilities
{
    /// <summary>
    /// Persists the last known source/destination tenant display names across app restarts, as a
    /// UI hint only. <see cref="InToolz.Graph.UserAuthenticationBase"/> builds its MSAL
    /// PublicClientApplication with no token-cache serialization, so there is no silently
    /// redeemable token across a restart — this just lets the tenant pills show "last used: X"
    /// before the user signs in again. Persisted as a plain JSON file under
    /// %LOCALAPPDATA%\InToolz\, same reasoning as <see cref="ThemeService"/>.
    /// </summary>
    public static class TenantStatePersistence
    {
        private sealed class TenantState
        {
            public string? Source { get; set; }
            public string? Destination { get; set; }
        }

        private static readonly string SettingsFilePath =
            Path.Combine(appDataPath, appFolderName, "tenantstate.json");

        /// <summary>
        /// Updates whichever name(s) are non-empty, leaving the other slot untouched — so a
        /// sign-out (which clears the live tenant name back to "") doesn't erase the hint for the
        /// next launch.
        /// </summary>
        public static void SaveLastKnownNames(string? source, string? destination)
        {
            try
            {
                var state = LoadState();

                if (!string.IsNullOrWhiteSpace(source))
                    state.Source = source;
                if (!string.IsNullOrWhiteSpace(destination))
                    state.Destination = destination;

                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(state));
            }
            catch
            {
                // Best-effort persistence — a write failure should never block the UI.
            }
        }

        public static (string? Source, string? Destination) LoadLastKnownNames()
        {
            var state = LoadState();
            return (state.Source, state.Destination);
        }

        private static TenantState LoadState()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<TenantState>(json) ?? new TenantState();
                }
            }
            catch
            {
                // Corrupt or unreadable settings file — start fresh rather than blocking the app.
            }

            return new TenantState();
        }
    }
}
