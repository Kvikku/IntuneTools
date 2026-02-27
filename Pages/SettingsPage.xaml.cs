using IntuneTools.Graph;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace IntuneTools.Pages
{
    /// <summary>
    /// Settings page for tenant authentication and application configuration.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        #region Constructor & Navigation

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RefreshLoginStatusUI();
        }

        /// <summary>
        /// Updates the login status UI for both source and destination tenants.
        /// </summary>
        private void RefreshLoginStatusUI()
        {
            var sourceSignedIn = !string.IsNullOrWhiteSpace(Variables.sourceTenantName);
            var destinationSignedIn = !string.IsNullOrWhiteSpace(Variables.destinationTenantName);

            UpdateTenantStatusUI(
                isSource: true,
                isSignedIn: sourceSignedIn,
                tenantName: Variables.sourceTenantName);

            UpdateTenantStatusUI(
                isSource: false,
                isSignedIn: destinationSignedIn,
                tenantName: Variables.destinationTenantName);
        }

        /// <summary>
        /// Updates the status UI elements for a specific tenant.
        /// </summary>
        private void UpdateTenantStatusUI(bool isSource, bool isSignedIn, string? tenantName)
        {
            var statusImage = isSource ? SourceLoginStatusImage : DestinationLoginStatusImage;
            var statusText = isSource ? SourceLoginStatusText : DestinationLoginStatusText;

            if (statusText != null)
            {
                statusText.Text = isSignedIn
                    ? $"Signed in: {tenantName}"
                    : "Not signed in";
            }

            UpdateImage(statusImage, isSignedIn ? "GreenCheck.png" : "RedCross.png");
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Authenticates to a tenant and updates the UI accordingly.
        /// </summary>
        /// <param name="isSource">True for source tenant, false for destination tenant.</param>
        private async Task AuthenticateToTenantAsync(bool isSource)
        {
            var client = isSource
                ? await SourceUserAuthentication.GetGraphClientAsync()
                : await DestinationUserAuthentication.GetGraphClientAsync();

            var tenantLabel = isSource ? "Source" : "Destination";

            if (client != null)
            {
                var tenantName = await GetAzureTenantName(client);

                if (isSource)
                {
                    sourceGraphServiceClient = client;
                    sourceTenantName = tenantName;
                    Variables.sourceTenantName = tenantName ?? string.Empty;
                }
                else
                {
                    destinationGraphServiceClient = client;
                    destinationTenantName = tenantName;
                    Variables.destinationTenantName = tenantName ?? string.Empty;
                }

                LogToFunctionFile(appFunction.Main, $"{tenantLabel} Tenant Name: {tenantName}");
                UpdateTenantStatusUI(isSource, isSignedIn: true, tenantName);
            }
            else
            {
                LogToFunctionFile(appFunction.Main, $"Failed to authenticate to {tenantLabel.ToLower()} tenant.");

                if (isSource)
                    Variables.sourceTenantName = string.Empty;
                else
                    Variables.destinationTenantName = string.Empty;

                UpdateTenantStatusUI(isSource, isSignedIn: false, tenantName: null);
            }
        }

        /// <summary>
        /// Clears the authentication session for a tenant.
        /// </summary>
        /// <param name="isSource">True for source tenant, false for destination tenant.</param>
        private async Task ClearTenantSessionAsync(bool isSource)
        {
            var tenantLabel = isSource ? "Source" : "Destination";

            try
            {
                var cleared = isSource
                    ? await SourceUserAuthentication.ClearSessionAsync()
                    : await DestinationUserAuthentication.ClearSessionAsync();

                if (cleared)
                {
                    if (isSource)
                    {
                        sourceGraphServiceClient = null;
                        sourceTenantName = null;
                        Variables.sourceTenantName = string.Empty;
                    }
                    else
                    {
                        destinationGraphServiceClient = null;
                        destinationTenantName = null;
                        Variables.destinationTenantName = string.Empty;
                    }

                    UpdateTenantStatusUI(isSource, isSignedIn: false, tenantName: null);
                    LogToFunctionFile(appFunction.Main, $"{tenantLabel} token/session cleared.");
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Failed to clear {tenantLabel.ToLower()} token: {ex.Message}");
            }
        }

        /// <summary>
        /// Swaps the source and destination tenant credentials (GraphServiceClients, tenant names, and IDs).
        /// Useful when user accidentally logged into the wrong tenant.
        /// </summary>
        private void SwapTenants()
        {
            // Swap GraphServiceClients (via global using static)
            (sourceGraphServiceClient, destinationGraphServiceClient) = 
                (destinationGraphServiceClient, sourceGraphServiceClient);

            // Swap tenant names (via global using static - these ARE Variables.*)
            (sourceTenantName, destinationTenantName) = 
                (destinationTenantName, sourceTenantName);

            // Swap tenant IDs
            (sourceTenantID, destinationTenantID) = 
                (destinationTenantID, sourceTenantID);

            // Swap client IDs
            (sourceClientID, destinationClientID) = 
                (destinationClientID, sourceClientID);

            // Update UI to reflect the swap
            RefreshLoginStatusUI();

            LogToFunctionFile(appFunction.Main, 
                $"Swapped tenants. Source is now '{sourceTenantName}', Destination is now '{destinationTenantName}'.");
        }

        #endregion

        #region Event Handlers

        private async void DestinationClearTokenButton_Click(object sender, RoutedEventArgs e)
        {
            await ClearTenantSessionAsync(isSource: false);
        }

        private async void DestinationLoginButton_Click(object sender, RoutedEventArgs e)
        {
            await AuthenticateToTenantAsync(isSource: false);
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

        private void SwapTenantsButton_Click(object sender, RoutedEventArgs e)
        {
            SwapTenants();
        }

        private async void SourceClearTokenButton_Click(object sender, RoutedEventArgs e)
        {
            await ClearTenantSessionAsync(isSource: true);
        }

        private async void SourceLoginButton_Click(object sender, RoutedEventArgs e)
        {
            await AuthenticateToTenantAsync(isSource: true);
        }

        #endregion
    }
}
