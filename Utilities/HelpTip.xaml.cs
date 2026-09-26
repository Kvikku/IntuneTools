using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Small "?" icon that shows a <see cref="TeachingTip"/> with a short explanation on click.
    /// See HelpTip.xaml for the usage pattern and docs/UI_STANDARD.md section 13 for placement
    /// guidance.
    /// </summary>
    public sealed partial class HelpTip : UserControl
    {
        public HelpTip()
        {
            InitializeComponent();
            Tip.Target = HelpButton;
        }

        /// <summary>Short heading shown at the top of the teaching tip.</summary>
        public string Title
        {
            get => Tip.Title;
            set => Tip.Title = value;
        }

        /// <summary>The explanation body. Keep this to 1-3 sentences.</summary>
        public string Text
        {
            get => Tip.Subtitle;
            set => Tip.Subtitle = value;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            Tip.IsOpen = true;
        }
    }
}
