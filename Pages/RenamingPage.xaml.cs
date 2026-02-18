using CommunityToolkit.WinUI.UI.Controls;
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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class RenamingPage : Page
    {
        public class ContentInfo
        {
            public string? ContentName { get; set; }
            public string? ContentPlatform { get; set; }
            public string? ContentType { get; set; }
            public string? ContentId { get; set; }
            public string? ContentDescription { get; set; }
        }

        // old list
        public ObservableCollection<ContentInfo> ContentList { get; set; } = new ObservableCollection<ContentInfo>();
        
        
        // new list
        public ObservableCollection<CustomContentInfo> CustomContentList { get; set; } = new ObservableCollection<CustomContentInfo>();




        public RenamingPage()
        {
            this.InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(RenamingDataGrid);
        }

        /// <summary>
        ///  Local helper methods
        /// </summary>

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (string.Equals(Variables.sourceTenantName, string.Empty))
            {
                TenantInfoBar.Title = "Authentication Required";
                TenantInfoBar.Message = "You must authenticate with a tenant before using renaming features.";
                TenantInfoBar.Severity = InfoBarSeverity.Warning;
                TenantInfoBar.IsOpen = true;

                // Disable controls until authenticated
                SearchQueryTextBox.IsEnabled = false;
                SearchButton.IsEnabled = false;
                ListAllButton.IsEnabled = false;
                ClearSelectedButton.IsEnabled = false;
                ClearAllButton.IsEnabled = false;
                NewNameTextBox.IsEnabled = false;
                PrefixButton.IsEnabled = false;
                RenameButton.IsEnabled = false;
                RenamingDataGrid.IsEnabled = false;
                ClearLogButton.IsEnabled = false;
                RenameModeComboBox.IsEnabled = false;
            }
            else
            {
                TenantInfoBar.Title = "Authenticated Tenant";
                TenantInfoBar.Message = Variables.sourceTenantName;
                TenantInfoBar.Severity = InfoBarSeverity.Informational;
                TenantInfoBar.IsOpen = true;

                // Enable controls
                SearchQueryTextBox.IsEnabled = true;
                SearchButton.IsEnabled = true;
                ListAllButton.IsEnabled = true;
                ClearSelectedButton.IsEnabled = true;
                ClearAllButton.IsEnabled = true;
                NewNameTextBox.IsEnabled = true;
                PrefixButton.IsEnabled = true;
                RenameButton.IsEnabled = true;
                RenamingDataGrid.IsEnabled = true;
                ClearLogButton.IsEnabled = true;
                RenameModeComboBox.IsEnabled = true;
            }
        }

        // Add this event handler to your RenamingPage class
        private void RenamingDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (CustomContentList == null || CustomContentList.Count == 0)
                return;

            var textColumn = e.Column as DataGridTextColumn;
            var binding = textColumn?.Binding as Binding;
            string sortProperty = binding?.Path?.Path;
            if (string.IsNullOrEmpty(sortProperty))
            {
                AppendToDetailsRichTextBlock("Sorting error: Unable to determine property name from column binding.");
                return;
            }

            var propInfo = typeof(CustomContentInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppendToDetailsRichTextBlock($"Sorting error: Property '{sortProperty}' not found on CustomContentInfo.");
                return;
            }

            ListSortDirection direction;
            if (e.Column.SortDirection.HasValue && e.Column.SortDirection.Value == DataGridSortDirection.Ascending)
            {
                direction = ListSortDirection.Descending;
            }
            else
            {
                direction = ListSortDirection.Ascending;
            }

            List<CustomContentInfo> sorted;
            try
            {
                if (direction == ListSortDirection.Ascending)
                {
                    sorted = CustomContentList.OrderBy(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
                else
                {
                    sorted = CustomContentList.OrderByDescending(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Sorting error: {ex.Message}");
                return;
            }

            CustomContentList.Clear();
            foreach (var item in sorted)
                CustomContentList.Add(item);

            foreach (var col in dataGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = direction == ListSortDirection.Ascending
                ? DataGridSortDirection.Ascending
                : DataGridSortDirection.Descending;

            // Prevent default sort
            // e.Handled = true; // Removed as per workaround

        }

        private void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            LoadingStatusText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingProgressRing.IsActive = true;

            // Optionally disable buttons during loading
            ListAllButton.IsEnabled = false;
            SearchButton.IsEnabled = false;
        }
        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingProgressRing.IsActive = false;

            // Re-enable buttons
            ListAllButton.IsEnabled = true;
            SearchButton.IsEnabled = true;
        }
        private void AppendToDetailsRichTextBlock(string text)
        {
            // Append log text to the LogConsole RichTextBlock
            Paragraph paragraph;
            if (LogConsole.Blocks.Count == 0)
            {
                paragraph = new Paragraph();
                LogConsole.Blocks.Add(paragraph);
            }
            else
            {
                paragraph = LogConsole.Blocks.First() as Paragraph;
                if (paragraph == null)
                {
                    paragraph = new Paragraph();
                    LogConsole.Blocks.Add(paragraph);
                }
            }
            if (paragraph.Inlines.Count > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new Run { Text = text });

            ScrollLogToEnd();
        }
        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            AppendToDetailsRichTextBlock("Starting to load all content. This could take a while...");
            try
            {
                // Clear the CustomContentList before loading new data
                CustomContentList.Clear();

                await LoadAllSettingsCatalogPoliciesAsync();
                await LoadAllDeviceCompliancePoliciesAsync();
                await LoadAllDeviceConfigurationPoliciesAsync();
                await LoadAllWindowsAutoPilotProfilesAsync();
                await LoadAllWindowsDriverUpdatesAsync();
                await LoadAllWindowsFeatureUpdatesAsync();
                await LoadAllWindowsQualityUpdatePoliciesAsync();
                await LoadAllWindowsQualityUpdateProfilesAsync();
                await LoadAllPowerShellScriptsAsync();
                await LoadAllProactiveRemediationsAsync();
                await LoadAllMacOSShellScriptsAsync();
                await LoadAllAppleBYODEnrollmentProfilesAsync();
                await LoadAllAssignmentFiltersAsync();
                await LoadAllEntraGroupsAsync();

                // Bind the combined list to the grid once
                //RenamingDataGrid.ItemsSource = ContentList;

                // New list
                RenamingDataGrid.ItemsSource = CustomContentList;
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Error during loading: {ex.Message}");
                HideLoading();
                return;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchOrchestrator(GraphServiceClient graphServiceClient, string searchQuery)
        {
            ShowLoading("Searching content in Microsoft Graph...");
            AppendToDetailsRichTextBlock($"Searching for content matching '{searchQuery}'. This may take a while...");
            try
            {
                // Clear the CustomContentList before loading new data
                CustomContentList.Clear();
                await SearchForSettingsCatalogPoliciesAsync(searchQuery);
                await SearchForDeviceCompliancePoliciesAsync(searchQuery);
                await SearchForDeviceConfigurationPoliciesAsync(searchQuery);
                await SearchForAppleBYODEnrollmentProfilesAsync(searchQuery);
                await SearchForAssignmentFiltersAsync(searchQuery);
                await SearchForEntraGroupsAsync(searchQuery);
                await SearchForPowerShellScriptsAsync(searchQuery);
                await SearchForProactiveRemediationsAsync(searchQuery);
                await SearchForMacOSShellScriptsAsync(searchQuery);
                await SearchForWindowsAutoPilotProfilesAsync(searchQuery);
                await SearchForWindowsDriverUpdatesAsync(searchQuery);
                await SearchForWindowsFeatureUpdatesAsync(searchQuery);
                await SearchForWindowsQualityUpdatePoliciesAsync(searchQuery);
                await SearchForWindowsQualityUpdateProfilesAsync(searchQuery);

                // Bind the combined list to the grid once
                RenamingDataGrid.ItemsSource = CustomContentList;
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Error during search: {ex.Message}");
                HideLoading();
                return;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task RenameContent(List<string> contentIDs, string newName)
        {

            string prefix = string.Empty;

            if (contentIDs == null || contentIDs.Count == 0)
            {
                AppendToDetailsRichTextBlock("No content IDs provided for renaming.");
                return;
            }
            if (string.IsNullOrWhiteSpace(newName))
            {
                AppendToDetailsRichTextBlock("New name cannot be empty.");
                return;
            }

            var prefixSymbol = GetSelectedPrefixOption();

            if (prefixSymbol == null && selectedRenameMode == "Prefix")
            {
                AppendToDetailsRichTextBlock("Please select a prefix option.");
                return;
            }


            if (selectedRenameMode == "Prefix")
            {

                prefix = $"{prefixSymbol[0]}{newName}{prefixSymbol[1]}";

                // Find the corresponding names for the content ID

                List<string> contentNames = new List<string>();
                foreach (var id in contentIDs)
                {
                    var name = string.Empty;
                    var content = CustomContentList.FirstOrDefault(c => c.ContentId == id);
                    if (content != null)
                    {
                        name = FindPreFixInPolicyName(content.ContentName, prefix);
                    }
                    contentNames.Add(name);
                }


                // display a dialog box with the new names and confirm renaming
                if (contentNames.Count == 0)
                {
                    AppendToDetailsRichTextBlock("No content names found for the provided IDs.");
                    return;
                }


                string contentNamesList = string.Join("\n", contentNames);
                ContentDialog renameDialog = new ContentDialog
                {
                    Title = "Confirm Renaming",
                    Content = $"The new policy names will look like this. Proceed?\n\n{contentNamesList}",
                    PrimaryButtonText = "Rename",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };
                var dialogResult = await renameDialog.ShowAsync();

                if (dialogResult != ContentDialogResult.Primary)
                {
                    AppendToDetailsRichTextBlock("Renaming operation cancelled.");
                    return;
                }
            }
            else if (selectedRenameMode == "Suffix")
            {

            }
            else if (selectedRenameMode == "Description")
            {
                prefix = newName; // For description, we just use the newName as the description text
                ContentDialog renameDialog = new ContentDialog
                {
                    Title = "Confirm updating description",
                    Content = $"The new policy descriptions will look like this. Proceed?\n\n{prefix}",
                    PrimaryButtonText = "Update",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };
                var dialogResult = await renameDialog.ShowAsync();
                if (dialogResult != ContentDialogResult.Primary)
                {
                    AppendToDetailsRichTextBlock("Renaming operation cancelled.");
                    return;
                }
            }


            try
            {
                if (CustomContentList.Any(c => c.ContentType == "Settings Catalog"))
                {
                    var settingsCatalogIDs = GetSettingsCatalogIDs();
                    if (settingsCatalogIDs.Count > 0)
                    {
                        await RenameSettingsCatalogs(settingsCatalogIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Device Compliance Policy"))
                {
                    var deviceCompliancePolicyIDs = GetDeviceCompliancePolicyIDs();
                    if (deviceCompliancePolicyIDs.Count > 0)
                    {
                        await RenameDeviceCompliancePolicies(deviceCompliancePolicyIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Device Configuration Policy"))
                {
                    var deviceConfigurationPolicyIDs = GetDeviceConfigurationPolicyIDs();
                    if (deviceConfigurationPolicyIDs.Count > 0)
                    {
                        await RenameDeviceConfigurationPolicies(deviceConfigurationPolicyIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Apple BYOD Enrollment Profile"))
                {
                    var appleBYODProfileIDs = GetAppleBYODEnrollmentProfileIDs();
                    if (appleBYODProfileIDs.Count > 0)
                    {
                        await RenameAppleBYODEnrollmentProfiles(appleBYODProfileIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "MacOS Shell Script"))
                {
                    var macOSShellScriptIDs = GetMacOSShellScriptIDs();
                    if (macOSShellScriptIDs.Count > 0)
                    {
                        await RenameMacOSShellScripts(macOSShellScriptIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "PowerShell Script"))
                {
                    var powerShellScriptIDs = GetPowerShellScriptIDs();
                    if (powerShellScriptIDs.Count > 0)
                    {
                        await RenamePowerShellScripts(powerShellScriptIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Proactive Remediation"))
                {
                    var proactiveRemediationIDs = GetProactiveRemediationIDs();
                    if (proactiveRemediationIDs.Count > 0)
                    {
                        await RenameProactiveRemediations(proactiveRemediationIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Windows AutoPilot Profile"))
                {
                    var windowsAutoPilotProfileIDs = GetWindowsAutoPilotProfileIDs();
                    if (windowsAutoPilotProfileIDs.Count > 0)
                    {
                        await RenameWindowsAutoPilotProfiles(windowsAutoPilotProfileIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Windows Driver Update"))
                {
                    var windowsDriverUpdateIDs = GetWindowsDriverUpdateIDs();
                    if (windowsDriverUpdateIDs.Count > 0)
                    {
                        await RenameWindowsDriverUpdates(windowsDriverUpdateIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Windows Feature Update"))
                {
                    var windowsFeatureUpdateIDs = GetWindowsFeatureUpdateIDs();
                    if (windowsFeatureUpdateIDs.Count > 0)
                    {
                        await RenameWindowsFeatureUpdates(windowsFeatureUpdateIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Windows Quality Update Policy"))
                {
                    var windowsQualityUpdatePolicyIDs = GetWindowsQualityUpdatePolicyIDs();
                    if (windowsQualityUpdatePolicyIDs.Count > 0)
                    {
                        await RenameWindowsQualityUpdatePolicies(windowsQualityUpdatePolicyIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Windows Quality Update Profile"))
                {
                    var windowsQualityUpdateProfileIDs = GetWindowsQualityUpdateProfileIDs();
                    if (windowsQualityUpdateProfileIDs.Count > 0)
                    {
                        await RenameWindowsQualityUpdateProfiles(windowsQualityUpdateProfileIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Assignment Filter"))
                {
                    var assignmentFilterIDs = GetAssignmentFilterIDs();
                    if (assignmentFilterIDs.Count > 0)
                    {
                        await RenameAssignmentFilters(assignmentFilterIDs, prefix);
                    }
                }

                if (CustomContentList.Any(c => c.ContentType == "Entra Group"))
                {
                    var entraGroupIDs = GetEntraGroupIDs();
                    if (entraGroupIDs.Count > 0)
                    {
                        await RenameEntraGroups(entraGroupIDs, prefix);
                    }
                }
                AppendToDetailsRichTextBlock($"Renamed {contentIDs.Count} items with prefix '{prefix}'.");
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Error during renaming: {ex.Message}");
            }
        }

        private async Task RenameAppleBYODEnrollmentProfiles(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                try
                {
                    var profile = await sourceGraphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameAppleBYODEnrollmentProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Apple BYOD Enrollment Profile '{profile.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Apple BYOD Enrollment Profile with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameMacOSShellScripts(List<string> scriptIDs, string prefix)
        {
            foreach (var id in scriptIDs)
            {
                try
                {
                    var script = await sourceGraphServiceClient.DeviceManagement.DeviceShellScripts[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameMacOSShellScript(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated MacOS Shell Script '{script.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming MacOS Shell Script with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenamePowerShellScripts(List<string> scriptIDs, string prefix)
        {
            foreach (var id in scriptIDs)
            {
                try
                {
                    var script = await sourceGraphServiceClient.DeviceManagement.DeviceManagementScripts[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenamePowerShellScript(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated PowerShell Script '{script.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming PowerShell Script with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameProactiveRemediations(List<string> scriptIDs, string prefix)
        {
            foreach (var id in scriptIDs)
            {
                try
                {
                    var remediation = await sourceGraphServiceClient.DeviceManagement.DeviceHealthScripts[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameProactiveRemediation(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Proactive Remediation '{remediation.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Proactive Remediation with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsAutoPilotProfiles(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                try
                {
                    var profile = await sourceGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsAutoPilotProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows AutoPilot Profile '{profile.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Windows AutoPilot Profile with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsDriverUpdates(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                try
                {
                    var update = await sourceGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameDriverProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Driver Update '{update.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Windows Driver Update with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsFeatureUpdates(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                try
                {
                    var update = await sourceGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsFeatureUpdateProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Feature Update '{update.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Windows Feature Update with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsQualityUpdatePolicies(List<string> policyIDs, string prefix)
        {
            foreach (var id in policyIDs)
            {
                try
                {
                    var policy = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsQualityUpdatePolicy(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Quality Update Policy '{policy.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Windows Quality Update Policy with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsQualityUpdateProfiles(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                try
                {
                    var profile = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsQualityUpdateProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Quality Update Profile '{profile.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Windows Quality Update Profile with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameAssignmentFilters(List<string> filterIDs, string prefix)
        {
            foreach (var id in filterIDs)
            {
                try
                {
                    var filter = await sourceGraphServiceClient.DeviceManagement.AssignmentFilters[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameAssignmentFilter(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Assignment Filter '{filter.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Assignment Filter with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameEntraGroups(List<string> groupIDs, string prefix)
        {
            foreach (var id in groupIDs)
            {
                try
                {
                    var group = await sourceGraphServiceClient.Groups[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameGroup(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Entra Group '{group.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Entra Group with ID {id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///  Settings catalog
        /// </summary>
        private async Task LoadAllSettingsCatalogPoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllSettingsCatalogContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} settings catalog policies.");
        }

        private async Task SearchForSettingsCatalogPoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchSettingsCatalogContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} settings catalog policies matching '{searchQuery}'.");
        }
        private List<string> GetSettingsCatalogIDs()
        {
            // This method retrieves the IDs of all settings catalog policies in CustomContentList
            return CustomContentList
                .Where(c => c.ContentType == "Settings Catalog")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        private async Task RenameSettingsCatalogs(List<string> settingsCatalogIDs, string prefix)
        {
            foreach (var id in settingsCatalogIDs)
            {
                try
                {
                    var policy = await sourceGraphServiceClient.DeviceManagement.ConfigurationPolicies[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "name" };
                    });

                    await RenameSettingsCatalogPolicy(sourceGraphServiceClient, id, prefix);

                    AppendToDetailsRichTextBlock($"Updated Settings Catalog '{policy.Name}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error updating Settings Catalog with ID {id}: {ex.Message}");
                }
            }
        }



        private async Task LoadAllDeviceCompliancePoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllDeviceComplianceContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} device compliance policies.");
        }
        private async Task SearchForDeviceCompliancePoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchDeviceComplianceContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} device compliance policies matching '{searchQuery}'.");
        }
        private List<string> GetDeviceCompliancePolicyIDs()
        {
            // This method retrieves the IDs of all device compliance policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Device Compliance Policy")
                .Select(c => c.ContentId ?? string.Empty)
                .ToList();
        }

        private async Task RenameDeviceCompliancePolicies(List<string> deviceCompliancePolicyIDs, string prefix)
        {
            foreach (var id in deviceCompliancePolicyIDs)
            {
                try
                {
                    var policyName = await sourceGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameDeviceCompliancePolicy(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Device Compliance Policy '{policyName.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Device Compliance Policy with ID {id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///  Device configuration policies
        /// </summary>

        private async Task LoadAllDeviceConfigurationPoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllDeviceConfigurationContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} device configuration policies.");
        }

        private async Task SearchForDeviceConfigurationPoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchDeviceConfigurationContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} device configuration policies matching '{searchQuery}'.");
        }

        private List<string> GetDeviceConfigurationPolicyIDs()
        {
            // This method retrieves the IDs of all device configuration policies in CustomContentList
            return CustomContentList
                .Where(c => c.ContentType == "Device Configuration Policy")
                .Select(c => c.ContentId ?? string.Empty)
                .ToList();
        }

        private async Task RenameDeviceConfigurationPolicies(List<string> deviceConfigurationPolicyIDs, string prefix)
        {
            foreach (var id in deviceConfigurationPolicyIDs)
            {
                try
                {
                    var policy = await sourceGraphServiceClient.DeviceManagement.DeviceConfigurations[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameDeviceConfigurationPolicy(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Renamed Device Configuration Policy '{policy.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Device Configuration Policy with ID {id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Apple BYOD Enrollment Profiles
        /// </summary>

        private async Task LoadAllAppleBYODEnrollmentProfilesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllAppleBYODEnrollmentContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} Apple BYOD enrollment profiles.");
        }
        private async Task SearchForAppleBYODEnrollmentProfilesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchAppleBYODEnrollmentContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Apple BYOD enrollment profiles matching '{searchQuery}'.");
        }
        private List<string> GetAppleBYODEnrollmentProfileIDs()
        {
            // This method retrieves the IDs of all Apple BYOD enrollment profiles in CustomContentList
            return CustomContentList
                .Where(c => c.ContentType == "Apple BYOD Enrollment Profile")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Assignment Filters
        /// </summary>

        private async Task LoadAllAssignmentFiltersAsync()
        {
            var filters = await GetAllAssignmentFilters(sourceGraphServiceClient);
            foreach (var filter in filters)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = filter.DisplayName,
                    ContentType = "Assignment Filter",
                    ContentPlatform = TranslatePolicyPlatformName(filter.Platform.ToString()),
                    ContentId = filter.Id,
                    ContentDescription = filter.Description
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {filters.Count()} assignment filters.");
        }
        private async Task SearchForAssignmentFiltersAsync(string searchQuery)
        {
            var filters = await SearchForAssignmentFilters(sourceGraphServiceClient, searchQuery);
            foreach (var filter in filters)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = filter.DisplayName,
                    ContentType = "Assignment Filter",
                    ContentPlatform = TranslatePolicyPlatformName(filter.Platform.ToString()),
                    ContentId = filter.Id,
                    ContentDescription = filter.Description
                });
            }
            AppendToDetailsRichTextBlock($"Found {filters.Count()} assignment filters matching '{searchQuery}'.");
        }
        private List<string> GetAssignmentFilterIDs()
        {
            // This method retrieves the IDs of all assignment filters in ContentList
            return ContentList
                .Where(c => c.ContentType == "Assignment Filter")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Entra Groups
        /// </summary>

        private async Task LoadAllEntraGroupsAsync()
        {
            var groups = await GetAllGroups(sourceGraphServiceClient);
            foreach (var group in groups)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = group.DisplayName,
                    ContentType = "Entra Group",
                    ContentPlatform = "Entra group",
                    ContentId = group.Id,
                    ContentDescription = group.Description
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {groups.Count()} Entra groups.");
        }
        private async Task SearchForEntraGroupsAsync(string searchQuery)
        {
            var groups = await SearchForGroups(sourceGraphServiceClient, searchQuery);
            foreach (var group in groups)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = group.DisplayName,
                    ContentType = "Entra Group",
                    ContentPlatform = "Entra group",
                    ContentId = group.Id,
                    ContentDescription = group.Description
                });
            }
            AppendToDetailsRichTextBlock($"Found {groups.Count()} Entra groups matching '{searchQuery}'.");
        }
        private List<string> GetEntraGroupIDs()
        {
            // This method retrieves the IDs of all Entra groups in ContentList
            return ContentList
                .Where(c => c.ContentType == "Entra Group")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Powershell Scripts
        /// </summary>

        private async Task LoadAllPowerShellScriptsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllPowerShellScriptContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} PowerShell scripts.");
        }
        private async Task SearchForPowerShellScriptsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchPowerShellScriptContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} PowerShell scripts matching '{searchQuery}'.");
        }
        private List<string> GetPowerShellScriptIDs()
        {
            // This method retrieves the IDs of all PowerShell scripts in CustomContentList
            return CustomContentList
                .Where(c => c.ContentType == "PowerShell Script")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Proactive Remediations
        /// </summary>

        private async Task LoadAllProactiveRemediationsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllProactiveRemediationContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} proactive remediations.");
        }
        private async Task SearchForProactiveRemediationsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchProactiveRemediationContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} proactive remediations matching '{searchQuery}'.");
        }
        private List<string> GetProactiveRemediationIDs()
        {
            // This method retrieves the IDs of all proactive remediations in CustomContentList
            return CustomContentList
                .Where(c => c.ContentType == "Proactive Remediation")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// MacOS shell scripts
        /// </summary>

        private async Task LoadAllMacOSShellScriptsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllMacOSShellScriptContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} MacOS shell scripts.");
        }
        private async Task SearchForMacOSShellScriptsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchMacOSShellScriptContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} MacOS shell scripts matching '{searchQuery}'.");
        }
        private List<string> GetMacOSShellScriptIDs()
        {
            // This method retrieves the IDs of all MacOS shell scripts in CustomContentList
            return CustomContentList
                .Where(c => c.ContentType == "MacOS Shell Script")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Windows AutoPilot Profiles
        /// </summary>

        private async Task LoadAllWindowsAutoPilotProfilesAsync()
        {
            var profiles = await GetAllWindowsAutoPilotProfiles(sourceGraphServiceClient);
            foreach (var profile in profiles)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows AutoPilot Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {profiles.Count()} Windows AutoPilot profiles.");
        }
        private async Task SearchForWindowsAutoPilotProfilesAsync(string searchQuery)
        {
            var profiles = await SearchForWindowsAutoPilotProfiles(sourceGraphServiceClient, searchQuery);
            foreach (var profile in profiles)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows AutoPilot Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }
            AppendToDetailsRichTextBlock($"Found {profiles.Count()} Windows AutoPilot profiles matching '{searchQuery}'.");
        }
        private List<string> GetWindowsAutoPilotProfileIDs()
        {
            // This method retrieves the IDs of all Windows AutoPilot profiles in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows AutoPilot Profile")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Windows Driver Updates
        /// </summary>
        private async Task LoadAllWindowsDriverUpdatesAsync()
        {
            var updates = await GetAllDriverProfiles(sourceGraphServiceClient);
            foreach (var update in updates)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = update.DisplayName,
                    ContentType = "Windows Driver Update",
                    ContentPlatform = "Windows",
                    ContentId = update.Id,
                    ContentDescription = update.Description
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {updates.Count()} Windows driver updates.");
        }
        private async Task SearchForWindowsDriverUpdatesAsync(string searchQuery)
        {
            var updates = await SearchForDriverProfiles(sourceGraphServiceClient, searchQuery);
            foreach (var update in updates)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = update.DisplayName,
                    ContentType = "Windows Driver Update",
                    ContentPlatform = "Windows",
                    ContentId = update.Id,
                    ContentDescription = update.Description
                });
            }
            AppendToDetailsRichTextBlock($"Found {updates.Count()} Windows driver updates matching '{searchQuery}'.");
        }
        private List<string> GetWindowsDriverUpdateIDs()
        {
            // This method retrieves the IDs of all Windows driver updates in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows Driver Update")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Windows Feature Updates
        /// </summary>

        private async Task LoadAllWindowsFeatureUpdatesAsync()
        {
            var updates = await GetAllWindowsFeatureUpdateProfiles(sourceGraphServiceClient);
            foreach (var update in updates)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = update.DisplayName,
                    ContentType = "Windows Feature Update",
                    ContentPlatform = "Windows",
                    ContentId = update.Id,
                    ContentDescription = update.Description
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {updates.Count()} Windows feature updates.");
        }
        private async Task SearchForWindowsFeatureUpdatesAsync(string searchQuery)
        {
            var updates = await SearchForWindowsFeatureUpdateProfiles(sourceGraphServiceClient, searchQuery);
            foreach (var update in updates)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = update.DisplayName,
                    ContentType = "Windows Feature Update",
                    ContentPlatform = "Windows",
                    ContentId = update.Id,
                    ContentDescription = update.Description
                });
            }
            AppendToDetailsRichTextBlock($"Found {updates.Count()} Windows feature updates matching '{searchQuery}'.");
        }
        private List<string> GetWindowsFeatureUpdateIDs()
        {
            // This method retrieves the IDs of all Windows feature updates in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows Feature Update")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Windows Quality Update Policy
        /// </summary>

        private async Task LoadAllWindowsQualityUpdatePoliciesAsync()
        {
            var policies = await GetAllWindowsQualityUpdatePolicies(sourceGraphServiceClient);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Windows Quality Update Policy",
                    ContentPlatform = "Windows",
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {policies.Count()} Windows quality update policies.");
        }
        private async Task SearchForWindowsQualityUpdatePoliciesAsync(string searchQuery)
        {
            var policies = await SearchForWindowsQualityUpdatePolicies(sourceGraphServiceClient, searchQuery);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Windows Quality Update Policy",
                    ContentPlatform = "Windows",
                    ContentId = policy.Id,
                    ContentDescription = policy.Description
                });
            }
            AppendToDetailsRichTextBlock($"Found {policies.Count()} Windows quality update policies matching '{searchQuery}'.");
        }
        private List<string> GetWindowsQualityUpdatePolicyIDs()
        {
            // This method retrieves the IDs of all Windows quality update policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows Quality Update Policy")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Windows Quality Update Profile
        /// </summary>

        private async Task LoadAllWindowsQualityUpdateProfilesAsync()
        {
            var profiles = await GetAllWindowsQualityUpdateProfiles(sourceGraphServiceClient);
            foreach (var profile in profiles)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows Quality Update Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {profiles.Count()} Windows quality update profiles.");
        }
        private async Task SearchForWindowsQualityUpdateProfilesAsync(string searchQuery)
        {
            var profiles = await SearchForWindowsQualityUpdateProfiles(sourceGraphServiceClient, searchQuery);
            foreach (var profile in profiles)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows Quality Update Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id,
                    ContentDescription = profile.Description
                });
            }
            AppendToDetailsRichTextBlock($"Found {profiles.Count()} Windows quality update profiles matching '{searchQuery}'.");
        }
        private List<string> GetWindowsQualityUpdateProfileIDs()
        {
            // This method retrieves the IDs of all Windows quality update profiles in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows Quality Update Profile")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Returns the value of the selected radio button in the OptionsExpander.
        /// </summary>
        public string? GetSelectedPrefixOption()
        {
            if (Parentheses.IsChecked == true)
                return "()";
            if (SquareBrackets.IsChecked == true)
                return "[]";
            if (CurlyBrackets.IsChecked == true)
                return "{}";
            return null;
        }









        /// <summary>
        /// Button handlers
        /// </summary>

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            RenamingDataGrid.ItemsSource = null;
            RenamingDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock("All items cleared from the list.");
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = RenamingDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                CustomContentList.Remove(item);
            }
            RenamingDataGrid.ItemsSource = null;
            RenamingDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string searchQuery = SearchQueryTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchQuery))
            {
                AppendToDetailsRichTextBlock("Please enter a search query.");
                return;
            }
            await SearchOrchestrator(sourceGraphServiceClient, searchQuery);
        }

        // Handler for the 'Clear Log' button
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
        private async void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var itemsToRename = ContentList.ToList();
            var renameMode = GetSelectedRenameMode();

            if (itemsToRename == null || itemsToRename.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items in the grid to rename.");
                return;
            }

            string newName = NewNameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(newName))
            {
                AppendToDetailsRichTextBlock("Please enter a new name.");
                return;
            }

            var prefixSymbol = GetSelectedPrefixOption();

            if (prefixSymbol == null && renameMode != RenameMode.Description)
            {
                AppendToDetailsRichTextBlock("Please select a prefix option.");
                return;
            }



            selectedRenameMode = renameMode.ToString();

            await RenameContent(itemsToRename.Select(i => i.ContentId).Where(id => !string.IsNullOrEmpty(id)).ToList(), newName);

        }

        private RenameMode GetSelectedRenameMode()
        {
            // Defaults to Prefix if the ComboBox is not available yet.
            var index = RenameModeComboBox?.SelectedIndex ?? 0;

            // Clamp to valid range [0..2].
            if (index < 0 || index > 2) index = 0;

            return (RenameMode)index;
        }

        private int GetSelectedRenameModeIndex()
        {
            return (int)GetSelectedRenameMode();
        }

        // Call this after appending to LogConsole
        private void ScrollLogToEnd()
        {
            // Ensure measure is up-to-date before scrolling
            LogConsole.UpdateLayout();
            LogScrollViewer.UpdateLayout();

            // Scroll to the bottom
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null, true);
        }

        private void RenameModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectionMode = GetSelectedRenameMode();

            if (PrefixButton is null) return;

            PrefixButton.IsEnabled = selectionMode != RenameMode.Description;


            if (selectionMode == RenameMode.Description)
            {
                PrefixButton.IsEnabled = false;
            }
            else
            {
                PrefixButton.IsEnabled = true;
            }
        }
    }

}
