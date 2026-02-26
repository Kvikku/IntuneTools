using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
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
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// Page for renaming Intune content with prefix, suffix, or description updates.
    /// </summary>
    public sealed partial class RenamingPage : BaseDataOperationPage
    {
        /// <summary>
        /// Alias for ContentList to maintain backward compatibility with existing code.
        /// </summary>
        private ObservableCollection<CustomContentInfo> CustomContentList => ContentList;

        // Progress tracking for rename operations
        private int _renameTotal;
        private int _renameCurrent;
        private int _renameSuccessCount;
        private int _renameErrorCount;

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before using renaming features.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "SearchQueryTextBox", "SearchButton", "ListAllButton", "ClearSelectedButton",
            "ClearAllButton", "NewNameTextBox", "PrefixButton", "RenameButton",
            "RenamingDataGrid", "ClearLogButton", "RenameModeComboBox"
        };


        public RenamingPage()
        {
            this.InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(RenamingDataGrid);
        }

        // DataGrid sorting handler - delegates to base class
        private void RenamingDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        protected override void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            base.ShowLoading(message);
            // Disable specific buttons during loading
            ListAllButton.IsEnabled = false;
            SearchButton.IsEnabled = false;
        }

        protected override void HideLoading()
        {
            base.HideLoading();
            // Re-enable buttons
            ListAllButton.IsEnabled = true;
            SearchButton.IsEnabled = true;
        }

        // Convenience method for logging - calls base class AppendToLog
        private void AppendToDetailsRichTextBlock(string text) => AppendToLog(text);

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
                await LoadAllApplicationsAsync();
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
                await SearchForApplicationsAsync(searchQuery);

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
                // Initialize progress tracking
                _renameTotal = contentIDs.Count;
                _renameCurrent = 0;
                _renameSuccessCount = 0;
                _renameErrorCount = 0;

                ShowOperationProgress("Preparing to rename items...", 0, _renameTotal);

                if (HasContentType(ContentTypes.SettingsCatalog))
                {
                    var ids = GetContentIdsByType(ContentTypes.SettingsCatalog);
                    if (ids.Count > 0)
                        await RenameSettingsCatalogs(ids, prefix);
                }

                if (HasContentType(ContentTypes.DeviceCompliancePolicy))
                {
                    var ids = GetContentIdsByType(ContentTypes.DeviceCompliancePolicy);
                    if (ids.Count > 0)
                        await RenameDeviceCompliancePolicies(ids, prefix);
                }

                if (HasContentType(ContentTypes.DeviceConfigurationPolicy))
                {
                    var ids = GetContentIdsByType(ContentTypes.DeviceConfigurationPolicy);
                    if (ids.Count > 0)
                        await RenameDeviceConfigurationPolicies(ids, prefix);
                }

                if (HasContentType(ContentTypes.AppleBYODEnrollmentProfile))
                {
                    var ids = GetContentIdsByType(ContentTypes.AppleBYODEnrollmentProfile);
                    if (ids.Count > 0)
                        await RenameAppleBYODEnrollmentProfiles(ids, prefix);
                }

                if (HasContentType(ContentTypes.MacOSShellScript))
                {
                    var ids = GetContentIdsByType(ContentTypes.MacOSShellScript);
                    if (ids.Count > 0)
                        await RenameMacOSShellScripts(ids, prefix);
                }

                if (HasContentType(ContentTypes.PowerShellScript))
                {
                    var ids = GetContentIdsByType(ContentTypes.PowerShellScript);
                    if (ids.Count > 0)
                        await RenamePowerShellScripts(ids, prefix);
                }

                if (HasContentType(ContentTypes.ProactiveRemediation))
                {
                    var ids = GetContentIdsByType(ContentTypes.ProactiveRemediation);
                    if (ids.Count > 0)
                        await RenameProactiveRemediations(ids, prefix);
                }

                if (HasContentType(ContentTypes.WindowsAutoPilotProfile))
                {
                    var ids = GetContentIdsByType(ContentTypes.WindowsAutoPilotProfile);
                    if (ids.Count > 0)
                        await RenameWindowsAutoPilotProfiles(ids, prefix);
                }

                if (HasContentType(ContentTypes.WindowsDriverUpdate))
                {
                    var ids = GetContentIdsByType(ContentTypes.WindowsDriverUpdate);
                    if (ids.Count > 0)
                        await RenameWindowsDriverUpdates(ids, prefix);
                }

                if (HasContentType(ContentTypes.WindowsFeatureUpdate))
                {
                    var ids = GetContentIdsByType(ContentTypes.WindowsFeatureUpdate);
                    if (ids.Count > 0)
                        await RenameWindowsFeatureUpdates(ids, prefix);
                }

                if (HasContentType(ContentTypes.WindowsQualityUpdatePolicy))
                {
                    var ids = GetContentIdsByType(ContentTypes.WindowsQualityUpdatePolicy);
                    if (ids.Count > 0)
                        await RenameWindowsQualityUpdatePolicies(ids, prefix);
                }

                if (HasContentType(ContentTypes.WindowsQualityUpdateProfile))
                {
                    var ids = GetContentIdsByType(ContentTypes.WindowsQualityUpdateProfile);
                    if (ids.Count > 0)
                        await RenameWindowsQualityUpdateProfiles(ids, prefix);
                }

                if (HasContentType(ContentTypes.AssignmentFilter))
                {
                    var ids = GetContentIdsByType(ContentTypes.AssignmentFilter);
                    if (ids.Count > 0)
                        await RenameAssignmentFilters(ids, prefix);
                }

                if (HasContentType(ContentTypes.EntraGroup))
                {
                    var ids = GetContentIdsByType(ContentTypes.EntraGroup);
                    if (ids.Count > 0)
                        await RenameEntraGroups(ids, prefix);
                }

                if (HasApplicationContent())
                {
                    var ids = GetApplicationContentIds();
                    if (ids.Count > 0)
                        await RenameApplications(ids, prefix);
                }

                // Show final success/error status
                if (_renameErrorCount == 0)
                {
                    ShowOperationSuccess($"Successfully renamed {_renameSuccessCount} items");
                }
                else
                {
                    ShowOperationError($"Completed with {_renameErrorCount} error(s). {_renameSuccessCount} items renamed successfully.");
                }
                AppendToDetailsRichTextBlock($"Renamed {_renameSuccessCount} items with prefix '{prefix}'.");
            }
            catch (Exception ex)
            {
                ShowOperationError($"Rename operation failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error during renaming: {ex.Message}");
            }
        }

        private async Task RenameAppleBYODEnrollmentProfiles(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Apple BYOD Enrollment Profile", _renameCurrent, _renameTotal);
                try
                {
                    var profile = await sourceGraphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameAppleBYODEnrollmentProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Apple BYOD Enrollment Profile '{profile.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Apple BYOD Enrollment Profile with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameApplications(List<string> appIDs, string prefix)
        {
            foreach (var id in appIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Application", _renameCurrent, _renameTotal);
                try
                {
                    await RenameApplication(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Application with ID '{id}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error updating Application with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameMacOSShellScripts(List<string> scriptIDs, string prefix)
        {
            foreach (var id in scriptIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming macOS Shell Script", _renameCurrent, _renameTotal);
                try
                {
                    var script = await sourceGraphServiceClient.DeviceManagement.DeviceShellScripts[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameMacOSShellScript(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated MacOS Shell Script '{script.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming MacOS Shell Script with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenamePowerShellScripts(List<string> scriptIDs, string prefix)
        {
            foreach (var id in scriptIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming PowerShell Script", _renameCurrent, _renameTotal);
                try
                {
                    var script = await sourceGraphServiceClient.DeviceManagement.DeviceManagementScripts[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenamePowerShellScript(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated PowerShell Script '{script.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming PowerShell Script with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameProactiveRemediations(List<string> scriptIDs, string prefix)
        {
            foreach (var id in scriptIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Proactive Remediation", _renameCurrent, _renameTotal);
                try
                {
                    var remediation = await sourceGraphServiceClient.DeviceManagement.DeviceHealthScripts[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameProactiveRemediation(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Proactive Remediation '{remediation.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Proactive Remediation with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsAutoPilotProfiles(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Windows AutoPilot Profile", _renameCurrent, _renameTotal);
                try
                {
                    var profile = await sourceGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsAutoPilotProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows AutoPilot Profile '{profile.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Windows AutoPilot Profile with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsDriverUpdates(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Windows Driver Update", _renameCurrent, _renameTotal);
                try
                {
                    var update = await sourceGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameDriverProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Driver Update '{update.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Windows Driver Update with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsFeatureUpdates(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Windows Feature Update", _renameCurrent, _renameTotal);
                try
                {
                    var update = await sourceGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsFeatureUpdateProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Feature Update '{update.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Windows Feature Update with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsQualityUpdatePolicies(List<string> policyIDs, string prefix)
        {
            foreach (var id in policyIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Windows Quality Update Policy", _renameCurrent, _renameTotal);
                try
                {
                    var policy = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsQualityUpdatePolicy(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Quality Update Policy '{policy.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Windows Quality Update Policy with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameWindowsQualityUpdateProfiles(List<string> profileIDs, string prefix)
        {
            foreach (var id in profileIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Windows Quality Update Profile", _renameCurrent, _renameTotal);
                try
                {
                    var profile = await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameWindowsQualityUpdateProfile(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Windows Quality Update Profile '{profile.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Windows Quality Update Profile with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameAssignmentFilters(List<string> filterIDs, string prefix)
        {
            foreach (var id in filterIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Assignment Filter", _renameCurrent, _renameTotal);
                try
                {
                    var filter = await sourceGraphServiceClient.DeviceManagement.AssignmentFilters[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameAssignmentFilter(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Assignment Filter '{filter.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming Assignment Filter with ID {id}: {ex.Message}");
                }
            }
        }

        private async Task RenameEntraGroups(List<string> groupIDs, string prefix)
        {
            foreach (var id in groupIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Entra Group", _renameCurrent, _renameTotal);
                try
                {
                    var group = await sourceGraphServiceClient.Groups[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameGroup(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Entra Group '{group.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
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

        private async Task RenameSettingsCatalogs(List<string> settingsCatalogIDs, string prefix)
        {
            foreach (var id in settingsCatalogIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Settings Catalog", _renameCurrent, _renameTotal);
                try
                {
                    var policy = await sourceGraphServiceClient.DeviceManagement.ConfigurationPolicies[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "name" };
                    });

                    await RenameSettingsCatalogPolicy(sourceGraphServiceClient, id, prefix);

                    AppendToDetailsRichTextBlock($"Updated Settings Catalog '{policy.Name}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
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

        private async Task RenameDeviceCompliancePolicies(List<string> deviceCompliancePolicyIDs, string prefix)
        {
            foreach (var id in deviceCompliancePolicyIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Device Compliance Policy", _renameCurrent, _renameTotal);
                try
                {
                    var policyName = await sourceGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameDeviceCompliancePolicy(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Updated Device Compliance Policy '{policyName.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
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

        private async Task RenameDeviceConfigurationPolicies(List<string> deviceConfigurationPolicyIDs, string prefix)
        {
            foreach (var id in deviceConfigurationPolicyIDs)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming Device Configuration Policy", _renameCurrent, _renameTotal);
                try
                {
                    var policy = await sourceGraphServiceClient.DeviceManagement.DeviceConfigurations[id].GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                    });
                    await RenameDeviceConfigurationPolicy(sourceGraphServiceClient, id, prefix);
                    AppendToDetailsRichTextBlock($"Renamed Device Configuration Policy '{policy.DisplayName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
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

        private async Task LoadAllApplicationsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllApplicationContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} applications.");
        }
        private async Task SearchForAppleBYODEnrollmentProfilesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchAppleBYODEnrollmentContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Apple BYOD enrollment profiles matching '{searchQuery}'.");
        }

        private async Task SearchForApplicationsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchApplicationContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} applications matching '{searchQuery}'.");
        }

        /// <summary>
        /// Assignment Filters
        /// </summary>

        private async Task LoadAllAssignmentFiltersAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllAssignmentFilterContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} assignment filters.");
        }
        private async Task SearchForAssignmentFiltersAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchAssignmentFilterContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} assignment filters matching '{searchQuery}'.");
        }

        /// <summary>
        /// Entra Groups
        /// </summary>

        private async Task LoadAllEntraGroupsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllGroupContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} Entra groups.");
        }
        private async Task SearchForEntraGroupsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchGroupContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Entra groups matching '{searchQuery}'.");
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

        /// <summary>
        /// Windows AutoPilot Profiles
        /// </summary>

        private async Task LoadAllWindowsAutoPilotProfilesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllWindowsAutoPilotContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} Windows AutoPilot profiles.");
        }
        private async Task SearchForWindowsAutoPilotProfilesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchWindowsAutoPilotContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Windows AutoPilot profiles matching '{searchQuery}'.");
        }

        /// <summary>
        /// Windows Driver Updates
        /// </summary>
        private async Task LoadAllWindowsDriverUpdatesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllWindowsDriverUpdateContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} Windows driver updates.");
        }
        private async Task SearchForWindowsDriverUpdatesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchWindowsDriverUpdateContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Windows driver updates matching '{searchQuery}'.");
        }

        /// <summary>
        /// Windows Feature Updates
        /// </summary>

        private async Task LoadAllWindowsFeatureUpdatesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllWindowsFeatureUpdateContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} Windows feature updates.");
        }
        private async Task SearchForWindowsFeatureUpdatesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchWindowsFeatureUpdateContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Windows feature updates matching '{searchQuery}'.");
        }

        /// <summary>
        /// Windows Quality Update Policy
        /// </summary>

        private async Task LoadAllWindowsQualityUpdatePoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllWindowsQualityUpdatePolicyContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} Windows quality update policies.");
        }
        private async Task SearchForWindowsQualityUpdatePoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchWindowsQualityUpdatePolicyContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Windows quality update policies matching '{searchQuery}'.");
        }

        /// <summary>
        /// Windows Quality Update Profile
        /// </summary>

        private async Task LoadAllWindowsQualityUpdateProfilesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await GetAllWindowsQualityUpdateProfileContentAsync(sourceGraphServiceClient));

            AppendToDetailsRichTextBlock($"Loaded {count} Windows quality update profiles.");
        }
        private async Task SearchForWindowsQualityUpdateProfilesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                CustomContentList,
                async () => await SearchWindowsQualityUpdateProfileContentAsync(sourceGraphServiceClient, searchQuery));

            AppendToDetailsRichTextBlock($"Found {count} Windows quality update profiles matching '{searchQuery}'.");
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
            CustomContentList.Clear();
            RenamingDataGrid.ItemsSource = null;
            RenamingDataGrid.ItemsSource = CustomContentList;
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
            RenamingDataGrid.ItemsSource = CustomContentList;
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

        private async void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var itemsToRename = CustomContentList.ToList();
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
