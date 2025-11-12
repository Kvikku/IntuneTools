using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.Graph.Beta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static IntuneTools.Graph.EntraHelperClasses.GroupHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
using static IntuneTools.Graph.IntuneHelperClasses.FilterHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static IntuneTools.Graph.IntuneHelperClasses.PowerShellScriptsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ProactiveRemediationsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.macOSShellScript;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsAutoPilotHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsDriverUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsFeatureUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdatePolicyHandler;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Pages
{
    public class AssignmentInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string Platform { get; set; }
    }

    public class AssignmentGroupInfo
    {
        public string? GroupName { get; set; }
    }

    public class AssignmentFilterInfo
    {
        public string? FilterName { get; set; }
    }

    public sealed partial class AssignmentPage : Page
    {
        public static ObservableCollection<AssignmentInfo> AssignmentList { get; } = new();
        public ObservableCollection<GroupInfo> GroupList { get; } = new();
        public ObservableCollection<string> FilterOptions { get; } = new();

        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        private readonly Dictionary<string, Func<Task>> _assignmentLoaders;

        public AssignmentPage()
        {
            this.InitializeComponent();

            _assignmentLoaders = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
            {
                ["SettingsCatalog"] = async () => await LoadAllSettingsCatalogPoliciesAsync(),
            };

            AssignmentList.Add(new AssignmentInfo { Name = "App One", Id = "001", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Two", Id = "002", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Three", Id = "003", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Four", Id = "004", Platform = "Windows", Type = "Win32" });

            AppDataGrid.ItemsSource = AssignmentList;

            this.Loaded += AssignmentPage_Loaded;

            AppendToDetailsRichTextBlock("Assignment page loaded.");
        }

        #region Loading Overlay
        private void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            LoadingStatusText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingProgressRing.IsActive = true;

            SearchButton.IsEnabled = false;
            ListAllButton.IsEnabled = false;
            RemoveSelectedButton.IsEnabled = false;
            AssignButton.IsEnabled = false;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingProgressRing.IsActive = false;

            SearchButton.IsEnabled = true;
            ListAllButton.IsEnabled = true;
            RemoveSelectedButton.IsEnabled = true;
            AssignButton.IsEnabled = true;
        }
        #endregion

        #region Orchestrators

        private async Task MainOrchestrator(GraphServiceClient graphServiceClient)
        {
            // Main orchestrator of assignment operations

            // Get all content

            var content = GetAllContentFromDatagrid();

            // Get groups
            var selectedGroups = GroupDataGrid.SelectedItems?.Cast<GroupInfo>().ToList();
            if (selectedGroups == null || selectedGroups.Count == 0)
            {
                AppendToDetailsRichTextBlock("No groups selected for assignment.");
                return;
            }


        }

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            AssignmentList.Clear();

            var selectedContent = GetCheckedOptionNames();
            if (selectedContent.Count == 0)
            {
                AppendToDetailsRichTextBlock("No content types selected for import.");
                AppendToDetailsRichTextBlock("Please select at least one content type and try again.");
                return;
            }

            AppendToDetailsRichTextBlock("Listing all content.");
            ShowLoading("Loading assignment data...");
            try
            {
                foreach (var option in selectedContent)
                {
                    if (_assignmentLoaders.TryGetValue(option, out var loader))
                    {
                        try { await loader(); }
                        catch (Exception ex)
                        {
                            AppendToDetailsRichTextBlock($"Failed loading assignments for '{option}': {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                HideLoading();
            }
        }


        #endregion

        #region Content loaders

        private Dictionary<string, string> GetAllContentFromDatagrid()
        {
            // Gather all content from the datagrid and send to orchestrator

            Dictionary<string, string> content = new();

            foreach (var item in AssignmentList)
            {
                content[item.Id] = item.Type;
            }

            AppendToDetailsRichTextBlock($"Gathered {content.Count} items from DataGrid.");
            
            return content;
        }

        
        private async Task LoadAllSettingsCatalogPoliciesAsync()
        {
            ShowLoading("Loading settings catalog policies from Microsoft Graph...");
            try
            {
                var policies = await GetAllSettingsCatalogPolicies(sourceGraphServiceClient);
                foreach (var policy in policies)
                {
                    AssignmentList.Add(new AssignmentInfo
                    {
                        Name = policy.Name,
                        Type = "Settings Catalog",
                        Platform = policy.Platforms?.ToString() ?? string.Empty,
                        Id = policy.Id
                    });
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion

        #region Group / Filter retrieval
        private async Task LoadAllGroupsAsync()
        {
            GroupList.Clear();
            ShowLoading("Loading groups from Microsoft Graph...");
            try
            {
                var groups = await GetAllGroups(sourceGraphServiceClient);
                foreach (var group in groups)
                {
                    GroupList.Add(new GroupInfo { GroupName = group.DisplayName });
                }
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task SearchForGroupsAsync(string searchQuery)
        {
            GroupList.Clear();
            ShowLoading("Searching for groups in Microsoft Graph...");
            try
            {
                var groups = await SearchForGroups(sourceGraphServiceClient, searchQuery);
                foreach (var group in groups)
                {
                    GroupList.Add(new GroupInfo { GroupName = group.DisplayName });
                }
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllAssignmentFiltersAsync()
        {
            filterNameAndID.Clear();
            ShowLoading("Loading assignment filters from Microsoft Graph...");
            try
            {
                FilterOptions.Clear();
                var filters = await GetAllAssignmentFilters(destinationGraphServiceClient);
                foreach (var filter in filters)
                {
                    FilterOptions.Add(filter.DisplayName);
                    if (!filterNameAndID.ContainsKey(filter.DisplayName))
                        filterNameAndID[filter.DisplayName] = filter.Id;
                }
                if (FilterSelectionComboBox.ItemsSource != FilterOptions)
                    FilterSelectionComboBox.ItemsSource = FilterOptions;
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion

        #region Button handlers
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            AppendToDetailsRichTextBlock("Search clicked (not implemented).");
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
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
            AppendToDetailsRichTextBlock($"Removed {toRemove.Count} item(s).");
        }

        private async void AssignButton_Click(object sender, RoutedEventArgs e)
        {

            await MainOrchestrator(sourceGraphServiceClient);

            // Validate selections
            if (AppDataGrid.SelectedItems == null || AppDataGrid.SelectedItems.Count == 0)
            {
                await ShowValidationDialogAsync("No Content Selected", 
                    "Please select at least one item from the content list to assign.");
                return;
            }

            if (GroupDataGrid.SelectedItems == null || GroupDataGrid.SelectedItems.Count == 0)
            {
                await ShowValidationDialogAsync("No Groups Selected", 
                    "Please select at least one group to assign the content to.");
                return;
            }

            // Get selected items and groups
            var selectedItems = AppDataGrid.SelectedItems.Cast<AssignmentInfo>().ToList();
            var selectedGroups = GroupDataGrid.SelectedItems.Cast<GroupInfo>().ToList();

            // Get filter if selected
            string filterInfo = string.Empty;
            if (FiltersCheckBox.IsChecked == true && FilterSelectionComboBox.SelectedItem != null)
            {
                filterInfo = $" with filter '{FilterSelectionComboBox.SelectedItem}'";
            }

            // Confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = "Confirm Assignment",
                Content = $"Assign {selectedItems.Count} item(s) to {selectedGroups.Count} group(s){filterInfo}?\n\n" +
                         $"This will create assignments in Microsoft Intune.",
                PrimaryButtonText = "Assign",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                AppendToDetailsRichTextBlock("Assignment cancelled by user.");
                return;
            }

            // Perform assignment
            ShowLoading("Assigning content to groups...");
            try
            {
                AppendToDetailsRichTextBlock($"Starting assignment of {selectedItems.Count} item(s) to {selectedGroups.Count} group(s)...");

                int successCount = 0;
                int failureCount = 0;

                foreach (var item in selectedItems)
                {
                    foreach (var group in selectedGroups)
                    {
                        try
                        {
                            // TODO: Implement actual assignment logic based on item.Type
                            // For now, just log the action
                            AppendToDetailsRichTextBlock($"Assigning '{item.Name}' to group '{group.GroupName}'...");
                            
                            // Simulate assignment delay
                            await Task.Delay(100);
                            
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            AppendToDetailsRichTextBlock($"❌ Failed to assign '{item.Name}' to '{group.GroupName}': {ex.Message}");
                            failureCount++;
                        }
                    }
                }

                AppendToDetailsRichTextBlock($"Assignment completed: {successCount} successful, {failureCount} failed.");
                
                // Show completion dialog
                await ShowValidationDialogAsync("Assignment Complete", 
                    $"Successfully assigned: {successCount}\nFailed: {failureCount}");
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"❌ Assignment operation failed: {ex.Message}");
                await ShowValidationDialogAsync("Assignment Error", 
                    $"An error occurred during assignment:\n{ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private async void GroupListAllClick(object sender, RoutedEventArgs e)
        {
            await LoadAllGroupsAsync();
        }

        private async void GroupSearchClick(object sender, RoutedEventArgs e)
        {
            await SearchForGroupsAsync(GroupSearchTextBox.Text);
        }

        private async void FilterCheckBoxClick(object sender, RoutedEventArgs e)
        {
            if (FiltersCheckBox.IsChecked == true)
            {
                await LoadAllAssignmentFiltersAsync();
            }
        }

        private async void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Clear Log Console?",
                Content = "Are you sure you want to clear all log console text? This action cannot be undone.",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync().AsTask();
            if (result == ContentDialogResult.Primary)
            {
                LogConsole.Blocks.Clear();
            }
        }

        private async Task ShowValidationDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        #endregion

        #region Event handlers (Groups / Filters UI)


        private void FiltersCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            FilterSelectionComboBox.Visibility = Visibility.Visible;
        }

        private void FiltersCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            FilterSelectionComboBox.Visibility = Visibility.Collapsed;
        }

        private void FilterSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterSelectionComboBox.SelectedItem != null)
            {
                var selected = FilterSelectionComboBox.SelectedItem.ToString();
                if (filterNameAndID.TryGetValue(selected, out var id))
                {
                    SelectedFilterID = id;
                }
            }
        }
        #endregion

        #region Helpers
        private void AssignmentPage_Loaded(object sender, RoutedEventArgs e)
        {
            AutoCheckAllOptions();
        }

        private void AutoCheckAllOptions()
        {
            _suppressOptionEvents = true;
            foreach (var cb in OptionsPanel.Children.OfType<CheckBox>().Where(cb => cb.Name != "OptionsAllCheckBox"))
            {
                cb.IsChecked = true;
            }
            _suppressOptionEvents = false;

            _suppressSelectAllEvents = true;
            OptionsAllCheckBox.IsChecked = true;
            _suppressSelectAllEvents = false;
        }

        private void AppendToDetailsRichTextBlock(string text)
        {
            Paragraph paragraph;
            if (LogConsole.Blocks.Count == 0)
            {
                paragraph = new Paragraph();
                LogConsole.Blocks.Add(paragraph);
            }
            else
            {
                paragraph = LogConsole.Blocks.First() as Paragraph ?? new Paragraph();
                if (!LogConsole.Blocks.Contains(paragraph))
                    LogConsole.Blocks.Add(paragraph);
            }
            if (paragraph.Inlines.Count > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new Run { Text = text });
        }

        public List<string> GetCheckedOptionNames()
        {
            var checkedNames = new List<string>();
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true)
                {
                    checkedNames.Add(cb.Name);
                }
            }
            return checkedNames;
        }

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

        private void SelectAll_Indeterminate(object sender, RoutedEventArgs e) { }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        private void Option_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

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
