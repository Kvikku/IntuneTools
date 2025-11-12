using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;

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


        #region Button click handlers
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: Implement a search UI/filter later.
            // Example: Show a dialog or filter AppList based on criteria.
        }

        private void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            // If you implement filtering later, reset ItemsSource here.
            AppDataGrid.ItemsSource = AppList;
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppDataGrid.SelectedItems == null || AppDataGrid.SelectedItems.Count == 0)
                return;

            var toRemove = AppDataGrid.SelectedItems.Cast<AssignmentInfo>().ToList();
            foreach (var item in toRemove)
            {
                AppList.Remove(item);
            }
        }

        #endregion
    }
}
