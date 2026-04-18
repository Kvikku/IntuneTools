using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IntuneTools.Pages.Controls
{
    /// <summary>
    /// Standard "nothing to show yet" placeholder used inside DataGrid / ListView
    /// content areas. See XAML_STYLE_GUIDE.md §9.
    /// </summary>
    public sealed partial class EmptyState : UserControl
    {
        public EmptyState()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Segoe Fluent Icons / MDL2 glyph code (e.g., "&#xE721;" for Find).
        /// </summary>
        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(EmptyState),
                new PropertyMetadata("\uE721"));

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState),
                new PropertyMetadata("Nothing here yet"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(EmptyState),
                new PropertyMetadata(string.Empty));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
    }
}
