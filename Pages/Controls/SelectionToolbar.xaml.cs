using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace IntuneTools.Pages.Controls
{
    /// <summary>
    /// Standard select-all / deselect-all / "Selected: N" cluster used next to the
    /// search box in the toolbar. Gives every page parity with Cleanup/Renaming.
    /// See XAML_STYLE_GUIDE.md §5 / §7.
    /// </summary>
    public sealed partial class SelectionToolbar : UserControl
    {
        public SelectionToolbar()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SelectedCountProperty =
            DependencyProperty.Register(nameof(SelectedCount), typeof(int), typeof(SelectionToolbar),
                new PropertyMetadata(0, OnSelectedCountChanged));

        public int SelectedCount
        {
            get => (int)GetValue(SelectedCountProperty);
            set => SetValue(SelectedCountProperty, value);
        }

        public static readonly DependencyProperty SelectionLabelProperty =
            DependencyProperty.Register(nameof(SelectionLabel), typeof(string), typeof(SelectionToolbar),
                new PropertyMetadata("Selected: 0"));

        public string SelectionLabel
        {
            get => (string)GetValue(SelectionLabelProperty);
            private set => SetValue(SelectionLabelProperty, value);
        }

        private static void OnSelectedCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SelectionToolbar)d;
            control.SelectionLabel = $"Selected: {control.SelectedCount}";
        }

        /// <summary>Raised when the user clicks "Select All".</summary>
        public event EventHandler? SelectAllClick;

        /// <summary>Raised when the user clicks "Clear Selected".</summary>
        public event EventHandler? DeselectAllClick;

        private void OnSelectAllClick(object sender, RoutedEventArgs e) => SelectAllClick?.Invoke(this, EventArgs.Empty);
        private void OnDeselectAllClick(object sender, RoutedEventArgs e) => DeselectAllClick?.Invoke(this, EventArgs.Empty);
    }
}
