using IntuneTools.Graph;
using IntuneTools.Pages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;




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
        private readonly DispatcherQueue _dispatcherQueue;

        public MainWindow()
        {
            this.InitializeComponent();

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Extend content into the title bar and set the custom title bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(CustomTitleBar);

            // Ensure the default system title bar is hidden
            var coreTitleBar = Microsoft.UI.Xaml.Window.Current;

            // Initialize AppWindow
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            appWindow = AppWindow.GetFromWindowId(windowId);

            // Set the taskbar icon
            // Ensure 'WindowIcon.ico' exists in the Assets folder and is set to 'Copy to Output Directory'
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.altform-lightunplated_targetsize-256.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            // Customize the AppWindow title bar
            if (appWindow.TitleBar != null)
            {
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                appWindow.TitleBar.ButtonHoverBackgroundColor = Microsoft.UI.Colors.DarkGray;
                appWindow.TitleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                appWindow.TitleBar.ButtonPressedBackgroundColor = Microsoft.UI.Colors.Gray;
                appWindow.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
            }

            // Minimize/close the NavigationView pane by default
            NavView.IsPaneOpen = false;
            // Navigate to the Home page by default
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag.ToString() == "Home");
            ContentFrame.Navigate(typeof(IntuneTools.Pages.HomePage));

            // Subscribe to global authentication-lost notifications so the app shell can
            // show a prominent re-auth banner regardless of which page is active.
            AuthenticationEvents.AuthenticationLost += OnAuthenticationLost;
            this.Closed += (_, _) => AuthenticationEvents.AuthenticationLost -= OnAuthenticationLost;
        }

        private void OnAuthenticationLost(string reason)
        {
            // Event may be raised from a background thread in the auth pipeline — marshal to UI thread.
            _dispatcherQueue?.TryEnqueue(() =>
            {
                if (ReauthInfoBar == null) return;
                ReauthInfoBar.Message = string.IsNullOrWhiteSpace(reason)
                    ? "Your tenant session has expired. Please sign in again to continue using InToolz."
                    : $"Your tenant session has expired. Please sign in again. Details: {reason}";
                ReauthInfoBar.IsOpen = true;
            });
        }

        private void ReauthInfoBar_ReauthClicked(object sender, RoutedEventArgs e)
        {
            // Send users to the Settings page where they can re-authenticate against
            // the source and destination tenants.
            ReauthInfoBar.IsOpen = false;
            var settingsItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), "Settings", StringComparison.Ordinal));
            if (settingsItem != null)
                NavView.SelectedItem = settingsItem;
            ContentFrame.Navigate(typeof(SettingsPage));
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            //myButton.Content = "Clicked";
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
                case "Application": // rename tag to "Assignment" if you change the NavigationViewItem
                    ContentFrame.Navigate(typeof(AssignmentPage));
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
                case "Json":
                    ContentFrame.Navigate(typeof(JsonPage));
                    break;
                case "ManageAssignments":
                    ContentFrame.Navigate(typeof(ManageAssignmentsPage));
                    break;
                case "AuditLog":
                    ContentFrame.Navigate(typeof(AuditLogPage));
                    break;
                default:
                    break;
            }
        }

    }
}
