using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Shared scoped "Loading…" overlay used by every data page. Replaces the
    /// per-page copy of the LoadingOverlay <c>Border</c> block.
    /// <see cref="BaseMultiTenantPage"/> dispatches its <c>ShowLoading</c> /
    /// <c>HideLoading</c> calls to this control when it is present on the page
    /// (looked up via <c>FindName</c> using the conventional name
    /// <c>LoadingOverlay</c>).
    /// </summary>
    public sealed partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Shows the overlay with the supplied status message and starts the spinner.
        /// </summary>
        public void Show(string message)
        {
            LoadingStatusText.Text = message;
            LoadingRing.IsActive = true;
            this.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the overlay and stops the spinner.
        /// </summary>
        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = false;
        }
    }
}
