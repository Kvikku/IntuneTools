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
using static IntuneTools.Graph.IntuneHelperClasses.FilterHelperClass;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;
using IntuneTools.Graph.IntuneHelperClasses;
using Microsoft.Graph.Beta.Models;

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
        public string? GroupId { get; set; }
    }

    public class AssignmentFilterInfo
    {
        public string? FilterName { get; set; }
    }

    public sealed partial class AssignmentPage : Page
    {
        #region Variables and Properties
        public static ObservableCollection<AssignmentInfo> AssignmentList { get; } = new();
        public ObservableCollection<AssignmentGroupInfo> GroupList { get; } = new();
        public ObservableCollection<DeviceAndAppManagementAssignmentFilter> FilterOptions { get; } = new();

        private List<AssignmentInfo> _allAssignments = new();
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        private readonly Dictionary<string, Func<Task>> _assignmentLoaders;

        private DeviceAndAppManagementAssignmentFilter? _selectedFilterID;
        private string _selectedFilterName;
        private InstallIntent _selectedInstallIntent;

        // New: Include / Exclude filter mode (default Include)
        private string _selectedFilterMode = "Include";

        // UI initialization flag to prevent early event handlers from using null controls (e.g., LogConsole)
        private bool _uiInitialized = false;
        #endregion

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

            _allAssignments.AddRange(AssignmentList);
            AppDataGrid.ItemsSource = AssignmentList;

            this.Loaded += AssignmentPage_Loaded;
            // Removed direct logging call here to avoid NullReference due to control construction order.
        }

        #region Loading Overlay
        private void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            LoadingStatusText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingProgressRing.IsActive = true;

            ContentSearchBox.IsEnabled = false;
            ListAllButton.IsEnabled = false;
            RemoveSelectedButton.IsEnabled = false;
            AssignButton.IsEnabled = false;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingProgressRing.IsActive = false;

            ContentSearchBox.IsEnabled = true;
            ListAllButton.IsEnabled = true;
            RemoveSelectedButton.IsEnabled = true;
            AssignButton.IsEnabled = true;
        }
        #endregion

        #region Orchestrators

        private async Task MainOrchestrator(GraphServiceClient graphServiceClient)
        {
            // Main orchestrator of assignment operations


            // Validate selections 
            if (GroupDataGrid.SelectedItems == null || GroupDataGrid.SelectedItems.Count == 0)
            {
                await ShowValidationDialogAsync("No Groups Selected",
                    "Please select at least one group to assign the content to.");
                return;
            }


            // Get all content
            var content = GetAllContentFromDatagrid();

            // Get groups
            var selectedGroups = GroupDataGrid.SelectedItems?.Cast<AssignmentGroupInfo>().ToList();
            if (selectedGroups == null || selectedGroups.Count == 0)
            {
                AppendToDetailsRichTextBlock("No groups selected for assignment.");
                AppendToDetailsRichTextBlock("Please select at least one group and try again.");
                return;
            }

            // Prepare group list for assignment
            List<string> groupList = new();

            foreach (var group in selectedGroups)
            {
                groupList.Add(group.GroupId);
            }

            // Log the filter
            AppendToDetailsRichTextBlock("Filter: " + _selectedFilterName);


            deviceAndAppManagementAssignmentFilterType =
                string.Equals(_selectedFilterMode, "Include", StringComparison.OrdinalIgnoreCase)
                    ? DeviceAndAppManagementAssignmentFilterType.Include
                    : DeviceAndAppManagementAssignmentFilterType.Exclude;

            // Get and log install intent
            UpdateSelectedInstallIntent();


            // Get selected items and groups
            //var selectedItems = AppDataGrid.SelectedItems.Cast<AssignmentInfo>().ToList();


            // Confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = "Confirm Assignment",
                Content = $"Assign {content.Count} item(s) to {selectedGroups.Count} group(s) with filter '{_selectedFilterName}' and intent '{_selectedInstallIntent}'?\n\n" +
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
                AppendToDetailsRichTextBlock($"Starting assignment of {content.Count} item(s) to {selectedGroups.Count} group(s)...");

                int successCount = 0;
                int failureCount = 0;

                foreach (var item in content)
                {

                    // Test settings catalog

                    await AssignGroupsToSingleSettingsCatalog(item.Value, groupList, sourceGraphServiceClient);

                    foreach (var group in selectedGroups)
                    {
                        try
                        {
                            // TODO: Implement actual assignment logic based on item.Type



                            // For now, just log the action
                            AppendToDetailsRichTextBlock($"Assigning '{item.Key}' to group '{group.GroupName}'.");


                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            AppendToDetailsRichTextBlock($"❌ Failed to assign '{item.Key}' to '{group.GroupName}': {ex.Message}");
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

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            AssignmentList.Clear();
            _allAssignments.Clear();

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
                _allAssignments.AddRange(AssignmentList);
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
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = policy.Name,
                        Type = "Settings Catalog",
                        Platform = policy.Platforms?.ToString() ?? string.Empty,
                        Id = policy.Id
                    };
                    AssignmentList.Add(assignmentInfo);
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
        private void UpdateSelectedInstallIntent()
        {
            if (AssignmentIntentComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content is string intent)
            {
                if (Enum.TryParse(intent, out InstallIntent parsedIntent))
                {
                    _selectedInstallIntent = parsedIntent;
                    AppendToDetailsRichTextBlock($"Intent: {_selectedInstallIntent}");
                }
                else
                {
                    AppendToDetailsRichTextBlock($"Warning: Could not parse assignment intent '{intent}'. Defaulting to 'Required'.");
                    _selectedInstallIntent = InstallIntent.Required;
                }
            }
            else
            {
                AppendToDetailsRichTextBlock("Warning: No assignment intent selected. Defaulting to 'Required'.");
                _selectedInstallIntent = InstallIntent.Required;
            }
        }

        private async Task LoadAllGroupsAsync()
        {
            GroupList.Clear();
            ShowLoading("Loading groups from Microsoft Graph...");
            try
            {
                var groups = await GetAllGroups(sourceGraphServiceClient);
                foreach (var group in groups)
                {
                    GroupList.Add(new AssignmentGroupInfo 
                    { 
                        GroupName = group.DisplayName,
                        GroupId = group.Id
                    });
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
                    GroupList.Add(new AssignmentGroupInfo 
                    { 
                        GroupName = group.DisplayName,
                        GroupId = group.Id
                    });
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
            ShowLoading("Loading assignment filters from Microsoft Graph...");
            try
            {
                FilterOptions.Clear();
                var filters = await GetAllAssignmentFilters(sourceGraphServiceClient);
                foreach (var filter in filters)
                {
                    FilterOptions.Add(filter);
                }

                if (FilterSelectionComboBox.ItemsSource != FilterOptions)
                {
                    FilterSelectionComboBox.ItemsSource = FilterOptions;
                    FilterSelectionComboBox.DisplayMemberPath = "DisplayName";
                }
            }
            finally
            {
                HideLoading();
            }
        }
        #endregion

        #region Button handlers
        private void ContentSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var query = sender.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                // If query is empty, restore the full list
                AssignmentList.Clear();
                foreach (var item in _allAssignments)
                {
                    AssignmentList.Add(item);
                }
                AppendToDetailsRichTextBlock("Search cleared. Displaying all items.");
            }
            else
            {
                // Perform search
                var filtered = _allAssignments.Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Type.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Platform.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                AssignmentList.Clear();
                foreach (var item in filtered)
                {
                    AssignmentList.Add(item);
                }
                AppendToDetailsRichTextBlock($"Search for '{query}' found {filtered.Count} item(s).");
            }
        }

        private void ContentSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // If the text box is cleared, restore the full list.
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && string.IsNullOrEmpty(sender.Text))
            {
                AssignmentList.Clear();
                foreach (var item in _allAssignments)
                {
                    AssignmentList.Add(item);
                }
            }
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppDataGrid.SelectedItems.Count > 0)
            {
                var selectedItems = AppDataGrid.SelectedItems.Cast<AssignmentInfo>().ToList();
                foreach (var item in selectedItems)
                {
                    AssignmentList.Remove(item);
                    _allAssignments.Remove(item);
                }
                AppendToDetailsRichTextBlock($"Removed {selectedItems.Count} selected item(s).");
            }
            else
            {
                AppendToDetailsRichTextBlock("No items selected to remove.");
            }
        }

        private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (AssignmentList.Count == 0)
            {
                AppendToDetailsRichTextBlock("The list is already empty.");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Remove All Items?",
                Content = $"Are you sure you want to remove all {AssignmentList.Count} items from the list?",
                PrimaryButtonText = "Remove All",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var count = AssignmentList.Count;
                AssignmentList.Clear();
                _allAssignments.Clear();
                AppendToDetailsRichTextBlock($"Removed all {count} items from the list.");
            }
            else
            {
                AppendToDetailsRichTextBlock("Operation to remove all items was cancelled.");
            }
        }

        private async void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            await MainOrchestrator(sourceGraphServiceClient);
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
            if (FilterSelectionComboBox.SelectedItem is DeviceAndAppManagementAssignmentFilter selectedFilter)
            {
                _selectedFilterID = selectedFilter;
                _selectedFilterName = selectedFilter.DisplayName;
                SelectedFilterID = _selectedFilterID.Id;

                //AppendToDetailsRichTextBlock($"Selected filter: '{_selectedFilterName}' (ID: {_selectedFilterID.Id})");
            }
            else
            {
                _selectedFilterID = null;
                _selectedFilterName = string.Empty;
                SelectedFilterID = null;
                //AppendToDetailsRichTextBlock("Filter selection cleared.");
            }
        }

        private async void FilterExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            if (FilterSelectionComboBox.Items.Count == 0)
            {
                try
                {
                    var filters = await FilterHelperClass.GetAllAssignmentFilters(sourceGraphServiceClient);
                    if (filters != null)
                    {
                        FilterSelectionComboBox.ItemsSource = filters;
                        FilterSelectionComboBox.DisplayMemberPath = "DisplayName";
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions, e.g., log them or show a message
                    // Log("Failed to load filters: " + ex.Message);
                }
            }
        }

        private async void FilterToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_uiInitialized) return; // Prevent early logging before controls are ready

            if (sender is ToggleSwitch toggleSwitch)
            {
                if (toggleSwitch.IsOn)
                {
                    FilterSelectionComboBox.Visibility = Visibility.Visible;

                    if (FilterModeToggle is not null)
                    {
                        // Ensure default is Include when shown
                        FilterModeToggle.IsOn = true; // On now means Include
                        FilterModeToggle.Visibility = Visibility.Visible;
                    }

                    if (FilterSelectionComboBox.Items.Count == 0)
                    {
                        await LoadAllAssignmentFiltersAsync();
                    }
                    _selectedFilterMode = "Include";
                    AppendToDetailsRichTextBlock("Assignment filter enabled (Mode=" + _selectedFilterMode + ").");
                }
                else
                {
                    FilterSelectionComboBox.Visibility = Visibility.Collapsed;
                    FilterSelectionComboBox.SelectedItem = null;

                    if (FilterModeToggle is not null)
                    {
                        FilterModeToggle.Visibility = Visibility.Collapsed;
                        FilterModeToggle.IsOn = true; // Keep semantic default (Include) even while hidden
                    }
                    _selectedFilterMode = "Include";
                    AppendToDetailsRichTextBlock("Assignment filter disabled.");
                }
            }
        }

        // Updated semantics: IsOn = Include, IsOff = Exclude
        private void FilterModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_uiInitialized) return; // Prevent logging before LogConsole is ready
            if (sender is ToggleSwitch ts)
            {
                _selectedFilterMode = ts.IsOn ? "Include" : "Exclude";
                AppendToDetailsRichTextBlock($"Filter mode set to '{_selectedFilterMode}'.");
            }
        }
        #endregion

        #region Helpers
        private void AssignmentPage_Loaded(object sender, RoutedEventArgs e)
        {
            _uiInitialized = true; // UI now safe for logging
            AutoCheckAllOptions();
            AppendToDetailsRichTextBlock("Assignment page loaded.");
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
            // Guard against null LogConsole (early calls) or not yet initialized UI
            if (LogConsole == null || !_uiInitialized) return;

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
            foreach (var checkbox in OptionsPanel.Children.OfType<CheckBox>())
            {
                checkbox.IsChecked = true;
            }
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
