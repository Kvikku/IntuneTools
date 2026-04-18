using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Windows.Storage;

namespace IntuneTools.Utilities
{
    public sealed class RecentActivityEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Status { get; set; } = "Info";
        public string Message { get; set; } = string.Empty;
        public string TimestampText => Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static class RecentActivityStore
    {
        private const string SettingsKey = "Home.RecentActivity";
        private const int MaxItems = 30;

        public static void Add(string message, string status = "Info")
        {
            var items = GetRecentInternal();
            items.Insert(0, new RecentActivityEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Status = status,
                Message = message
            });

            if (items.Count > MaxItems)
            {
                items = items.Take(MaxItems).ToList();
            }

            Save(items);
        }

        public static IReadOnlyList<RecentActivityEntry> GetRecent(int limit = 8)
        {
            return GetRecentInternal()
                .Take(Math.Max(1, limit))
                .ToList();
        }

        private static List<RecentActivityEntry> GetRecentInternal()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var raw = settings.Values[SettingsKey] as string;
            if (string.IsNullOrWhiteSpace(raw))
                return new List<RecentActivityEntry>();

            try
            {
                return JsonSerializer.Deserialize<List<RecentActivityEntry>>(raw) ?? new List<RecentActivityEntry>();
            }
            catch
            {
                return new List<RecentActivityEntry>();
            }
        }

        private static void Save(List<RecentActivityEntry> items)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[SettingsKey] = JsonSerializer.Serialize(items);
        }
    }
}
