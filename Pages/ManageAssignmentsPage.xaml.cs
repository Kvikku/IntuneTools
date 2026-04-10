using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

namespace IntuneTools.Pages
{
    /// <summary>
    /// Page for viewing and removing group assignments from Intune content.
    /// </summary>
    public sealed partial class ManageAssignmentsPage : BaseDataOperationPage
    {
        #region Fields & Types

        /// <summary>
        /// Defines a remove operation for a specific content type's assignments.
        /// </summary>
        private record RemoveAssignmentDefinition(
            string TypeKey,
            string DisplayName,
            Func<GraphServiceClient, string, Task> RemoveAsync);

        /// <summary>
        /// Holds assignment retrieval results for a single content item.
        /// </summary>
        private record AssignmentResult(
            string ContentName,
            string ContentType,
            List<AssignmentInfo>? Assignments,
            string? ErrorMessage);

        // Progress tracking for remove operations
        private int _removeTotal;
        private int _removeCurrent;
        private int _removeSuccessCount;
        private int _removeErrorCount;

        /// <summary>
        /// Content types that support group assignments.
        /// </summary>
        private static readonly string[] AssignableContentTypes = new[]
        {
            ContentTypes.SettingsCatalog,
            ContentTypes.DeviceCompliancePolicy,
            ContentTypes.DeviceConfigurationPolicy,
            ContentTypes.AppleBYODEnrollmentProfile,
            ContentTypes.PowerShellScript,
            ContentTypes.ProactiveRemediation,
            ContentTypes.MacOSShellScript,
            ContentTypes.WindowsAutoPilotProfile,
            ContentTypes.WindowsDriverUpdate,
            ContentTypes.WindowsFeatureUpdate,
            ContentTypes.WindowsQualityUpdatePolicy,
            ContentTypes.WindowsQualityUpdateProfile,
        };

        #endregion

        #region Constructor & Configuration

        public ManageAssignmentsPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(AssignmentsDataGrid);
            LogConsole.ItemsSource = LogEntries;
        }

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before managing assignments.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton", "ViewAssignmentsButton",
            "RemoveAssignmentsButton", "ClearSelectedButton", "ClearAllButton",
            "AssignmentsDataGrid", "ClearLogButton"
        };

        #endregion

        #region Base Class Overrides

        protected override void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            base.ShowLoading(message);
            ListAllButton.IsEnabled = false;
            SearchButton.IsEnabled = false;
        }

        protected override void HideLoading()
        {
            base.HideLoading();
            ListAllButton.IsEnabled = true;
            SearchButton.IsEnabled = true;
        }

        #endregion

        #region Registries

        /// <summary>
        /// Returns the registry mapping content types to their assignment detail retrieval functions.
        /// </summary>
        private Dictionary<string, Func<GraphServiceClient, string, Task<List<AssignmentInfo>?>>> GetViewAssignmentRegistry() => new()
        {
            [ContentTypes.SettingsCatalog] = GetSettingsCatalogAssignmentDetailsAsync,
            [ContentTypes.DeviceCompliancePolicy] = GetDeviceComplianceAssignmentDetailsAsync,
            [ContentTypes.DeviceConfigurationPolicy] = GetDeviceConfigurationAssignmentDetailsAsync,
            [ContentTypes.AppleBYODEnrollmentProfile] = GetAppleBYODAssignmentDetailsAsync,
            [ContentTypes.PowerShellScript] = GetPowerShellScriptAssignmentDetailsAsync,
            [ContentTypes.ProactiveRemediation] = GetProactiveRemediationAssignmentDetailsAsync,
            [ContentTypes.MacOSShellScript] = GetMacOSShellScriptAssignmentDetailsAsync,
            [ContentTypes.WindowsAutoPilotProfile] = GetWindowsAutoPilotAssignmentDetailsAsync,
            [ContentTypes.WindowsDriverUpdate] = GetWindowsDriverUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsFeatureUpdate] = GetWindowsFeatureUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdatePolicy] = GetWindowsQualityUpdatePolicyAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdateProfile] = GetWindowsQualityUpdateProfileAssignmentDetailsAsync,
        };

        /// <summary>
        /// Returns the registry mapping content types to their assignment removal functions.
        /// </summary>
        private IEnumerable<RemoveAssignmentDefinition> GetRemoveAssignmentRegistry() =>
        [
            new(ContentTypes.SettingsCatalog, "Settings Catalog",
                async (client, id) => await RemoveAllSettingsCatalogAssignmentsAsync(client, id)),

            new(ContentTypes.DeviceCompliancePolicy, "Device Compliance Policy",
                async (client, id) => await RemoveAllDeviceComplianceAssignmentsAsync(client, id)),

            new(ContentTypes.DeviceConfigurationPolicy, "Device Configuration Policy",
                async (client, id) => await RemoveAllDeviceConfigurationAssignmentsAsync(client, id)),

            new(ContentTypes.AppleBYODEnrollmentProfile, "Apple BYOD Enrollment Profile",
                async (client, id) => await RemoveAllAppleBYODAssignmentsAsync(client, id)),

            new(ContentTypes.PowerShellScript, "PowerShell Script",
                async (client, id) => await RemoveAllPowerShellScriptAssignmentsAsync(client, id)),

            new(ContentTypes.ProactiveRemediation, "Proactive Remediation",
                async (client, id) => await RemoveAllProactiveRemediationAssignmentsAsync(client, id)),

            new(ContentTypes.MacOSShellScript, "macOS Shell Script",
                async (client, id) => await RemoveAllMacOSShellScriptAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsAutoPilotProfile, "Windows AutoPilot Profile",
                async (client, id) => await RemoveAllWindowsAutoPilotAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsDriverUpdate, "Windows Driver Update",
                async (client, id) => await RemoveAllWindowsDriverUpdateAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsFeatureUpdate, "Windows Feature Update",
                async (client, id) => await RemoveAllWindowsFeatureUpdateAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsQualityUpdatePolicy, "Windows Quality Update Policy",
                async (client, id) => await RemoveAllWindowsQualityUpdatePolicyAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsQualityUpdateProfile, "Windows Quality Update Profile",
                async (client, id) => await RemoveAllWindowsQualityUpdateProfileAssignmentsAsync(client, id)),
        ];

        #endregion

        #region Core Operations

        /// <summary>
        /// Loads all assignable content types from Microsoft Graph.
        /// </summary>
        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading assignable content from Microsoft Graph...");
            AppendToLog("Starting to load all assignable content types. This could take a while...");
            try
            {
                ContentList.Clear();
                await LoadContentTypesAsync(graphServiceClient, AssignableContentTypes, AppendToLog);
                AssignmentsDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                AppendToLog($"Error during loading: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        /// <summary>
        /// Searches for content matching the specified query.
        /// </summary>
        private async Task SearchOrchestrator(GraphServiceClient graphServiceClient, string searchQuery)
        {
            ShowLoading("Searching content in Microsoft Graph...");
            AppendToLog($"Searching for content matching '{searchQuery}'. This may take a while...");
            try
            {
                ContentList.Clear();
                await SearchContentTypesAsync(graphServiceClient, searchQuery, AssignableContentTypes, AppendToLog);
                AssignmentsDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                AppendToLog($"Error during search: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        /// <summary>
        /// Views assignment details for selected content items and displays them in a dialog.
        /// </summary>
        private async Task ViewAssignmentsOrchestrator(GraphServiceClient graphServiceClient, List<CustomContentInfo> selectedItems)
        {
            var viewRegistry = GetViewAssignmentRegistry();
            var checkedCount = 0;
            var totalItems = selectedItems.Count;
            var results = new List<AssignmentResult>();

            ShowOperationProgress("Retrieving assignment details...", 0, totalItems);

            foreach (var item in selectedItems)
            {
                checkedCount++;
                ShowOperationProgress($"Checking assignments ({checkedCount}/{totalItems})", checkedCount, totalItems);

                if (item.ContentType == null || item.ContentId == null)
                {
                    LogWarning($"Skipping item with missing type or ID.");
                    continue;
                }

                if (!viewRegistry.TryGetValue(item.ContentType, out var getDetailsFunc))
                {
                    LogWarning($"No assignment viewer available for type '{item.ContentType}'. Skipping.");
                    continue;
                }

                try
                {
                    var details = await getDetailsFunc(graphServiceClient, item.ContentId);
                    results.Add(new AssignmentResult(
                        item.ContentName ?? "Unknown",
                        item.ContentType ?? "Unknown",
                        details,
                        details == null ? "Failed to retrieve assignments." : null));
                }
                catch (Exception ex)
                {
                    results.Add(new AssignmentResult(
                        item.ContentName ?? "Unknown",
                        item.ContentType ?? "Unknown",
                        null,
                        ex.Message));
                }
            }

            int totalAssignments = results.Where(r => r.Assignments != null).Sum(r => r.Assignments!.Count);

            // Resolve group IDs to display names
            var groupIds = results
                .Where(r => r.Assignments != null)
                .SelectMany(r => r.Assignments!)
                .Where(a => !string.IsNullOrEmpty(a.GroupId))
                .Select(a => a.GroupId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, string> groupNames = new();
            if (groupIds.Count > 0)
            {
                ShowOperationProgress("Resolving group names...");
                groupNames = await ResolveGroupNamesAsync(graphServiceClient, groupIds);
            }

            ShowOperationSuccess($"Checked assignments for {totalItems} item(s) — {totalAssignments} assignment(s) found");
            LogInfo($"Retrieved assignments for {totalItems} item(s): {totalAssignments} total assignment(s).");

            if (results.Count > 0)
            {
                await ShowAssignmentsDialogAsync(results, groupNames);
            }
        }

        /// <summary>
        /// Removes all assignments from selected content items.
        /// </summary>
        private async Task RemoveAssignmentsOrchestrator(GraphServiceClient graphServiceClient, List<CustomContentInfo> selectedItems)
        {
            _removeTotal = selectedItems.Count;
            _removeCurrent = 0;
            _removeSuccessCount = 0;
            _removeErrorCount = 0;

            ShowOperationProgress("Removing assignments...", 0, _removeTotal);

            foreach (var definition in GetRemoveAssignmentRegistry())
            {
                var itemsOfType = selectedItems
                    .Where(i => string.Equals(i.ContentType, definition.TypeKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in itemsOfType)
                {
                    _removeCurrent++;
                    ShowOperationProgress($"Removing assignments from {definition.DisplayName}", _removeCurrent, _removeTotal);

                    if (string.IsNullOrEmpty(item.ContentId))
                    {
                        LogWarning($"Skipping item '{item.ContentName}' with missing ID.");
                        continue;
                    }

                    try
                    {
                        await definition.RemoveAsync(graphServiceClient, item.ContentId);
                        _removeSuccessCount++;
                        LogSuccess($"Removed assignments from '{item.ContentName}'.");
                    }
                    catch (Exception ex)
                    {
                        _removeErrorCount++;
                        LogError($"Error removing assignments from '{item.ContentName}': {ex.Message}");
                    }
                }
            }

            if (_removeErrorCount == 0)
            {
                ShowOperationSuccess($"Successfully removed assignments from {_removeSuccessCount} item(s)");
            }
            else
            {
                ShowOperationError($"Completed with {_removeErrorCount} error(s). {_removeSuccessCount} item(s) processed successfully.");
            }
        }

        #endregion

        #region Assignment Details Dialog

        /// <summary>
        /// Resolves a list of group IDs to their display names via Microsoft Graph.
        /// Returns a dictionary mapping group ID to display name.
        /// </summary>
        private static async Task<Dictionary<string, string>> ResolveGroupNamesAsync(GraphServiceClient graphServiceClient, List<string> groupIds)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var groupId in groupIds)
            {
                try
                {
                    var group = await graphServiceClient.Groups[groupId].GetAsync(config =>
                    {
                        config.QueryParameters.Select = new[] { "displayName" };
                    });

                    if (group?.DisplayName != null)
                    {
                        names[groupId] = group.DisplayName;
                    }
                }
                catch
                {
                    // If we can't resolve a group name, we'll fall back to the ID in the UI
                }
            }

            return names;
        }

        /// <summary>
        /// Displays assignment details in a structured dialog with summary stats and expandable items.
        /// </summary>
        private async Task ShowAssignmentsDialogAsync(List<AssignmentResult> results, Dictionary<string, string> groupNames)
        {
            var rootPanel = new StackPanel { Spacing = 16 };

            // Summary statistics
            int totalAssignments = results.Where(r => r.Assignments != null).Sum(r => r.Assignments!.Count);
            int withAssignments = results.Count(r => r.Assignments is { Count: > 0 });
            int withoutAssignments = results.Count(r => r.Assignments != null && r.Assignments.Count == 0);
            int withErrors = results.Count(r => r.ErrorMessage != null);

            var summaryPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            summaryPanel.Children.Add(BuildStatBadge(results.Count.ToString(), "Checked", 0x00, 0x78, 0xD4));
            summaryPanel.Children.Add(BuildStatBadge(totalAssignments.ToString(), "Assignments", 0x10, 0x7C, 0x10));
            summaryPanel.Children.Add(BuildStatBadge(withoutAssignments.ToString(), "Unassigned", 0xCA, 0x50, 0x10));
            if (withErrors > 0)
                summaryPanel.Children.Add(BuildStatBadge(withErrors.ToString(), "Errors", 0xC4, 0x2B, 0x1C));
            rootPanel.Children.Add(summaryPanel);

            // Item expanders
            foreach (var result in results)
            {
                rootPanel.Children.Add(BuildItemExpander(result, results.Count <= 5, groupNames));
            }

            var dialog = new ContentDialog
            {
                Title = "Assignment Details",
                Content = new ScrollViewer
                {
                    Content = rootPanel,
                    MaxHeight = 500,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Builds a colored stat badge for the summary row.
        /// </summary>
        private static Border BuildStatBadge(string value, string label, byte r, byte g, byte b)
        {
            var color = Windows.UI.Color.FromArgb(255, r, g, b);
            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            return new Border
            {
                Background = new SolidColorBrush(color) { Opacity = 0.1 },
                BorderBrush = new SolidColorBrush(color) { Opacity = 0.3 },
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Child = panel
            };
        }

        /// <summary>
        /// Builds an expander for a single content item showing its assignments.
        /// </summary>
        private static Expander BuildItemExpander(AssignmentResult result, bool autoExpand, Dictionary<string, string> groupNames)
        {
            var expander = new Expander
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = autoExpand
            };

            // Header: name + type badge + assignment count
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerPanel.Children.Add(new TextBlock
            {
                Text = result.ContentName,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 300,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            headerPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 128, 128, 128)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = result.ContentType,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            var countText = result.ErrorMessage != null
                ? "Error"
                : $"{result.Assignments?.Count ?? 0} assignment(s)";
            headerPanel.Children.Add(new TextBlock
            {
                Text = countText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.6
            });
            expander.Header = headerPanel;

            // Content: assignments list, empty state, or error
            if (result.ErrorMessage != null)
            {
                expander.Content = new InfoBar
                {
                    Severity = InfoBarSeverity.Error,
                    Message = result.ErrorMessage,
                    IsOpen = true,
                    IsClosable = false
                };
            }
            else if (result.Assignments == null || result.Assignments.Count == 0)
            {
                expander.Content = new TextBlock
                {
                    Text = "No assignments found.",
                    Opacity = 0.6,
                    Margin = new Thickness(0, 4, 0, 4)
                };
            }
            else
            {
                var listPanel = new StackPanel { Spacing = 4 };
                foreach (var assignment in result.Assignments)
                {
                    listPanel.Children.Add(BuildAssignmentRow(assignment, groupNames));
                }
                expander.Content = listPanel;
            }

            return expander;
        }

        /// <summary>
        /// Builds a single assignment row with a colored indicator dot and details.
        /// </summary>
        private static UIElement BuildAssignmentRow(AssignmentInfo assignment, Dictionary<string, string> groupNames)
        {
            var (r, g, b) = assignment.TargetType switch
            {
                "All Users" => ((byte)0x00, (byte)0x78, (byte)0xD4),
                "All Devices" => ((byte)0x00, (byte)0x78, (byte)0xD4),
                "Group" => ((byte)0x10, (byte)0x7C, (byte)0x10),
                "Exclusion Group" => ((byte)0xC4, (byte)0x2B, (byte)0x1C),
                _ => ((byte)0x88, (byte)0x88, (byte)0x88)
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 2, 0, 2)
            };

            // Color indicator dot
            row.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b)),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Target type label
            row.Children.Add(new TextBlock
            {
                Text = assignment.TargetType ?? "Unknown",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Details: Group name (with ID fallback), Filter info
            var details = new List<string>();
            if (!string.IsNullOrEmpty(assignment.GroupId))
            {
                var groupLabel = groupNames.TryGetValue(assignment.GroupId, out var name)
                    ? $"{name} ({assignment.GroupId})"
                    : assignment.GroupId;
                details.Add(groupLabel);
            }
            if (!string.IsNullOrEmpty(assignment.FilterId))
                details.Add($"Filter: {assignment.FilterId} ({assignment.FilterType})");

            if (details.Count > 0)
            {
                row.Children.Add(new TextBlock
                {
                    Text = string.Join(" \u00B7 ", details),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsTextSelectionEnabled = true,
                    Opacity = 0.8
                });
            }

            return row;
        }

        #endregion

        #region Event Handlers

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            AssignmentsDataGrid.ItemsSource = null;
            AssignmentsDataGrid.ItemsSource = ContentList;
            AppendToLog("All items cleared from the list.");
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = AssignmentsDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }
            AssignmentsDataGrid.ItemsSource = null;
            AssignmentsDataGrid.ItemsSource = ContentList;
            AppendToLog($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        private void AssignmentsDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = InputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                AppendToLog("Please enter a search query.");
                return;
            }
            await SearchOrchestrator(sourceGraphServiceClient, searchQuery);
        }

        private async void ViewAssignmentsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = AssignmentsDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected. Please select one or more items to view their assignments.");
                return;
            }

            await ViewAssignmentsOrchestrator(sourceGraphServiceClient, selectedItems);
        }

        private async void RemoveAssignmentsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = AssignmentsDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected. Please select one or more items to remove their assignments.");
                return;
            }

            var numberOfItems = selectedItems.Count;

            // Bulk operation safeguard: warn when removing assignments from 10 or more items
            if (numberOfItems >= 10)
            {
                var bulkWarning = new ContentDialog
                {
                    Title = "\u26A0 Large Bulk Operation",
                    Content = $"You are about to remove assignments from {numberOfItems} items. This is a large operation. Are you sure you want to continue?",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var bulkResult = await bulkWarning.ShowAsync().AsTask();
                if (bulkResult != ContentDialogResult.Primary)
                {
                    AppendToLog("Bulk assignment removal cancelled by user.");
                    return;
                }
            }

            var dialog = new ContentDialog
            {
                Title = "Remove Assignments?",
                Content = $"Are you sure you want to remove all group assignments from the {numberOfItems} selected item(s)? This cannot be undone.",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync().AsTask();
            if (result == ContentDialogResult.Primary)
            {
                await RemoveAssignmentsOrchestrator(sourceGraphServiceClient, selectedItems);
            }
            else
            {
                AppendToLog("Assignment removal cancelled by user.");
            }
        }

        #endregion
    }
}
