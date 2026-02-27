using IntuneTools.Graph;
using IntuneTools.Utilities;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private async void SourceViewPermissionsButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowPermissionsDialogAsync(isSource: true);
        }

        private async void DestinationViewPermissionsButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowPermissionsDialogAsync(isSource: false);
        }

        #endregion

        #region Permissions

        /// <summary>
        /// Shows a dialog displaying the granted vs required permissions for a tenant.
        /// </summary>
        private async Task ShowPermissionsDialogAsync(bool isSource)
        {
            var tenantLabel = isSource ? "Source" : "Destination";
            var tenantName = isSource ? sourceTenantName : destinationTenantName;
            var requiredScopes = isSource 
                ? SourceUserAuthentication.DefaultScopes 
                : DestinationUserAuthentication.DefaultScopes;

            // Create dialog controls programmatically
            var infoBar = new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var permissionsPanel = new StackPanel { Spacing = 4 };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 300,
                Content = permissionsPanel
            };

            var contentGrid = new Grid
            {
                MinWidth = 500,
                MaxHeight = 400,
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                }
            };

            Grid.SetRow(infoBar, 0);
            Grid.SetRow(scrollViewer, 1);
            contentGrid.Children.Add(infoBar);
            contentGrid.Children.Add(scrollViewer);

            var dialog = new ContentDialog
            {
                Title = $"{tenantLabel} Tenant Permissions",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Content = contentGrid
            };

            // Check if authenticated
            if (string.IsNullOrWhiteSpace(tenantName))
            {
                infoBar.Severity = InfoBarSeverity.Warning;
                infoBar.Title = "Not Authenticated";
                infoBar.Message = $"Please sign in to the {tenantLabel.ToLower()} tenant first.";
                await dialog.ShowAsync();
                return;
            }

            // Get granted scopes
            string[] grantedScopes;
            try
            {
                grantedScopes = isSource
                    ? await SourceUserAuthentication.GetGrantedScopesAsync()
                    : await DestinationUserAuthentication.GetGrantedScopesAsync();
            }
            catch (Exception ex)
            {
                infoBar.Severity = InfoBarSeverity.Error;
                infoBar.Title = "Error";
                infoBar.Message = $"Failed to retrieve permissions: {ex.Message}";
                await dialog.ShowAsync();
                return;
            }

            // Build permissions list
            var grantedSet = grantedScopes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Filter out non-permission scopes for display
            var relevantScopes = requiredScopes
                .Where(s => !s.Equals("openid", StringComparison.OrdinalIgnoreCase) 
                         && !s.Equals("offline_access", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .ToList();

            int grantedCount = 0;
            int missingCount = 0;

            foreach (var scope in relevantScopes)
            {
                var isGranted = grantedSet.Contains(scope);
                if (isGranted) grantedCount++; else missingCount++;

                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                
                var icon = new FontIcon
                {
                    Glyph = isGranted ? "\uE73E" : "\uE711", // Checkmark or X
                    FontSize = 14,
                    Foreground = new SolidColorBrush(isGranted ? Colors.Green : Colors.Red)
                };
                
                var text = new TextBlock
                {
                    Text = scope,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(isGranted ? Colors.Green : Colors.Red)
                };

                itemPanel.Children.Add(icon);
                itemPanel.Children.Add(text);
                permissionsPanel.Children.Add(itemPanel);
            }

            // Update dialog header
            dialog.Title = $"{tenantLabel} Tenant Permissions - {tenantName}";
            
            if (missingCount == 0)
            {
                infoBar.Severity = InfoBarSeverity.Success;
                infoBar.Title = "All Permissions Granted";
                infoBar.Message = $"{grantedCount} of {grantedCount} required permissions are granted.";
            }
            else
            {
                infoBar.Severity = InfoBarSeverity.Warning;
                infoBar.Title = "Missing Permissions";
                infoBar.Message = $"{grantedCount} granted, {missingCount} missing. Some features may not work correctly.";
            }

            await dialog.ShowAsync();
        }

        #endregion
    }
}
