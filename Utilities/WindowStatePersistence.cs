namespace InToolz.Utilities
{
    /// <summary>
    /// Persists the main window's size and position across app restarts. Plain JSON file under
    /// %LOCALAPPDATA%\InToolz\, same reasoning as <see cref="ThemeService"/>.
    /// </summary>
    public static class WindowStatePersistence
    {
        private sealed class WindowState
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private static readonly string SettingsFilePath =
            Path.Combine(appDataPath, appFolderName, "windowstate.json");

        public static void SaveBounds(int x, int y, int width, int height)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var state = new WindowState { X = x, Y = y, Width = width, Height = height };
                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(state));
            }
            catch
            {
                // Best-effort persistence — a write failure should never block the UI.
            }
        }

        /// <summary>
        /// Returns the saved bounds, or null if nothing was saved yet or the saved size was
        /// nonsensical (so the caller can fall back to its own default).
        /// </summary>
        public static (int X, int Y, int Width, int Height)? TryLoadBounds()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var state = JsonSerializer.Deserialize<WindowState>(json);
                    if (state != null && state.Width > 0 && state.Height > 0)
                        return (state.X, state.Y, state.Width, state.Height);
                }
            }
            catch
            {
                // Corrupt or unreadable settings file — fall back to the caller's default.
            }

            return null;
        }
    }
}
