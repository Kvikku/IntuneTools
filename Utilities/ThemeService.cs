using Microsoft.UI.Xaml;

namespace InToolz.Utilities
{
    /// <summary>
    /// Applies and persists the user's Light/Dark/"Match Windows" theme choice (Settings →
    /// Theme). Persisted as a plain JSON file under %LOCALAPPDATA%\InToolz\ rather than
    /// Windows.Storage.ApplicationData, which throws for this unpackaged build — same
    /// reasoning as <see cref="PageStatePersistence"/>.
    /// </summary>
    public static class ThemeService
    {
        private sealed class ThemeState
        {
            public string Theme { get; set; } = nameof(ElementTheme.Default);
        }

        private static readonly string SettingsFilePath =
            Path.Combine(appDataPath, appFolderName, "theme.json");

        /// <summary>Raised after a theme change has been applied and persisted.</summary>
        public static event Action<ElementTheme>? ThemeChanged;

        public static ElementTheme LoadSavedTheme()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var state = JsonSerializer.Deserialize<ThemeState>(json);
                    if (state != null && Enum.TryParse<ElementTheme>(state.Theme, out var parsed))
                    {
                        return parsed;
                    }
                }
            }
            catch
            {
                // Corrupt or unreadable settings file — fall back to following the OS theme.
            }

            return ElementTheme.Default;
        }

        /// <summary>
        /// Applies the theme to the main window's root element and persists the choice.
        /// </summary>
        public static void ApplyTheme(ElementTheme theme)
        {
            if (App.MainWindowInstance?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }

            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(new ThemeState { Theme = theme.ToString() });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Best-effort persistence — a write failure should never block theme switching.
            }

            ThemeChanged?.Invoke(theme);
        }
    }
}
