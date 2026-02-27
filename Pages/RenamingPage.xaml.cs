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

        /// <summary>
        /// Generic helper to rename items, reducing code duplication across all content types.
        /// </summary>
        /// <param name="ids">List of content IDs to rename.</param>
        /// <param name="prefix">The prefix/suffix/description to apply.</param>
        /// <param name="contentTypeName">Display name for logging (e.g., "Settings Catalog").</param>
        /// <param name="renameAction">Async action that performs the actual rename for a single ID.</param>
        /// <param name="getDisplayName">Optional async function to retrieve the item's display name for logging.</param>
        private async Task RenameItemsAsync(
            List<string> ids,
            string prefix,
            string contentTypeName,
            Func<string, string, Task> renameAction,
            Func<string, Task<string?>>? getDisplayName = null)
        {
            foreach (var id in ids)
            {
                _renameCurrent++;
                ShowOperationProgress($"Renaming {contentTypeName}", _renameCurrent, _renameTotal);
                try
                {
                    string? displayName = getDisplayName != null ? await getDisplayName(id) : null;
                    await renameAction(id, prefix);

                    var logName = displayName ?? $"ID '{id}'";
                    AppendToDetailsRichTextBlock($"Updated {contentTypeName} '{logName}' with '{prefix}'.");
                    UpdateTotalTimeSaved(secondsSavedOnRenaming, appFunction.Rename);
                    _renameSuccessCount++;
                }
                catch (Exception ex)
                {
                    _renameErrorCount++;
                    AppendToDetailsRichTextBlock($"Error renaming {contentTypeName} with ID {id}: {ex.Message}");
                }
            }
        }

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
        /// <summary>
        /// Main entry point for rename operations. Validates, prepares, and executes the rename.
        /// </summary>
        private async Task RenameContent(List<string> contentIDs, string newName)
        {
            if (!ValidateRenameInputs(contentIDs, newName))
                return;

            var operationText = await PrepareRenameOperation(contentIDs, newName);
            if (operationText == null)
                return; // Cancelled or validation failed

            await ExecuteRenameOperations(contentIDs.Count, operationText);
        }

        /// <summary>
        /// Validates basic input requirements for rename operations.
        /// </summary>
        private bool ValidateRenameInputs(List<string> contentIDs, string newName)
        {
            if (contentIDs == null || contentIDs.Count == 0)
            {
                AppendToDetailsRichTextBlock("No content IDs provided for renaming.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(newName))
            {
                AppendToDetailsRichTextBlock("New name cannot be empty.");
                return false;
            }

            var prefixSymbol = GetSelectedPrefixOption();
            if (prefixSymbol == null && selectedRenameMode == "Prefix")
            {
                AppendToDetailsRichTextBlock("Please select a prefix option.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Prepares the rename operation based on mode (Prefix/Suffix/Description).
        /// Shows confirmation dialog and returns the text to apply, or null if cancelled.
        /// </summary>
        private async Task<string?> PrepareRenameOperation(List<string> contentIDs, string newName)
        {
            return selectedRenameMode switch
            {
                "Prefix" => await PreparePrefixRename(contentIDs, newName),
                "Suffix" => await PrepareSuffixRename(contentIDs, newName),
                "Description" => await PrepareDescriptionUpdate(newName),
                _ => null
            };
        }

        private async Task<string?> PreparePrefixRename(List<string> contentIDs, string newName)
        {
            var prefixSymbol = GetSelectedPrefixOption();
            if (prefixSymbol == null) return null;

            var prefix = $"{prefixSymbol[0]}{newName}{prefixSymbol[1]}";

            // Build preview of new names
            var contentNames = contentIDs
                .Select(id => CustomContentList.FirstOrDefault(c => c.ContentId == id))
                .Where(c => c != null)
                .Select(c => FindPreFixInPolicyName(c!.ContentName, prefix))
                .ToList();

            if (contentNames.Count == 0)
            {
                AppendToDetailsRichTextBlock("No content names found for the provided IDs.");
                return null;
            }

            var confirmed = await ShowConfirmationDialog(
                "Confirm Renaming",
                $"The new policy names will look like this. Proceed?\n\n{string.Join("\n", contentNames)}",
                "Rename");

            return confirmed ? prefix : null;
        }

        private async Task<string?> PrepareSuffixRename(List<string> contentIDs, string newName)
        {
            // TODO: Implement suffix rename logic similar to prefix
            // For now, return null (not implemented)
            await Task.CompletedTask;
            return null;
        }

        private async Task<string?> PrepareDescriptionUpdate(string newDescription)
        {
            var confirmed = await ShowConfirmationDialog(
                "Confirm updating description",
                $"The new policy descriptions will look like this. Proceed?\n\n{newDescription}",
                "Update");

            return confirmed ? newDescription : null;
        }

        /// <summary>
        /// Shows a confirmation dialog and returns true if user confirmed.
        /// </summary>
        private async Task<bool> ShowConfirmationDialog(string title, string content, string confirmButtonText)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = confirmButtonText,
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                AppendToDetailsRichTextBlock("Renaming operation cancelled.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Mapping of content types to their rename methods.
        /// </summary>
        private IEnumerable<(string ContentType, Func<List<string>, string, Task> RenameAction)> GetContentTypeRenameActions() =>
        [
            (ContentTypes.SettingsCatalog, RenameSettingsCatalogs),
            (ContentTypes.DeviceCompliancePolicy, RenameDeviceCompliancePolicies),
            (ContentTypes.DeviceConfigurationPolicy, RenameDeviceConfigurationPolicies),
            (ContentTypes.AppleBYODEnrollmentProfile, RenameAppleBYODEnrollmentProfiles),
            (ContentTypes.MacOSShellScript, RenameMacOSShellScripts),
            (ContentTypes.PowerShellScript, RenamePowerShellScripts),
            (ContentTypes.ProactiveRemediation, RenameProactiveRemediations),
            (ContentTypes.WindowsAutoPilotProfile, RenameWindowsAutoPilotProfiles),
            (ContentTypes.WindowsDriverUpdate, RenameWindowsDriverUpdates),
            (ContentTypes.WindowsFeatureUpdate, RenameWindowsFeatureUpdates),
            (ContentTypes.WindowsQualityUpdatePolicy, RenameWindowsQualityUpdatePolicies),
            (ContentTypes.WindowsQualityUpdateProfile, RenameWindowsQualityUpdateProfiles),
            (ContentTypes.AssignmentFilter, RenameAssignmentFilters),
            (ContentTypes.EntraGroup, RenameEntraGroups),
        ];

        /// <summary>
        /// Executes rename operations for all content types present in the list.
        /// </summary>
        private async Task ExecuteRenameOperations(int totalItems, string operationText)
        {
            try
            {
                InitializeProgressTracking(totalItems);
                ShowOperationProgress("Preparing to rename items...", 0, _renameTotal);

                // Process all mapped content types
                foreach (var (contentType, renameAction) in GetContentTypeRenameActions())
                {
                    if (HasContentType(contentType))
                    {
                        var ids = GetContentIdsByType(contentType);
                        if (ids.Count > 0)
                            await renameAction(ids, operationText);
                    }
                }

                // Handle applications separately (different API pattern)
                if (HasApplicationContent())
                {
                    var ids = GetApplicationContentIds();
                    if (ids.Count > 0)
                        await RenameApplications(ids, operationText);
                }

                ReportRenameResults(operationText);
            }
            catch (Exception ex)
            {
                ShowOperationError($"Rename operation failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error during renaming: {ex.Message}");
            }
        }

        private void InitializeProgressTracking(int totalItems)
        {
            _renameTotal = totalItems;
            _renameCurrent = 0;
            _renameSuccessCount = 0;
            _renameErrorCount = 0;
        }

        private void ReportRenameResults(string operationText)
        {
            if (_renameErrorCount == 0)
            {
                ShowOperationSuccess($"Successfully renamed {_renameSuccessCount} items");
            }
            else
            {
                ShowOperationError($"Completed with {_renameErrorCount} error(s). {_renameSuccessCount} items renamed successfully.");
            }
            AppendToDetailsRichTextBlock($"Renamed {_renameSuccessCount} items with '{operationText}'.");
        }

        private Task RenameAppleBYODEnrollmentProfiles(List<string> profileIDs, string prefix) =>
            RenameItemsAsync(profileIDs, prefix, "Apple BYOD Enrollment Profile",
                async (id, p) => await RenameAppleBYODEnrollmentProfile(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameApplications(List<string> appIDs, string prefix) =>
            RenameItemsAsync(appIDs, prefix, "Application",
                async (id, p) => await RenameApplication(sourceGraphServiceClient, id, p));

        private Task RenameMacOSShellScripts(List<string> scriptIDs, string prefix) =>
            RenameItemsAsync(scriptIDs, prefix, "macOS Shell Script",
                async (id, p) => await RenameMacOSShellScript(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.DeviceShellScripts[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenamePowerShellScripts(List<string> scriptIDs, string prefix) =>
            RenameItemsAsync(scriptIDs, prefix, "PowerShell Script",
                async (id, p) => await RenamePowerShellScript(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.DeviceManagementScripts[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameProactiveRemediations(List<string> scriptIDs, string prefix) =>
            RenameItemsAsync(scriptIDs, prefix, "Proactive Remediation",
                async (id, p) => await RenameProactiveRemediation(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.DeviceHealthScripts[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameWindowsAutoPilotProfiles(List<string> profileIDs, string prefix) =>
            RenameItemsAsync(profileIDs, prefix, "Windows AutoPilot Profile",
                async (id, p) => await RenameWindowsAutoPilotProfile(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameWindowsDriverUpdates(List<string> profileIDs, string prefix) =>
            RenameItemsAsync(profileIDs, prefix, "Windows Driver Update",
                async (id, p) => await RenameDriverProfile(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.WindowsDriverUpdateProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameWindowsFeatureUpdates(List<string> profileIDs, string prefix) =>
            RenameItemsAsync(profileIDs, prefix, "Windows Feature Update",
                async (id, p) => await RenameWindowsFeatureUpdateProfile(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.WindowsFeatureUpdateProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameWindowsQualityUpdatePolicies(List<string> policyIDs, string prefix) =>
            RenameItemsAsync(policyIDs, prefix, "Windows Quality Update Policy",
                async (id, p) => await RenameWindowsQualityUpdatePolicy(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdatePolicies[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameWindowsQualityUpdateProfiles(List<string> profileIDs, string prefix) =>
            RenameItemsAsync(profileIDs, prefix, "Windows Quality Update Profile",
                async (id, p) => await RenameWindowsQualityUpdateProfile(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.WindowsQualityUpdateProfiles[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameAssignmentFilters(List<string> filterIDs, string prefix) =>
            RenameItemsAsync(filterIDs, prefix, "Assignment Filter",
                async (id, p) => await RenameAssignmentFilter(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.AssignmentFilters[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

        private Task RenameEntraGroups(List<string> groupIDs, string prefix) =>
            RenameItemsAsync(groupIDs, prefix, "Entra Group",
                async (id, p) => await RenameGroup(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.Groups[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

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

        private Task RenameSettingsCatalogs(List<string> settingsCatalogIDs, string prefix) =>
            RenameItemsAsync(settingsCatalogIDs, prefix, "Settings Catalog",
                async (id, p) => await RenameSettingsCatalogPolicy(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.ConfigurationPolicies[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "name" }))?.Name);



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

        private Task RenameDeviceCompliancePolicies(List<string> deviceCompliancePolicyIDs, string prefix) =>
            RenameItemsAsync(deviceCompliancePolicyIDs, prefix, "Device Compliance Policy",
                async (id, p) => await RenameDeviceCompliancePolicy(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

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

        private Task RenameDeviceConfigurationPolicies(List<string> deviceConfigurationPolicyIDs, string prefix) =>
            RenameItemsAsync(deviceConfigurationPolicyIDs, prefix, "Device Configuration Policy",
                async (id, p) => await RenameDeviceConfigurationPolicy(sourceGraphServiceClient, id, p),
                async id => (await sourceGraphServiceClient.DeviceManagement.DeviceConfigurations[id]
                    .GetAsync(r => r.QueryParameters.Select = new[] { "displayName" }))?.DisplayName);

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
