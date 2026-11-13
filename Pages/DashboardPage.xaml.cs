using InToolz.Graph.IntuneHelperClasses;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace InToolz.Pages
{
    /// <summary>
    /// 2.0 Command Center dashboard — replaces HomePage. Stat tiles for the three Cleanup
    /// scans read from <see cref="DashboardStatsCache"/> rather than running a live scan
    /// (that cache has no writer until CleanupPage is restyled), while "Changes (7d)" and
    /// "Jump back in" use the existing <see cref="AuditLogHelper"/> directly, since that's
    /// already a cheap, demo-mode-transparent call.
    /// </summary>
    public sealed partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            ActionsGridView.ItemsSource = AppActions.All.Where(a => a.TargetPage != typeof(DashboardPage)).ToList();
            Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateVersionStatusAsync();
            UpdateTimeSavedCounter();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // WinUI assigns initial keyboard focus to the first focusable control on a
            // freshly-navigated page, which can auto-scroll the ScrollViewer to bring it
            // into view. Force back to the top so the page always opens at the hero banner.
            PageScrollViewer.ChangeView(null, 0, null, true);

            LoadScanStatTiles();
            _ = LoadRecentActivityAsync();
        }

        #region Version check (ported from HomePage)

        private async Task UpdateVersionStatusAsync()
        {
            try
            {
                var status = await VersionCheck.CheckAsync();

                if (status.IsUpdateAvailable)
                {
                    VersionStatusText.Text = $"Newer version available: {status.LatestVersion} (current {status.CurrentVersion})";
                    SetIndicatorColor(Windows.UI.Color.FromArgb(255, 255, 165, 0));
                    VersionStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
                    UpdateButtonsPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    VersionStatusText.Text = $"You are up to date ({status.CurrentVersion}).";
                    SetIndicatorColor(Windows.UI.Color.FromArgb(255, 46, 139, 87));
                    VersionStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 139, 87));
                    UpdateButtonsPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Version check failed: {ex}", appFunction.Main);
                VersionStatusText.Text = "Version check failed.";
                SetIndicatorColor(Windows.UI.Color.FromArgb(255, 128, 128, 128));
                VersionStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
                UpdateButtonsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SetIndicatorColor(Windows.UI.Color color)
        {
            if (VersionStatusBrush != null)
            {
                VersionStatusBrush.Color = color;
            }
        }

        #endregion

        #region Time saved (ported from HomePage)

        private void UpdateTimeSavedCounter()
        {
            var totalMinutes = TimeSaved.GetTotalTimeSavedInMinutes();

            LogBreakdown("Rename", numberOfItemsRenamed, secondsSavedOnRenaming);
            LogBreakdown("Assignment", numberOfItemsAssigned, secondsSavedOnAssignments);
            LogBreakdown("Delete", numberOfItemsDeleted, secondsSavedOnDeleting);
            LogBreakdown("Import", numberOfItemsImported, secondsSavedOnImporting);
            LogBreakdown("Find Unassigned", numberOfItemsCheckedForAssignments, secondsSavedOnFindingUnassigned);
            LogBreakdown("JSON Export", numberOfItemsJsonExported, secondsSavedOnJsonExport);
            LogBreakdown("Audit Log", numberOfAuditLogsRetrieved, secondsSavedOnAuditLog);
            LogBreakdown("Manage Assignments", numberOfAssignmentsManaged, secondsSavedOnManagingAssignments);

            TimeSavedMinutesText.Text = totalMinutes.ToString();
            TimeSavedProgress.Value = Math.Min(TimeSavedProgress.Maximum, totalMinutes);

            UpdateTimeSavedBreakdown();
        }

        private static void LogBreakdown(string label, int itemCount, int secondsPerItem)
        {
            var totalSeconds = itemCount * secondsPerItem;
            AppLogger.Info($"Time saved breakdown - {label}: {itemCount} item(s), {totalSeconds} sec ({totalSeconds / 60.0:F2} min).", appFunction.Summary);
        }

        private void UpdateTimeSavedBreakdown()
        {
            var anyVisible = false;

            anyVisible |= UpdateBreakdownRow(RenamedItemsPanel, RenamedItemsCountText, numberOfItemsRenamed);
            anyVisible |= UpdateBreakdownRow(AssignedItemsPanel, AssignedItemsCountText, numberOfItemsAssigned);
            anyVisible |= UpdateBreakdownRow(DeletedItemsPanel, DeletedItemsCountText, numberOfItemsDeleted);
            anyVisible |= UpdateBreakdownRow(ImportedItemsPanel, ImportedItemsCountText, numberOfItemsImported);
            anyVisible |= UpdateBreakdownRow(CheckedAssignmentsPanel, CheckedAssignmentsCountText, numberOfItemsCheckedForAssignments);
            anyVisible |= UpdateBreakdownRow(JsonExportedItemsPanel, JsonExportedItemsCountText, numberOfItemsJsonExported);
            anyVisible |= UpdateBreakdownRow(AuditLogsPanel, AuditLogsCountText, numberOfAuditLogsRetrieved);
            anyVisible |= UpdateBreakdownRow(ManagedAssignmentsPanel, ManagedAssignmentsCountText, numberOfAssignmentsManaged);

            TimeSavedBreakdownPanel.Visibility = anyVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool UpdateBreakdownRow(StackPanel panel, TextBlock countText, int count)
        {
            if (count > 0)
            {
                countText.Text = count.ToString();
                panel.Visibility = Visibility.Visible;
                return true;
            }

            panel.Visibility = Visibility.Collapsed;
            return false;
        }

        #endregion

        #region Stat tiles

        private void LoadScanStatTiles()
        {
            SetScanTile(ScanKeys.Unassigned, UnassignedStatValueText, UnassignedStatCaptionText);
            SetScanTile(ScanKeys.StaleDevices, StaleStatValueText, StaleStatCaptionText);
            SetScanTile(ScanKeys.Duplicates, DuplicatesStatValueText, DuplicatesStatCaptionText);
        }

        private static void SetScanTile(string scanKey, TextBlock valueText, TextBlock captionText)
        {
            var stat = DashboardStatsCache.GetResult(scanKey);
            if (stat == null)
            {
                valueText.Text = "—";
                captionText.Text = "Not yet scanned";
                return;
            }

            valueText.Text = stat.Count.ToString();
            captionText.Text = $"Last scanned {ToRelativeTime(stat.ScannedAtUtc)}";
        }

        private async Task LoadRecentActivityAsync()
        {
            RecentActivityList.Children.Clear();

            if (sourceGraphServiceClient == null)
            {
                ChangesStatValueText.Text = "—";
                ChangesStatCaptionText.Text = "Sign in to see activity";
                RecentActivityEmptyText.Text = "Sign in via Settings to see recent tenant activity here.";
                RecentActivityEmptyText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var events = await AuditLogHelper.GetAuditEventsAsync(sourceGraphServiceClient, days: 7);

                ChangesStatValueText.Text = events.Count.ToString();
                ChangesStatCaptionText.Text = "last 7 days";

                if (events.Count == 0)
                {
                    RecentActivityEmptyText.Text = "No changes in the last 7 days.";
                    RecentActivityEmptyText.Visibility = Visibility.Visible;
                    return;
                }

                RecentActivityEmptyText.Visibility = Visibility.Collapsed;

                foreach (var evt in events.Take(3))
                {
                    var actorName = evt.Actor?.UserPrincipalName
                        ?? evt.Actor?.ApplicationDisplayName
                        ?? evt.Actor?.ServicePrincipalName
                        ?? "Unknown";
                    var activityName = evt.DisplayName ?? evt.Activity ?? "Change";
                    var whenText = evt.ActivityDateTime.HasValue
                        ? ToRelativeTime(evt.ActivityDateTime.Value.UtcDateTime)
                        : "recently";

                    RecentActivityList.Children.Add(BuildRecentActivityRow(activityName, actorName, whenText));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Dashboard recent-activity load failed: {ex.Message}", appFunction.Main);
                ChangesStatValueText.Text = "—";
                ChangesStatCaptionText.Text = "Couldn't load activity";
                RecentActivityEmptyText.Text = "Couldn't load recent activity right now.";
                RecentActivityEmptyText.Visibility = Visibility.Visible;
            }
        }

        private static Border BuildRecentActivityRow(string activity, string actor, string whenText)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new TextBlock { Text = activity, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = $"by {actor}", Opacity = 0.65, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = whenText, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center });

            return new Border
            {
                Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["CardBorderStyle"],
                Child = row
            };
        }

        private static string ToRelativeTime(DateTime utcTimestamp)
        {
            var span = DateTime.UtcNow - utcTimestamp;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return utcTimestamp.ToLocalTime().ToString("yyyy-MM-dd");
        }

        #endregion

        #region Navigation

        private void ActionsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppAction action)
            {
                Frame.Navigate(action.TargetPage);
            }
        }

        private void UnassignedStatTile_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CleanupPage));
        private void StaleStatTile_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CleanupPage));
        private void DuplicatesStatTile_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CleanupPage));
        private void ChangesStatTile_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(AuditLogPage));

        private async void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.NavigateUri != null)
            {
                await Launcher.LaunchUriAsync(button.NavigateUri);
            }
        }

        private async void OpenGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/Kvikku/InToolz"));
        }

        private async void OpenStoreButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://apps.microsoft.com/detail/9phqrcx3gkxd"));
        }

        #endregion
    }
}
