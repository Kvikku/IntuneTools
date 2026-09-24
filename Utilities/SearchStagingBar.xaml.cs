using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Shared "Search &amp; Staging" toolbar cluster: a search box plus Search / List All /
    /// Clear Selected / Clear All / Clear Log / Export CSV buttons. Replaces the near-identical
    /// copy of this block that used to be hand-rolled on every data-operation page.
    ///
    /// The control raises events rather than performing any Graph/staging logic itself — each
    /// host page keeps its own validation, persistence, and orchestrator calls exactly as
    /// before, just triggered by an event subscription instead of an inline XAML Click handler.
    /// </summary>
    public sealed partial class SearchStagingBar : UserControl
    {
        public event RoutedEventHandler? SearchRequested;
        public event RoutedEventHandler? ListAllRequested;
        public event RoutedEventHandler? ClearSelectedRequested;
        public event RoutedEventHandler? ClearAllRequested;
        public event RoutedEventHandler? ClearLogRequested;
        public event RoutedEventHandler? ExportCsvRequested;

        public SearchStagingBar()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// The current text in the search box.
        /// </summary>
        public string SearchText
        {
            get => SearchQueryTextBox.Text;
            set => SearchQueryTextBox.Text = value;
        }

        /// <summary>
        /// The search box's placeholder text (defaults to "Enter search query...").
        /// </summary>
        public string PlaceholderText
        {
            get => SearchQueryTextBox.PlaceholderText;
            set => SearchQueryTextBox.PlaceholderText = value;
        }

        /// <summary>
        /// Extra content (buttons/separators) inserted between "List All" and the "Clear
        /// Selected" group, for pages that need one more page-specific verb here (e.g.
        /// Cleanup's "Find Unassigned", Import's "Content Types" flyout).
        /// </summary>
        public UIElement? ExtraButtons
        {
            get => ExtraButtonsPresenter.Content as UIElement;
            set => ExtraButtonsPresenter.Content = value;
        }

        /// <summary>
        /// Enables or disables just the Search and List All buttons — used while a load is in
        /// progress, mirroring each page's previous ShowLoading/HideLoading behavior.
        /// </summary>
        public void SetSearchAndListEnabled(bool enabled)
        {
            SearchButton.IsEnabled = enabled;
            ListAllButton.IsEnabled = enabled;
        }

        /// <summary>
        /// Enables or disables just the Clear Selected and Clear All buttons — used by pages
        /// that lock staging edits while a multi-step scan (e.g. Find Unassigned) is running.
        /// </summary>
        public void SetClearButtonsEnabled(bool enabled)
        {
            ClearSelectedButton.IsEnabled = enabled;
            ClearAllButton.IsEnabled = enabled;
        }

        private void SearchQueryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && SearchButton.IsEnabled)
            {
                e.Handled = true;
                SearchRequested?.Invoke(this, e);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) => SearchRequested?.Invoke(this, e);
        private void ListAllButton_Click(object sender, RoutedEventArgs e) => ListAllRequested?.Invoke(this, e);
        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e) => ClearSelectedRequested?.Invoke(this, e);
        private void ClearAllButton_Click(object sender, RoutedEventArgs e) => ClearAllRequested?.Invoke(this, e);
        private void ClearLogButton_Click(object sender, RoutedEventArgs e) => ClearLogRequested?.Invoke(this, e);
        private void ExportCsvButton_Click(object sender, RoutedEventArgs e) => ExportCsvRequested?.Invoke(this, e);
    }
}
