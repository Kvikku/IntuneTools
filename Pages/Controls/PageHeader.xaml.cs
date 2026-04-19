using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IntuneTools.Pages.Controls
{
    /// <summary>
    /// Shared page header. Renders the page title, optional subtitle, an optional
    /// instructional <see cref="InfoBar"/>, and an extra content slot (typically used
    /// for additional InfoBars such as TenantInfoBar / OperationStatusBar).
    ///
    /// See XAML_STYLE_GUIDE.md §2 — the order Title → Subtitle → Instruction → Extras
    /// is intentional and shared across every page.
    /// </summary>
    public sealed partial class PageHeader : UserControl
    {
        public PageHeader()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeader),
                new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(PageHeader),
                new PropertyMetadata(string.Empty, OnSubtitleChanged));

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly DependencyProperty SubtitleVisibilityProperty =
            DependencyProperty.Register(nameof(SubtitleVisibility), typeof(Visibility), typeof(PageHeader),
                new PropertyMetadata(Visibility.Collapsed));

        public Visibility SubtitleVisibility
        {
            get => (Visibility)GetValue(SubtitleVisibilityProperty);
            private set => SetValue(SubtitleVisibilityProperty, value);
        }

        private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PageHeader)d;
            control.SubtitleVisibility = string.IsNullOrEmpty(control.Subtitle) ? Visibility.Collapsed : Visibility.Visible;
        }

        public static readonly DependencyProperty InstructionTextProperty =
            DependencyProperty.Register(nameof(InstructionText), typeof(string), typeof(PageHeader),
                new PropertyMetadata(string.Empty, OnInstructionChanged));

        public string InstructionText
        {
            get => (string)GetValue(InstructionTextProperty);
            set => SetValue(InstructionTextProperty, value);
        }

        public static readonly DependencyProperty InstructionTitleProperty =
            DependencyProperty.Register(nameof(InstructionTitle), typeof(string), typeof(PageHeader),
                new PropertyMetadata("How this page works"));

        public string InstructionTitle
        {
            get => (string)GetValue(InstructionTitleProperty);
            set => SetValue(InstructionTitleProperty, value);
        }

        public static readonly DependencyProperty InstructionSeverityProperty =
            DependencyProperty.Register(nameof(InstructionSeverity), typeof(InfoBarSeverity), typeof(PageHeader),
                new PropertyMetadata(InfoBarSeverity.Informational));

        public InfoBarSeverity InstructionSeverity
        {
            get => (InfoBarSeverity)GetValue(InstructionSeverityProperty);
            set => SetValue(InstructionSeverityProperty, value);
        }

        public static readonly DependencyProperty InstructionVisibilityProperty =
            DependencyProperty.Register(nameof(InstructionVisibility), typeof(Visibility), typeof(PageHeader),
                new PropertyMetadata(Visibility.Collapsed));

        public Visibility InstructionVisibility
        {
            get => (Visibility)GetValue(InstructionVisibilityProperty);
            private set => SetValue(InstructionVisibilityProperty, value);
        }

        private static void OnInstructionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PageHeader)d;
            control.InstructionVisibility = string.IsNullOrEmpty(control.InstructionText) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Slot for additional content rendered after the instruction InfoBar.
        /// Typically a StackPanel of further InfoBars (TenantInfoBar, OperationStatusBar).
        /// </summary>
        public static readonly DependencyProperty ExtraContentProperty =
            DependencyProperty.Register(nameof(ExtraContent), typeof(object), typeof(PageHeader),
                new PropertyMetadata(null));

        public object ExtraContent
        {
            get => GetValue(ExtraContentProperty);
            set => SetValue(ExtraContentProperty, value);
        }
    }
}
