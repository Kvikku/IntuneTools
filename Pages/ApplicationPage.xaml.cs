using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Collections.ObjectModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    public class AppInfo
    {
        public string AppName { get; set; }
        public string AppId { get; set; }
        public string Version { get; set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ApplicationPage : Page
    {
        public ObservableCollection<AppInfo> AppList { get; set; } = new ObservableCollection<AppInfo>();

        public ApplicationPage()
        {
            this.InitializeComponent();
            // Sample data
            AppList.Add(new AppInfo { AppName = "App One", AppId = "001", Version = "1.0" });
            AppList.Add(new AppInfo { AppName = "App Two", AppId = "002", Version = "2.0" });
            AppList.Add(new AppInfo { AppName = "App Three", AppId = "003", Version = "3.0" });
            AppDataGrid.ItemsSource = AppList;
        }
    }
}
