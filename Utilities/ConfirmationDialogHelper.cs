using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Provides a consistent confirmation-dialog pattern for destructive or large bulk operations.
    /// Use this from any page before kicking off an action that changes data in a tenant.
    /// </summary>
    public static class ConfirmationDialogHelper
    {
        /// <summary>
        /// Shows a confirmation dialog with item count and optional tenant context.
        /// Returns true if the user confirmed; false if they cancelled.
        /// </summary>
        /// <param name="root">XamlRoot of the calling page (use <c>this.XamlRoot</c>).</param>
        /// <param name="title">Dialog title (e.g. "Confirm assignment").</param>
        /// <param name="action">Verb describing the action (e.g. "assign", "delete", "copy").</param>
        /// <param name="itemCount">Number of items affected.</param>
        /// <param name="tenantName">Optional tenant name to display (e.g. destination tenant for an import).</param>
        /// <param name="extraMessage">Optional additional warning text (e.g. "This action cannot be undone.").</param>
        /// <param name="severity">Severity for the embedded InfoBar.</param>
        /// <param name="confirmText">Label for the primary button.</param>
        public static async Task<bool> ConfirmAsync(
            XamlRoot root,
            string title,
            string action,
            int itemCount,
            string? tenantName = null,
            string? extraMessage = null,
            InfoBarSeverity severity = InfoBarSeverity.Warning,
            string confirmText = "Continue")
        {
            if (root == null) return false;

            var itemWord = itemCount == 1 ? "item" : "items";
            var message = string.IsNullOrWhiteSpace(tenantName)
                ? $"You are about to {action} {itemCount} {itemWord}."
                : $"You are about to {action} {itemCount} {itemWord} in {tenantName}.";

            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = severity,
                Title = "Please confirm",
                Message = message
            });

            if (!string.IsNullOrWhiteSpace(extraMessage))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = extraMessage,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.85
                });
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = stack,
                PrimaryButtonText = confirmText,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
    }
}
