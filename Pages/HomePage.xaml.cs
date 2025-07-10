using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
        }

        private async void GitHubLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.NavigateUri != null)
            {
                await Launcher.LaunchUriAsync(button.NavigateUri);
            }
        }
    }
}
