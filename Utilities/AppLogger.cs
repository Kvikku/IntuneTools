using Microsoft.UI.Dispatching;
using System.Diagnostics;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Unified logging hub. Every log call writes to the appropriate .log file and,
    /// if a page is active, also pushes the entry to the UI log console.
    /// </summary>
    public static class AppLogger
    {
        private static Action<LogEntry>? _uiHandler;
        private static DispatcherQueue? _dispatcher;

        /// <summary>
        /// Called by the active page when it becomes the foreground view.
        /// </summary>
        public static void RegisterUiHandler(Action<LogEntry> handler, DispatcherQueue dispatcher)
        {
            _uiHandler = handler;
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Called by the active page when it navigates away.
        /// </summary>
        public static void UnregisterUiHandler()
        {
            _uiHandler = null;
            _dispatcher = null;
        }

        public static void Log(LogLevel level, string message, appFunction function = appFunction.Main)
        {
            var entry = new LogEntry(level, message);
            WriteToFile(function, entry);
            PushToUi(entry);
        }

        public static void Info(string message, appFunction function = appFunction.Main)
            => Log(LogLevel.Info, message, function);

        public static void Success(string message, appFunction function = appFunction.Main)
            => Log(LogLevel.Success, message, function);

        public static void Warning(string message, appFunction function = appFunction.Main)
            => Log(LogLevel.Warning, message, function);

        public static void Error(string message, appFunction function = appFunction.Main)
            => Log(LogLevel.Error, message, function);

        public static void UiOnly(string message)
            => PushToUi(new LogEntry(LogLevel.Info, message));

        private static void PushToUi(LogEntry entry)
        {
            if (_uiHandler == null) return;

            if (_dispatcher != null && !_dispatcher.HasThreadAccess)
                _dispatcher.TryEnqueue(() => _uiHandler(entry));
            else
                _uiHandler(entry);
        }

        private static void WriteToFile(appFunction function, LogEntry entry)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(timestampedAppFolder))
                    timestampedAppFolder = HelperClass.CreateTimestampedAppFolder();

                var logFilePath = Path.Combine(timestampedAppFolder, $"{function}.log");

                var levelLabel = entry.Level switch
                {
                    LogLevel.Warning => "Warning",
                    LogLevel.Error => "Error",
                    _ => "Info"
                };

                var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} - [{levelLabel}] - {entry.Message}";
                File.AppendAllText(logFilePath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppLogger: failed to write to file: {ex.Message}");
            }
        }
    }
}
