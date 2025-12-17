using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Graph.IntuneHelperClasses;
using IntuneTools.Utilities;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
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
using static IntuneTools.Graph.IntuneHelperClasses.ApplicationHelper;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;
using Microsoft.Graph.Beta.DeviceManagement.DeviceConfigurations.Item.GetOmaSettingPlainTextValueWithSecretReferenceValueId;

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
            // Removed direct logging call here to avoid NullReference due to control construction order.
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (string.Equals(Variables.sourceTenantName, string.Empty))
            {
                TenantInfoBar.Title = "Authentication Required";
                TenantInfoBar.Message = "You must authenticate with a tenant before using assignment features.";
                TenantInfoBar.Severity = InfoBarSeverity.Warning;
                TenantInfoBar.IsOpen = true;

                // Disable main controls
                ContentSearchBox.IsEnabled = false;
                ListAllButton.IsEnabled = false;
                RemoveSelectedButton.IsEnabled = false;
                RemoveAllButton.IsEnabled = false;
                AssignButton.IsEnabled = false;
                GroupSearchTextBox.IsEnabled = false;
                GroupSearchButton.IsEnabled = false;
                GroupListAllButton.IsEnabled = false;
                AppDataGrid.IsEnabled = false;
                GroupDataGrid.IsEnabled = false;
                FilterToggle.IsEnabled = false;
                FilterSelectionComboBox.IsEnabled = false;
                FilterModeToggle.IsEnabled = false;
                //OptionsPanel.IsEnabled = false;
                OptionsAllCheckBox.IsEnabled = false;
                ClearLogButton.IsEnabled = false;
                OptionsAllCheckBox.IsEnabled = false;
                ContentTypesButton.IsEnabled = false;
                IntentToggle.IsEnabled = false;
            }
            else
            {
                TenantInfoBar.Title = "Authenticated Tenant";
                TenantInfoBar.Message = Variables.sourceTenantName;
                TenantInfoBar.Severity = InfoBarSeverity.Informational;
                TenantInfoBar.IsOpen = true;

                // Enable main controls
                ContentSearchBox.IsEnabled = true;
                ListAllButton.IsEnabled = true;
                RemoveSelectedButton.IsEnabled = true;
                RemoveAllButton.IsEnabled = true;
                AssignButton.IsEnabled = true;
                GroupSearchTextBox.IsEnabled = true;
                GroupSearchButton.IsEnabled = true;
                GroupListAllButton.IsEnabled = true;
                AppDataGrid.IsEnabled = true;
                GroupDataGrid.IsEnabled = true;
                FilterToggle.IsEnabled = true;
                FilterSelectionComboBox.IsEnabled = true;
                FilterModeToggle.IsEnabled = true;
                //OptionsPanel.IsEnabled = true;
                OptionsAllCheckBox.IsEnabled = true;
                ClearLogButton.IsEnabled = true;
                OptionsAllCheckBox.IsEnabled = true;
                ContentTypesButton.IsEnabled = true;
                IntentToggle.IsEnabled = true;
            }
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
                AppendToDetailsRichTextBlock("Assignment cancelled by user.");
                return;
            }

            var deploymentOptions = await ShowAppDeploymentOptionsDialog();

            if (deploymentOptions == false)
            {
                AppendToDetailsRichTextBlock("Assignment cancelled by user during deployment options selection.");
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
                    if (item.Value.Type == "Device Compliance")
                    {
                        await AssignGroupsToSingleDeviceCompliance(item.Value.Id, groupList, sourceGraphServiceClient);
                    }

                    if (item.Value.Type == "Settings Catalog")
                    {
                        await AssignGroupsToSingleSettingsCatalog(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Device Configuration")
                    {
                        await AssignGroupsToSingleDeviceConfiguration(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "macOS Shell Script")
                    {
                        await AssignGroupsToSingleShellScriptmacOS(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "PowerShell Script")
                    {
                        await AssignGroupsToSinglePowerShellScript(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Proactive Remediation Script")
                    {
                        await AssignGroupsToSingleProactiveRemediation(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Windows Autopilot Profile")
                    {
                        await AssignGroupsToSingleWindowsAutoPilotProfile(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Windows Driver Update Profile")
                    {
                        await AssignGroupsToSingleDriverProfile(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Windows Feature Update Profile")
                    {
                        await AssignGroupsToSingleWindowsFeatureUpdateProfile(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Windows Quality Update Policy")
                    {
                        await AssignGroupsToSingleWindowsQualityUpdatePolicy(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Windows Quality Update Profile")
                    {
                        await AssignGroupsToSingleWindowsQualityUpdateProfile(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type == "Apple BYOD Enrollment Profile")
                    {
                        await AssignGroupsToSingleAppleBYODEnrollmentProfile(item.Value.Id, groupList, sourceGraphServiceClient);
                    }
                    if (item.Value.Type.StartsWith("App - "))
                    {
                        // Must first handle the app type
                        await PrepareApplicationForAssignment(item,groupList, sourceGraphServiceClient);

                        //await AssignGroupsToSingleApplication(item.Value.Id, groupList, sourceGraphServiceClient, _selectedInstallIntent);

                    }





                    foreach (var group in selectedGroups)
                    {
                        try
                        {
                            AppendToDetailsRichTextBlock(
                                $"Assigning '{item.Value.Name}' to group '{group.GroupName}'.");
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            AppendToDetailsRichTextBlock(
                                $"❌ Failed to assign '{item.Value.Name}' (ID: {item.Key}) to '{group.GroupName}': {ex.Message}");
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

        private Dictionary<string, AssignmentInfo> GetAllContentFromDatagrid()
        {
            // Gather all content (full objects) from the datagrid and send to orchestrator
            var content = new Dictionary<string, AssignmentInfo>();

            foreach (var item in AssignmentList)
            {
                // Key = Id, Value = full AssignmentInfo (includes Name, Type, Platform)
                content[item.Id] = item;
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

        private async Task LoadAllDeviceCompliancePoliciesAsync()
        {
            ShowLoading("Loading device compliance policies from Microsoft Graph...");
            try
            {
                var policies = await GetAllDeviceCompliancePolicies(sourceGraphServiceClient);
                foreach (var policy in policies)
                {
                    var platform = TranslatePolicyPlatformName(policy.OdataType);

                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = policy.DisplayName,
                        Type = "Device Compliance",
                        Platform = platform,
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

        private async Task LoadAllDeviceConfigurationPoliciesAsync()
        {
            ShowLoading("Loading device configuration policies from Microsoft Graph...");
            try
            {
                var policies = await GetAllDeviceConfigurations(sourceGraphServiceClient);
                foreach (var policy in policies)
                {
                    var platform = TranslatePolicyPlatformName(policy.OdataType);

                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = policy.DisplayName,
                        Type = "Device Configuration",
                        Platform = platform,
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

        private async Task LoadAllmacOSShellScriptsAsync()
        {
            ShowLoading("Loading macOS shell scripts from Microsoft Graph...");
            try
            {
                var scripts = await GetAllmacOSShellScripts(sourceGraphServiceClient);
                foreach (var script in scripts)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = script.DisplayName,
                        Type = "macOS Shell Script",
                        Platform = "macOS",
                        Id = script.Id
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

        private async Task LoadAllPowershellScriptsAsync()
        {
            ShowLoading("Loading PowerShell scripts from Microsoft Graph...");
            try
            {
                var scripts = await GetAllPowerShellScripts(sourceGraphServiceClient);
                foreach (var script in scripts)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = script.DisplayName,
                        Type = "PowerShell Script",
                        Platform = "Windows",
                        Id = script.Id
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

        private async Task LoadAllProactiveRemediationScriptsAsync()
        {
            ShowLoading("Loading proactive remediation scripts from Microsoft Graph...");
            try
            {
                var scripts = await GetAllProactiveRemediations(sourceGraphServiceClient);
                foreach (var script in scripts)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = script.DisplayName,
                        Type = "Proactive Remediation Script",
                        Platform = "Windows",
                        Id = script.Id
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

        private async Task LoadAllWindowsAutopilotProfilesAsync()
        {
            ShowLoading("Loading Windows Autopilot profiles from Microsoft Graph...");
            try
            {
                var profiles = await GetAllWindowsAutoPilotProfiles(sourceGraphServiceClient);
                foreach (var profile in profiles)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = profile.DisplayName,
                        Type = "Windows Autopilot Profile",
                        Platform = "Windows",
                        Id = profile.Id
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


        private async Task LoadAllWindowsDriverUpdateProfilesAsync()
        {
            ShowLoading("Loading Windows Driver Update profiles from Microsoft Graph...");
            try
            {
                var profiles = await GetAllDriverProfiles(sourceGraphServiceClient);
                foreach (var profile in profiles)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = profile.DisplayName,
                        Type = "Windows Driver Update Profile",
                        Platform = "Windows",
                        Id = profile.Id
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

        private async Task LoadAllWindowsFeatureUpdateProfilesAsync()
        {
            ShowLoading("Loading Windows Feature Update profiles from Microsoft Graph...");
            try
            {
                var profiles = await GetAllWindowsFeatureUpdateProfiles(sourceGraphServiceClient);
                foreach (var profile in profiles)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = profile.DisplayName,
                        Type = "Windows Feature Update Profile",
                        Platform = "Windows",
                        Id = profile.Id
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

        private async Task LoadAllWindowsQualityUpdatePoliciesAsync()
        {
            ShowLoading("Loading Windows Quality Update policies from Microsoft Graph...");
            try
            {
                var profiles = await GetAllWindowsQualityUpdatePolicies(sourceGraphServiceClient);
                foreach (var profile in profiles)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = profile.DisplayName,
                        Type = "Windows Quality Update Policy",
                        Platform = "Windows",
                        Id = profile.Id
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

        private async Task LoadAllWindowsQualityUpdateProfilesAsync()
        {
            ShowLoading("Loading Windows Quality Update profiles from Microsoft Graph...");
            try
            {
                var profiles = await GetAllWindowsQualityUpdateProfiles(sourceGraphServiceClient);
                foreach (var profile in profiles)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = profile.DisplayName,
                        Type = "Windows Quality Update Profile",
                        Platform = "Windows",
                        Id = profile.Id
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

        private async Task LoadAllAppleBYODEnrollmentProfilesAsync()
        {
            ShowLoading("Loading Apple BYOD enrollment profiles from Microsoft Graph...");
            try
            {
                var profiles = await GetAllAppleBYODEnrollmentProfiles(sourceGraphServiceClient);
                foreach (var profile in profiles)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = profile.DisplayName,
                        Type = "Apple BYOD Enrollment Profile",
                        Platform = "iOS",
                        Id = profile.Id
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

        private async Task LoadAllApplicationsAsync()
        {
            ShowLoading("Loading applications from Microsoft Graph...");
            try
            {
                var applications = await ApplicationHelper.GetAllMobileApps(sourceGraphServiceClient);
                foreach (var app in applications)
                {
                    var assignmentInfo = new AssignmentInfo
                    {
                        Name = app.DisplayName,
                        Type = TranslateApplicationType(app.OdataType),
                        Platform = TranslatePolicyPlatformName(app.OdataType),
                        Id = app.Id
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
                    AppendToDetailsRichTextBlock("Assignment filter enabled.");
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

        private void IntentToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_uiInitialized) return;
            if (sender is ToggleSwitch ts)
            {
                _selectedInstallIntent = ts.IsOn ? InstallIntent.Required : InstallIntent.Available;
                AppendToDetailsRichTextBlock($"Assignment intent set to '{_selectedInstallIntent}'.");
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

            ScrollLogToEnd();
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

        private void ScrollLogToEnd()
        {
            // Use DispatcherQueue to ensure layout updates are processed
            DispatcherQueue.TryEnqueue(() =>
            {
                LogConsole.UpdateLayout();
                LogScrollViewer.UpdateLayout();
                LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null, true);
            });
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
                AppendToDetailsRichTextBlock("Sorting error: Unable to determine property name from column binding.");
                return;
            }

            // Check if property exists on AssignmentInfo
            var propInfo = typeof(AssignmentInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppendToDetailsRichTextBlock($"Sorting error: Property '{sortProperty}' not found on AssignmentInfo.");
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
            List<AssignmentInfo> sorted;
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
                AppendToDetailsRichTextBlock($"Sorting error: {ex.Message}");
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
                    AppendToDetailsRichTextBlock("Application Deployment Options Configured:");
                    AppendToDetailsRichTextBlock($" • Intent: {_selectedInstallIntent}");
                    AppendToDetailsRichTextBlock($" • Group Mode: {_selectedDeploymentMode}");
                    AppendToDetailsRichTextBlock($" • Notifications: {_selectedNotificationSetting}");
                    AppendToDetailsRichTextBlock($" • Delivery Opt: {_selectedDeliveryOptimizationPriority}");

                    return true;
                }
                else if(result == ContentDialogResult.Secondary)
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
                AppendToDetailsRichTextBlock($"Error showing app options dialog: {ex.Message}");
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
                AppendToDetailsRichTextBlock("Sorting error: Unable to determine property name from column binding.");
                return;
            }

            // Check if property exists on AssignmentGroupInfo
            var propInfo = typeof(AssignmentGroupInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppendToDetailsRichTextBlock($"Sorting error: Property '{sortProperty}' not found on AssignmentGroupInfo.");
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
                AppendToDetailsRichTextBlock($"Sorting error: {ex.Message}");
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
