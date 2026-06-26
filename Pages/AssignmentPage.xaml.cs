using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Graph.IntuneHelperClasses;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System.ComponentModel;
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
    #region Helper Types

    public class AssignmentGroupInfo
    {
        public string? GroupName { get; set; }
        public string? GroupId { get; set; }
    }

    public class AssignmentFilterInfo
    {
        public string? FilterName { get; set; }
    }

    #endregion

    public sealed partial class AssignmentPage : BaseMultiTenantPage
    {
        #region Fields & Types

        /// <summary>
        /// Defines an assignment operation for a specific content type.
        /// </summary>
        /// <param name="ContentTypeDisplayName">The display name used in ContentType property (e.g., "Device Compliance Policy").</param>
        /// <param name="AssignAsync">Async function that performs the assignment operation.</param>
        private record AssignTypeDefinition(
            string ContentTypeDisplayName,
            Func<string, string, List<string>, GraphServiceClient, Task> AssignAsync);

        public static ObservableCollection<CustomContentInfo> AssignmentList { get; } = new();
        public ObservableCollection<AssignmentGroupInfo> GroupList { get; } = new();
        public ObservableCollection<DeviceAndAppManagementAssignmentFilter> FilterOptions { get; } = new();

        private List<CustomContentInfo> _allAssignments = new();
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        /// <summary>
        /// Maps checkbox names to ContentTypes constants for registry lookup.
        /// </summary>
        private static readonly Dictionary<string, string> CheckboxToContentType = new()
        {
            ["SettingsCatalog"] = ContentTypes.SettingsCatalog,
            ["DeviceCompliance"] = ContentTypes.DeviceCompliancePolicy,
            ["DeviceConfiguration"] = ContentTypes.DeviceConfigurationPolicy,
            ["macOSShellScript"] = ContentTypes.MacOSShellScript,
            ["PowerShellScript"] = ContentTypes.PowerShellScript,
            ["ProactiveRemediation"] = ContentTypes.ProactiveRemediation,
            ["WindowsAutopilot"] = ContentTypes.WindowsAutoPilotProfile,
            ["WindowsDriverUpdate"] = ContentTypes.WindowsDriverUpdate,
            ["WindowsFeatureUpdate"] = ContentTypes.WindowsFeatureUpdate,
            ["WindowsQualityUpdatePolicy"] = ContentTypes.WindowsQualityUpdatePolicy,
            ["WindowsQualityUpdateProfile"] = ContentTypes.WindowsQualityUpdateProfile,
            ["AppleBYODEnrollmentProfile"] = ContentTypes.AppleBYODEnrollmentProfile,
            ["Application"] = ContentTypes.Application,
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

        private DeviceAndAppManagementAssignmentFilter? _selectedFilterID;
        private string _selectedFilterName = string.Empty;


        // Include / Exclude filter mode (default Include)
        private string _selectedFilterMode = "Include";

        // Virtual group quick-select state
        private bool _includeAllUsers = false;
        private bool _includeAllDevices = false;

        // Progress tracking for assignment operations
        private int _assignTotal;
        private int _assignCurrent;
        private int _assignSuccessCount;
        private int _assignErrorCount;

        // UI initialization flag to prevent early event handlers from using null controls (e.g., LogConsole)
        private bool _uiInitialized = false;

        #endregion

        #region Constructor & Configuration

        public AssignmentPage()
        {
            this.InitializeComponent();

            _allAssignments.AddRange(AssignmentList);
            AppDataGrid.ItemsSource = AssignmentList;
            LogConsole.ItemsSource = LogEntries;

            this.Loaded += AssignmentPage_Loaded;
            RightClickMenu.AttachDataGridContextMenu(AppDataGrid);
        }

        protected override appFunction PageLogFunction => appFunction.Assignment;

        protected override string[] GetManagedControlNames() => new[]
        {
            "ContentSearchBox", "ListAllButton", "RemoveSelectedButton", "RemoveAllButton",
            "AssignButton", "GroupSearchTextBox", "GroupSearchButton", "GroupListAllButton",
            "AppDataGrid", "GroupDataGrid", "FilterExpander", "FilterSelectionComboBox",
            "FilterModeComboBox", "OptionsAllCheckBox", "ClearLogButton", "ContentTypesButton", "ExportCsvButton",
            "AllUsersToggle", "AllDevicesToggle"
        };

        #endregion

        #region Assignment Logic

        /// <summary>
        /// Gets the registry of all assignment type definitions.
        /// </summary>
        private IEnumerable<AssignTypeDefinition> GetAssignTypeRegistry()
        {
            yield return new AssignTypeDefinition(
                "Device Compliance Policy",
                async (id, name, groups, client) => await AssignGroupsToSingleDeviceCompliance(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Settings Catalog",
                async (id, name, groups, client) => await AssignGroupsToSingleSettingsCatalog(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Device Configuration Policy",
                async (id, name, groups, client) => await AssignGroupsToSingleDeviceConfiguration(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "MacOS Shell Script",
                async (id, name, groups, client) => await AssignGroupsToSingleShellScriptmacOS(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "PowerShell Script",
                async (id, name, groups, client) => await AssignGroupsToSinglePowerShellScript(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Proactive Remediation",
                async (id, name, groups, client) => await AssignGroupsToSingleProactiveRemediation(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Windows AutoPilot Profile",
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsAutoPilotProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Windows Driver Update",
                async (id, name, groups, client) => await AssignGroupsToSingleDriverProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Windows Feature Update",
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsFeatureUpdateProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Windows Quality Update Policy",
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsQualityUpdatePolicy(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Windows Quality Update Profile",
                async (id, name, groups, client) => await AssignGroupsToSingleWindowsQualityUpdateProfile(id, name, groups, client));

            yield return new AssignTypeDefinition(
                "Apple BYOD Enrollment Profile",
                async (id, name, groups, client) => await AssignGroupsToSingleAppleBYODEnrollmentProfile(id, name, groups, client));
        }

        /// <summary>
        /// Assigns a single content item to the specified groups using the registry.
        /// </summary>
        private async Task<bool> AssignContentItemAsync(
            CustomContentInfo item,
            List<string> groupList,
            GraphServiceClient graphServiceClient)
        {
            // Handle apps specially (they have "App - " prefix)
            if (item.ContentType.StartsWith("App - "))
            {
                await PrepareApplicationForAssignment(
                    new KeyValuePair<string, CustomContentInfo>(item.ContentId, item),
                    groupList,
                    graphServiceClient);
                return true;
            }

            // Find matching definition in registry
            var registry = GetAssignTypeRegistry().ToList();
            var definition = registry.FirstOrDefault(d => d.ContentTypeDisplayName == item.ContentType);

            if (definition != null)
            {
                await definition.AssignAsync(item.ContentId, item.ContentName, groupList, graphServiceClient);
                return true;
            }

            return false;
        }

        #endregion

        #region Orchestrators

        /// <summary>
        /// Main orchestrator that validates selections, confirms with user, and performs
        /// group assignments for all content items in the DataGrid.
        /// </summary>
        private async Task MainOrchestrator(GraphServiceClient graphServiceClient)
        {
            // Open the combined dialog (group selection + filter + deployment options).
            // Group validation is enforced inside the dialog \u2014 it won't close without groups.
            var deploymentOptions = await ShowAppDeploymentOptionsDialog();
            if (deploymentOptions == false)
            {
                AppLogger.UiOnly("Assignment cancelled by user.");
                return;
            }

            // Group selection and filter state are captured from dialog controls.
            var selectedGroups = GroupDataGrid.SelectedItems?.Cast<AssignmentGroupInfo>().ToList() ?? new();

            // Get all content
            var content = GetAllContentFromDatagrid();

            // Bulk operation safeguard: warn when assigning 10 or more items
            if (content.Count >= 10)
            {
                var bulkWarning = new ContentDialog
                {
                    Title = "\u26A0 Large Bulk Assignment",
                    Content = $"You are about to assign {content.Count} items. Are you sure you want to continue?",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var bulkResult = await bulkWarning.ShowAsync();
                if (bulkResult != ContentDialogResult.Primary)
                {
                    AppLogger.UiOnly("Bulk assignment cancelled by user.");
                    return;
                }
            }

            // Set filter type from expander state
            if (FilterExpander.IsExpanded)
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

            // Build group list: selected grid rows + any active virtual groups
            List<string> groupList = selectedGroups.Select(g => g.GroupId).ToList();
            if (_includeAllUsers)  groupList.Add(allUsersVirtualGroupID);
            if (_includeAllDevices) groupList.Add(allDevicesVirtualGroupID);

            var groupSummaryParts = new List<string>();
            if (selectedGroups.Count > 0) groupSummaryParts.Add($"{selectedGroups.Count} group(s)");
            if (_includeAllUsers)  groupSummaryParts.Add("All Users");
            if (_includeAllDevices) groupSummaryParts.Add("All Devices");
            var groupSummary = string.Join(", ", groupSummaryParts);



            // Final confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = "Confirm Assignment",
                Content = $"Assign {content.Count} item(s) to {groupSummary}" +
                         (string.IsNullOrEmpty(_selectedFilterName) ? "" : $" with filter '{_selectedFilterName}'") +
                         "?\n\nThis will create assignments in Microsoft Intune.",
                PrimaryButtonText = "Assign",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                AppLogger.UiOnly("Assignment cancelled by user.");
                return;
            }

            // Perform assignment
            ShowLoading("Assigning content to groups...");
            try
            {
                AppLogger.UiOnly($"Starting assignment of {content.Count} item(s) to {groupSummary}...");
                AppLogger.Info($"Assignment operation started ({content.Count} item(s)) — see Assignment.log for details.", appFunction.Main);

                // Initialize progress tracking
                _assignTotal = content.Count;
                _assignCurrent = 0;
                _assignSuccessCount = 0;
                _assignErrorCount = 0;

                ShowOperationProgress("Starting assignment...", 0, _assignTotal);

                var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int failureCount = 0;

                foreach (var item in content)
                {
                    _assignCurrent++;
                    ShowOperationProgress($"Assigning '{item.Value.ContentName}'...", _assignCurrent, _assignTotal);

                    try
                    {
                        await AssignContentItemAsync(item.Value, groupList, sourceGraphServiceClient);

                        _assignSuccessCount++;
                        typeCounts.TryGetValue(item.Value.ContentType, out int existing);
                        typeCounts[item.Value.ContentType] = existing + 1;
                        AppLogger.UiOnly($"Assigned '{item.Value.ContentName}' to {groupSummary}.");
                    }
                    catch (Exception ex)
                    {
                        _assignErrorCount++;
                        LogError($"Failed to assign '{item.Value.ContentName}': {ex.Message}");
                        failureCount++;
                    }
                }

                // Build per-type breakdown for the completion dialog
                var typeBreakdown = string.Join("\n", typeCounts
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"  · {kv.Value}× {kv.Key}"));
                var summaryBody = $"Assigned {_assignSuccessCount} item(s) to {groupSummary}.\n{typeBreakdown}";
                if (failureCount > 0)
                    summaryBody += $"\n\nFailed: {failureCount} item(s) — see log for details.";

                AppendToLog($"Assignment completed: {_assignSuccessCount} successful, {failureCount} failed.");
                AppLogger.Info($"Assignment operation completed — {_assignSuccessCount} succeeded, {failureCount} failed.", appFunction.Main);

                // Show final status
                if (_assignErrorCount == 0)
                    ShowOperationSuccess($"Assignment complete — {_assignSuccessCount} item(s) assigned to {groupSummary}");
                else
                    ShowOperationError($"Assignment completed with errors: {_assignSuccessCount} succeeded, {_assignErrorCount} failed");

                await ShowValidationDialogAsync("Assignment Complete", summaryBody);
            }
            catch (Exception ex)
            {
                AppendToLog($"Error: Assignment operation failed: {ex.Message}");
                await ShowValidationDialogAsync("Assignment Error",
                    $"An error occurred during assignment:\n{ex.Message}");
            }
            finally
            {
                HideLoading();
            }

        }

        /// <summary>
        /// Loads all content items for the selected content types from Microsoft Graph
        /// and populates the DataGrid.
        /// </summary>
        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            AssignmentList.Clear();
            _allAssignments.Clear();

            var selectedContentTypes = GetSelectedContentTypes().ToList();
            if (selectedContentTypes.Count == 0)
            {
                AppLogger.UiOnly("No content types selected.");
                AppLogger.UiOnly("Please select at least one content type and try again.");
                return;
            }

            AppLogger.UiOnly("Listing all content.");
            ShowLoading("Loading assignment data...");
            try
            {
                foreach (var contentType in selectedContentTypes)
                {
                    var op = ContentTypeRegistry.Get(contentType);
                    if (op != null)
                    {
                        try
                        {
                            var items = await op.LoadAll(graphServiceClient);
                            foreach (var item in items)
                            {
                                AssignmentList.Add(item);
                            }
                            AppLogger.UiOnly($"Loaded {items.Count()} {op.DisplayNamePlural}.");
                        }
                        catch (Exception ex)
                        {
                            LogError($"Failed loading {op.DisplayNamePlural}: {ex.Message}");
                        }
                    }
                }
                _allAssignments.AddRange(AssignmentList);
                AppDataGrid.ItemsSource = AssignmentList;
            }
            finally
            {
                HideLoading();
            }
        }


        #endregion

        #region Content loaders

        /// <summary>
        /// Gathers all content items from the DataGrid into a dictionary keyed by ContentId.
        /// </summary>
        private Dictionary<string, CustomContentInfo> GetAllContentFromDatagrid()
        {
            // Gather all content (full objects) from the datagrid and send to orchestrator
            var content = new Dictionary<string, CustomContentInfo>();

            foreach (var item in AssignmentList)
            {
                // Key = Id, Value = full CustomContentInfo (includes ContentName, ContentType, ContentPlatform)
                content[item.ContentId] = item;
            }

            AppLogger.UiOnly($"Gathered {content.Count} items from DataGrid.");
            return content;
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
                    AppLogger.UiOnly($"Intent: {_selectedInstallIntent}");
                }
                else
                {
                    AppLogger.UiOnly($"Warning: Could not parse assignment intent '{intent}'. Defaulting to 'Required'.");
                    _selectedInstallIntent = InstallIntent.Required;
                }
            }
            else
            {
                AppLogger.UiOnly("Warning: No assignment intent selected. Defaulting to 'Required'.");
                _selectedInstallIntent = InstallIntent.Required;
            }
        }

        /// <summary>
        /// Loads all Entra ID groups from Microsoft Graph into the GroupList.
        /// </summary>
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

        /// <summary>
        /// Searches for Entra ID groups matching the specified query.
        /// </summary>
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

        /// <summary>
        /// Loads all assignment filters from Microsoft Graph into the FilterOptions collection.
        /// </summary>
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
                AppLogger.UiOnly("Search cleared. Displaying all items.");
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
                AppLogger.UiOnly($"Search for '{query}' found {filtered.Count} item(s).");
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
                AppLogger.UiOnly($"Removed {selectedItems.Count} selected item(s).");
            }
            else
            {
                AppLogger.UiOnly("No items selected to remove.");
            }
        }

        private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (AssignmentList.Count == 0)
            {
                AppLogger.UiOnly("The list is already empty.");
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
                AppLogger.UiOnly($"Removed all {count} items from the list.");
            }
            else
            {
                AppLogger.UiOnly("Operation to remove all items was cancelled.");
            }
        }

        private async void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            await MainOrchestrator(sourceGraphServiceClient);
        }

        private async void GroupListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllGroupsAsync();
        }

        private async void GroupSearchButton_Click(object sender, RoutedEventArgs e)
        {
            await SearchForGroupsAsync(GroupSearchTextBox.Text?.Trim() ?? string.Empty);
        }

        private void VirtualGroupToggle_Click(object sender, RoutedEventArgs e)
        {
            _includeAllUsers  = AllUsersToggle.IsChecked == true;
            _includeAllDevices = AllDevicesToggle.IsChecked == true;
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

        private void FilterSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterSelectionComboBox.SelectedItem is DeviceAndAppManagementAssignmentFilter selectedFilter)
            {
                _selectedFilterID = selectedFilter;
                _selectedFilterName = selectedFilter.DisplayName ?? string.Empty;
                SelectedFilterID = _selectedFilterID.Id;
                IsFilterSelected = FilterExpander.IsExpanded && !string.IsNullOrWhiteSpace(SelectedFilterID);
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
            if (!_uiInitialized) return;

            if (FilterModeComboBox is not null)
            {
                FilterModeComboBox.SelectedIndex = 0; // Default to Include
            }

            if (FilterSelectionComboBox.Items.Count == 0)
            {
                await LoadAllAssignmentFiltersAsync();
            }
            _selectedFilterMode = "Include";
            IsFilterSelected = !string.IsNullOrWhiteSpace(SelectedFilterID);
            AppLogger.UiOnly("Assignment filter enabled.");
        }

        private void FilterExpander_Collapsed(Expander sender, ExpanderCollapsedEventArgs args)
        {
            if (!_uiInitialized) return;

            FilterSelectionComboBox.SelectedItem = null;

            if (FilterModeComboBox is not null)
            {
                FilterModeComboBox.SelectedIndex = 0; // Reset to Include
            }
            _selectedFilterMode = "Include";
            SelectedFilterID = null;
            IsFilterSelected = false;
            deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
            AppLogger.UiOnly("Assignment filter disabled.");
        }

        private void FilterModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiInitialized) return;
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            {
                _selectedFilterMode = item.Content?.ToString() ?? "Include";
                AppLogger.UiOnly($"Filter mode set to '{_selectedFilterMode}'.");
            }
        }

        private void IntentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Intent is now configured in the deployment options dialog
        }

        #endregion

        #region Helpers
        private void AssignmentPage_Loaded(object sender, RoutedEventArgs e)
        {
            _uiInitialized = true; // UI now safe for logging
            AutoCheckAllOptions();
            AppLogger.UiOnly("Assignment page loaded.");
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
                AppLogger.UiOnly("Sorting error: Unable to determine property name from column binding.");
                return;
            }

            // Check if property exists on AssignmentInfo
            var propInfo = typeof(CustomContentInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppLogger.UiOnly($"Sorting error: Property '{sortProperty}' not found on AssignmentInfo.");
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
                AppLogger.UiOnly($"Sorting error: {ex.Message}");
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
                // Analyse staged content to show only relevant dialog tabs/controls
                bool hasApps     = AssignmentList.Any(x => x.ContentType.StartsWith("App - ", StringComparison.OrdinalIgnoreCase));
                bool hasWin      = AssignmentList.Any(x => x.ContentType is "App - Windows app (Win32)" or "App - Windows app (WinGet)");
                bool hasIos      = AssignmentList.Any(x => x.ContentType == "App - iOS VPP app");
                bool hasAndroid  = AssignmentList.Any(x => x.ContentType.Contains("Android", StringComparison.OrdinalIgnoreCase) && x.ContentType.StartsWith("App - "));
                bool hasPolicies = AssignmentList.Any(x => !x.ContentType.StartsWith("App - ", StringComparison.OrdinalIgnoreCase));

                // Show/hide intent combo — only relevant for apps
                AssignmentIntentComboBox.Visibility = hasApps ? Visibility.Visible : Visibility.Collapsed;
                AvailableIntentInfoBar.Visibility   = hasApps ? Visibility.Visible : Visibility.Collapsed;

                // Show/hide platform tabs based on what's staged
                foreach (var tab in new[] { WindowsPivotItem, iOSPivotItem, AndroidPivotItem })
                    if (DeploymentPivot.Items.Contains(tab))
                        DeploymentPivot.Items.Remove(tab);

                if (hasWin)     DeploymentPivot.Items.Add(WindowsPivotItem);
                if (hasIos)     DeploymentPivot.Items.Add(iOSPivotItem);
                if (hasAndroid) DeploymentPivot.Items.Add(AndroidPivotItem);

                DeploymentPivot.SelectedIndex = 0;

                // Update dialog info message based on what's staged; reset severity to Informational
                DialogInfoBar.Severity = InfoBarSeverity.Informational;
                DialogInfoBar.Title = string.Empty;
                if (hasApps && !hasPolicies)
                {
                    AppDeployment.Title = "Configure Assignment";
                    DialogInfoBar.Message = "Configure app intent and platform-specific deployment settings.";
                }
                else if (!hasApps && hasPolicies)
                {
                    AppDeployment.Title = "Configure Assignment";
                    DialogInfoBar.Message = "Configure settings for this assignment.";
                }
                else
                {
                    AppDeployment.Title = "Configure Assignment";
                    DialogInfoBar.Message = "Mixed content staged — app-specific settings apply to apps only.";
                }

                // Prevent dialog from closing if no groups are selected
                void ValidateGroupsOnPrimaryClick(ContentDialog _, ContentDialogButtonClickEventArgs args)
                {
                    var selectedGroups = GroupDataGrid.SelectedItems?.Cast<AssignmentGroupInfo>().ToList() ?? new();
                    if (selectedGroups.Count == 0 && !_includeAllUsers && !_includeAllDevices)
                    {
                        DialogInfoBar.Severity = InfoBarSeverity.Error;
                        DialogInfoBar.Title = "No Groups Selected";
                        DialogInfoBar.Message = "Select at least one group, or enable 'All Users' / 'All Devices'.";
                        DialogInfoBar.IsOpen = true;
                        args.Cancel = true;
                    }
                }

                AppDeployment.PrimaryButtonClick += ValidateGroupsOnPrimaryClick;
                ContentDialogResult result = ContentDialogResult.None;
                try
                {
                    result = await AppDeployment.ShowAsync();
                }
                finally
                {
                    AppDeployment.PrimaryButtonClick -= ValidateGroupsOnPrimaryClick;
                }

                if (result == ContentDialogResult.Primary)
                {
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

                    GetInstallIntent(_selectedIntent);

                    // Validate: Available intent cannot target All Devices
                    if (_selectedInstallIntent == InstallIntent.Available && _includeAllDevices)
                    {
                        AppDeployment.Hide();
                        await ShowValidationDialogAsync("Invalid Assignment",
                            "The 'Available' intent cannot be used with the 'All Devices' virtual group.\n\n" +
                            "Please select 'Required' or 'Uninstall', or deselect 'All Devices'.");
                        return false;
                    }

                    // Warn (non-blocking) if a filter is set and content spans multiple platforms
                    if (IsFilterSelected)
                    {
                        var platforms = AssignmentList.Select(x => x.ContentPlatform).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        if (platforms.Count > 1)
                            AppLogger.UiOnly($"Warning: Assignment filter selected but content spans {platforms.Count} platforms ({string.Join(", ", platforms)}). Filters are platform-specific.");
                    }

                    GetDeliveryOptimizationPriority(_selectedDeliveryOptimizationPriority);
                    GetWin32AppNotificationValue(_selectedNotificationSetting);
                    GetAndroidManagedStoreAutoUpdateMode(_selectedAndroidManagedStoreAutoUpdateMode);

                    var iOSOptions = CreateiOSVppAppAssignmentSettings(isDeviceLicensing, uninstallOnDeviceRemoval, isRemovable, preventManagedAppBackup, preventAutoUpdate);
                    iOSAppDeploymentSettings = iOSOptions;

                    AppLogger.UiOnly("Deployment options configured:");
                    AppLogger.UiOnly($" - Intent: {_selectedInstallIntent}");
                    AppLogger.UiOnly($" - Notifications: {_selectedNotificationSetting}");
                    AppLogger.UiOnly($" - Delivery Opt: {_selectedDeliveryOptimizationPriority}");

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                AppLogger.UiOnly($"Error showing deployment options dialog: {ex.Message}");
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
                AppLogger.UiOnly("Sorting error: Unable to determine property name from column binding.");
                return;
            }

            // Check if property exists on AssignmentGroupInfo
            var propInfo = typeof(AssignmentGroupInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppLogger.UiOnly($"Sorting error: Property '{sortProperty}' not found on AssignmentGroupInfo.");
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
                AppLogger.UiOnly($"Sorting error: {ex.Message}");
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


        private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (AssignmentList.Count == 0)
            {
                LogWarning("Nothing to export — the list is empty.");
                return;
            }
            try
            {
                var savedPath = await CsvExporter.ExportContentListAsync(AssignmentList, "Assignments");
                if (savedPath != null)
                    ShowOperationSuccess($"Exported {AssignmentList.Count} items to CSV.", savedPath);
            }
            catch (Exception ex)
            {
                LogError($"CSV export failed: {ex.Message}");
            }
        }

        #endregion
    }
}
