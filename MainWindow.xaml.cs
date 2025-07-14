using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using IntuneTools.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;
using Windows.Graphics;




// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private AppWindow appWindow;

        public MainWindow()
        {
            this.InitializeComponent();

            // Extend content into the title bar and set the custom title bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(CustomTitleBar);

            // Ensure the default system title bar is hidden
            var coreTitleBar = Microsoft.UI.Xaml.Window.Current;

            // Initialize AppWindow
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            appWindow = AppWindow.GetFromWindowId(windowId);

            // Customize the AppWindow title bar
            if (appWindow.TitleBar != null)
            {
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Transparent;
            }

            // Minimize/close the NavigationView pane by default
            NavView.IsPaneOpen = false;
            // Navigate to the Home page by default
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag.ToString() == "Home");
            ContentFrame.Navigate(typeof(HomePage));
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            //myButton.Content = "Clicked";
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Minimize();
            }
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                {
                    presenter.Restore();
                }
                else
                {
                    presenter.Maximize();
                }
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            appWindow.Destroy();
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                NavigateToPage(navItemTag);
            }
        }

        private void NavigateToPage(string navItemTag)
        {
            switch (navItemTag)
            {
                case "Home":
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case "Application":
                    ContentFrame.Navigate(typeof(ApplicationPage));
                    break;
                case "Settings":
                    ContentFrame.Navigate(typeof(SettingsPage));
                    break;
                case "Import":
                    ContentFrame.Navigate(typeof(ImportPage));
                    break;
                case "Cleanup":
                    ContentFrame.Navigate(typeof(CleanupPage));
                    break;
                case "Renaming":
                    ContentFrame.Navigate(typeof(RenamingPage));
                    break;
                default:
                    break;
            }
        }

    }
}
