using IntuneTools.Graph;
using IntuneTools.Graph.DemoMode;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;

namespace IntuneTools.Pages
{
    /// <summary>
    /// Settings page for tenant authentication and application configuration.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        #region Constructor & Navigation

        // Tracks whether the last permission check for each tenant found missing scopes, so the
        // warning survives navigating away from and back to this page (NavigationCacheMode="Required"
        // keeps this page instance, and its fields, alive for the whole app session).
        private bool _sourceHasPermissionWarning;
        private bool _destinationHasPermissionWarning;

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
                tenantName: Variables.sourceTenantName,
                hasPermissionWarning: sourceSignedIn && _sourceHasPermissionWarning);

            UpdateTenantStatusUI(
                isSource: false,
                isSignedIn: destinationSignedIn,
                tenantName: Variables.destinationTenantName,
                hasPermissionWarning: destinationSignedIn && _destinationHasPermissionWarning);
        }

        /// <summary>
        /// Updates the status UI elements for a specific tenant.
        /// </summary>
        private void UpdateTenantStatusUI(bool isSource, bool isSignedIn, string? tenantName, bool hasPermissionWarning = false)
        {
            var statusImage = isSource ? SourceLoginStatusImage : DestinationLoginStatusImage;
            var statusText = isSource ? SourceLoginStatusText : DestinationLoginStatusText;

            if (statusText != null)
            {
                statusText.Text = isSignedIn
                    ? (hasPermissionWarning ? $"Signed in: {tenantName} — missing permissions" : $"Signed in: {tenantName}")
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

                AppLogger.Info($"{tenantLabel} Tenant Name: {tenantName}", appFunction.Main);
                UpdateTenantStatusUI(isSource, isSignedIn: true, tenantName);

                // Verify all required Graph permissions were actually granted right away, rather
                // than letting the user discover a gap later as a cryptic failure deep in some
                // other page's operation.
                await CheckPermissionsAfterSignInAsync(isSource, tenantName);
            }
            else
            {
                AppLogger.Info($"Failed to authenticate to {tenantLabel.ToLower()} tenant.", appFunction.Main);

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
                        _sourceHasPermissionWarning = false;
                    }
                    else
                    {
                        destinationGraphServiceClient = null;
                        destinationTenantName = null;
                        Variables.destinationTenantName = string.Empty;
                        _destinationHasPermissionWarning = false;
                    }

                    UpdateTenantStatusUI(isSource, isSignedIn: false, tenantName: null);
                    AppLogger.Info($"{tenantLabel} token/session cleared.", appFunction.Main);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Info($"Failed to clear {tenantLabel.ToLower()} token: {ex.Message}", appFunction.Main);
            }
        }

        /// <summary>
        /// Swaps the source and destination tenant credentials (GraphServiceClients, tenant names, and IDs).
        /// Useful when user accidentally logged into the wrong tenant.
        /// </summary>
        private void SwapTenants()
        {
            // Swap the full auth instances (GraphClient, token provider, signed-in account, etc.)
            DestinationUserAuthentication.SwapAuthInstances();

            // Swap tenant names (via global using static - these ARE Variables.*)
            (sourceTenantName, destinationTenantName) = 
                (destinationTenantName, sourceTenantName);

            // Swap tenant IDs
            (sourceTenantID, destinationTenantID) = 
                (destinationTenantID, sourceTenantID);

            // Swap client IDs
            (sourceClientID, destinationClientID) =
                (destinationClientID, sourceClientID);

            // Swap permission-warning state so it stays attached to the tenant it describes
            (_sourceHasPermissionWarning, _destinationHasPermissionWarning) =
                (_destinationHasPermissionWarning, _sourceHasPermissionWarning);

            // Update UI to reflect the swap
            RefreshLoginStatusUI();

            AppLogger.Info($"Swapped tenants. Source is now '{sourceTenantName}', Destination is now '{destinationTenantName}'.", appFunction.Main);
        }

        /// <summary>
        /// Enables Demo Mode: replaces both tenant connections with in-memory fictitious data,
        /// bypassing MSAL entirely, so pages can be navigated without a live tenant.
        /// </summary>
        private void EnableDemoMode()
        {
            var (sourceClient, sourceName) = DemoModeService.CreateSourceTenant();
            var (destinationClient, destinationName) = DemoModeService.CreateDestinationTenant();

            sourceGraphServiceClient = sourceClient;
            sourceTenantName = sourceName;
            Variables.sourceTenantName = sourceName;

            destinationGraphServiceClient = destinationClient;
            destinationTenantName = destinationName;
            Variables.destinationTenantName = destinationName;

            // Demo Mode bypasses MSAL/Graph entirely, so any permission warning from a prior
            // real sign-in no longer applies.
            _sourceHasPermissionWarning = false;
            _destinationHasPermissionWarning = false;

            UpdateTenantStatusUI(isSource: true, isSignedIn: true, sourceName);
            UpdateTenantStatusUI(isSource: false, isSignedIn: true, destinationName);

            AppLogger.Info("Demo Mode enabled: Source and Destination are now backed by fictitious sample data (no live Graph connection, no sign-in performed).", appFunction.Main);
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
                AppLogger.Info($"Invalid log file folder path: {folderToOpen}", appFunction.Main);
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

        private void EnableDemoModeButton_Click(object sender, RoutedEventArgs e)
        {
            EnableDemoMode();
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
        /// Result of comparing a tenant's granted Graph scopes against the app's required scopes.
        /// </summary>
        private sealed record PermissionCheckResult(
            bool IsAuthenticated,
            string? Error,
            int GrantedCount,
            int MissingCount,
            List<string> RelevantScopes,
            HashSet<string> GrantedSet);

        /// <summary>
        /// Compares a tenant's currently granted Graph scopes against
        /// <see cref="SourceUserAuthentication.DefaultScopes"/>/<see cref="DestinationUserAuthentication.DefaultScopes"/>.
        /// Used both by the manual "View Permissions" dialog and the automatic post-sign-in check.
        /// </summary>
        private async Task<PermissionCheckResult> CheckTenantPermissionsAsync(bool isSource)
        {
            var tenantName = isSource ? sourceTenantName : destinationTenantName;
            var requiredScopes = isSource
                ? SourceUserAuthentication.DefaultScopes
                : DestinationUserAuthentication.DefaultScopes;

            var relevantScopes = requiredScopes
                .Where(s => !s.Equals("openid", StringComparison.OrdinalIgnoreCase)
                         && !s.Equals("offline_access", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .ToList();

            if (string.IsNullOrWhiteSpace(tenantName))
                return new PermissionCheckResult(false, null, 0, 0, relevantScopes, new HashSet<string>());

            string[] grantedScopes;
            try
            {
                grantedScopes = isSource
                    ? await SourceUserAuthentication.GetGrantedScopesAsync()
                    : await DestinationUserAuthentication.GetGrantedScopesAsync();
            }
            catch (Exception ex)
            {
                return new PermissionCheckResult(true, ex.Message, 0, relevantScopes.Count, relevantScopes, new HashSet<string>());
            }

            var grantedSet = grantedScopes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingCount = relevantScopes.Count(s => !grantedSet.Contains(s));
            var grantedCount = relevantScopes.Count - missingCount;

            return new PermissionCheckResult(true, null, grantedCount, missingCount, relevantScopes, grantedSet);
        }

        /// <summary>
        /// Runs automatically right after a successful sign-in so permission gaps surface
        /// immediately instead of as a confusing failure later in some other page. Stays quiet
        /// (just a log line) when everything is granted; proactively opens the permissions
        /// dialog and flags the tenant's status row when something is missing.
        /// </summary>
        private async Task CheckPermissionsAfterSignInAsync(bool isSource, string? tenantName)
        {
            var result = await CheckTenantPermissionsAsync(isSource);
            if (!result.IsAuthenticated) return;

            var tenantLabel = isSource ? "source" : "destination";

            if (result.Error != null)
            {
                AppLogger.Warning($"Could not verify permissions for the {tenantLabel} tenant: {result.Error}", appFunction.Main);
                return;
            }

            if (isSource)
                _sourceHasPermissionWarning = result.MissingCount > 0;
            else
                _destinationHasPermissionWarning = result.MissingCount > 0;

            if (result.MissingCount > 0)
            {
                AppLogger.Warning(
                    $"The {tenantLabel} tenant '{tenantName}' is missing {result.MissingCount} of {result.RelevantScopes.Count} required Graph permission(s).",
                    appFunction.Main);
                UpdateTenantStatusUI(isSource, isSignedIn: true, tenantName, hasPermissionWarning: true);
                await ShowPermissionsDialogAsync(isSource, result);
            }
            else
            {
                AppLogger.Info($"All required Graph permissions are granted for the {tenantLabel} tenant '{tenantName}'.", appFunction.Main);
            }
        }

        /// <summary>
        /// Shows a dialog displaying the granted vs required permissions for a tenant.
        /// </summary>
        /// <param name="precomputedResult">
        /// Reuses a check already performed by <see cref="CheckPermissionsAfterSignInAsync"/> to
        /// avoid a redundant token fetch; the manual "View Permissions" button always passes null
        /// so it re-checks fresh.
        /// </param>
        private async Task ShowPermissionsDialogAsync(bool isSource, PermissionCheckResult? precomputedResult = null)
        {
            var tenantLabel = isSource ? "Source" : "Destination";
            var tenantName = isSource ? sourceTenantName : destinationTenantName;

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

            var result = precomputedResult ?? await CheckTenantPermissionsAsync(isSource);

            if (!result.IsAuthenticated)
            {
                infoBar.Severity = InfoBarSeverity.Warning;
                infoBar.Title = "Not Authenticated";
                infoBar.Message = $"Please sign in to the {tenantLabel.ToLower()} tenant first.";
                await dialog.ShowAsync();
                return;
            }

            if (result.Error != null)
            {
                infoBar.Severity = InfoBarSeverity.Error;
                infoBar.Title = "Error";
                infoBar.Message = $"Failed to retrieve permissions: {result.Error}";
                await dialog.ShowAsync();
                return;
            }

            foreach (var scope in result.RelevantScopes)
            {
                var isGranted = result.GrantedSet.Contains(scope);

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

            if (result.MissingCount == 0)
            {
                infoBar.Severity = InfoBarSeverity.Success;
                infoBar.Title = "All Permissions Granted";
                infoBar.Message = $"{result.GrantedCount} of {result.GrantedCount} required permissions are granted.";
            }
            else
            {
                infoBar.Severity = InfoBarSeverity.Warning;
                infoBar.Title = "Missing Permissions";
                infoBar.Message = $"{result.GrantedCount} granted, {result.MissingCount} missing. Some features may not work correctly.";
            }

            await dialog.ShowAsync();
        }

        #endregion
    }
}
