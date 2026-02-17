using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace IntuneTools.Pages
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateVersionStatusAsync();
            UpdateTimeSavedCounter();
        }

        private async Task UpdateVersionStatusAsync()
        {
            try
            {
                var status = await VersionCheck.CheckAsync();

                if (status.IsUpdateAvailable)
                {
                    VersionStatusText.Text = $"Newer version available: {status.LatestVersion} (current {status.CurrentVersion})";
                    SetIndicatorColor(Windows.UI.Color.FromArgb(255, 255, 165, 0)); // OrangeRed
                    VersionStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
                    UpdateButtonsPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    VersionStatusText.Text = $"You are up to date ({status.CurrentVersion}).";
                    SetIndicatorColor(Windows.UI.Color.FromArgb(255, 46, 139, 87)); // SeaGreen
                    VersionStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 139, 87));
                    UpdateButtonsPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception)
            {
                VersionStatusText.Text = "Version check failed.";
                SetIndicatorColor(Windows.UI.Color.FromArgb(255, 128, 128, 128)); // Gray
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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateLoginStatus();
        }

        private void UpdateLoginStatus()
        {
            if (sourceTenantName != string.Empty)
            {
                UpdateImage(LoginStatusImage, "GreenCheck.png");
                TenantNameText.Text = sourceTenantName;
            }
            else
            {
                UpdateImage(LoginStatusImage, "RedCross.png");
            }
        }

        private void UpdateTimeSavedCounter(int minutesAdded = 0)
        {
            if (minutesAdded > 0)
            {
                TimeSaved.UpdateTotalTimeSaved(minutesAdded, appFunction.Main);
            }

            var totalMinutes = TimeSaved.GetTotalTimeSavedInMinutes();

            HelperClass.LogToFunctionFile(
                appFunction.Summary,
                $"Time saved updated. Added: {minutesAdded} minute(s). Total: {totalMinutes} minute(s).",
                LogLevels.Info);

            LogBreakdown("Rename", numberOfItemsRenamed, secondsSavedOnRenaming);
            LogBreakdown("Assignment", numberOfItemsAssigned, secondsSavedOnAssignments);
            LogBreakdown("Delete", numberOfItemsDeleted, secondsSavedOnDeleting);
            LogBreakdown("Import", numberOfItemsImported, secondsSavedOnImporting);

            TimeSavedMinutesText.Text = totalMinutes.ToString();
            TimeSavedProgress.Value = Math.Min(TimeSavedProgress.Maximum, totalMinutes);

            UpdateTimeSavedBreakdown();
        }

        private void LogBreakdown(string label, int itemCount, int secondsPerItem)
        {
            var totalSeconds = itemCount * secondsPerItem;
            HelperClass.LogToFunctionFile(
                appFunction.Summary,
                $"Time saved breakdown - {label}: {itemCount} item(s), {totalSeconds} sec ({totalSeconds / 60.0:F2} min).",
                LogLevels.Info);
        }

        private void UpdateTimeSavedBreakdown()
        {
            var anyVisible = false;

            anyVisible |= UpdateBreakdownRow(RenamedItemsPanel, RenamedItemsCountText, numberOfItemsRenamed);
            anyVisible |= UpdateBreakdownRow(AssignedItemsPanel, AssignedItemsCountText, numberOfItemsAssigned);
            anyVisible |= UpdateBreakdownRow(DeletedItemsPanel, DeletedItemsCountText, numberOfItemsDeleted);
            anyVisible |= UpdateBreakdownRow(ImportedItemsPanel, ImportedItemsCountText, numberOfItemsImported);

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

        private async void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.NavigateUri != null)
            {
                await Launcher.LaunchUriAsync(button.NavigateUri);
            }
        }

        private async void OpenGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/Kvikku/IntuneTools"));
        }

        private async void OpenStoreButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://apps.microsoft.com/detail/9phqrcx3gkxd"));
        }
    }
}
