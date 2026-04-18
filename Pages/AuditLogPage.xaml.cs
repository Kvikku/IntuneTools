using IntuneTools.Graph.IntuneHelperClasses;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace IntuneTools.Pages
{
    /// <summary>
    /// View model for displaying audit events in the DataGrid.
    /// </summary>
    public class AuditEventViewModel
    {
        public string? ActivityDateTimeFormatted { get; set; }
        public string? ActorDisplayName { get; set; }
        public string? ActivityDisplayName { get; set; }
        public string? CategoryName { get; set; }
        public string? ResultText { get; set; }
        public string? ComponentName { get; set; }
        public string? OperationType { get; set; }
        public string? ResourceInfo { get; set; }
        public DateTimeOffset? ActivityDateTime { get; set; }
    }

    /// <summary>
    /// View model for the actor summary list.
    /// </summary>
    public class ActorSummaryItem
    {
        public string? ActorName { get; set; }
        public string? CountText { get; set; }
    }

    /// <summary>
    /// Page for viewing and summarizing Intune audit logs.
    /// Shows who made changes, what was done, with summary cards and detailed event list.
    /// </summary>
    public sealed partial class AuditLogPage : BaseMultiTenantPage
    {
        private readonly ObservableCollection<AuditEventViewModel> _auditEvents = new();
        private readonly ObservableCollection<ActorSummaryItem> _actorSummary = new();
        private List<AuditEvent> _rawAuditEvents = new();

        // All view-models produced from the most recent load. Filters applied client-side
        // produce the visible subset shown in _auditEvents.
        private readonly List<AuditEventViewModel> _allViewModels = new();

        // Tracks whether we have auto-loaded events in this session so we do not
        // re-trigger a (potentially expensive) load every time the user navigates
        // back to the page.
        private bool _hasAutoLoaded;

        private CancellationTokenSource? _loadCts;

        private const string AllActorsOption = "All actors";

        public AuditLogPage()
        {
            InitializeComponent();
            AuditDataGrid.ItemsSource = _auditEvents;
            ActorSummaryList.ItemsSource = _actorSummary;
            LogConsole.ItemsSource = LogEntries;
            // Initial empty-state visibility.
            UpdateAuditEmptyState();
            _auditEvents.CollectionChanged += (_, _) => UpdateAuditEmptyState();
        }

        protected override IEnumerable<string> GetManagedControlNames()
        {
            yield return "LoadButton";
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Auto-load audit events the first time the user navigates here while authenticated.
            // Subsequent visits do not re-query; the user can press Load to refresh explicitly.
            if (_hasAutoLoaded) return;
            if (string.IsNullOrEmpty(Variables.sourceTenantName)) return;
            if (sourceGraphServiceClient == null) return;

            _hasAutoLoaded = true;
            LogInfo("Auto-loading audit events on navigation…");
            await LoadAuditEventsAsync(GetSelectedDays());
        }

        #region Event Handlers

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            int days = GetSelectedDays();
            await LoadAuditEventsAsync(days);
        }

        /// <summary>
        /// Toggles the custom date-range picker visibility based on the dropdown selection.
        /// </summary>
        private void DaysComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomRangePanel == null) return;
            var isCustom = DaysComboBox.SelectedItem is ComboBoxItem item
                           && string.Equals(item.Tag?.ToString(), "custom", StringComparison.OrdinalIgnoreCase);
            CustomRangePanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            // Default the custom range to the last 7 days so the user has a sensible starting point.
            if (isCustom && CustomFromPicker != null && CustomFromPicker.Date == null)
            {
                CustomFromPicker.Date = DateTimeOffset.Now.AddDays(-7).Date;
                CustomToPicker.Date = DateTimeOffset.Now.Date;
            }
        }

        private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportToCsvAsync();
        }

        private async void ExportReportButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportReportAsync();
        }

        private void AuditDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AuditDataGrid.SelectedItem is AuditEventViewModel selectedEvent)
            {
                LogInfo($"Selected: {selectedEvent.ActivityDisplayName} by {selectedEvent.ActorDisplayName} at {selectedEvent.ActivityDateTimeFormatted}");

                if (!string.IsNullOrEmpty(selectedEvent.ResourceInfo))
                {
                    LogInfo($"  Resources: {selectedEvent.ResourceInfo}");
                }
            }
        }

        private void CancelLoadButton_Click(object sender, RoutedEventArgs e)
        {
            _loadCts?.Cancel();
            if (sender is Button btn)
            {
                btn.IsEnabled = false;
                btn.Content = "Cancelling\u2026";
            }
            LogWarning("Cancellation requested \u2014 waiting for current page to finish...");
        }

        private void SearchTextBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_allViewModels.Count == 0) return;
            ApplyFilters();
        }

        private void ActorFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allViewModels.Count == 0) return;
            ApplyFilters();
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox != null) SearchTextBox.Text = string.Empty;
            if (ActorFilterComboBox != null && ActorFilterComboBox.Items.Count > 0)
                ActorFilterComboBox.SelectedIndex = 0;
            // SearchTextBox_TextChanged / ActorFilterComboBox_SelectionChanged will re-apply.
        }

        #endregion

        #region Core Operations

        private async Task LoadAuditEventsAsync(int days)
        {
            // Cancel any already-running load
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            // Show cancel button and reset its state
            var cancelBtn = FindName("CancelLoadButton") as Button;
            var progressDetail = FindName("LoadingProgressDetail") as TextBlock;
            if (cancelBtn != null)
            {
                cancelBtn.Content = "Cancel";
                cancelBtn.IsEnabled = true;
                cancelBtn.Visibility = Visibility.Visible;
            }
            if (progressDetail != null)
                progressDetail.Text = "";

            await ExecuteWithLoadingAsync(async () =>
            {
                LogInfo($"Loading audit events for the last {days} day(s)...");
                LogInfo("This may take several minutes for large tenants. You can cancel at any time.");

                if (sourceGraphServiceClient == null)
                {
                    LogError("Not authenticated. Please log in to a tenant first.");
                    return;
                }

                var lastLoggedCount = 0;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    _rawAuditEvents = await AuditLogHelper.GetAuditEventsAsync(
                        sourceGraphServiceClient,
                        days,
                        ct,
                        onProgress: count =>
                        {
                            // Update the UI periodically (every 100 events) to avoid excessive dispatches
                            if (count - lastLoggedCount >= 100)
                            {
                                lastLoggedCount = count;
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    if (progressDetail != null)
                                        progressDetail.Text = $"{count:N0} events retrieved \u2014 {stopwatch.Elapsed.Minutes}m {stopwatch.Elapsed.Seconds}s elapsed";
                                });
                            }
                        });
                }
                catch (OperationCanceledException)
                {
                    LogWarning($"Load cancelled after retrieving {lastLoggedCount:N0} event(s) in {stopwatch.Elapsed.Minutes}m {stopwatch.Elapsed.Seconds}s.");
                    ClearSummary();
                    return;
                }
                finally
                {
                    stopwatch.Stop();
                }

                if (_rawAuditEvents.Count == 0)
                {
                    LogWarning("No audit events found for the selected time range.");
                    ClearSummary();
                    return;
                }

                LogSuccess($"Retrieved {_rawAuditEvents.Count:N0} audit event(s) in {stopwatch.Elapsed.Minutes}m {stopwatch.Elapsed.Seconds}s.");

                if (progressDetail != null)
                    progressDetail.Text = "Processing events\u2026";
                PopulateDataGrid();

                UpdateTotalTimeSaved(secondsSavedOnAuditLog, appFunction.AuditLog);

                LogInfo("Audit log summary generated successfully.");
            },
            "Loading audit events from Microsoft Graph...",
            errorMessagePrefix: "Failed to load audit events");

            // Hide cancel button when done regardless of outcome
            if (cancelBtn != null)
                cancelBtn.Visibility = Visibility.Collapsed;
            if (progressDetail != null)
                progressDetail.Text = "";
        }

        private void PopulateDataGrid()
        {
            _allViewModels.Clear();

            foreach (var evt in _rawAuditEvents)
            {
                var actorName = evt.Actor?.UserPrincipalName
                    ?? evt.Actor?.ApplicationDisplayName
                    ?? evt.Actor?.ServicePrincipalName
                    ?? "Unknown";

                var resourceInfo = string.Empty;
                if (evt.Resources != null && evt.Resources.Count > 0)
                {
                    var resourceNames = evt.Resources
                        .Where(r => !string.IsNullOrEmpty(r.DisplayName))
                        .Select(r => $"{r.DisplayName} ({r.AuditResourceType ?? r.Type ?? "Unknown"})");
                    resourceInfo = string.Join(", ", resourceNames);
                }

                _allViewModels.Add(new AuditEventViewModel
                {
                    ActivityDateTime = evt.ActivityDateTime,
                    ActivityDateTimeFormatted = evt.ActivityDateTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "N/A",
                    ActorDisplayName = actorName,
                    ActivityDisplayName = evt.DisplayName ?? evt.Activity ?? "N/A",
                    CategoryName = evt.Category ?? "N/A",
                    ResultText = evt.ActivityResult ?? "N/A",
                    ComponentName = evt.ComponentName ?? "N/A",
                    OperationType = evt.ActivityOperationType ?? "N/A",
                    ResourceInfo = resourceInfo
                });
            }

            PopulateActorFilterOptions();
            ApplyFilters();

            LogInfo($"Summary: {_allViewModels.Count:N0} events loaded. Use the filter controls to narrow the view.");
        }

        /// <summary>
        /// Rebuilds the Actor filter dropdown from the loaded event set, preserving the
        /// current selection when possible. Always includes an "All actors" sentinel option.
        /// </summary>
        private void PopulateActorFilterOptions()
        {
            if (ActorFilterComboBox == null) return;

            var previouslySelected = ActorFilterComboBox.SelectedItem as string;

            var distinctActors = _allViewModels
                .Select(v => v.ActorDisplayName ?? "Unknown")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ActorFilterComboBox.ItemsSource = null;
            var items = new List<string> { AllActorsOption };
            items.AddRange(distinctActors);
            ActorFilterComboBox.ItemsSource = items;

            // Restore previous selection if still present; otherwise default to "All actors".
            if (!string.IsNullOrEmpty(previouslySelected) && items.Contains(previouslySelected, StringComparer.OrdinalIgnoreCase))
            {
                ActorFilterComboBox.SelectedItem = items.First(i =>
                    string.Equals(i, previouslySelected, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                ActorFilterComboBox.SelectedIndex = 0;
            }

            // FilterPanel is always visible now; we toggle individual input enablement
            // in ApplyFilters() based on whether events are loaded.
        }

        /// <summary>
        /// Applies the current search text and actor filter to the loaded view-models,
        /// refreshing the visible DataGrid and the summary cards/actor summary to reflect
        /// the filtered subset.
        /// </summary>
        private void ApplyFilters()
        {
            var search = SearchTextBox?.Text?.Trim() ?? string.Empty;
            var actor = ActorFilterComboBox?.SelectedItem as string;
            var customRange = GetCustomDateRange();

            IEnumerable<AuditEventViewModel> filtered = _allViewModels;

            // Custom date-range filter (when 'Custom range…' is selected).
            if (customRange.HasValue)
            {
                var (from, to) = customRange.Value;
                filtered = filtered.Where(v =>
                    v.ActivityDateTime.HasValue
                    && v.ActivityDateTime.Value.LocalDateTime >= from
                    && v.ActivityDateTime.Value.LocalDateTime <= to);
            }

            if (!string.IsNullOrEmpty(actor) && !string.Equals(actor, AllActorsOption, StringComparison.Ordinal))
            {
                filtered = filtered.Where(v =>
                    string.Equals(v.ActorDisplayName, actor, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(v =>
                    ContainsCI(v.ActivityDisplayName, search)
                    || ContainsCI(v.CategoryName, search)
                    || ContainsCI(v.ResultText, search)
                    || ContainsCI(v.ComponentName, search)
                    || ContainsCI(v.OperationType, search)
                    || ContainsCI(v.ResourceInfo, search)
                    || ContainsCI(v.ActorDisplayName, search));
            }

            _auditEvents.Clear();
            foreach (var vm in filtered)
            {
                _auditEvents.Add(vm);
            }

            UpdateSummaryCards();
            UpdateActorSummary();

            if (FilterResultCountText != null)
            {
                FilterResultCountText.Text = _auditEvents.Count == _allViewModels.Count
                    ? $"Showing all {_allViewModels.Count:N0} event(s)"
                    : $"Showing {_auditEvents.Count:N0} of {_allViewModels.Count:N0} event(s)";
            }

            ExportCsvButton.IsEnabled = _auditEvents.Count > 0;
            ExportReportButton.IsEnabled = _auditEvents.Count > 0;

            // Filter inputs are only useful once events are loaded.
            var hasLoaded = _allViewModels.Count > 0;
            if (SearchTextBox != null) SearchTextBox.IsEnabled = hasLoaded;
            if (ActorFilterComboBox != null) ActorFilterComboBox.IsEnabled = hasLoaded;
            if (ClearFiltersButton != null) ClearFiltersButton.IsEnabled = hasLoaded;
            UpdateAuditEmptyState();
        }

        private static bool ContainsCI(string? haystack, string needle)
            => !string.IsNullOrEmpty(haystack)
               && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private void UpdateSummaryCards()
        {
            SummaryCardsPanel.Visibility = Visibility.Visible;

            TotalEventsText.Text = _auditEvents.Count.ToString();

            var uniqueActors = _auditEvents
                .Select(e => e.ActorDisplayName)
                .Where(a => !string.IsNullOrEmpty(a) && a != "Unknown")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            UniqueActorsText.Text = uniqueActors.ToString();

            var categories = _auditEvents
                .Select(e => e.CategoryName)
                .Where(c => !string.IsNullOrEmpty(c) && c != "N/A")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            CategoriesText.Text = categories.ToString();

            var successCount = _auditEvents.Count(e =>
                string.Equals(e.ResultText, "Success", StringComparison.OrdinalIgnoreCase));
            var failureCount = _auditEvents.Count(e =>
                string.Equals(e.ResultText, "Failure", StringComparison.OrdinalIgnoreCase));
            SuccessFailureText.Text = $"{successCount} / {failureCount}";
        }

        private void UpdateActorSummary()
        {
            _actorSummary.Clear();

            var actorGroups = _auditEvents
                .GroupBy(e => e.ActorDisplayName ?? "Unknown", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var group in actorGroups)
            {
                _actorSummary.Add(new ActorSummaryItem
                {
                    ActorName = group.Key,
                    CountText = $"{group.Count()} event(s)"
                });
            }

            ActorSummaryPanel.Visibility = _actorSummary.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearSummary()
        {
            _auditEvents.Clear();
            _allViewModels.Clear();
            _actorSummary.Clear();
            SummaryCardsPanel.Visibility = Visibility.Collapsed;
            ActorSummaryPanel.Visibility = Visibility.Collapsed;
            ExportCsvButton.IsEnabled = false;
            ExportReportButton.IsEnabled = false;

            if (FilterPanel != null)
                FilterPanel.Visibility = Visibility.Visible;
            if (SearchTextBox != null) SearchTextBox.IsEnabled = false;
            if (ActorFilterComboBox != null)
            {
                ActorFilterComboBox.ItemsSource = null;
                ActorFilterComboBox.IsEnabled = false;
            }
            if (ClearFiltersButton != null) ClearFiltersButton.IsEnabled = false;
            if (FilterResultCountText != null)
                FilterResultCountText.Text = string.Empty;

            TotalEventsText.Text = "0";
            UniqueActorsText.Text = "0";
            CategoriesText.Text = "0";
            SuccessFailureText.Text = "0 / 0";
        }

        #endregion

        #region Export

        private async Task ExportToCsvAsync()
        {
            if (_auditEvents.Count == 0)
            {
                LogWarning("No audit events to export.");
                return;
            }

            try
            {
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("CSV Files", new List<string> { ".csv" });
                savePicker.SuggestedFileName = $"IntuneAuditLog_{DateTime.Now:yyyyMMdd_HHmmss}";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file == null)
                {
                    LogInfo("Export cancelled by user.");
                    return;
                }

                LogInfo("Exporting audit events to CSV...");

                var csv = new StringBuilder();
                csv.AppendLine("Date/Time,Actor,Activity,Category,Result,Component,Operation Type,Resources");

                foreach (var evt in _auditEvents)
                {
                    csv.AppendLine(string.Join(",",
                        CsvEscape(evt.ActivityDateTimeFormatted),
                        CsvEscape(evt.ActorDisplayName),
                        CsvEscape(evt.ActivityDisplayName),
                        CsvEscape(evt.CategoryName),
                        CsvEscape(evt.ResultText),
                        CsvEscape(evt.ComponentName),
                        CsvEscape(evt.OperationType),
                        CsvEscape(evt.ResourceInfo)));
                }

                await File.WriteAllTextAsync(file.Path, csv.ToString(), Encoding.UTF8);
                LogSuccess($"Exported {_auditEvents.Count} audit event(s) to {file.Path}");
            }
            catch (Exception ex)
            {
                LogError($"Export failed: {ex.Message}");
                LogToFunctionFile(appFunction.Main, $"CSV export failed: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task ExportReportAsync()
        {
            if (_auditEvents.Count == 0)
            {
                LogWarning("No audit events to export.");
                return;
            }

            try
            {
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("HTML Files", new List<string> { ".html" });
                savePicker.SuggestedFileName = $"IntuneAuditReport_{DateTime.Now:yyyyMMdd_HHmmss}";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file == null)
                {
                    LogInfo("Report export cancelled by user.");
                    return;
                }

                LogInfo("Generating audit log report...");

                int days = GetSelectedDays();
                var html = AuditLogReportGenerator.Generate(_auditEvents, days);

                await File.WriteAllTextAsync(file.Path, html, Encoding.UTF8);
                LogSuccess($"Exported audit report to {file.Path}");
            }
            catch (Exception ex)
            {
                LogError($"Report export failed: {ex.Message}");
                LogToFunctionFile(appFunction.Main, $"Report export failed: {ex.Message}", LogLevels.Error);
            }
        }

        private static string CsvEscape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Escape quotes by doubling them, replace newlines, and wrap in quotes
            var escaped = value
                .Replace("\"", "\"\"")
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");
            return $"\"{escaped}\"";
        }

        #endregion

        #region Helpers

        private int GetSelectedDays()
        {
            if (DaysComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
                {
                    // For custom range, query enough days to cover the From date, then post-filter
                    // to the [From, To] window in ApplyFilters().
                    if (CustomFromPicker?.Date is DateTimeOffset from)
                    {
                        var span = (DateTime.Today - from.Date).Days + 1;
                        return Math.Max(1, Math.Min(span, 90));
                    }
                    return 7;
                }
                if (int.TryParse(tag, out int days))
                {
                    return days;
                }
            }
            return 7; // Default to 7 days
        }

        /// <summary>
        /// Returns the explicit custom date window if 'Custom range…' is selected, otherwise null.
        /// Used by ApplyFilters to restrict the visible events to that window.
        /// </summary>
        private (DateTime From, DateTime To)? GetCustomDateRange()
        {
            if (DaysComboBox.SelectedItem is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), "custom", StringComparison.OrdinalIgnoreCase)
                && CustomFromPicker?.Date is DateTimeOffset from
                && CustomToPicker?.Date is DateTimeOffset to)
            {
                return (from.Date, to.Date.AddDays(1).AddTicks(-1));
            }
            return null;
        }

        /// <summary>
        /// Toggles the empty-state placeholder over the audit grid based on the current event list,
        /// and customizes the message to distinguish "nothing loaded" from "filters yield 0 results".
        /// </summary>
        private void UpdateAuditEmptyState()
        {
            if (AuditEmptyState == null) return;
            if (_auditEvents.Count > 0)
            {
                AuditEmptyState.Visibility = Visibility.Collapsed;
                return;
            }
            AuditEmptyState.Visibility = Visibility.Visible;
            if (_allViewModels.Count == 0)
            {
                AuditEmptyStateTitle.Text = "No audit events loaded";
                AuditEmptyStateBody.Text = "Pick a time range above and click 'Load Audit Events'.";
            }
            else
            {
                AuditEmptyStateTitle.Text = "No events match your filters";
                AuditEmptyStateBody.Text = "Try a broader search or actor selection, or click 'Clear filters'.";
            }
        }

        #endregion
    }
}
