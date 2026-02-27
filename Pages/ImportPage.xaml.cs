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

    public class GroupInfo
    {
        public string? GroupName { get; set; }
    }

    public class FilterInfo
    {
        public string? FilterName { get; set; }
    }

    public sealed partial class ImportPage : BaseDataOperationPage
    {
        public ObservableCollection<GroupInfo> GroupList { get; set; } = new ObservableCollection<GroupInfo>();
        public ObservableCollection<FilterInfo> FilterList { get; set; } = new ObservableCollection<FilterInfo>();
        public ObservableCollection<string> FilterOptions { get; set; } = new ObservableCollection<string>();

        private bool _suppressUpdateSelectAll = false;
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

            // Count total content types to import
            _importTotal = 0;
            if (HasContentType(ContentTypes.EntraGroup)) _importTotal++;
            if (HasContentType(ContentTypes.SettingsCatalog)) _importTotal++;
            if (HasContentType(ContentTypes.DeviceCompliancePolicy)) _importTotal++;
            if (HasContentType(ContentTypes.DeviceConfigurationPolicy)) _importTotal++;
            if (HasContentType(ContentTypes.AppleBYODEnrollmentProfile)) _importTotal++;
            if (HasContentType(ContentTypes.AssignmentFilter)) _importTotal++;
            if (HasContentType(ContentTypes.PowerShellScript)) _importTotal++;
            if (HasContentType(ContentTypes.ProactiveRemediation)) _importTotal++;
            if (HasContentType(ContentTypes.MacOSShellScript)) _importTotal++;
            if (HasContentType(ContentTypes.WindowsAutoPilotProfile)) _importTotal++;
            if (HasContentType(ContentTypes.WindowsDriverUpdate)) _importTotal++;
            if (HasContentType(ContentTypes.WindowsFeatureUpdate)) _importTotal++;
            if (HasContentType(ContentTypes.WindowsQualityUpdatePolicy)) _importTotal++;
            if (HasContentType(ContentTypes.WindowsQualityUpdateProfile)) _importTotal++;

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

            // Extract group IDs into a list for later use
            List<string> groupIDs = new List<string>();
            foreach (var group in selectedGroupNameAndID)
            {
                if (!string.IsNullOrEmpty(group.Value))
                {
                    groupIDs.Add(group.Value); // Add the group ID to the list
                }
            }

            // Perform the import process

            // TODO  - Check that all info is available before proceeding with the import

            if (HasContentType(ContentTypes.EntraGroup))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Entra Groups...", _importCurrent, _importTotal);
                try
                {
                    // Import Entra Groups
                    AppendToLog("Importing Entra Groups...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Entra Groups...", LogLevels.Info);
                    var groups = GetContentIdsByType(ContentTypes.EntraGroup);
                    await ImportMultipleGroups(sourceGraphServiceClient, destinationGraphServiceClient, groups);
                    AppendToLog("Entra Groups imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Entra Groups: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Entra Groups: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.SettingsCatalog))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Settings Catalog...", _importCurrent, _importTotal);
                try
                {
                    // Import Settings Catalog policies
                    AppendToLog("Importing Settings Catalog policies...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Settings Catalog policies...", LogLevels.Info);
                    var policies = GetContentIdsByType(ContentTypes.SettingsCatalog);
                    await ImportMultipleSettingsCatalog(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Settings Catalog policies imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Settings Catalog: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Settings Catalog: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.DeviceCompliancePolicy))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Device Compliance Policies...", _importCurrent, _importTotal);
                try
                {
                    // Import Device Compliance policies
                    AppendToLog("Importing Device Compliance policies...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Device Compliance policies...", LogLevels.Info);
                    var policies = GetContentIdsByType(ContentTypes.DeviceCompliancePolicy);
                    await ImportMultipleDeviceCompliancePolicies(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Device Compliance policies imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Device Compliance Policies: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Device Compliance Policies: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.DeviceConfigurationPolicy))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Device Configuration Policies...", _importCurrent, _importTotal);
                try
                {
                    // Import Device Configuration policies
                    AppendToLog("Importing Device Configuration policies...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Device Configuration policies...", LogLevels.Info);
                    var policies = GetContentIdsByType(ContentTypes.DeviceConfigurationPolicy);
                    await ImportMultipleDeviceConfigurations(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Device Configuration policies imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Device Configuration Policies: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Device Configuration Policies: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.AppleBYODEnrollmentProfile))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Apple BYOD Enrollment Profiles...", _importCurrent, _importTotal);
                try
                {
                    // Import Apple BYOD Enrollment Profiles
                    AppendToLog("Importing Apple BYOD Enrollment Profiles...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Apple BYOD Enrollment Profiles...", LogLevels.Info);
                    var profiles = GetContentIdsByType(ContentTypes.AppleBYODEnrollmentProfile);
                    await ImportMultipleAppleBYODEnrollmentProfiles(sourceGraphServiceClient, destinationGraphServiceClient, profiles, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Apple BYOD Enrollment Profiles imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Apple BYOD Enrollment Profiles: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Apple BYOD Enrollment Profiles: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.AssignmentFilter))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Assignment Filters...", _importCurrent, _importTotal);
                try
                {
                    // Import Assignment Filters
                    AppendToLog("Importing Assignment Filters...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Assignment Filters...", LogLevels.Info);
                    var filters = GetContentIdsByType(ContentTypes.AssignmentFilter);
                    await ImportMultipleAssignmentFilters(sourceGraphServiceClient, destinationGraphServiceClient, filters);
                    AppendToLog("Assignment Filters imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Assignment Filters: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Assignment Filters: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.PowerShellScript))
            {
                _importCurrent++;
                ShowOperationProgress("Importing PowerShell Scripts...", _importCurrent, _importTotal);
                try
                {
                    // Import PowerShell Scripts
                    AppendToLog("Importing PowerShell Scripts...\n");
                    LogToFunctionFile(appFunction.Main, "Importing PowerShell Scripts...", LogLevels.Info);
                    var scripts = GetContentIdsByType(ContentTypes.PowerShellScript);
                    await ImportMultiplePowerShellScripts(sourceGraphServiceClient, destinationGraphServiceClient, scripts, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("PowerShell Scripts imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing PowerShell Scripts: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing PowerShell Scripts: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.ProactiveRemediation))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Proactive Remediations...", _importCurrent, _importTotal);
                try
                {
                    // Import Proactive Remediations
                    AppendToLog("Importing Proactive Remediations...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Proactive Remediations...", LogLevels.Info);
                    var scripts = GetContentIdsByType(ContentTypes.ProactiveRemediation);
                    await ImportMultipleProactiveRemediations(sourceGraphServiceClient, destinationGraphServiceClient, scripts, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Proactive Remediations imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Proactive Remediations: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Proactive Remediations: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.MacOSShellScript))
            {
                _importCurrent++;
                ShowOperationProgress("Importing macOS Shell Scripts...", _importCurrent, _importTotal);
                try
                {
                    // Import macOS Shell Scripts
                    AppendToLog("Importing macOS Shell Scripts...\n");
                    LogToFunctionFile(appFunction.Main, "Importing macOS Shell Scripts...", LogLevels.Info);
                    var scripts = GetContentIdsByType(ContentTypes.MacOSShellScript);
                    await ImportMultiplemacOSShellScripts(sourceGraphServiceClient, destinationGraphServiceClient, scripts, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("macOS Shell Scripts imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing macOS Shell Scripts: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing macOS Shell Scripts: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.WindowsAutoPilotProfile))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Windows AutoPilot Profiles...", _importCurrent, _importTotal);
                try
                {
                    // Import Windows AutoPilot Profiles
                    AppendToLog("Importing Windows AutoPilot Profiles...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Windows AutoPilot Profiles...", LogLevels.Info);
                    var profiles = GetContentIdsByType(ContentTypes.WindowsAutoPilotProfile);
                    await ImportMultipleWindowsAutoPilotProfiles(sourceGraphServiceClient, destinationGraphServiceClient, profiles, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Windows AutoPilot Profiles imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Windows AutoPilot Profiles: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Windows AutoPilot Profiles: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.WindowsDriverUpdate))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Windows Driver Updates...", _importCurrent, _importTotal);
                try
                {
                    // Import Windows Driver Updates
                    AppendToLog("Importing Windows Driver Updates...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Windows Driver Updates...", LogLevels.Info);
                    var updates = GetContentIdsByType(ContentTypes.WindowsDriverUpdate);
                    await ImportMultipleDriverProfiles(sourceGraphServiceClient, destinationGraphServiceClient, updates, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Windows Driver Updates imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Windows Driver Updates: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Windows Driver Updates: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.WindowsFeatureUpdate))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Windows Feature Updates...", _importCurrent, _importTotal);
                try
                {
                    // Import Windows Feature Updates
                    AppendToLog("Importing Windows Feature Updates...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Windows Feature Updates...", LogLevels.Info);
                    var updates = GetContentIdsByType(ContentTypes.WindowsFeatureUpdate);
                    await ImportMultipleWindowsFeatureUpdateProfiles(sourceGraphServiceClient, destinationGraphServiceClient, updates, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Windows Feature Updates imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Windows Feature Updates: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Windows Feature Updates: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.WindowsQualityUpdatePolicy))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Windows Quality Update Policies...", _importCurrent, _importTotal);
                try
                {
                    // Import Windows Quality Update Policies
                    AppendToLog("Importing Windows Quality Update Policies...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Windows Quality Update Policies...", LogLevels.Info);
                    var policies = GetContentIdsByType(ContentTypes.WindowsQualityUpdatePolicy);
                    await ImportMultipleWindowsQualityUpdatePolicies(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Windows Quality Update Policies imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Windows Quality Update Policies: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Windows Quality Update Policies: {ex.Message}", LogLevels.Error);
                    _importErrorCount++;
                }
            }
            if (HasContentType(ContentTypes.WindowsQualityUpdateProfile))
            {
                _importCurrent++;
                ShowOperationProgress("Importing Windows Quality Update Profiles...", _importCurrent, _importTotal);
                try
                {
                    // Import Windows Quality Update Profiles
                    AppendToLog("Importing Windows Quality Update Profiles...\n");
                    LogToFunctionFile(appFunction.Main, "Importing Windows Quality Update Profiles...", LogLevels.Info);
                    var profiles = GetContentIdsByType(ContentTypes.WindowsQualityUpdateProfile);
                    await ImportMultipleWindowsQualityUpdateProfiles(sourceGraphServiceClient, destinationGraphServiceClient, profiles, IsGroupSelected, IsFilterSelected, groupIDs);
                    AppendToLog("Windows Quality Update Profiles imported successfully.\n");
                    _importSuccessCount++;
                }
                catch (Exception ex)
                {
                    AppendToLog($"Error importing Windows Quality Update Profiles: {ex.Message}\n");
                    LogToFunctionFile(appFunction.Main, $"Error importing Windows Quality Update Profiles: {ex.Message}", LogLevels.Error);
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
            if (!string.IsNullOrEmpty(SearchQueryTextBox.Text))
            {
                await SearchOrchestrator(sourceGraphServiceClient, SearchQueryTextBox.Text);
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

        private async void GroupListAllClick(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All Groups" button is clicked
            await LoadAllGroupsAsync();
        }

        private async void GroupSearchClick(object sender, RoutedEventArgs e)
        {
            // This method is called when the "Search Groups" button is clicked
            await SearchForGroupsAsync(GroupSearchTextBox.Text);
        }

        private async void FilterCheckBoxClick(object sender, RoutedEventArgs e)
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

    }
}