using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Pages
{
    public class AssignmentInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
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

            AssignmentList.Add(new AssignmentInfo { Name = "App One", Id = "001", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Two", Id = "002", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Three", Id = "003", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Four", Id = "004", Platform = "Windows", Type = "Win32" });

            AppDataGrid.ItemsSource = AssignmentList;

            // Ensure all option checkboxes are auto-checked when the page loads.
            this.Loaded += AssignmentPage_Loaded;
        }

        private void AssignmentPage_Loaded(object sender, RoutedEventArgs e)
        {
            AutoCheckAllOptions();
        }

        private void AutoCheckAllOptions()
        {
            // Set individual option checkboxes to checked without triggering cascading updates.
            _suppressOptionEvents = true;
            foreach (var cb in OptionsPanel.Children.OfType<CheckBox>().Where(cb => cb.Name != "OptionsAllCheckBox"))
            {
                cb.IsChecked = true;
            }
            _suppressOptionEvents = false;

            // Reflect the state in the 'Select all' checkbox without triggering its handler logic.
            _suppressSelectAllEvents = true;
            OptionsAllCheckBox.IsChecked = true;
            _suppressSelectAllEvents = false;
        }

        #region Orchestrators

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            // Main logic to list all assignments

            // Clear the list before populating
            AssignmentList.Clear();


            var selectedContent = GetCheckedOptionNames();
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

        public List<string> GetCheckedOptionNames()
        {
            var checkedNames = new List<string>();
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true)
                {
                    checkedNames.Add(cb.Name); // or cb.Content.ToString() for display text
                }
            }
            return checkedNames;
        }


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
            // Optional: handle indeterminate state
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
