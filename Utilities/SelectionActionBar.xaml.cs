using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InToolz.Utilities
{
    /// <summary>
    /// Shared bottom action-bar chrome (live count + a slot for page-specific buttons).
    /// See SelectionActionBar.xaml for the full rationale and usage example.
    /// </summary>
    public sealed partial class SelectionActionBar : UserControl
    {
        public SelectionActionBar()
        {
            this.InitializeComponent();
        }

        /// <summary>The live count label text — the host decides what's being counted ("12 staged", "3 selected", ...).</summary>
        public string CountText
        {
            get => CountTextBlock.Text;
            set => CountTextBlock.Text = value;
        }

        /// <summary>Page-specific action buttons (Renaming's Update button, Assign/Remove/Delete/Export elsewhere).</summary>
        public UIElement? ExtraButtons
        {
            get => ExtraButtonsPresenter.Content as UIElement;
            set => ExtraButtonsPresenter.Content = value;
        }
    }
}
