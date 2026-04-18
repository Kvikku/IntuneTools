using IntuneTools.Pages;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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

        public MainWindow()
        {
            this.InitializeComponent();

            // Apply Mica system backdrop
            this.SystemBackdrop = new MicaBackdrop();

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
            }

            // Set theme-aware title bar button colors and update when theme changes
            UpdateTitleBarButtonColors();
            if (NavView is FrameworkElement themeSource)
            {
                themeSource.ActualThemeChanged += (_, _) => UpdateTitleBarButtonColors();
            }

            // Minimize/close the NavigationView pane by default
            NavView.IsPaneOpen = false;
            // Navigate to the Home page by default
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag.ToString() == "Home");
            ContentFrame.Navigate(typeof(IntuneTools.Pages.HomePage));
        }

        /// <summary>
        /// Updates title bar button foreground / hover colors to match the current app theme.
        /// </summary>
        private void UpdateTitleBarButtonColors()
        {
            if (appWindow?.TitleBar == null) return;

            var rootElement = Content as FrameworkElement;
            var isDark = rootElement?.ActualTheme == ElementTheme.Dark;
            var fg = isDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
            var hoverBg = isDark
                ? Windows.UI.Color.FromArgb(40, 255, 255, 255)
                : Windows.UI.Color.FromArgb(40, 0, 0, 0);
            var pressedBg = isDark
                ? Windows.UI.Color.FromArgb(80, 255, 255, 255)
                : Windows.UI.Color.FromArgb(80, 0, 0, 0);

            appWindow.TitleBar.ButtonForegroundColor = fg;
            appWindow.TitleBar.ButtonInactiveForegroundColor = fg;
            appWindow.TitleBar.ButtonHoverBackgroundColor = hoverBg;
            appWindow.TitleBar.ButtonHoverForegroundColor = fg;
            appWindow.TitleBar.ButtonPressedBackgroundColor = pressedBg;
            appWindow.TitleBar.ButtonPressedForegroundColor = fg;
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

        /// <summary>
        /// Refreshes the pane-footer tenant status pills whenever navigation completes
        /// (so changes made on the Settings page are reflected immediately).
        /// </summary>
        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            UpdateTenantPills();
        }

        /// <summary>
        /// Updates the source / destination tenant status indicators in the NavigationView pane footer.
        /// </summary>
        private void UpdateTenantPills()
        {
            var sourceConnected = !string.IsNullOrWhiteSpace(sourceTenantName);
            var destConnected   = !string.IsNullOrWhiteSpace(destinationTenantName);

            SourceTenantPill.Text = sourceConnected
                ? $"Source: {sourceTenantName}"
                : "Source: Not signed in";

            SourceTenantDotBrush.Color = sourceConnected
                ? Windows.UI.Color.FromArgb(255, 46, 139, 87)   // SeaGreen
                : Windows.UI.Color.FromArgb(255, 128, 128, 128); // Gray

            DestTenantPill.Text = destConnected
                ? $"Destination: {destinationTenantName}"
                : "Destination: Not signed in";

            DestTenantDotBrush.Color = destConnected
                ? Windows.UI.Color.FromArgb(255, 46, 139, 87)
                : Windows.UI.Color.FromArgb(255, 128, 128, 128);
        }

    }
}

