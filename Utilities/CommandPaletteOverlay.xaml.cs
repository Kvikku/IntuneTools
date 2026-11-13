using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace InToolz.Utilities
{
    /// <summary>
    /// Full-window Ctrl+K command palette. Fires <see cref="ActionInvoked"/> with the
    /// selected action's target page type; holds no navigation state of its own — the
    /// host (MainWindow) owns the actual Frame.Navigate call, the same "fires events,
    /// holds no state" convention <see cref="SearchStagingBar"/> already uses.
    /// </summary>
    public sealed partial class CommandPaletteOverlay : UserControl
    {
        public event EventHandler<Type>? ActionInvoked;

        public CommandPaletteOverlay()
        {
            this.InitializeComponent();
        }

        public void Open()
        {
            QueryBox.Text = string.Empty;
            ApplyFilter(string.Empty);
            Visibility = Visibility.Visible;
            QueryBox.Focus(FocusState.Programmatic);
        }

        public void Close()
        {
            Visibility = Visibility.Collapsed;
        }

        private void ApplyFilter(string query)
        {
            var q = query.Trim();
            var matches = string.IsNullOrEmpty(q)
                ? AppActions.All
                : AppActions.All.Where(a =>
                        a.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        a.Keywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            ResultsList.ItemsSource = matches;
            if (matches.Count > 0)
            {
                ResultsList.SelectedIndex = 0;
            }
        }

        private void QueryBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(QueryBox.Text);

        private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Escape:
                    Close();
                    e.Handled = true;
                    break;
                case VirtualKey.Enter:
                    if (ResultsList.SelectedItem is AppAction selected)
                    {
                        Invoke(selected);
                    }
                    e.Handled = true;
                    break;
                case VirtualKey.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    break;
                case VirtualKey.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    break;
            }
        }

        private void MoveSelection(int delta)
        {
            var count = ResultsList.Items.Count;
            if (count == 0) return;
            ResultsList.SelectedIndex = Math.Clamp(ResultsList.SelectedIndex + delta, 0, count - 1);
        }

        private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppAction action)
            {
                Invoke(action);
            }
        }

        private void Invoke(AppAction action)
        {
            Close();
            ActionInvoked?.Invoke(this, action.TargetPage);
        }

        private void Card_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

        private void Scrim_Tapped(object sender, TappedRoutedEventArgs e) => Close();
    }
}
