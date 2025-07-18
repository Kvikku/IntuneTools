﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(m_window); // Store the window handle
            m_window.Activate();

            // Set the window size
            var size = m_window.AppWindow.Size;
            size.Width = 1750;
            size.Height = 800;
            m_window.AppWindow.Resize(size);

            CreateLogFile();

            LogApplicationStart();
        }

        private Window? m_window;
        public static Window? MainWindowInstance { get { return (Current as App)?.m_window; } } // Expose the main window instance
        public static IntPtr WindowHandle { get; private set; } // Expose the window handle
    }
}
