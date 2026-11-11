using InToolz.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace InToolz
{
    /// <summary>
    /// The 2.0 "Command Center" shell: top bar (logo, Ctrl+K command box, tenant pills),
    /// a shell-level breadcrumb, and the content Frame. Replaces the old NavigationView
    /// sidebar — see MainWindow.xaml and the implementation plan for the full picture.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly AppWindow appWindow;

        public MainWindow()
        {
            this.InitializeComponent();

            // Extend content into the title bar and designate the slim top strip as the
            // draggable region (no interactive controls live there, so this carries zero
            // risk to the command box / tenant pills below it).
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(CustomTitleBar);

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            appWindow = AppWindow.GetFromWindowId(windowId);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.altform-lightunplated_targetsize-256.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            if (Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged += (_, _) => ApplyTitleBarButtonColors();
            }
            ApplyTitleBarButtonColors();

            Variables.TenantStateChanged += () => DispatcherQueue.TryEnqueue(RefreshTenantPills);
            RefreshTenantPills();

            ContentFrame.Navigate(typeof(HomePage));
        }

        /// <summary>
        /// Caption button (minimize/maximize/close) colors need to track the active theme
        /// since AppWindow.TitleBar colors aren't theme-resource-aware the way XAML is.
        /// </summary>
        private void ApplyTitleBarButtonColors()
        {
            if (appWindow.TitleBar == null) return;

            var isLight = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Light;
            var foreground = isLight ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White;
            var hover = isLight ? Microsoft.UI.Colors.LightGray : Microsoft.UI.Colors.DarkGray;

            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonForegroundColor = foreground;
            appWindow.TitleBar.ButtonHoverBackgroundColor = hover;
            appWindow.TitleBar.ButtonHoverForegroundColor = foreground;
            appWindow.TitleBar.ButtonPressedBackgroundColor = hover;
            appWindow.TitleBar.ButtonPressedForegroundColor = foreground;
        }

        private void RefreshTenantPills()
        {
            var sourceSignedIn = !string.IsNullOrEmpty(Variables.sourceTenantName);
            SourceTenantText.Text = sourceSignedIn ? $"Source: {Variables.sourceTenantName}" : "Source: not signed in";
            SourceTenantDot.Fill = new SolidColorBrush(sourceSignedIn ? Microsoft.UI.Colors.LimeGreen : Microsoft.UI.Colors.IndianRed);

            var destinationSignedIn = !string.IsNullOrEmpty(Variables.destinationTenantName);
            DestinationTenantText.Text = destinationSignedIn ? $"Dest: {Variables.destinationTenantName}" : "Dest: not signed in";
            DestinationTenantDot.Fill = new SolidColorBrush(destinationSignedIn ? Microsoft.UI.Colors.LimeGreen : Microsoft.UI.Colors.IndianRed);
        }

        private void CommandBoxButton_Click(object sender, RoutedEventArgs e) => CommandPalette.Open();

        private void CommandPaletteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            CommandPalette.Open();
        }

        private void CommandPalette_ActionInvoked(object sender, Type targetPage)
        {
            ContentFrame.Navigate(targetPage);
        }

        private void TenantPill_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var match = AppActions.All.FirstOrDefault(a => a.TargetPage == e.SourcePageType);
            var title = match?.Title ?? e.SourcePageType.Name;

            PageBreadcrumb.ItemsSource = e.SourcePageType == typeof(HomePage)
                ? new List<string> { title }
                : new List<string> { "Home", title };
        }

        private void PageBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Index == 0)
            {
                ContentFrame.Navigate(typeof(HomePage));
            }
        }
    }
}
