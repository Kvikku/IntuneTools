using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Shows a small post-operation summary dialog with succeeded / failed counts,
    /// and an optional "Retry failed items" callback. Reusable across all bulk-op pages.
    /// </summary>
    public static class OperationSummaryHelper
    {
        /// <summary>
        /// Shows a summary dialog after a bulk operation completes.
        /// </summary>
        /// <param name="root">XamlRoot of the calling page.</param>
        /// <param name="title">Dialog title (e.g. "Import complete").</param>
        /// <param name="succeeded">Number of items that succeeded.</param>
        /// <param name="failed">Number of items that failed.</param>
        /// <param name="onRetry">Optional async callback invoked when the user clicks "Retry failed".
        /// When provided, a "Retry failed" button appears (only if <paramref name="failed"/> &gt; 0).</param>
        /// <param name="extraDetail">Optional extra text shown below the summary (e.g. output folder).</param>
        public static async Task ShowAsync(
            XamlRoot root,
            string title,
            int succeeded,
            int failed,
            Func<Task>? onRetry = null,
            string? extraDetail = null)
        {
            if (root == null) return;

            var anyFail = failed > 0;
            var stack = new StackPanel { Spacing = 12 };

            stack.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = anyFail
                    ? (succeeded > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Error)
                    : InfoBarSeverity.Success,
                Title = anyFail
                    ? (succeeded > 0 ? "Completed with errors" : "Operation failed")
                    : "Operation succeeded",
                Message = $"{succeeded} succeeded · {failed} failed"
            });

            if (!string.IsNullOrWhiteSpace(extraDetail))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = extraDetail,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.85
                });
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = stack,
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root
            };

            if (anyFail && onRetry != null)
            {
                dialog.PrimaryButtonText = $"Retry failed ({failed})";
                dialog.DefaultButton = ContentDialogButton.Primary;
            }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && onRetry != null)
            {
                await onRetry();
            }
        }
    }
}
