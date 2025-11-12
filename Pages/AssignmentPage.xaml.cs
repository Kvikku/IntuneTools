using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace IntuneTools.Pages
{
    public class AppInfo
    {
        public string AppName { get; set; }
        public string AppId { get; set; }
        public string Version { get; set; }
    }

    public sealed partial class AssignmentPage : Page
    {
        public ObservableCollection<AppInfo> AppList { get; } = new();

        public AssignmentPage()
        {
            this.InitializeComponent();

            AppList.Add(new AppInfo { AppName = "App One", AppId = "001", Version = "1.0" });
            AppList.Add(new AppInfo { AppName = "App Two", AppId = "002", Version = "2.0" });
            AppList.Add(new AppInfo { AppName = "App Three", AppId = "003", Version = "3.0" });

            AppDataGrid.ItemsSource = AppList;
        }
    }
}
