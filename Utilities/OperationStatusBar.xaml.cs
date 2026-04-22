using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Shared status bar shown in the page header during bulk operations.
    /// Replaces the per-page copy of the OperationStatusBar InfoBar block.
    /// <see cref="BaseMultiTenantPage"/> dispatches its
    /// <c>ShowOperationProgress</c> / <c>ShowOperationSuccess</c> /
    /// <c>ShowOperationError</c> / <c>HideOperationStatus</c> calls to this
    /// control when it is present on the page (looked up via <c>FindName</c>
    /// using the conventional name <c>OperationStatusBar</c>).
    /// </summary>
    public sealed partial class OperationStatusBar : UserControl
    {
        public OperationStatusBar()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Shows operation progress with an indeterminate spinner.
        /// </summary>
        public void ShowProgress(string message)
        {
            UpdateStatus(OperationState.InProgress, message, null, null, isIndeterminate: true);
        }

        /// <summary>
        /// Shows operation progress with a determinate progress bar.
        /// </summary>
        public void ShowProgress(string message, int current, int total)
        {
            UpdateStatus(OperationState.InProgress, message, current, total, isIndeterminate: false);
        }

        /// <summary>
        /// Shows operation success status.
        /// </summary>
        public void ShowSuccess(string message)
        {
            UpdateStatus(OperationState.Success, message, null, null, isIndeterminate: false);
        }

        /// <summary>
        /// Shows operation error status.
        /// </summary>
        public void ShowError(string message)
        {
            UpdateStatus(OperationState.Error, message, null, null, isIndeterminate: false);
        }

        /// <summary>
        /// Hides the operation status bar.
        /// </summary>
        public void Hide()
        {
            StatusBar.IsOpen = false;

            ProgressRing.IsActive = false;
            ProgressRing.Visibility = Visibility.Collapsed;

            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Maximum = 1;
            ProgressBar.Value = 0;
        }

        private void UpdateStatus(OperationState state, string message, int? current, int? total, bool isIndeterminate)
        {
            switch (state)
            {
                case OperationState.InProgress:
                    StatusBar.Severity = InfoBarSeverity.Informational;
                    StatusBar.Title = "Operation in Progress";
                    break;
                case OperationState.Success:
                    StatusBar.Severity = InfoBarSeverity.Success;
                    StatusBar.Title = "Operation Complete";
                    break;
                case OperationState.Error:
                    StatusBar.Severity = InfoBarSeverity.Error;
                    StatusBar.Title = "Operation Failed";
                    break;
                default:
                    StatusBar.IsOpen = false;
                    return;
            }

            string displayMessage = message;
            if (current.HasValue && total.HasValue && total.Value > 0)
            {
                displayMessage = $"{message} ({current}/{total})";
            }
            StatusBar.Message = displayMessage;

            ProgressRing.IsActive = state == OperationState.InProgress && isIndeterminate;
            ProgressRing.Visibility = (state == OperationState.InProgress && isIndeterminate)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (state == OperationState.InProgress && !isIndeterminate && current.HasValue && total.HasValue)
            {
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = total.Value;
                ProgressBar.Value = current.Value;
            }
            else
            {
                ProgressBar.Visibility = Visibility.Collapsed;
            }

            StatusBar.IsOpen = true;
        }
    }
}
