using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
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
//using static IntuneTools.Utilities.SourceTenantGraphClient;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// Page for cleaning up (deleting) Intune content.
    /// </summary>
    public sealed partial class CleanupPage : BaseDataOperationPage
    {
        // Progress tracking for delete operations
        private int _deleteTotal;
        private int _deleteCurrent;
        private int _deleteSuccessCount;
        private int _deleteErrorCount;

        /// <summary>
        /// Content types supported by CleanupPage (excludes Application since delete is not supported).
        /// </summary>
        private static readonly string[] SupportedContentTypes = new[]
        {
            ContentTypes.SettingsCatalog,
            ContentTypes.DeviceCompliancePolicy,
            ContentTypes.DeviceConfigurationPolicy,
            ContentTypes.AppleBYODEnrollmentProfile,
            ContentTypes.AssignmentFilter,
            ContentTypes.EntraGroup,
            ContentTypes.PowerShellScript,
            ContentTypes.ProactiveRemediation,
            ContentTypes.MacOSShellScript,
            ContentTypes.WindowsAutoPilotProfile,
            ContentTypes.WindowsDriverUpdate,
            ContentTypes.WindowsFeatureUpdate,
            ContentTypes.WindowsQualityUpdatePolicy,
            ContentTypes.WindowsQualityUpdateProfile,
        };

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before using cleanup features.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton", "ClearSelectedButton",
            "ClearAllButton", "DeleteButton", "CleanupDataGrid", "ClearLogButton"
        };

        public CleanupPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(CleanupDataGrid);
        }

        // DataGrid sorting handler - delegates to base class
        private void CleanupDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        protected override void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            base.ShowLoading(message);
            ListAllButton.IsEnabled = false;
            SearchButton.IsEnabled = false;
        }

        protected override void HideLoading()
        {
            base.HideLoading();
            ListAllButton.IsEnabled = true;
            SearchButton.IsEnabled = true;
        }

        // Convenience method for logging - calls base class AppendToLog
        private void AppendToDetailsRichTextBlock(string text) => AppendToLog(text);

        private async Task DeleteContent()
        {
            // Initialize progress tracking - count total items across all content types
            _deleteTotal = ContentList.Count;
            _deleteCurrent = 0;
            _deleteSuccessCount = 0;
            _deleteErrorCount = 0;

            if (_deleteTotal == 0)
            {
                AppendToDetailsRichTextBlock("No content to delete.");
                return;
            }

            ShowOperationProgress("Preparing to delete items...", 0, _deleteTotal);

            await DeleteSettingsCatalogsAsync();
            await DeleteDeviceCompliancePoliciesAsync();
            await DeleteDeviceConfigurationPoliciesAsync();
            await DeleteAppleBYODEnrollmentProfilesAsync();
            await DeleteAssignmentFiltersAsync();
            await DeleteEntraGroupsAsync();
            await DeletePowerShellScriptsAsync();
            await DeleteProactiveRemediationsAsync();
            await DeleteMacOSShellScriptsAsync();
            await DeleteWindowsAutoPilotProfilesAsync();
            await DeleteWindowsDriverUpdatesAsync();
            await DeleteWindowsFeatureUpdatesAsync();
            await DeleteWindowsQualityUpdatePoliciesAsync();
            await DeleteWindowsQualityUpdateProfilesAsync();

            // Show final status
            if (_deleteErrorCount == 0)
            {
                ShowOperationSuccess($"Successfully deleted {_deleteSuccessCount} items");
            }
            else
            {
                ShowOperationError($"Completed with {_deleteErrorCount} error(s). {_deleteSuccessCount} items deleted successfully.");
            }

            AppendToDetailsRichTextBlock("Content deletion completed.");
        }

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            AppendToDetailsRichTextBlock("Starting to load all content. This could take a while...");
            try
            {
                ContentList.Clear();
                await LoadContentTypesAsync(graphServiceClient, SupportedContentTypes, AppendToDetailsRichTextBlock);
                CleanupDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Error during loading: {ex.Message}");
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
                ContentList.Clear();
                await SearchContentTypesAsync(graphServiceClient, searchQuery, SupportedContentTypes, AppendToDetailsRichTextBlock);
                CleanupDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Error during search: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }


        private async Task DeleteSettingsCatalogsAsync()
        {
            try
            {
                // Get all settings catalog IDs
                var settingsCatalogIDs = GetContentIdsByType(ContentTypes.SettingsCatalog);
                if (settingsCatalogIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No settings catalog policies found to delete.");
                    return;
                }

                LogToFunctionFile(appFunction.Main, $"Found {settingsCatalogIDs.Count} settings catalog policies to delete.");

                // Delete each settings catalog policy
                foreach (var id in settingsCatalogIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Settings Catalog", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteSettingsCatalog(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted settings catalog policy with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting settings catalog policy {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {settingsCatalogIDs.Count} settings catalog policies.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting settings catalog policies: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteDeviceCompliancePoliciesAsync()
        {
            try
            {
                // Get all device compliance policy IDs
                var deviceCompliancePolicyIDs = GetContentIdsByType(ContentTypes.DeviceCompliancePolicy);
                if (deviceCompliancePolicyIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No device compliance policies found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {deviceCompliancePolicyIDs.Count} device compliance policies to delete.");
                // Delete each device compliance policy
                foreach (var id in deviceCompliancePolicyIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Device Compliance Policy", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteDeviceCompliancePolicy(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted device compliance policy with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting device compliance policy {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {deviceCompliancePolicyIDs.Count} device compliance policies.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting device compliance policies: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteDeviceConfigurationPoliciesAsync()
        {
            try
            {
                // Get all device configuration policy IDs
                var deviceConfigurationPolicyIDs = GetContentIdsByType(ContentTypes.DeviceConfigurationPolicy);
                if (deviceConfigurationPolicyIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No device configuration policies found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {deviceConfigurationPolicyIDs.Count} device configuration policies to delete.");
                // Delete each device configuration policy
                foreach (var id in deviceConfigurationPolicyIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Device Configuration Policy", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteDeviceConfigurationPolicy(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted device configuration policy with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting device configuration policy {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {deviceConfigurationPolicyIDs.Count} device configuration policies.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting device configuration policies: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteAppleBYODEnrollmentProfilesAsync()
        {
            try
            {
                // Get all Apple BYOD enrollment profile IDs
                var appleBYODEnrollmentProfileIDs = GetContentIdsByType(ContentTypes.AppleBYODEnrollmentProfile);
                if (appleBYODEnrollmentProfileIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No Apple BYOD enrollment profiles found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {appleBYODEnrollmentProfileIDs.Count} Apple BYOD enrollment profiles to delete.");
                // Delete each Apple BYOD enrollment profile
                foreach (var id in appleBYODEnrollmentProfileIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Apple BYOD Enrollment Profile", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteAppleBYODEnrollmentProfile(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted Apple BYOD enrollment profile with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting Apple BYOD enrollment profile {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {appleBYODEnrollmentProfileIDs.Count} Apple BYOD enrollment profiles.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Apple BYOD enrollment profiles: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteAssignmentFiltersAsync()
        {
            try
            {
                // Get all assignment filter IDs
                var assignmentFilterIDs = GetContentIdsByType(ContentTypes.AssignmentFilter);
                if (assignmentFilterIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No assignment filters found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {assignmentFilterIDs.Count} assignment filters to delete.");
                // Delete each assignment filter
                foreach (var id in assignmentFilterIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Assignment Filter", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteAssignmentFilter(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted assignment filter with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting assignment filter {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {assignmentFilterIDs.Count} assignment filters.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting assignment filters: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteEntraGroupsAsync()
        {
            try
            {
                // Get all Entra group IDs
                var entraGroupIDs = GetContentIdsByType(ContentTypes.EntraGroup);
                if (entraGroupIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No Entra groups found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {entraGroupIDs.Count} Entra groups to delete.");
                // Delete each Entra group
                foreach (var id in entraGroupIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Entra Group", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteSecurityGroup(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted Entra group with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting Entra group {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {entraGroupIDs.Count} Entra groups.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Entra groups: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeletePowerShellScriptsAsync()
        {
            try
            {
                // Get all PowerShell script IDs
                var powerShellScriptIDs = GetContentIdsByType(ContentTypes.PowerShellScript);
                if (powerShellScriptIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No PowerShell scripts found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {powerShellScriptIDs.Count} PowerShell scripts to delete.");
                // Delete each PowerShell script
                foreach (var id in powerShellScriptIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting PowerShell Script", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeletePowerShellScript(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted PowerShell script with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting PowerShell script {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {powerShellScriptIDs.Count} PowerShell scripts.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting PowerShell scripts: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteProactiveRemediationsAsync()
        {
            try
            {
                // Get all proactive remediation IDs
                var proactiveRemediationIDs = GetContentIdsByType(ContentTypes.ProactiveRemediation);
                if (proactiveRemediationIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No proactive remediations found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {proactiveRemediationIDs.Count} proactive remediations to delete.");
                // Delete each proactive remediation
                foreach (var id in proactiveRemediationIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Proactive Remediation", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteProactiveRemediationScript(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted proactive remediation with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting proactive remediation {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {proactiveRemediationIDs.Count} proactive remediations.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting proactive remediations: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteMacOSShellScriptsAsync()
        {
            try
            {
                // Get all MacOS shell script IDs
                var macOSShellScriptIDs = GetContentIdsByType(ContentTypes.MacOSShellScript);
                if (macOSShellScriptIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No MacOS shell scripts found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {macOSShellScriptIDs.Count} MacOS shell scripts to delete.");
                // Delete each MacOS shell script
                foreach (var id in macOSShellScriptIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting macOS Shell Script", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteMacosShellScript(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted MacOS shell script with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting MacOS shell script {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {macOSShellScriptIDs.Count} MacOS shell scripts.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting MacOS shell scripts: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteWindowsAutoPilotProfilesAsync()

        {
            try
            {
                // Get all Windows AutoPilot profile IDs
                var windowsAutoPilotProfileIDs = GetContentIdsByType(ContentTypes.WindowsAutoPilotProfile);
                if (windowsAutoPilotProfileIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No Windows AutoPilot profiles found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {windowsAutoPilotProfileIDs.Count} Windows AutoPilot profiles to delete.");
                // Delete each Windows AutoPilot profile
                foreach (var id in windowsAutoPilotProfileIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Windows AutoPilot Profile", _deleteCurrent, _deleteTotal);
                    
                    // Check if the policy is assigned to any devices
                    // The policy cannot be deleted if it has assignments
                    var isAssigned = await CheckIfAutoPilotProfileHasAssignments(sourceGraphServiceClient, id);

                    if (isAssigned)
                    {
                        // Ask the user if they want to delete the assignments
                        var dialog = new ContentDialog
                        {
                            Title = "Delete AutoPilot Profile",
                            Content = $"The Windows AutoPilot profile with ID: {id} is assigned to devices. Do you want to delete the assignments before deleting the profile?",
                            PrimaryButtonText = "Delete Assignments",
                            SecondaryButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Secondary,
                            XamlRoot = this.XamlRoot
                        };
                        var result = await dialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            try
                            {
                                // Delete the assignments first
                                await DeleteWindowsAutoPilotProfileAssignments(sourceGraphServiceClient, id);
                                LogToFunctionFile(appFunction.Main, $"Deleted assignments for Windows AutoPilot profile with ID: {id}");

                                // Now delete the profile
                                await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                                LogToFunctionFile(appFunction.Main, $"Deleted Windows AutoPilot profile with ID: {id}");
                                UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                                _deleteSuccessCount++;
                            }
                            catch (Exception ex)
                            {
                                _deleteErrorCount++;
                                LogToFunctionFile(appFunction.Main, $"Error deleting Windows AutoPilot profile {id}: {ex.Message}", LogLevels.Error);
                            }
                        }
                        else
                        {
                            // User chose not to delete assignments, skip deletion of the profile
                            LogToFunctionFile(appFunction.Main, $"Skipped deletion of Windows AutoPilot profile with ID: {id} as it is assigned to devices.", LogLevels.Warning);
                        }
                    }
                    else
                    {
                        try
                        {
                            await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                            LogToFunctionFile(appFunction.Main, $"Deleted Windows AutoPilot profile with ID: {id}");
                            _deleteSuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            _deleteErrorCount++;
                            LogToFunctionFile(appFunction.Main, $"Error deleting Windows AutoPilot profile {id}: {ex.Message}", LogLevels.Error);
                        }
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted Windows AutoPilot profiles.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows AutoPilot profiles: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteWindowsDriverUpdatesAsync()
        {
            try
            {
                // Get all Windows driver update IDs
                var windowsDriverUpdateIDs = GetContentIdsByType(ContentTypes.WindowsDriverUpdate);
                if (windowsDriverUpdateIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No Windows driver updates found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {windowsDriverUpdateIDs.Count} Windows driver updates to delete.");
                // Delete each Windows driver update
                foreach (var id in windowsDriverUpdateIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Windows Driver Update", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteDriverProfile(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted Windows driver update with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting Windows driver update {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {windowsDriverUpdateIDs.Count} Windows driver updates.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows driver updates: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteWindowsFeatureUpdatesAsync()
        {
            try
            {
                // Get all Windows feature update IDs
                var windowsFeatureUpdateIDs = GetContentIdsByType(ContentTypes.WindowsFeatureUpdate);
                if (windowsFeatureUpdateIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No Windows feature updates found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {windowsFeatureUpdateIDs.Count} Windows feature updates to delete.");
                // Delete each Windows feature update
                foreach (var id in windowsFeatureUpdateIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Windows Feature Update", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteWindowsFeatureUpdateProfile(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted Windows feature update with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting Windows feature update {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {windowsFeatureUpdateIDs.Count} Windows feature updates.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows feature updates: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteWindowsQualityUpdatePoliciesAsync()
        {
            try
            {
                // Get all Windows quality update IDs
                var windowsQualityUpdateIDs = GetContentIdsByType(ContentTypes.WindowsQualityUpdatePolicy);
                if (windowsQualityUpdateIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No Windows quality updates found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {windowsQualityUpdateIDs.Count} Windows quality updates to delete.");
                // Delete each Windows quality update
                foreach (var id in windowsQualityUpdateIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Windows Quality Update Policy", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteWindowsQualityUpdatePolicy(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted Windows quality update with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting Windows quality update {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {windowsQualityUpdateIDs.Count} Windows quality updates.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows quality updates: {ex.Message}", LogLevels.Error);
            }
        }

        private async Task DeleteWindowsQualityUpdateProfilesAsync()
        {
            try
            {
                // Get all Windows quality update profile IDs
                var windowsQualityUpdateProfileIDs = GetContentIdsByType(ContentTypes.WindowsQualityUpdateProfile);
                if (windowsQualityUpdateProfileIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No Windows quality update profiles found to delete.");
                    return;
                }
                LogToFunctionFile(appFunction.Main, $"Found {windowsQualityUpdateProfileIDs.Count} Windows quality update profiles to delete.");
                // Delete each Windows quality update profile
                foreach (var id in windowsQualityUpdateProfileIDs)
                {
                    _deleteCurrent++;
                    ShowOperationProgress($"Deleting Windows Quality Update Profile", _deleteCurrent, _deleteTotal);
                    try
                    {
                        await DeleteWindowsQualityUpdateProfile(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted Windows quality update profile with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _deleteErrorCount++;
                        LogToFunctionFile(appFunction.Main, $"Error deleting Windows quality update profile {id}: {ex.Message}", LogLevels.Error);
                    }
                }
                AppendToDetailsRichTextBlock($"Deleted {windowsQualityUpdateProfileIDs.Count} Windows quality update profiles.");
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows quality update profiles: {ex.Message}", LogLevels.Error);
            }
        }


        /// BUTTON HANDLERS ///
        /// Buttons should be defined in the XAML file and linked to these methods.
        /// Buttons should call other methods to perform specific actions.
        /// Buttons should not directly perform actions themselves.
        private async void ListAll_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }
        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchQuery))
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

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var numberOfItems = ContentList.Count;

            var dialog = new ContentDialog
            {
                Title = "Delete content?",
                Content = $"Are you sure you want to delete all {numberOfItems} items? This action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync().AsTask();
            if (result == ContentDialogResult.Primary)
            {
                await DeleteContent();
                ContentList.Clear();
                AppendToDetailsRichTextBlock("Cleared the data grid.");
            }
        }

        // Handler for the 'Clear All' button
        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            CleanupDataGrid.ItemsSource = null;
            CleanupDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock("All items cleared from the list.");
        }

        // Handler for the 'Clear Selected' button
        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = CleanupDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }
            CleanupDataGrid.ItemsSource = null;
            CleanupDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }
    }
}


