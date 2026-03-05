using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Log severity levels for the log console.
    /// </summary>
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Represents a single log entry with timestamp, level, and message.
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public string Message { get; }

        /// <summary>
        /// Formatted timestamp string for display (HH:mm:ss).
        /// </summary>
        public string TimestampText => Timestamp.ToString("HH:mm:ss");

        /// <summary>
        /// Gets the foreground brush based on log level.
        /// </summary>
        public SolidColorBrush Foreground => Level switch
        {
            LogLevel.Success => new SolidColorBrush(Colors.LimeGreen),
            LogLevel.Warning => new SolidColorBrush(Colors.Orange),
            LogLevel.Error => new SolidColorBrush(Colors.Tomato),
            _ => new SolidColorBrush(Colors.White) // Info uses default text color
        };

        /// <summary>
        /// Gets the level indicator symbol for display.
        /// </summary>
        public string LevelIndicator => Level switch
        {
            LogLevel.Success => "\u2714", // ✔
            LogLevel.Warning => "\u26A0", // ⚠
            LogLevel.Error => "\u2716",   // ✖
            _ => "\u2022"                 // • (bullet for info)
        };

        public LogEntry(LogLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
        }

        /// <summary>
        /// Creates an Info-level log entry.
        /// </summary>
        public static LogEntry Info(string message) => new(LogLevel.Info, message);

        /// <summary>
        /// Creates a Success-level log entry.
        /// </summary>
        public static LogEntry Success(string message) => new(LogLevel.Success, message);

        /// <summary>
        /// Creates a Warning-level log entry.
        /// </summary>
        public static LogEntry Warning(string message) => new(LogLevel.Warning, message);

        /// <summary>
        /// Creates an Error-level log entry.
        /// </summary>
        public static LogEntry Error(string message) => new(LogLevel.Error, message);
    }
}
