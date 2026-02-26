using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Graph.IntuneHelperClasses;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using static IntuneTools.Graph.EntraHelperClasses.GroupHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ApplicationHelper;
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

namespace IntuneTools.Pages
{
    public class AssignmentGroupInfo
    {
        public string? GroupName { get; set; }
        public string? GroupId { get; set; }
    }

    public class AssignmentFilterInfo
    {
        public string? FilterName { get; set; }
    }

    public sealed partial class AssignmentPage : BaseMultiTenantPage
    {
        #region Variables and Properties
        public static ObservableCollection<CustomContentInfo> AssignmentList { get; } = new();
        public ObservableCollection<AssignmentGroupInfo> GroupList { get; } = new();
        public ObservableCollection<DeviceAndAppManagementAssignmentFilter> FilterOptions { get; } = new();

        private List<CustomContentInfo> _allAssignments = new();
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        private readonly Dictionary<string, Func<Task>> _assignmentLoaders;

        private DeviceAndAppManagementAssignmentFilter? _selectedFilterID;
        private string _selectedFilterName;


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
                ["DeviceCompliance"] = async () => await LoadAllDeviceCompliancePoliciesAsync(),
                ["DeviceConfiguration"] = async () => await LoadAllDeviceConfigurationPoliciesAsync(),
                ["macOSShellScript"] = async () => await LoadAllmacOSShellScriptsAsync(),
                ["PowerShellScript"] = async () => await LoadAllPowershellScriptsAsync(),
                ["ProactiveRemediation"] = async () => await LoadAllProactiveRemediationScriptsAsync(),
                ["WindowsAutopilot"] = async () => await LoadAllWindowsAutopilotProfilesAsync(),
                ["WindowsDriverUpdate"] = async () => await LoadAllWindowsDriverUpdateProfilesAsync(),
                ["WindowsFeatureUpdate"] = async () => await LoadAllWindowsFeatureUpdateProfilesAsync(),
                ["WindowsQualityUpdatePolicy"] = async () => await LoadAllWindowsQualityUpdatePoliciesAsync(),
                ["WindowsQualityUpdateProfile"] = async () => await LoadAllWindowsQualityUpdateProfilesAsync(),
                ["AppleBYODEnrollmentProfile"] = async () => await LoadAllAppleBYODEnrollmentProfilesAsync(),
                ["Application"] = async () => await LoadAllApplicationsAsync()
            };

            _allAssignments.AddRange(AssignmentList);
            AppDataGrid.ItemsSource = AssignmentList;

            this.Loaded += AssignmentPage_Loaded;
            RightClickMenu.AttachDataGridContextMenu(AppDataGrid);
            // Removed direct logging call here to avoid NullReference due to control construction order.
        }

        protected override string[] GetManagedControlNames() => new[]
        {
            "ContentSearchBox", "ListAllButton", "RemoveSelectedButton", "RemoveAllButton",
            "AssignButton", "GroupSearchTextBox", "GroupSearchButton", "GroupListAllButton",
            "AppDataGrid", "GroupDataGrid", "FilterToggle", "FilterSelectionComboBox",
            "FilterModeToggle", "OptionsAllCheckBox", "ClearLogButton", "ContentTypesButton",
            "IntentToggle"
        };

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
                AppendToLog("No groups selected for assignment.");
                AppendToLog("Please select at least one group and try again.");
                return;
            }

            // Prepare group list for assignment
            List<string> groupList = new();

            foreach (var group in selectedGroups)
            {
                groupList.Add(group.GroupId);
            }

            // Log the filter
            AppendToLog("Filter: " + _selectedFilterName);


            // Check if FilterToggle is enabled, otherwise set filter type to None
            if (FilterToggle.IsOn)
            {
                deviceAndAppManagementAssignmentFilterType =
                    string.Equals(_selectedFilterMode, "Include", StringComparison.OrdinalIgnoreCase)
                        ? DeviceAndAppManagementAssignmentFilterType.Include
                        : DeviceAndAppManagementAssignmentFilterType.Exclude;
            }
            else
            {
                deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
            }



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
                AppendToLog("Assignment cancelled by user.");
                return;
            }

            var deploymentOptions = await ShowAppDeploymentOptionsDialog();

            if (deploymentOptions == false)
            {
                AppendToLog("Assignment cancelled by user during deployment options selection.");
                return;
            }

            // Perform assignment
            ShowLoading("Assigning content to groups...");
            try
            {
                AppendToLog($"Starting assignment of {content.Count} item(s) to {selectedGroups.Count} group(s)...");

                int successCount = 0;
                int failureCount = 0;

                foreach (var item in content)
                {
                    if (item.Value.ContentType == "Device Compliance Policy")
                    {
                        await AssignGroupsToSingleDeviceCompliance(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }

                    if (item.Value.ContentType == "Settings Catalog")
                    {
                        await AssignGroupsToSingleSettingsCatalog(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Device Configuration Policy")
                    {
                        await AssignGroupsToSingleDeviceConfiguration(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "MacOS Shell Script")
                    {
                        await AssignGroupsToSingleShellScriptmacOS(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "PowerShell Script")
                    {
                        await AssignGroupsToSinglePowerShellScript(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Proactive Remediation")
                    {
                        await AssignGroupsToSingleProactiveRemediation(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Windows AutoPilot Profile")
                    {
                        await AssignGroupsToSingleWindowsAutoPilotProfile(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Windows Driver Update")
                    {
                        await AssignGroupsToSingleDriverProfile(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Windows Feature Update")
                    {
                        await AssignGroupsToSingleWindowsFeatureUpdateProfile(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Windows Quality Update Policy")
                    {
                        await AssignGroupsToSingleWindowsQualityUpdatePolicy(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Windows Quality Update Profile")
                    {
                        await AssignGroupsToSingleWindowsQualityUpdateProfile(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType == "Apple BYOD Enrollment Profile")
                    {
                        await AssignGroupsToSingleAppleBYODEnrollmentProfile(item.Value.ContentId, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.ContentType.StartsWith("App - "))
                    {
                        // Must first handle the app type
                        await PrepareApplicationForAssignment(item, groupList, sourceGraphServiceClient);

                        //await AssignGroupsToSingleApplication(item.Value.Id, groupList, sourceGraphServiceClient, _selectedInstallIntent);

                    }


                    foreach (var group in selectedGroups)
                    {
                        try
                        {
                            AppendToLog(
                                $"Assigning '{item.Value.ContentName}' to group '{group.GroupName}'.");
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            AppendToLog(
                                $"? Failed to assign '{item.Value.ContentName}' (ID: {item.Key}) to '{group.GroupName}': {ex.Message}");
                            failureCount++;
                        }
                    }
                }


                AppendToLog($"Assignment completed: {successCount} successful, {failureCount} failed.");

                // Show completion dialog
                await ShowValidationDialogAsync("Assignment Complete",
                    $"Successfully assigned: {successCount}\nFailed: {failureCount}");
            }
            catch (Exception ex)
            {
                AppendToLog($"? Assignment operation failed: {ex.Message}");
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
                AppendToLog("No content types selected for import.");
                AppendToLog("Please select at least one content type and try again.");
                return;
            }

            AppendToLog("Listing all content.");
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
                            AppendToLog($"Failed loading assignments for '{option}': {ex.Message}");
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

        private Dictionary<string, CustomContentInfo> GetAllContentFromDatagrid()
        {
            // Gather all content (full objects) from the datagrid and send to orchestrator
            var content = new Dictionary<string, CustomContentInfo>();

            foreach (var item in AssignmentList)
            {
                // Key = Id, Value = full CustomContentInfo (includes ContentName, ContentType, ContentPlatform)
                content[item.ContentId] = item;
            }

            AppendToLog($"Gathered {content.Count} items from DataGrid.");
            return content;
        }


        private async Task LoadAllSettingsCatalogPoliciesAsync()
        {
            ShowLoading("Loading settings catalog policies from Microsoft Graph...");
            try
            {
                var contentList = await GetAllSettingsCatalogContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllDeviceCompliancePoliciesAsync()
        {
            ShowLoading("Loading device compliance policies from Microsoft Graph...");
            try
            {
                var contentList = await GetAllDeviceComplianceContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllDeviceConfigurationPoliciesAsync()
        {
            ShowLoading("Loading device configuration policies from Microsoft Graph...");
            try
            {
                var contentList = await GetAllDeviceConfigurationContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllmacOSShellScriptsAsync()
        {
            ShowLoading("Loading macOS shell scripts from Microsoft Graph...");
            try
            {
                var contentList = await GetAllMacOSShellScriptContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllPowershellScriptsAsync()
        {
            ShowLoading("Loading PowerShell scripts from Microsoft Graph...");
            try
            {
                var contentList = await GetAllPowerShellScriptContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllProactiveRemediationScriptsAsync()
        {
            ShowLoading("Loading proactive remediation scripts from Microsoft Graph...");
            try
            {
                var contentList = await GetAllProactiveRemediationContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllWindowsAutopilotProfilesAsync()
        {
            ShowLoading("Loading Windows Autopilot profiles from Microsoft Graph...");
            try
            {
                var contentList = await GetAllWindowsAutoPilotContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }


        private async Task LoadAllWindowsDriverUpdateProfilesAsync()
        {
            ShowLoading("Loading Windows Driver Update profiles from Microsoft Graph...");
            try
            {
                var contentList = await GetAllWindowsDriverUpdateContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllWindowsFeatureUpdateProfilesAsync()
        {
            ShowLoading("Loading Windows Feature Update profiles from Microsoft Graph...");
            try
            {
                var contentList = await GetAllWindowsFeatureUpdateContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllWindowsQualityUpdatePoliciesAsync()
        {
            ShowLoading("Loading Windows Quality Update policies from Microsoft Graph...");
            try
            {
                var contentList = await GetAllWindowsQualityUpdatePolicyContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllWindowsQualityUpdateProfilesAsync()
        {
            ShowLoading("Loading Windows Quality Update profiles from Microsoft Graph...");
            try
            {
                var contentList = await GetAllWindowsQualityUpdateProfileContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllAppleBYODEnrollmentProfilesAsync()
        {
            ShowLoading("Loading Apple BYOD enrollment profiles from Microsoft Graph...");
            try
            {
                var contentList = await GetAllAppleBYODEnrollmentContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task LoadAllApplicationsAsync()
        {
            ShowLoading("Loading applications from Microsoft Graph...");
            try
            {
                var contentList = await GetAllApplicationContentAsync(sourceGraphServiceClient);
                foreach (var content in contentList)
                {
                    AssignmentList.Add(content);
                }
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task SearchForApplicationsAsync()
        {
            // Implement search logic if needed
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
                    AppendToLog($"Intent: {_selectedInstallIntent}");
                }
                else
                {
                    AppendToLog($"Warning: Could not parse assignment intent '{intent}'. Defaulting to 'Required'.");
                    _selectedInstallIntent = InstallIntent.Required;
                }
            }
            else
            {
                AppendToLog("Warning: No assignment intent selected. Defaulting to 'Required'.");
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
                AppendToLog("Search cleared. Displaying all items.");
            }
            else
            {
                // Perform search
                var filtered = _allAssignments.Where(item =>
                    item.ContentName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.ContentType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.ContentPlatform.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                AssignmentList.Clear();
                foreach (var item in filtered)
                {
                    AssignmentList.Add(item);
                }
                AppendToLog($"Search for '{query}' found {filtered.Count} item(s).");
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
                var selectedItems = AppDataGrid.SelectedItems.Cast<CustomContentInfo>().ToList();
                foreach (var item in selectedItems)
                {
                    AssignmentList.Remove(item);
                    _allAssignments.Remove(item);
                }
                AppendToLog($"Removed {selectedItems.Count} selected item(s).");
            }
            else
            {
                AppendToLog("No items selected to remove.");
            }
        }

        private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (AssignmentList.Count == 0)
            {
                AppendToLog("The list is already empty.");
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
                AppendToLog($"Removed all {count} items from the list.");
            }
            else
            {
                AppendToLog("Operation to remove all items was cancelled.");
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
                _selectedFilterName = selectedFilter.DisplayName ?? string.Empty;
                SelectedFilterID = _selectedFilterID.Id;
                IsFilterSelected = FilterToggle.IsOn && !string.IsNullOrWhiteSpace(SelectedFilterID);
            }
            else
            {
                _selectedFilterID = null;
                _selectedFilterName = string.Empty;
                SelectedFilterID = null;
                IsFilterSelected = false;
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
                    FilterPlatformInfoBar.IsOpen = true;

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
                    IsFilterSelected = !string.IsNullOrWhiteSpace(SelectedFilterID);
                    AppendToLog("Assignment filter enabled.");
                }
                else
                {
                    FilterSelectionComboBox.Visibility = Visibility.Collapsed;
                    FilterSelectionComboBox.SelectedItem = null;
                    FilterPlatformInfoBar.IsOpen = false;

                    if (FilterModeToggle is not null)
                    {
                        FilterModeToggle.Visibility = Visibility.Collapsed;
                        FilterModeToggle.IsOn = true; // Keep semantic default (Include) even while hidden
                    }
                    _selectedFilterMode = "Include";
                    SelectedFilterID = null;
                    IsFilterSelected = false;
                    deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
                    AppendToLog("Assignment filter disabled.");
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
                AppendToLog($"Filter mode set to '{_selectedFilterMode}'.");
            }
        }

        private void IntentToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_uiInitialized) return;
            if (sender is ToggleSwitch ts)
            {
                _selectedInstallIntent = ts.IsOn ? InstallIntent.Required : InstallIntent.Available;
                AppendToLog($"Assignment intent set to '{_selectedInstallIntent}'.");
            }
        }

        #endregion

        #region Helpers
        private void AssignmentPage_Loaded(object sender, RoutedEventArgs e)
        {
            _uiInitialized = true; // UI now safe for logging
            AutoCheckAllOptions();
            AppendToLog("Assignment page loaded.");
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

        private void AppDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (AssignmentList == null || AssignmentList.Count == 0)
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

            // Check if property exists on AssignmentInfo
            var propInfo = typeof(CustomContentInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppendToLog($"Sorting error: Property '{sortProperty}' not found on AssignmentInfo.");
                return;
            }

            // Toggle sort direction
            DataGridSortDirection? currentDirection = e.Column.SortDirection;
            ListSortDirection direction;
            if (currentDirection.HasValue && currentDirection.Value == DataGridSortDirection.Ascending)
                direction = ListSortDirection.Descending;
            else
                direction = ListSortDirection.Ascending;

            // Sort the AssignmentList in place
            List<CustomContentInfo> sorted;
            try
            {
                if (direction == ListSortDirection.Ascending)
                {
                    sorted = AssignmentList.OrderBy(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
                else
                {
                    sorted = AssignmentList.OrderByDescending(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
            }
            catch (Exception ex)
            {
                AppendToLog($"Sorting error: {ex.Message}");
                return;
            }

            // Update AssignmentList
            AssignmentList.Clear();
            foreach (var item in sorted)
                AssignmentList.Add(item);

            // Update sort direction indicator
            foreach (var col in dataGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = direction == ListSortDirection.Ascending
                ? DataGridSortDirection.Ascending
                : DataGridSortDirection.Descending;

            // Prevent default sort
            // e.Handled = true; // Removed as per workaround
        }

        private async Task<bool> ShowAppDeploymentOptionsDialog()
        {
            try
            {
                // TODO - reset the variables 

                // Show the dialog defined in XAML
                var result = await AppDeployment.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // User clicked Confirm - Store values in class-level variables
                    Variables._selectedDeploymentMode = (DeploymentModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._selectedIntent = (AssignmentIntentComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._selectedNotificationSetting = (NotificationSettingsCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._selectedDeliveryOptimizationPriority = (DeliveryOptimizationCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._selectedAndroidManagedStoreAutoUpdateMode = (UpdatePriority.SelectedItem as ComboBoxItem)?.Content.ToString();
                    Variables._licensingType = (UseDeviceLicensingCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._deviceRemovalAction = (UninstallOnDeviceRemovalCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._removable = (IsRemovableCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._preventAutoUpdate = (PreventAutoAppUpdateCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    Variables._preventManagedAppBackup = (PreventManagedAppBackupCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

                    bool isDeviceLicensing = bool.TryParse(Variables._licensingType, out bool deviceLicensing) && deviceLicensing;
                    bool uninstallOnDeviceRemoval = bool.TryParse(Variables._deviceRemovalAction, out bool deviceRemoval) && deviceRemoval;
                    bool isRemovable = bool.TryParse(Variables._removable, out bool removable) && removable;
                    bool preventAutoUpdate = bool.TryParse(Variables._preventAutoUpdate, out bool autoUpdate) && autoUpdate;
                    bool preventManagedAppBackup = bool.TryParse(Variables._preventManagedAppBackup, out bool managedAppBackup) && managedAppBackup;

                    // Store Assignment Intent (Available, Required, Uninstall)
                    GetInstallIntent(_selectedIntent);

                    // Store the delivery optimization priority (Windows)
                    GetDeliveryOptimizationPriority(_selectedDeliveryOptimizationPriority);

                    // Store the notifications mode (Windows)
                    GetWin32AppNotificationValue(_selectedNotificationSetting);

                    // Store the Android managed app auto update mode (Android)
                    GetAndroidManagedStoreAutoUpdateMode(_selectedAndroidManagedStoreAutoUpdateMode);

                    // Store the iOS options
                    var iOSOptions = CreateiOSVppAppAssignmentSettings(isDeviceLicensing, uninstallOnDeviceRemoval, isRemovable, preventManagedAppBackup, preventAutoUpdate);
                    iOSAppDeploymentSettings = iOSOptions;


                    // Log the selected options
                    AppendToLog("Application Deployment Options Configured:");
                    AppendToLog($" � Intent: {_selectedInstallIntent}");
                    AppendToLog($" � Group Mode: {_selectedDeploymentMode}");
                    AppendToLog($" � Notifications: {_selectedNotificationSetting}");
                    AppendToLog($" � Delivery Opt: {_selectedDeliveryOptimizationPriority}");

                    return true;
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // User clicked Cancel
                    return false;
                }
                else
                {
                    // Dialog was dismissed without explicit confirmation or cancellation
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppendToLog($"Error showing app options dialog: {ex.Message}");
                return false;
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

            // Check if property exists on AssignmentGroupInfo
            var propInfo = typeof(AssignmentGroupInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppendToLog($"Sorting error: Property '{sortProperty}' not found on AssignmentGroupInfo.");
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
            List<AssignmentGroupInfo> sorted;
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

        private void AssignmentIntentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AvailableIntentInfoBar == null)
                return;

            if (AssignmentIntentComboBox.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Content is string intent)
            {
                AvailableIntentInfoBar.IsOpen = string.Equals(intent, "Available", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                AvailableIntentInfoBar.IsOpen = false;
            }
        }


        #endregion
    }
}
