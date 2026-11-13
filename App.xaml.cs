using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace InToolz
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Microsoft.UI.Xaml.Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            // Ensure DashboardPage is shown on launch so the version check runs immediately.
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            if (m_window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ThemeService.LoadSavedTheme();
            }

            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(m_window); // Store the window handle
            m_window.Activate();

            ApplyWindowBounds();

            var appFolderPath = CreateTimestampedAppFolder();
            Variables.timestampedAppFolder = appFolderPath;

            // Persist the tenant display names as a UI hint for next launch (no live token to
            // restore — see TenantStatePersistence's remarks).
            Variables.TenantStateChanged += () =>
                TenantStatePersistence.SaveLastKnownNames(Variables.sourceTenantName, Variables.destinationTenantName);

            LogApplicationStart();
        }

        /// <summary>
        /// Restores the window's last saved size/position, falling back to the original 1750x800
        /// default if nothing was saved yet or the saved position no longer lands on a connected
        /// display (e.g. a monitor that's since been unplugged).
        /// </summary>
        private void ApplyWindowBounds()
        {
            try
            {
                var bounds = WindowStatePersistence.TryLoadBounds();
                if (bounds is { } b && IsOnScreen(b.X, b.Y, b.Width, b.Height))
                {
                    m_window!.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(b.X, b.Y, b.Width, b.Height));
                    return;
                }
            }
            catch
            {
                // Fall through to the default size below — a window-restore failure should
                // never prevent the app from launching.
            }

            var size = m_window!.AppWindow.Size;
            size.Width = 1750;
            size.Height = 800;
            m_window.AppWindow.Resize(size);
        }

        private static bool IsOnScreen(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return false;

            // Indexed access, not foreach: DisplayArea.FindAll()'s WinRT-projected IReadOnlyList
            // throws an InvalidCastException from its GetEnumerator() path on this WinAppSDK/
            // CsWinRT version — Count + indexer avoid the broken enumerator entirely.
            var displays = DisplayArea.FindAll();
            for (var i = 0; i < displays.Count; i++)
            {
                var wa = displays[i].WorkArea;
                var intersects = x < wa.X + wa.Width && x + width > wa.X
                               && y < wa.Y + wa.Height && y + height > wa.Y;
                if (intersects)
                    return true;
            }

            return false;
        }

        private Window? m_window;
        public static Window? MainWindowInstance { get { return (Current as App)?.m_window; } } // Expose the main window instance
        public static IntPtr WindowHandle { get; private set; } // Expose the window handle
    }
}
