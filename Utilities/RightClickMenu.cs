using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace IntuneTools.Utilities
{
    public class RightClickMenu
    {
        #region Datagrid context menu helper methods

        private sealed class DataGridContext
        {
            public DataGridContext(DataGrid dataGrid, DataGridCell cell, DataGridRow? row, string? cellText)
            {
                DataGrid = dataGrid;
                Cell = cell;
                Row = row;
                CellText = cellText;
            }
            public DataGrid DataGrid { get; }
            public DataGridCell Cell { get; }
            public DataGridRow? Row { get; }
            public string? CellText { get; }
        }

        public static void AttachDataGridContextMenu(DataGrid dataGrid)
        {
            if (dataGrid == null)
                return;

            var menuFlyout = new MenuFlyout();
            var menuUpdaters = new List<Action<DataGridContext>>();

            var copyItem = CreateCopyCellMenuItem();
            menuFlyout.Items.Add(copyItem);
            menuUpdaters.Add(context => UpdateCopyCellMenuItem(copyItem, context));

            var lookupItem = CreateLookupMenuItem();
            menuFlyout.Items.Add(lookupItem);
            menuUpdaters.Add(context => UpdateLookupMenuItem(lookupItem, context));

            dataGrid.RightTapped += (_, e) =>
            {
                var cell = FindParent<DataGridCell>(e.OriginalSource as DependencyObject);
                if (cell == null)
                    return;

                var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
                var cellText = GetCellText(cell);

                var context = new DataGridContext(dataGrid, cell, row, cellText);

                foreach (var update in menuUpdaters)
                    update(context);

                menuFlyout.ShowAt(dataGrid, e.GetPosition(dataGrid));
                e.Handled = true;
            };

            dataGrid.ContextFlyout = menuFlyout;
        }

        private static MenuFlyoutItem CreateCopyCellMenuItem()
        {
            var copyItem = new MenuFlyoutItem { Text = "Copy cell" };

            copyItem.Click += (_, __) =>
            {
                if (copyItem.Tag is not string text || string.IsNullOrWhiteSpace(text))
                    return;

                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);
            };

            return copyItem;
        }

        private static void UpdateCopyCellMenuItem(MenuFlyoutItem item, DataGridContext context)
        {
            item.Tag = context.CellText ?? string.Empty;
            item.IsEnabled = !string.IsNullOrWhiteSpace(context.CellText);
        }

        private static MenuFlyoutItem CreateLookupMenuItem()
        {
            var lookupItem = new MenuFlyoutItem { Text = "Lookup" };

            lookupItem.Click += (_, __) =>
            {
                if (lookupItem.Tag is not string url || string.IsNullOrWhiteSpace(url))
                    return;

                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            };

            return lookupItem;
        }

        private static void UpdateLookupMenuItem(MenuFlyoutItem item, DataGridContext context)
        {
            var url = TryBuildLookupUrl(context);
            item.Tag = url ?? string.Empty;
            item.IsEnabled = !string.IsNullOrWhiteSpace(url);
        }

        private static string? TryBuildLookupUrl(DataGridContext context)
        {
            if (context.Row == null)
                return null;

            var contentType = GetRowCellText(context.DataGrid, context.Row, 1);
            var id = GetRowCellText(context.DataGrid, context.Row, 3);

            if (string.IsNullOrWhiteSpace(contentType) || string.IsNullOrWhiteSpace(id))
                return null;

            if (!TryGetLookupUrlTemplate(contentType, out var template))
                return null;

            return template.Replace("INSERT_ID_HERE", id, StringComparison.Ordinal);
        }

        private static bool TryGetLookupUrlTemplate(string contentType, out string template)
        {
            var trimmed = contentType.Trim();

            if (trimmed.StartsWith("App", StringComparison.OrdinalIgnoreCase))
            {
                template = "https://intune.microsoft.com/#view/Microsoft_Intune_Apps/SettingsMenu/~/0/appId/INSERT_ID_HERE";
                return true;
            }

            var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Settings Catalog"] = "https://intune.microsoft.com/#view/Microsoft_Intune_Workflows/PolicySummaryBlade/policyId/INSERT_ID_HERE/isAssigned~/true/technology/mdm/templateId//platformName/windows10",
                ["Device Compliance Policy"] = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesMenu/~/compliance",
                ["Device Compliance"] = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesMenu/~/compliance",
                ["Application"] = "https://intune.microsoft.com/#view/Microsoft_Intune_Apps/SettingsMenu/~/0/appId/INSERT_ID_HERE",
                ["Windows Autopilot Profile"] = "https://intune.microsoft.com/#view/Microsoft_Intune_Enrollment/AutopilotMenuBlade/~/overview/apProfileId/INSERT_ID_HERE",
                ["Entra Group"] = "https://intune.microsoft.com/#view/Microsoft_AAD_IAM/GroupDetailsMenuBlade/~/Overview/groupId/INSERT_ID_HERE/menuId/",
                ["Assignment Filter"] = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/AssignmentFilterSummaryBlade/assignmentFilterId/INSERT_ID_HERE/filterType~/2,",
                ["MacOS Shell Script"] = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/ConfigureWMPolicyMenuBlade/~/overview/policyId/INSERT_ID_HERE/policyType~/1",

                // Note : For "Device Configuration Profile", the template is more complex and may require additional context to determine the correct URL, so it's not included in this dictionary
                // Note 2: The other templates require special formatting of the URL that may not be achievable with a simple template replacement, so they are also not included in this dictionary. If needed, they can be added with custom logic to handle the URL formatting.

            };

            return templates.TryGetValue(trimmed, out template);
        }

        private static string? GetRowCellText(DataGrid dataGrid, DataGridRow row, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dataGrid.Columns.Count)
                return null;

            var content = dataGrid.Columns[columnIndex].GetCellContent(row);

            if (content is TextBlock textBlock)
                return textBlock.Text;

            if (content is FrameworkElement element)
            {
                var innerTextBlock = FindChild<TextBlock>(element);
                if (innerTextBlock != null)
                    return innerTextBlock.Text;
            }

            return content?.ToString();
        }

        private static string? GetCellText(DataGridCell? cell)
        {
            if (cell == null)
                return null;

            if (cell.Content is TextBlock textBlock)
                return textBlock.Text;

            if (cell.Content is FrameworkElement element)
            {
                var innerTextBlock = FindChild<TextBlock>(element);
                if (innerTextBlock != null)
                    return innerTextBlock.Text;
            }

            return cell.Content?.ToString();
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;

                var nested = FindChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T match)
                    return match;

                element = VisualTreeHelper.GetParent(element);
            }

            return null;
        }


        #endregion
    }
}
