using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace IntuneTools.Pages
{
    public class AssignmentInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Platform { get; set; }
    }

    public sealed partial class AssignmentPage : Page
    {
        public ObservableCollection<AssignmentInfo> AppList { get; } = new();

        public AssignmentPage()
        {
            this.InitializeComponent();

            AppList.Add(new AssignmentInfo { Name = "App One", Id = "001", Platform = "Windows" });
            AppList.Add(new AssignmentInfo { Name = "App Two", Id = "002", Platform = "Windows" });
            AppList.Add(new AssignmentInfo { Name = "App Three", Id = "003", Platform = "Windows" });

            AppDataGrid.ItemsSource = AppList;
        }
    }
}
