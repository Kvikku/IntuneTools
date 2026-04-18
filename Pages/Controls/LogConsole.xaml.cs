using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections;

namespace IntuneTools.Pages.Controls
{
    /// <summary>
    /// Shared log console used across pages that expose a per-operation log.
    /// Renders <see cref="LogEntry"/> rows with the canonical 58 / 18 / * layout.
    /// See XAML_STYLE_GUIDE.md §11.
    /// </summary>
    public sealed partial class LogConsole : UserControl
    {
        public LogConsole()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(LogConsole),
                new PropertyMetadata("Log Console"));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public static readonly DependencyProperty EntriesProperty =
            DependencyProperty.Register(nameof(Entries), typeof(IEnumerable), typeof(LogConsole),
                new PropertyMetadata(null));

        public IEnumerable? Entries
        {
            get => (IEnumerable?)GetValue(EntriesProperty);
            set => SetValue(EntriesProperty, value);
        }

        /// <summary>
        /// Exposes the underlying <see cref="ListView"/> so existing page code-behind
        /// (e.g., right-click menu, copy logic) can reach it without restructuring.
        /// </summary>
        public ListView ListView => EntriesList;
    }
}
