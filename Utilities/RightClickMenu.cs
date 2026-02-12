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

        public static void AttachDataGridContextMenu(DataGrid dataGrid)
        {
            if (dataGrid == null)
                return;

            var menuFlyout = new MenuFlyout();
            var menuUpdaters = new List<Action<DataGridContext>>();

            var copyItem = CreateCopyCellMenuItem();
            menuFlyout.Items.Add(copyItem);
            menuUpdaters.Add(context => UpdateCopyCellMenuItem(copyItem, context));

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
