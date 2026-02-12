using IntuneTools.Graph;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation; // Added for NavigationEventArgs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private Dictionary<string, Dictionary<string, string>>? _sourceTenantSettings;
        private Dictionary<string, Dictionary<string, string>>? _destinationTenantSettings;

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Reflect persisted variables in UI when arriving on the page.
            // If you keep Graph clients alive elsewhere, you can also check those to decide the icon.
            var sourceSignedIn = !string.IsNullOrWhiteSpace(Variables.sourceTenantName);
            var destinationSignedIn = !string.IsNullOrWhiteSpace(Variables.destinationTenantName);

            // Source
            if (SourceLoginStatusText != null)
            {
                SourceLoginStatusText.Text = sourceSignedIn
                    ? $"Signed in: {Variables.sourceTenantName}"
                    : "Not signed in";
            }
            UpdateImage(SourceLoginStatusImage, sourceSignedIn ? "GreenCheck.png" : "RedCross.png");

            // Destination
            if (DestinationLoginStatusText != null)
            {
                DestinationLoginStatusText.Text = destinationSignedIn
                    ? $"Signed in: {Variables.destinationTenantName}"
                    : "Not signed in";
            }
            UpdateImage(DestinationLoginStatusImage, destinationSignedIn ? "GreenCheck.png" : "RedCross.png");
        }



        private async void SourceLoginButton_Click(object sender, RoutedEventArgs e)
        {
            //await Utilities.HelperClass.ShowMessageBox("Source Tenant Login", "Authenticating to the source tenant. Please wait...","NO");
            await AuthenticateToSourceTenant();
        }

        private async Task AuthenticateToSourceTenant()
        {
            var Client = await SourceUserAuthentication.GetGraphClientAsync();
            if (Client != null)
            {
                sourceGraphServiceClient = Client;
                sourceTenantName = await GetAzureTenantName(Client);
                Variables.sourceTenantName = sourceTenantName ?? string.Empty;

                LogToFunctionFile(appFunction.Main, $"Source Tenant Name: {sourceTenantName}");
                UpdateImage(SourceLoginStatusImage, "GreenCheck.png");
                SourceLoginStatusText.Text = $"Signed in: {sourceTenantName}";
            }
            else
            {
                LogToFunctionFile(appFunction.Main, "Failed to authenticate to source tenant.");
                UpdateImage(SourceLoginStatusImage, "RedCross.png");
                SourceLoginStatusText.Text = "Not signed in";
                Variables.sourceTenantName = string.Empty;
            }
        }

        private void DestinationLoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Add your logic here for handling the DestinationLoginButton click event.
            // Example:
            AuthenticateToDestinationTenant();
        }

        private async Task AuthenticateToDestinationTenant()
        {
            var client = await DestinationUserAuthentication.GetGraphClientAsync();
            if (client != null)
            {
                destinationGraphServiceClient = client;
                destinationTenantName = await GetAzureTenantName(client);
                Variables.destinationTenantName = destinationTenantName ?? string.Empty;

                LogToFunctionFile(appFunction.Main, $"Destination Tenant Name: {destinationTenantName}");
                UpdateImage(DestinationLoginStatusImage, "GreenCheck.png");
                DestinationLoginStatusText.Text = $"Signed in: {destinationTenantName}";
            }
            else
            {
                LogToFunctionFile(appFunction.Main, "Failed to authenticate to destination tenant.");
                UpdateImage(DestinationLoginStatusImage, "RedCross.png");
                DestinationLoginStatusText.Text = "Not signed in";
                Variables.destinationTenantName = string.Empty;
            }
        }

        private void OpenLogFileLocation_Click(object sender, RoutedEventArgs e)
        {
            var folderToOpen = timestampedAppFolder;

            if (Directory.Exists(folderToOpen))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = folderToOpen,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
            else
            {
                LogToFunctionFile(appFunction.Main, $"Invalid log file folder path: {folderToOpen}");
            }
        }

        private async void SourceClearTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cleared = await SourceUserAuthentication.ClearSessionAsync();
                if (cleared)
                {
                    sourceGraphServiceClient = null;
                    sourceTenantName = null;
                    Variables.sourceTenantName = string.Empty;

                    UpdateImage(SourceLoginStatusImage, "RedCross.png");
                    SourceLoginStatusText.Text = "Not signed in";
                    LogToFunctionFile(appFunction.Main, "Source token/session cleared.");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Failed to clear source token: {ex.Message}");
            }
        }

        private async void DestinationClearTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cleared = await DestinationUserAuthentication.ClearSessionAsync();
                if (cleared)
                {
                    destinationGraphServiceClient = null;
                    destinationTenantName = null;
                    Variables.destinationTenantName = string.Empty;

                    UpdateImage(DestinationLoginStatusImage, "RedCross.png");
                    DestinationLoginStatusText.Text = "Not signed in";
                    LogToFunctionFile(appFunction.Main, "Destination token/session cleared.");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Failed to clear destination token: {ex.Message}");
            }
        }
    }
}
