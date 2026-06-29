using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Graph.EntraHelperClasses;
using IntuneTools.Graph.IntuneHelperClasses;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
using static IntuneTools.Graph.IntuneHelperClasses.macOSShellScript;
using static IntuneTools.Graph.IntuneHelperClasses.PowerShellScriptsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ProactiveRemediationsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsAutoPilotHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsDriverUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsFeatureUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdatePolicyHandler;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;

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

        private sealed record AssignmentTarget(string ContentId, string ContentType);

        /// <summary>
        /// Attaches a right-click context menu to a DataGrid.
        /// Pass getGraphClient to enable the "View assignments" item.
        /// </summary>
        public static void AttachDataGridContextMenu(DataGrid dataGrid, Func<GraphServiceClient?>? getGraphClient = null)
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

            if (getGraphClient != null)
            {
                menuFlyout.Items.Add(new MenuFlyoutSeparator());
                var viewItem = CreateViewAssignmentsMenuItem(getGraphClient);
                menuFlyout.Items.Add(viewItem);
                menuUpdaters.Add(context => UpdateViewAssignmentsMenuItem(viewItem, context));
            }

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

            copyItem.Click += async (_, __) =>
            {
                if (copyItem.Tag is not string text || string.IsNullOrWhiteSpace(text))
                    return;

                try
                {
                    var package = new DataPackage();
                    package.SetText(text);
                    Clipboard.SetContent(package);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Copy failed to set clipboard content. {ex}", appFunction.Main);
                    await ShowErrorDialogAsync("Copy failed", "The clipboard is unavailable or blocked. Please try again.");
                }
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

            lookupItem.Click += async (_, __) =>
            {
                if (lookupItem.Tag is not string url || string.IsNullOrWhiteSpace(url))
                {
                    AppLogger.Warning("Lookup failed: empty URL.", appFunction.Main);
                    await ShowErrorDialogAsync("Lookup failed", "No URL was available for this item.");
                    return;
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    AppLogger.Warning($"Lookup failed: invalid URL '{url}'.", appFunction.Main);
                    await ShowErrorDialogAsync("Lookup failed", $"The lookup URL is invalid:\n{url}");
                    return;
                }

                try
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo
                    {
                        FileName = uri.AbsoluteUri,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Lookup failed to open URL '{uri.AbsoluteUri}'. {ex}", appFunction.Main);
                    await ShowErrorDialogAsync("Lookup failed", "The lookup page could not be opened. Please try again.");
                }
            };

            return lookupItem;
        }

        private static void UpdateLookupMenuItem(MenuFlyoutItem item, DataGridContext context)
        {
            var url = TryBuildLookupUrl(context);
            item.Tag = url ?? string.Empty;
            item.IsEnabled = !string.IsNullOrWhiteSpace(url);
        }

        private static MenuFlyoutItem CreateViewAssignmentsMenuItem(Func<GraphServiceClient?> getGraphClient)
        {
            var viewItem = new MenuFlyoutItem { Text = "View assignments" };

            viewItem.Click += async (_, __) =>
            {
                if (viewItem.Tag is not AssignmentTarget target)
                    return;

                var client = getGraphClient();
                if (client == null)
                {
                    await ShowErrorDialogAsync("Not authenticated", "Please authenticate with a tenant before viewing assignments.");
                    return;
                }

                var registry = GetAssignmentViewRegistry();
                if (!registry.TryGetValue(target.ContentType, out var getAssignments))
                {
                    if (target.ContentType.StartsWith("App - ", StringComparison.OrdinalIgnoreCase))
                        getAssignments = ApplicationHelper.GetApplicationAssignmentDetailsAsync;
                    else
                    {
                        await ShowErrorDialogAsync("Not supported", $"Assignment lookup is not supported for '{target.ContentType}'.");
                        return;
                    }
                }

                try
                {
                    var assignments = await getAssignments(client, target.ContentId) ?? new List<AssignmentInfo>();

                    var groupIds = assignments
                        .Where(a => !string.IsNullOrEmpty(a.GroupId))
                        .Select(a => a.GroupId!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var groupNames = groupIds.Count > 0
                        ? await GroupHelperClass.ResolveGroupNamesAsync(client, groupIds)
                        : new Dictionary<string, string>();

                    await ShowAssignmentsDialogAsync(target.ContentType, assignments, groupNames);
                }
                catch (Exception ex)
                {
                    await ShowErrorDialogAsync("Lookup failed", $"Could not fetch assignments:\n{ex.Message}");
                }
            };

            return viewItem;
        }

        private static void UpdateViewAssignmentsMenuItem(MenuFlyoutItem item, DataGridContext context)
        {
            if (context.Row == null)
            {
                item.Tag = null;
                item.IsEnabled = false;
                return;
            }

            var contentType = GetRowCellTextByHeader(context.DataGrid, context.Row, "Type");
            var contentId   = GetRowCellTextByHeader(context.DataGrid, context.Row, "ID");

            var isApp = contentType?.StartsWith("App - ", StringComparison.OrdinalIgnoreCase) == true;
            var supported = !string.IsNullOrWhiteSpace(contentType)
                         && !string.IsNullOrWhiteSpace(contentId)
                         && (GetAssignmentViewRegistry().ContainsKey(contentType) || isApp);

            item.Tag = supported ? new AssignmentTarget(contentId!, contentType!) : null;
            item.IsEnabled = supported;
        }

        private static Dictionary<string, Func<GraphServiceClient, string, Task<List<AssignmentInfo>?>>> GetAssignmentViewRegistry() => new()
        {
            [ContentTypes.SettingsCatalog]              = GetSettingsCatalogAssignmentDetailsAsync,
            [ContentTypes.DeviceCompliancePolicy]       = GetDeviceComplianceAssignmentDetailsAsync,
            [ContentTypes.DeviceConfigurationPolicy]    = GetDeviceConfigurationAssignmentDetailsAsync,
            [ContentTypes.AppleBYODEnrollmentProfile]   = GetAppleBYODAssignmentDetailsAsync,
            [ContentTypes.PowerShellScript]             = GetPowerShellScriptAssignmentDetailsAsync,
            [ContentTypes.ProactiveRemediation]         = GetProactiveRemediationAssignmentDetailsAsync,
            [ContentTypes.MacOSShellScript]             = GetMacOSShellScriptAssignmentDetailsAsync,
            [ContentTypes.WindowsAutoPilotProfile]      = GetWindowsAutoPilotAssignmentDetailsAsync,
            [ContentTypes.WindowsDriverUpdate]          = GetWindowsDriverUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsFeatureUpdate]         = GetWindowsFeatureUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdatePolicy]   = GetWindowsQualityUpdatePolicyAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdateProfile]  = GetWindowsQualityUpdateProfileAssignmentDetailsAsync,
        };

        private static async Task ShowAssignmentsDialogAsync(
            string contentType,
            List<AssignmentInfo> assignments,
            Dictionary<string, string> groupNames)
        {
            var xamlRoot = (App.MainWindowInstance?.Content as FrameworkElement)?.XamlRoot;
            if (xamlRoot == null)
                return;

            string body;
            if (assignments.Count == 0)
            {
                body = "No assignments found.";
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{assignments.Count} assignment(s) found:\n");
                foreach (var a in assignments)
                {
                    var target = a.TargetType ?? "Unknown";
                    if (!string.IsNullOrEmpty(a.GroupId))
                    {
                        var name = groupNames.TryGetValue(a.GroupId, out var n) ? n : a.GroupId;
                        target += $" — {name}";
                    }
                    if (!string.IsNullOrEmpty(a.FilterId))
                        target += $"  (Filter: {a.FilterType})";

                    sb.AppendLine($"• {target}");
                }
                body = sb.ToString().TrimEnd();
            }

            var dialog = new ContentDialog
            {
                Title = $"Assignments — {contentType}",
                Content = new ScrollViewer
                {
                    MaxHeight = 400,
                    Content = new TextBlock
                    {
                        Text = body,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 0)
                    }
                },
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            };

            await dialog.ShowAsync();
        }

        private static async Task ShowErrorDialogAsync(string title, string message)
        {
            var xamlRoot = (App.MainWindowInstance?.Content as FrameworkElement)?.XamlRoot;
            if (xamlRoot == null)
                return;

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = xamlRoot
            };

            await dialog.ShowAsync();
        }

        private static string? TryBuildLookupUrl(DataGridContext context)
        {
            if (context.Row == null)
                return null;

            var contentType = GetRowCellTextByHeader(context.DataGrid, context.Row, "Type");
            var id          = GetRowCellTextByHeader(context.DataGrid, context.Row, "ID");

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
                ["Settings Catalog"]          = "https://intune.microsoft.com/#view/Microsoft_Intune_Workflows/PolicySummaryBlade/policyId/INSERT_ID_HERE/isAssigned~/true/technology/mdm/templateId//platformName/windows10",
                ["Device Compliance Policy"]  = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesMenu/~/compliance",
                ["Device Compliance"]         = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/DevicesMenu/~/compliance",
                ["Application"]              = "https://intune.microsoft.com/#view/Microsoft_Intune_Apps/SettingsMenu/~/0/appId/INSERT_ID_HERE",
                ["Windows Autopilot Profile"] = "https://intune.microsoft.com/#view/Microsoft_Intune_Enrollment/AutopilotMenuBlade/~/overview/apProfileId/INSERT_ID_HERE/menuId/",
                ["Entra Group"]              = "https://intune.microsoft.com/#view/Microsoft_AAD_IAM/GroupDetailsMenuBlade/~/Overview/groupId/INSERT_ID_HERE/menuId/",
                ["Assignment Filter"]         = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/AssignmentFilterSummaryBlade/assignmentFilterId/INSERT_ID_HERE/filterType~/2,",
                ["MacOS Shell Script"]        = "https://intune.microsoft.com/#view/Microsoft_Intune_DeviceSettings/ConfigureWMPolicyMenuBlade/~/overview/policyId/INSERT_ID_HERE/policyType~/1",
            };

            return templates.TryGetValue(trimmed, out template);
        }

        /// <summary>
        /// Gets the text from the row cell whose column header matches the given name.
        /// More robust than index-based lookup since column order varies per DataGrid.
        /// </summary>
        private static string? GetRowCellTextByHeader(DataGrid dataGrid, DataGridRow row, string header)
        {
            for (int i = 0; i < dataGrid.Columns.Count; i++)
            {
                var col = dataGrid.Columns[i];
                if (!string.Equals(col.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase))
                    continue;

                return GetRowCellText(dataGrid, row, i);
            }
            return null;
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
