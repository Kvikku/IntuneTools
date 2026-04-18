using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace IntuneTools.Pages.Controls
{
    /// <summary>
    /// Standard bordered card used in the toolbar row of every feature page.
    /// Renders an optional section label (12pt SemiBold) and a content slot.
    ///
    /// See XAML_STYLE_GUIDE.md §1 — pages should compose toolbar rows out of these.
    /// Set the inner content via the <c>CardContent</c> XAML property.
    /// </summary>
    [ContentProperty(Name = nameof(CardContent))]
    public sealed partial class ToolbarCard : UserControl
    {
        public ToolbarCard()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(ToolbarCard),
                new PropertyMetadata(string.Empty, OnHeaderTextChanged));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public static readonly DependencyProperty HasHeaderProperty =
            DependencyProperty.Register(nameof(HasHeader), typeof(Visibility), typeof(ToolbarCard),
                new PropertyMetadata(Visibility.Collapsed));

        public Visibility HasHeader
        {
            get => (Visibility)GetValue(HasHeaderProperty);
            private set => SetValue(HasHeaderProperty, value);
        }

        private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ToolbarCard)d;
            control.HasHeader = string.IsNullOrEmpty(control.HeaderText) ? Visibility.Collapsed : Visibility.Visible;
        }

        public static readonly DependencyProperty CardContentProperty =
            DependencyProperty.Register(nameof(CardContent), typeof(object), typeof(ToolbarCard),
                new PropertyMetadata(null));

        public object CardContent
        {
            get => GetValue(CardContentProperty);
            set => SetValue(CardContentProperty, value);
        }
    }
}
