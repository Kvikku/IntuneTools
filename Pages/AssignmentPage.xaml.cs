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
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;
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

        #region Helpers
        // Handler for the 'Select all' checkbox Checked event
        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.Name != "OptionsAllCheckBox")
                {
                    cb.IsChecked = true;
                }
            }
            _suppressOptionEvents = false;
        }

        // Handler for the 'Select all' checkbox Unchecked event
        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.Name != "OptionsAllCheckBox")
                {
                    cb.IsChecked = false;
                }
            }
            _suppressOptionEvents = false;
        }

        // Handler for the 'Select all' checkbox Indeterminate event
        private void SelectAll_Indeterminate(object sender, RoutedEventArgs e)
        {
            // Do nothing, or optionally set all to null if you want
            // Option1CheckBox.IsChecked = null;
            // Option2CheckBox.IsChecked = null;
            // Option3CheckBox.IsChecked = null;
        }

        // Handler for individual option checkbox Checked event
        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        // Handler for individual option checkbox Unchecked event
        private void Option_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        // Helper to update the 'Select all' checkbox state based on options
        private void UpdateSelectAllCheckBox()
        {
            var optionCheckBoxes = OptionsPanel.Children.OfType<CheckBox>().Where(cb => cb.Name != "OptionsAllCheckBox").ToList();

            if (!optionCheckBoxes.Any())
                return;

            bool?[] states = optionCheckBoxes.Select(cb => cb.IsChecked).ToArray();

            _suppressSelectAllEvents = true;
            if (states.All(x => x == true))
                OptionsAllCheckBox.IsChecked = true;
            else if (states.All(x => x == false))
                OptionsAllCheckBox.IsChecked = false;
            else
                OptionsAllCheckBox.IsChecked = null;
            _suppressSelectAllEvents = false;
        }

        #endregion
    }
}
