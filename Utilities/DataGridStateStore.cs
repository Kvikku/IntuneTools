using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Windows.Storage;

namespace IntuneTools.Utilities
{
    internal sealed class DataGridState
    {
        public List<DataGridColumnState> Columns { get; set; } = new();
        public string? SortProperty { get; set; }
        public bool SortDescending { get; set; }
        public List<string> SelectedIds { get; set; } = new();
    }

    internal sealed class DataGridColumnState
    {
        public string Key { get; set; } = string.Empty;
        public string Width { get; set; } = "Auto";
    }

    public static class DataGridStateStore
    {
        public static void Save(DataGrid dataGrid, string stateKey)
        {
            var state = new DataGridState();

            foreach (var col in dataGrid.Columns)
            {
                state.Columns.Add(new DataGridColumnState
                {
                    Key = GetColumnKey(col),
                    Width = SerializeWidth(col.Width)
                });
            }

            var sortedColumn = dataGrid.Columns.FirstOrDefault(c => c.SortDirection.HasValue);
            if (sortedColumn != null)
            {
                state.SortProperty = GetColumnKey(sortedColumn);
                state.SortDescending = sortedColumn.SortDirection == DataGridSortDirection.Descending;
            }

            state.SelectedIds = dataGrid.SelectedItems
                .OfType<CustomContentInfo>()
                .Select(i => i.ContentId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            ApplicationData.Current.LocalSettings.Values[stateKey] = JsonSerializer.Serialize(state);
        }

        public static void RestoreLayout(DataGrid dataGrid, string stateKey)
        {
            var state = Read(stateKey);
            if (state == null)
                return;

            foreach (var col in dataGrid.Columns)
            {
                var key = GetColumnKey(col);
                var saved = state.Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
                if (saved == null)
                    continue;

                col.Width = DeserializeWidth(saved.Width);
            }
        }

        public static void RestoreSelection(DataGrid dataGrid, string stateKey)
        {
            var state = Read(stateKey);
            if (state == null || state.SelectedIds.Count == 0 || dataGrid.ItemsSource == null)
                return;

            dataGrid.SelectedItems.Clear();
            foreach (var item in dataGrid.ItemsSource.OfType<CustomContentInfo>())
            {
                if (!string.IsNullOrWhiteSpace(item.ContentId) &&
                    state.SelectedIds.Contains(item.ContentId, StringComparer.OrdinalIgnoreCase))
                {
                    dataGrid.SelectedItems.Add(item);
                }
            }
        }

        public static void ApplySort(ObservableCollection<CustomContentInfo> contentList, DataGrid dataGrid, string stateKey)
        {
            var state = Read(stateKey);
            if (state == null || string.IsNullOrWhiteSpace(state.SortProperty) || contentList.Count == 0)
                return;

            var propInfo = typeof(CustomContentInfo).GetProperty(state.SortProperty);
            if (propInfo == null)
                return;

            var sorted = state.SortDescending
                ? contentList.OrderByDescending(x => propInfo.GetValue(x, null) ?? string.Empty).ToList()
                : contentList.OrderBy(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();

            contentList.Clear();
            foreach (var item in sorted)
            {
                contentList.Add(item);
            }

            foreach (var col in dataGrid.Columns)
            {
                col.SortDirection = null;
                if (string.Equals(GetColumnKey(col), state.SortProperty, StringComparison.OrdinalIgnoreCase))
                {
                    col.SortDirection = state.SortDescending
                        ? DataGridSortDirection.Descending
                        : DataGridSortDirection.Ascending;
                }
            }
        }

        private static DataGridState? Read(string stateKey)
        {
            var raw = ApplicationData.Current.LocalSettings.Values[stateKey] as string;
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                return JsonSerializer.Deserialize<DataGridState>(raw);
            }
            catch
            {
                return null;
            }
        }

        private static string GetColumnKey(DataGridColumn column)
        {
            if (column is DataGridTextColumn textColumn && textColumn.Binding is Binding binding)
            {
                var path = binding.Path?.Path;
                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }

            return column.Header?.ToString() ?? string.Empty;
        }

        private static string SerializeWidth(DataGridLength width)
        {
            if (width.IsAuto)
                return "Auto";
            if (width.IsStar)
                return $"{width.Value.ToString(CultureInfo.InvariantCulture)}*";

            return width.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static DataGridLength DeserializeWidth(string width)
        {
            if (string.Equals(width, "Auto", StringComparison.OrdinalIgnoreCase))
                return DataGridLength.Auto;

            if (width.EndsWith("*", StringComparison.Ordinal))
            {
                var raw = width.TrimEnd('*');
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var starValue))
                    return new DataGridLength(starValue, DataGridLengthUnitType.Star);
            }

            if (double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixelValue))
                return new DataGridLength(pixelValue);

            return DataGridLength.Auto;
        }
    }
}
