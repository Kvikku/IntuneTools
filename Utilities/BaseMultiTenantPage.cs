using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Represents the state of a bulk operation for visual feedback.
    /// </summary>
    public enum OperationState
    {
        /// <summary>No operation in progress.</summary>
        Idle,
        /// <summary>Operation is currently running.</summary>
        InProgress,
        /// <summary>Operation completed successfully.</summary>
        Success,
        /// <summary>Operation encountered an error.</summary>
        Error
    }

    /// <summary>
    /// Base class for pages that require tenant authentication and share common UI patterns.
    /// Provides logging, loading overlay, and authentication state management.
    /// 
    /// Expected XAML control names:
    /// - LogConsole (RichTextBlock) - for log output
    /// - LogScrollViewer (ScrollViewer) - wraps LogConsole for scrolling
    /// - LoadingOverlay (Grid) - overlay shown during loading
    /// - LoadingProgressRing (ProgressRing) - progress indicator
    /// - LoadingStatusText (TextBlock) - loading status message
    /// - TenantInfoBar (InfoBar) - displays authentication status
    /// - OperationStatusBar (InfoBar) - displays operation progress/status (optional)
    /// - OperationProgressRing (ProgressRing) - progress indicator inside OperationStatusBar (optional)
    /// - OperationProgressBar (ProgressBar) - determinate progress bar inside OperationStatusBar (optional)
    /// </summary>
    public abstract class BaseMultiTenantPage : Page
    {
        /// <summary>
        /// Override to specify controls that should be enabled/disabled based on authentication state.
        /// </summary>
        protected virtual IEnumerable<string> GetManagedControlNames() => Enumerable.Empty<string>();

        /// <summary>
        /// Override to require both source and destination tenant authentication.
        /// Default is false (source tenant only).
        /// </summary>
        protected virtual bool RequiresBothTenants => false;

        /// <summary>
        /// Override to customize the unauthenticated warning message.
        /// </summary>
        protected virtual string UnauthenticatedMessage => "You must authenticate with a tenant before using this feature.";

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ValidateAuthenticationState();
        }

        /// <summary>
        /// Validates tenant authentication and updates UI accordingly.
        /// </summary>
        protected virtual void ValidateAuthenticationState()
        {
            bool isSourceAuthenticated = !string.IsNullOrEmpty(Variables.sourceTenantName);
            bool isDestinationAuthenticated = !string.IsNullOrEmpty(Variables.destinationTenantName);

            bool isAuthenticated = RequiresBothTenants
                ? isSourceAuthenticated && isDestinationAuthenticated
                : isSourceAuthenticated;

            var tenantInfoBar = FindName("TenantInfoBar") as InfoBar;
            if (tenantInfoBar != null)
            {
                if (isAuthenticated)
                {
                    string tenantDisplay = RequiresBothTenants
                        ? $"Source: {Variables.sourceTenantName} | Destination: {Variables.destinationTenantName}"
                        : Variables.sourceTenantName;

                    tenantInfoBar.Title = "Authenticated Tenant";
                    tenantInfoBar.Message = tenantDisplay;
                    tenantInfoBar.Severity = InfoBarSeverity.Informational;
                }
                else
                {
                    tenantInfoBar.Title = "Authentication Required";
                    tenantInfoBar.Message = UnauthenticatedMessage;
                    tenantInfoBar.Severity = InfoBarSeverity.Warning;
                }
                tenantInfoBar.IsOpen = true;
            }

            SetManagedControlsEnabled(isAuthenticated);
        }

        /// <summary>
        /// Enables or disables all managed controls based on authentication state.
        /// </summary>
        protected void SetManagedControlsEnabled(bool enabled)
        {
            foreach (var controlName in GetManagedControlNames())
            {
                if (FindName(controlName) is Control control)
                {
                    control.IsEnabled = enabled;
                }
            }
        }

        /// <summary>
        /// Shows the loading overlay with a custom message.
        /// </summary>
        protected virtual void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            if (FindName("LoadingStatusText") is TextBlock loadingStatusText)
                loadingStatusText.Text = message;

            if (FindName("LoadingOverlay") is Grid loadingOverlay)
                loadingOverlay.Visibility = Visibility.Visible;

            if (FindName("LoadingProgressRing") is ProgressRing loadingProgressRing)
                loadingProgressRing.IsActive = true;
        }

        /// <summary>
        /// Hides the loading overlay.
        /// </summary>
        protected virtual void HideLoading()
        {
            if (FindName("LoadingOverlay") is Grid loadingOverlay)
                loadingOverlay.Visibility = Visibility.Collapsed;

            if (FindName("LoadingProgressRing") is ProgressRing loadingProgressRing)
                loadingProgressRing.IsActive = false;
        }

        #region Operation Status Methods

        /// <summary>
        /// Shows operation progress with an indeterminate spinner.
        /// Use for operations where total count is unknown.
        /// </summary>
        /// <param name="message">Status message to display</param>
        protected void ShowOperationProgress(string message)
        {
            UpdateOperationStatus(OperationState.InProgress, message, null, null, isIndeterminate: true);
        }

        /// <summary>
        /// Shows operation progress with a determinate progress bar.
        /// Use for operations where you know the total count.
        /// </summary>
        /// <param name="message">Status message to display</param>
        /// <param name="current">Current item number (1-based)</param>
        /// <param name="total">Total number of items</param>
        protected void ShowOperationProgress(string message, int current, int total)
        {
            UpdateOperationStatus(OperationState.InProgress, message, current, total, isIndeterminate: false);
        }

        /// <summary>
        /// Shows operation success status.
        /// </summary>
        /// <param name="message">Success message to display</param>
        protected void ShowOperationSuccess(string message)
        {
            UpdateOperationStatus(OperationState.Success, message, null, null, isIndeterminate: false);
        }

        /// <summary>
        /// Shows operation error status.
        /// </summary>
        /// <param name="message">Error message to display</param>
        protected void ShowOperationError(string message)
        {
            UpdateOperationStatus(OperationState.Error, message, null, null, isIndeterminate: false);
        }

        /// <summary>
        /// Hides the operation status bar.
        /// </summary>
        protected void HideOperationStatus()
        {
            if (FindName("OperationStatusBar") is InfoBar statusBar)
            {
                statusBar.IsOpen = false;
            }
        }

        /// <summary>
        /// Updates the operation status InfoBar with the given state and message.
        /// </summary>
        private void UpdateOperationStatus(OperationState state, string message, int? current, int? total, bool isIndeterminate)
        {
            if (!(FindName("OperationStatusBar") is InfoBar statusBar))
                return;

            // Update severity and title based on state
            switch (state)
            {
                case OperationState.InProgress:
                    statusBar.Severity = InfoBarSeverity.Informational;
                    statusBar.Title = "Operation in Progress";
                    break;
                case OperationState.Success:
                    statusBar.Severity = InfoBarSeverity.Success;
                    statusBar.Title = "Operation Complete";
                    break;
                case OperationState.Error:
                    statusBar.Severity = InfoBarSeverity.Error;
                    statusBar.Title = "Operation Failed";
                    break;
                default:
                    statusBar.IsOpen = false;
                    return;
            }

            // Build message with progress if applicable
            string displayMessage = message;
            if (current.HasValue && total.HasValue && total.Value > 0)
            {
                displayMessage = $"{message} ({current}/{total})";
            }
            statusBar.Message = displayMessage;

            // Handle progress ring (indeterminate spinner)
            if (FindName("OperationProgressRing") is ProgressRing progressRing)
            {
                progressRing.IsActive = state == OperationState.InProgress && isIndeterminate;
                progressRing.Visibility = (state == OperationState.InProgress && isIndeterminate)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // Handle progress bar (determinate progress)
            if (FindName("OperationProgressBar") is ProgressBar progressBar)
            {
                if (state == OperationState.InProgress && !isIndeterminate && current.HasValue && total.HasValue)
                {
                    progressBar.Visibility = Visibility.Visible;
                    progressBar.IsIndeterminate = false;
                    progressBar.Maximum = total.Value;
                    progressBar.Value = current.Value;
                }
                else
                {
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }

            statusBar.IsOpen = true;
        }

        #endregion

        /// <summary>
        /// Appends a log message to the LogConsole RichTextBlock.
        /// </summary>
        protected void AppendToLog(string text)
        {
            if (!(FindName("LogConsole") is RichTextBlock logConsole)) return;

            Paragraph paragraph;
            if (logConsole.Blocks.Count == 0)
            {
                paragraph = new Paragraph();
                logConsole.Blocks.Add(paragraph);
            }
            else
            {
                paragraph = logConsole.Blocks.First() as Paragraph;
                if (paragraph == null)
                {
                    paragraph = new Paragraph();
                    logConsole.Blocks.Add(paragraph);
                }
            }

            if (paragraph.Inlines.Count > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new Run { Text = text });

            ScrollLogToEnd();
        }

        /// <summary>
        /// Scrolls the log console to the end.
        /// </summary>
        protected void ScrollLogToEnd()
        {
            var logConsole = FindName("LogConsole") as RichTextBlock;
            var logScrollViewer = FindName("LogScrollViewer") as ScrollViewer;

            if (logScrollViewer == null) return;

            logConsole?.UpdateLayout();
            logScrollViewer.UpdateLayout();
            logScrollViewer.ChangeView(null, logScrollViewer.ScrollableHeight, null, true);
        }

        /// <summary>
        /// Clears all text from the LogConsole.
        /// </summary>
        protected void ClearLog()
        {
            if (FindName("LogConsole") is RichTextBlock logConsole)
                logConsole.Blocks.Clear();
        }

        /// <summary>
        /// Executes an async operation with loading overlay and error handling.
        /// </summary>
        protected async Task ExecuteWithLoadingAsync(
            Func<Task> operation,
            string loadingMessage = "Loading data from Microsoft Graph...",
            string? successMessage = null,
            string? errorMessagePrefix = null)
        {
            ShowLoading(loadingMessage);
            try
            {
                await operation();
                if (!string.IsNullOrEmpty(successMessage))
                {
                    AppendToLog(successMessage);
                }
            }
            catch (Exception ex)
            {
                string prefix = errorMessagePrefix ?? "Error";
                AppendToLog($"{prefix}: {ex.Message}");
                LogToFunctionFile(appFunction.Main, $"{prefix}: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        /// <summary>
        /// Shows a confirmation dialog.
        /// </summary>
        protected async Task<bool> ShowConfirmationDialogAsync(string title, string content, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = confirmText,
                CloseButtonText = cancelText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        /// <summary>
        /// Handler for clear log button - shows confirmation then clears.
        /// </summary>
        protected async void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (await ShowConfirmationDialogAsync(
                "Clear Log Console?",
                "Are you sure you want to clear all log console text? This action cannot be undone.",
                "Clear",
                "Cancel"))
            {
                ClearLog();
            }
        }
    }
}
