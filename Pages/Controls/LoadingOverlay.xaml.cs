using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IntuneTools.Pages.Controls
{
    /// <summary>
    /// Standard acrylic loading overlay used on all data pages.
    /// See XAML_STYLE_GUIDE.md §10.
    /// </summary>
    public sealed partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingOverlay),
                new PropertyMetadata(false, OnIsLoadingChanged));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty OverlayVisibilityProperty =
            DependencyProperty.Register(nameof(OverlayVisibility), typeof(Visibility), typeof(LoadingOverlay),
                new PropertyMetadata(Visibility.Collapsed));

        public Visibility OverlayVisibility
        {
            get => (Visibility)GetValue(OverlayVisibilityProperty);
            private set => SetValue(OverlayVisibilityProperty, value);
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (LoadingOverlay)d;
            control.OverlayVisibility = control.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(LoadingOverlay),
                new PropertyMetadata("Loading..."));

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }
    }
}
