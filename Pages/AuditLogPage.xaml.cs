using IntuneTools.Graph.IntuneHelperClasses;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        private CancellationTokenSource? _loadCts;

        public AuditLogPage()
        {
            InitializeComponent();
            AuditDataGrid.ItemsSource = _auditEvents;
            ActorSummaryList.ItemsSource = _actorSummary;
            LogConsole.ItemsSource = LogEntries;
        }

        protected override IEnumerable<string> GetManagedControlNames()
        {
            yield return "LoadButton";
        }

        #region Event Handlers

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            int days = GetSelectedDays();
            await LoadAuditEventsAsync(days);
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
                UpdateSummaryCards();
                UpdateActorSummary();
                ExportCsvButton.IsEnabled = _auditEvents.Count > 0;
                ExportReportButton.IsEnabled = _auditEvents.Count > 0;

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
            _auditEvents.Clear();

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

                _auditEvents.Add(new AuditEventViewModel
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
        }

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

            LogInfo($"Summary: {_auditEvents.Count} events, {uniqueActors} actors, {categories} categories, {successCount} success / {failureCount} failure");
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
            _actorSummary.Clear();
            SummaryCardsPanel.Visibility = Visibility.Collapsed;
            ActorSummaryPanel.Visibility = Visibility.Collapsed;
            ExportCsvButton.IsEnabled = false;
            ExportReportButton.IsEnabled = false;

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
            if (DaysComboBox.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Tag?.ToString(), out int days))
            {
                return days;
            }
            return 7; // Default to 7 days
        }

        #endregion
    }
}
