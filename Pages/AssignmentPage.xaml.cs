using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

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
        public static ObservableCollection<AssignmentInfo> AssignmentList { get; } = new();

        public AssignmentPage()
        {
            this.InitializeComponent();

            AssignmentList.Add(new AssignmentInfo { Name = "App One", Id = "001", Platform = "Windows" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Two", Id = "002", Platform = "Windows" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Three", Id = "003", Platform = "Windows" });

            AppDataGrid.ItemsSource = AssignmentList;
        }


        #region Orchestrators

        private static async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            // Main logic to list all assignments

            // Clear the list before populating
            AssignmentList.Clear();
        }

        #endregion




        #region Button click handlers
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: Implement a search UI/filter later.
            // Example: Show a dialog or filter AppList based on criteria.
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            // If you implement filtering later, reset ItemsSource here.

            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppDataGrid.SelectedItems == null || AppDataGrid.SelectedItems.Count == 0)
                return;

            var toRemove = AppDataGrid.SelectedItems.Cast<AssignmentInfo>().ToList();
            foreach (var item in toRemove)
            {
                AssignmentList.Remove(item);
            }
        }

        #endregion
    }
}
