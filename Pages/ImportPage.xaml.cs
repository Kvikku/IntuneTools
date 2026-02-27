using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents; // Added for Paragraph and Run
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Add this for ObservableCollection
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using static IntuneTools.Graph.EntraHelperClasses.GroupHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
using static IntuneTools.Graph.IntuneHelperClasses.FilterHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.macOSShellScript;
using static IntuneTools.Graph.IntuneHelperClasses.PowerShellScriptsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ProactiveRemediationsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsAutoPilotHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsDriverUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsFeatureUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdatePolicyHandler;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    #region Helper Types

    public class GroupInfo
    {
        public string? GroupName { get; set; }
    }

    public class FilterInfo
    {
        public string? FilterName { get; set; }
    }

    #endregion

    public sealed partial class ImportPage : BaseDataOperationPage
    {
        #region Fields & Types

        /// <summary>
        /// Defines an import operation for a specific content type.
        /// </summary>
        /// <param name="TypeKey">The ContentTypes constant identifying this content type.</param>
        /// <param name="DisplayName">Human-readable name for logging.</param>
        /// <param name="ImportAsync">Async function that performs the import operation.</param>
        private record ImportTypeDefinition(
            string TypeKey,
            string DisplayName,
            Func<List<string>, List<string>, Task> ImportAsync);

        public ObservableCollection<GroupInfo> GroupList { get; set; } = new ObservableCollection<GroupInfo>();
        public ObservableCollection<FilterInfo> FilterList { get; set; } = new ObservableCollection<FilterInfo>();
        public ObservableCollection<string> FilterOptions { get; set; } = new ObservableCollection<string>();

        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        // Progress tracking for import operations
        private int _importTotal;
        private int _importCurrent;
        private int _importSuccessCount;
        private int _importErrorCount;

        /// <summary>
        /// Maps checkbox names to ContentTypes constants.
        /// </summary>
        private static readonly Dictionary<string, string> CheckboxToContentType = new()
        {
            ["SettingsCatalog"] = ContentTypes.SettingsCatalog,
            ["DeviceCompliance"] = ContentTypes.DeviceCompliancePolicy,
            ["DeviceConfiguration"] = ContentTypes.DeviceConfigurationPolicy,
            ["AppleBYODEnrollmentProfile"] = ContentTypes.AppleBYODEnrollmentProfile,
            ["PowerShellScript"] = ContentTypes.PowerShellScript,
            ["ProactiveRemediation"] = ContentTypes.ProactiveRemediation,
            ["macOSShellScript"] = ContentTypes.MacOSShellScript,
            ["WindowsAutopilot"] = ContentTypes.WindowsAutoPilotProfile,
            ["WindowsDriverUpdate"] = ContentTypes.WindowsDriverUpdate,
            ["WindowsFeatureUpdate"] = ContentTypes.WindowsFeatureUpdate,
            ["WindowsQualityUpdatePolicy"] = ContentTypes.WindowsQualityUpdatePolicy,
            ["WindowsQualityUpdateProfile"] = ContentTypes.WindowsQualityUpdateProfile,
            ["Filters"] = ContentTypes.AssignmentFilter,
            ["EntraGroups"] = ContentTypes.EntraGroup,
        };

        #endregion

        #region Constructor & Configuration

        public ImportPage()
        {
            this.InitializeComponent();
            SelectAll_Checked(LoadingOverlay, null); // Initialize the 'Select all' checkbox to checked state
            // Ensure the new controls panel is not visible by default
            NewControlsPanel.Visibility = Visibility.Collapsed;
            //LoadFilterOptions();
            AppendToLog("Console output");
            RightClickMenu.AttachDataGridContextMenu(ContentDataGrid);
        }

        protected override string[] GetManagedControlNames() => new[]
        {
            "SearchQueryTextBox", "Search", "ListAll", "ClearSelected", "ClearAll",
            "ContentTypesButton", "GroupsCheckBox", "FiltersCheckBox", "ContentDataGrid",
            "Import", "FilterSelectionComboBox", "GroupSearchTextBox", "NewButton1",
            "NewButton2", "GroupDataGrid", "ClearLogButton"
        };

        private void LoadFilterOptions()
        {
            // Add dummy data for now
            FilterOptions.Add("Filter 1");
            FilterOptions.Add("Filter 2");
            FilterOptions.Add("Filter 3");
            FilterSelectionComboBox.ItemsSource = FilterOptions;
        }

        private List<string> GetCheckedOptionNames()
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

        /// <summary>
        /// Gets the selected ContentTypes based on checked checkboxes.
        /// </summary>
        private IEnumerable<string> GetSelectedContentTypes()
        {
            var checkedNames = GetCheckedOptionNames();
            foreach (var name in checkedNames)
            {
                if (CheckboxToContentType.TryGetValue(name, out var contentType))
                {
                    yield return contentType;
                }
            }
        }

        #endregion

        #region Core Operations

        /// Graph API Methods ///
        /// These methods should handle the actual API calls to Microsoft Graph.
        /// 

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            try
            {
                // Clear the ContentList before loading new data
                ContentList.Clear();

                // Get the selected content types
                var selectedContentTypes = GetSelectedContentTypes().ToList();

                if (selectedContentTypes.Count == 0)
                {
                    // If no options are selected, show a message and return
                    AppendToLog("No content types selected for import.");
                    return;
                }

                // Load all selected content types using the registry
                await LoadContentTypesAsync(graphServiceClient, selectedContentTypes, AppendToLog);

                // Clean up content platform value (operating system names) in ContentList
                foreach (var content in ContentList)
                {
                    var cleanedValue = TranslatePolicyPlatformName(content?.ContentPlatform);
                    content.ContentPlatform = cleanedValue ?? string.Empty;
                }

                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task SearchOrchestrator(GraphServiceClient graphServiceClient, string searchQuery)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            try
            {
                // Clear the ContentList before loading new data
                ContentList.Clear();

                // Get the selected content types
                var selectedContentTypes = GetSelectedContentTypes().ToList();

                if (selectedContentTypes.Count == 0)
                {
                    // If no options are selected, show a message and return
                    AppendToLog("No content types selected for import.");
                    return;
                }

                // Search all selected content types using the registry
                await SearchContentTypesAsync(graphServiceClient, searchQuery, selectedContentTypes, AppendToLog);

                // Clean up content platform value (operating system names) in ContentList
                foreach (var content in ContentList)
                {
                    var cleanedValue = TranslatePolicyPlatformName(content?.ContentPlatform);
                    content.ContentPlatform = cleanedValue ?? string.Empty;
                }

                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        // Note: LoadAllGroupsAsync, SearchForGroupsAsync, and LoadAllAssignmentFiltersAsync
        // are kept because they are used for the secondary group/filter assignment controls
        // and use destinationGraphServiceClient.

        private async Task SearchForGroupsAsync(string searchQuery)
        {
            // Clear the GroupList before loading new data
            GroupList.Clear();

            ShowLoading("Searching for groups in Microsoft Graph...");
            try
            {
                // Clear the GroupList before loading new data
                GroupList.Clear();
                // Search for groups using the provided query
                var groups = await SearchForGroups(destinationGraphServiceClient, searchQuery);
                // Update GroupList for DataGrid
                foreach (var group in groups)
                {
                    GroupList.Add(new GroupInfo
                    {
                        GroupName = group.DisplayName
                    });
                }
                // Bind to DataGrid
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();

            }
        }
        private async Task LoadAllGroupsAsync()
        {
            // Clear the GroupList before loading new data
            GroupList.Clear();

            ShowLoading("Loading groups from Microsoft Graph...");
            try
            {
                // Retrieve all groups
                var groups = await GetAllGroups(destinationGraphServiceClient);
                // Update ContentList for DataGrid
                foreach (var group in groups)
                {
                    GroupList.Add(new GroupInfo
                    {
                        GroupName = group.DisplayName
                    });
                }
                // Bind to DataGrid
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();
            }
        }


        /// <summary>
        /// Assignment filters for the destination tenant filter selection.
        /// This is separate from the source content loading and uses destinationGraphServiceClient.
        /// </summary>
        private async Task LoadAllAssignmentFiltersAsync()
        {
            // Clear the dictionary for filter names and IDs
            filterNameAndID.Clear();

            // TODO - update filter variables 

            ShowLoading("Loading assignment filters from Microsoft Graph...");
            try
            {
                // Clear existing filter options
                FilterOptions.Clear();

                // Retrieve all assignment filters
                var filters = await GetAllAssignmentFilters(destinationGraphServiceClient);
                // Update FilterOptions for ComboBox
                foreach (var filter in filters)
                {
                    FilterOptions.Add(filter.DisplayName); // Add filter display name to ComboBox options

                }
                // Ensure ComboBox is bound to FilterOptions (though it should be from XAML or initialization)
                if (FilterSelectionComboBox.ItemsSource != FilterOptions)
                {
                    FilterSelectionComboBox.ItemsSource = FilterOptions;
                }
            }
            finally
            {
                HideLoading();
            }
        }

        #endregion

        #region Import Logic

        /// <summary>
        /// Gets the registry of all import type definitions.
        /// </summary>
        private IEnumerable<ImportTypeDefinition> GetImportTypeRegistry(
            bool isGroupSelected,
            bool isFilterSelected,
            List<string> groupIds)
        {
            yield return new ImportTypeDefinition(
                ContentTypes.EntraGroup,
                "Entra Groups",
                async (ids, _) => await ImportMultipleGroups(sourceGraphServiceClient, destinationGraphServiceClient, ids));

            yield return new ImportTypeDefinition(
                ContentTypes.SettingsCatalog,
                "Settings Catalog policies",
                async (ids, grpIds) => await ImportMultipleSettingsCatalog(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.DeviceCompliancePolicy,
                "Device Compliance policies",
                async (ids, grpIds) => await ImportMultipleDeviceCompliancePolicies(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.DeviceConfigurationPolicy,
                "Device Configuration policies",
                async (ids, grpIds) => await ImportMultipleDeviceConfigurations(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.AppleBYODEnrollmentProfile,
                "Apple BYOD Enrollment Profiles",
                async (ids, grpIds) => await ImportMultipleAppleBYODEnrollmentProfiles(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.AssignmentFilter,
                "Assignment Filters",
                async (ids, _) => await ImportMultipleAssignmentFilters(sourceGraphServiceClient, destinationGraphServiceClient, ids));

            yield return new ImportTypeDefinition(
                ContentTypes.PowerShellScript,
                "PowerShell Scripts",
                async (ids, grpIds) => await ImportMultiplePowerShellScripts(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.ProactiveRemediation,
                "Proactive Remediations",
                async (ids, grpIds) => await ImportMultipleProactiveRemediations(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.MacOSShellScript,
                "macOS Shell Scripts",
                async (ids, grpIds) => await ImportMultiplemacOSShellScripts(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsAutoPilotProfile,
                "Windows AutoPilot Profiles",
                async (ids, grpIds) => await ImportMultipleWindowsAutoPilotProfiles(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsDriverUpdate,
                "Windows Driver Updates",
                async (ids, grpIds) => await ImportMultipleDriverProfiles(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsFeatureUpdate,
                "Windows Feature Updates",
                async (ids, grpIds) => await ImportMultipleWindowsFeatureUpdateProfiles(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsQualityUpdatePolicy,
                "Windows Quality Update Policies",
                async (ids, grpIds) => await ImportMultipleWindowsQualityUpdatePolicies(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));

            yield return new ImportTypeDefinition(
                ContentTypes.WindowsQualityUpdateProfile,
                "Windows Quality Update Profiles",
                async (ids, grpIds) => await ImportMultipleWindowsQualityUpdateProfiles(sourceGraphServiceClient, destinationGraphServiceClient, ids, isGroupSelected, isFilterSelected, grpIds));
        }

        /// <summary>
        /// Main import process
        /// </summary>

        private List<string> LogContentToImport()
        {
            LogToFunctionFile(appFunction.Main, "Importing the following content:", LogLevels.Info);
            AppendToLog("Importing the following content:\n");

            List<string> contentTypes = new List<string>();

            foreach (var content in ContentList)
            {
                // add content type to the list if not already present
                if (!contentTypes.Contains(content.ContentType))
                {
                    contentTypes.Add(content.ContentType);
                    LogToFunctionFile(appFunction.Main, $"- {content.ContentType}", LogLevels.Info);
                    AppendToLog($"- {content.ContentType}\n");
                }
            }

            LogToFunctionFile(appFunction.Main, "--------------------------------------------------", LogLevels.Info);
            AppendToLog("--------------------------------------------------\n");
            return contentTypes;
        }

        private void LogGroupsToBeAssigned()
        {
            selectedGroupNameAndID.Clear(); // Clear previous selections
            IsGroupSelected = false; // Reset group selection status

            LogToFunctionFile(appFunction.Main, "Assigning to the following groups:", LogLevels.Info);
            AppendToLog("Assigning to the following groups:\n");
            if (GroupDataGrid.SelectedItems != null && GroupDataGrid.SelectedItems.Count > 0)
            {
                foreach (GroupInfo selectedGroup in GroupDataGrid.SelectedItems)
                {
                    if (selectedGroup != null && !string.IsNullOrEmpty(selectedGroup.GroupName))
                    {
                        LogToFunctionFile(appFunction.Main, $"- {selectedGroup.GroupName}", LogLevels.Info);
                        AppendToLog($"- {selectedGroup.GroupName}\n");
                        // Add the group name and ID to the selectedGroupNameAndID dictionary
                        if (!selectedGroupNameAndID.ContainsKey(selectedGroup.GroupName))
                        {
                            selectedGroupNameAndID[selectedGroup.GroupName] = groupNameAndID[selectedGroup.GroupName];
                        }
                    }
                }
                IsGroupSelected = true; // Set group selection status to true if any groups are selected
            }
            else
            {
                LogToFunctionFile(appFunction.Main, "No groups selected for assignment.", LogLevels.Info);
                AppendToLog("No groups selected for assignment.\n");
            }
            LogToFunctionFile(appFunction.Main, "--------------------------------------------------", LogLevels.Info);
            AppendToLog("--------------------------------------------------\n");
        }

        private void LogFiltersToBeApplied()
        {
            IsFilterSelected = false; // Reset filter selection status

            LogToFunctionFile(appFunction.Main, "Applying the following filters:", LogLevels.Info);
            AppendToLog("Applying the following filters:\n");
            if (FilterSelectionComboBox.SelectedItem != null)
            {
                string selectedFilter = FilterSelectionComboBox.SelectedItem.ToString();

                SelectedFilterID = filterNameAndID.ContainsKey(selectedFilter) ? filterNameAndID[selectedFilter] : null;
                deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.Include;

                LogToFunctionFile(appFunction.Main, $"- {selectedFilter}", LogLevels.Info);
                AppendToLog($"- {selectedFilter}\n");
                IsFilterSelected = true; // Set filter selection status to true if a filter is selected
            }
            else
            {
                LogToFunctionFile(appFunction.Main, "No filter selected for assignment.", LogLevels.Info);
                AppendToLog("No filter selected for assignment.\n");
            }
            LogToFunctionFile(appFunction.Main, "--------------------------------------------------", LogLevels.Info);
            AppendToLog("--------------------------------------------------\n");
        }


        private async Task MainImportProcess()
        {
            AppendToLog("Starting import process...\n");

            // Check if there is content to import
            if (ContentList.Count == 0)
            {
                LogToFunctionFile(appFunction.Main, "No content to import.", LogLevels.Warning);
                AppendToLog("No content to import.\n");
                return;
            }

            // Initialize progress tracking
            _importCurrent = 0;
            _importSuccessCount = 0;
            _importErrorCount = 0;

            // Extract group IDs into a list for later use
            List<string> groupIds = selectedGroupNameAndID
                .Where(g => !string.IsNullOrEmpty(g.Value))
                .Select(g => g.Value)
                .ToList();

            // Get import registry with current group/filter selection state
            var importRegistry = GetImportTypeRegistry(IsGroupSelected, IsFilterSelected, groupIds).ToList();

            // Count total content types to import
            _importTotal = importRegistry.Count(def => HasContentType(def.TypeKey));

            ShowOperationProgress("Starting import...", 0, _importTotal);

            // Log the start of the import process
            LogToFunctionFile(appFunction.Main, "Starting import process...", LogLevels.Info);
            LogToFunctionFile(appFunction.Main, $"Source Tenant: {sourceTenantName}", LogLevels.Info);
            LogToFunctionFile(appFunction.Main, $"Destination Tenant: {destinationTenantName}", LogLevels.Info);
            AppendToLog($"Source Tenant: {sourceTenantName}\n");
            AppendToLog($"Destination Tenant: {destinationTenantName}\n");

            // Log what content is being imported
            var contentList = LogContentToImport();

            // Log which group(s) are being assigned
            LogGroupsToBeAssigned();

            // Log which filter(s) are being applied
            LogFiltersToBeApplied();

            // Perform the import process using the registry
            foreach (var definition in importRegistry)
            {
                if (!HasContentType(definition.TypeKey))
                    continue;

                _importCurrent++;
                ShowOperationProgress($"Importing {definition.DisplayName}...", _importCurrent, _importTotal);

                try
                {
                    AppendToLog($"Importing {definition.DisplayName}...\n");
                    LogToFunctionFile(appFunction.Main, $"Importing {definition.DisplayName}...", LogLevels.Info);

                    var contentIds = GetContentIdsByType(definition.TypeKey);
                    await definition.ImportAsync(contentIds, groupIds);

                    AppendToLog($"{definition.DisplayName} imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing {definition.DisplayName}: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing {definition.DisplayName}: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }

            // Show final status
            if (_importErrorCount == 0)
            {
                ShowOperationSuccess($"Import completed: {_importSuccessCount} content type(s) imported successfully");
            }
            else
            {
                ShowOperationError($"Import completed with errors: {_importSuccessCount} succeeded, {_importErrorCount} failed");
            }

            AppendToLog("Import process finished.\n");
        }

        #endregion

        #region Event Handlers

        /// BUTTON HANDLERS ///
        /// Buttons should be defined in the XAML file and linked to these methods.
        /// Buttons should call other methods to perform specific actions.
        /// Buttons should not directly perform actions themselves.
        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            await MainImportProcess();
        }
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = SearchQueryTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(searchQuery))
            {
                await SearchOrchestrator(sourceGraphServiceClient, searchQuery);
            }
            else
            {
                AppendToLog("Search query cannot be empty.");
            }
        }
        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All" button is clicked
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private async void GroupListAllButton_Click(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All Groups" button is clicked
            await LoadAllGroupsAsync();
        }

        private async void GroupSearchButton_Click(object sender, RoutedEventArgs e)
        {
            // This method is called when the "Search Groups" button is clicked
            await SearchForGroupsAsync(GroupSearchTextBox.Text?.Trim() ?? string.Empty);
        }

        private async void FiltersCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All Assignment Filters" button is clicked
            await LoadAllAssignmentFiltersAsync();
            NewControlsPanel.Visibility = Visibility.Visible;
            GroupsCheckBox.IsChecked = true;
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all items from ContentList, which will update the DataGrid
            ContentList.Clear();
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            // Remove only the selected items from ContentList
            if (ContentDataGrid.SelectedItems != null && ContentDataGrid.SelectedItems.Count > 0)
            {
                // To avoid modifying the collection while iterating, copy selected items to a list
                var itemsToRemove = ContentDataGrid.SelectedItems.Cast<CustomContentInfo>().ToList();
                foreach (var item in itemsToRemove)
                {
                    ContentList.Remove(item);
                }
            }
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

        private void GroupsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            NewControlsPanel.Visibility = Visibility.Visible;
            // Call the general Option_Checked handler if needed for other logic (like updating SelectAllCheckBox)
            Option_Checked(sender, e);
        }

        private void GroupsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            NewControlsPanel.Visibility = Visibility.Collapsed;
            // Call the general Option_Unchecked handler if needed for other logic
            Option_Unchecked(sender, e);
        }

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
            // Handle filter selection change
            // For now, just a placeholder
            if (FilterSelectionComboBox.SelectedItem != null)
            {
                string selectedFilter = FilterSelectionComboBox.SelectedItem.ToString();
                // You can add logic here to use the selectedFilter
            }
        }

        private void GroupDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (GroupList == null || GroupList.Count == 0)
                return;

            // Get the property name from the column binding
            var textColumn = e.Column as DataGridTextColumn;
            var binding = textColumn?.Binding as Binding;
            string sortProperty = binding?.Path?.Path;
            if (string.IsNullOrEmpty(sortProperty))
            {
                AppendToLog("Sorting error: Unable to determine property name from column binding.");
                return;
            }

            // Check if property exists on GroupInfo
            var propInfo = typeof(GroupInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppendToLog($"Sorting error: Property '{sortProperty}' not found on GroupInfo.");
                return;
            }

            // Toggle sort direction
            DataGridSortDirection? currentDirection = e.Column.SortDirection;
            ListSortDirection direction;
            if (currentDirection.HasValue && currentDirection.Value == DataGridSortDirection.Ascending)
                direction = ListSortDirection.Descending;
            else
                direction = ListSortDirection.Ascending;

            // Sort the GroupList in place
            List<GroupInfo> sorted;
            try
            {
                if (direction == ListSortDirection.Ascending)
                {
                    sorted = GroupList.OrderBy(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
                else
                {
                    sorted = GroupList.OrderByDescending(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
            }
            catch (Exception ex)
            {
                AppendToLog($"Sorting error: {ex.Message}");
                return;
            }

            // Update GroupList
            GroupList.Clear();
            foreach (var item in sorted)
                GroupList.Add(item);

            // Update sort direction indicator
            foreach (var col in dataGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = direction == ListSortDirection.Ascending
                ? DataGridSortDirection.Ascending
                : DataGridSortDirection.Descending;

            // Prevent default sort
            // e.Handled = true; // Uncomment if needed for your toolkit version
        }

        private void ContentDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        #endregion
    }
}