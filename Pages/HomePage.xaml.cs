using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
using Windows.System;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            // Perform version check on page load (app launch shows HomePage)
            await UpdateVersionStatusAsync();
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
                }
                else
                {
                    VersionStatusText.Text = $"You are up to date ({status.CurrentVersion}).";
                    SetIndicatorColor(Windows.UI.Color.FromArgb(255, 46, 139, 87)); // SeaGreen
                    VersionStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 139, 87));
                }
            }
            catch (Exception)
            {
                VersionStatusText.Text = "Version check failed.";
                SetIndicatorColor(Windows.UI.Color.FromArgb(255, 128, 128, 128)); // Gray
                VersionStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
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

        private async void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.NavigateUri != null)
            {
                await Launcher.LaunchUriAsync(button.NavigateUri);
            }
        }
    }
}
