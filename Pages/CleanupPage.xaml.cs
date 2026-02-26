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

            AppendToDetailsRichTextBlock("Content deletion completed.");
        }

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            AppendToDetailsRichTextBlock("Starting to load all content. This could take a while...");
            try
            {
                // Clear the ContentList before loading new data
                ContentList.Clear();

                await LoadAllDeviceCompliancePoliciesAsync();
                await LoadAllSettingsCatalogPoliciesAsync();
                await LoadAllDeviceConfigurationPoliciesAsync();
                await LoadAllAppleBYODEnrollmentProfilesAsync();
                await LoadAllAssignmentFiltersAsync();
                await LoadAllEntraGroupsAsync();
                await LoadAllPowerShellScriptsAsync();
                await LoadAllProactiveRemediationsAsync();
                await LoadAllMacOSShellScriptsAsync();
                await LoadAllWindowsAutoPilotProfilesAsync();
                await LoadAllWindowsDriverUpdatesAsync();
                await LoadAllWindowsFeatureUpdatesAsync();
                await LoadAllWindowsQualityUpdatePoliciesAsync();
                await LoadAllWindowsQualityUpdateProfilesAsync();

                // Bind the combined list to the grid once
                CleanupDataGrid.ItemsSource = ContentList;
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
                // Clear the ContentList before loading new data
                ContentList.Clear();
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
                CleanupDataGrid.ItemsSource = ContentList;
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
        ///  Settings catalog
        /// </summary>
        private async Task LoadAllSettingsCatalogPoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllSettingsCatalogContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} settings catalog policies.");
        }
        private async Task SearchForSettingsCatalogPoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchSettingsCatalogContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} settings catalog policies matching '{searchQuery}'.");
        }

        private async Task DeleteSettingsCatalogsAsync()
        {
            int count = 0;
            ShowLoading("Deleting settings catalog policies from Microsoft Graph...");
            try
            {
                // Get all settings catalog IDs
                var settingsCatalogIDs = GetContentIdsByType(ContentTypes.SettingsCatalog);
                if (settingsCatalogIDs.Count == 0)
                {
                    LogToFunctionFile(appFunction.Main, "No settings catalog policies found to delete.");
                    return;
                }

                count = settingsCatalogIDs.Count;

                LogToFunctionFile(appFunction.Main, $"Found {count} settings catalog policies to delete.");

                // Delete each settings catalog policy

                foreach (var id in settingsCatalogIDs)
                {
                    await DeleteSettingsCatalog(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted settings catalog policy with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting settings catalog policies: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} settings catalog policies.");
                HideLoading();
            }
        }

        /// <summary>
        ///  Device Compliance Policies
        /// </summary>

        private async Task LoadAllDeviceCompliancePoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllDeviceComplianceContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} device compliance policies.");
        }
        private async Task SearchForDeviceCompliancePoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchDeviceComplianceContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} device compliance policies matching '{searchQuery}'.");
        }

        private async Task DeleteDeviceCompliancePoliciesAsync()
        {
            int count = 0;
            ShowLoading("Deleting device compliance policies from Microsoft Graph...");
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
                    await DeleteDeviceCompliancePolicy(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted device compliance policy with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting device compliance policies: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} device compliance policies.");
                HideLoading();
            }
        }

        /// <summary>
        ///  Device configuration policies
        /// </summary>

        private async Task LoadAllDeviceConfigurationPoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllDeviceConfigurationContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} device configuration policies.");
        }
        private async Task SearchForDeviceConfigurationPoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchDeviceConfigurationContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} device configuration policies matching '{searchQuery}'.");
        }
        private async Task DeleteDeviceConfigurationPoliciesAsync()
        {
            int count = 0;
            ShowLoading("Deleting device configuration policies from Microsoft Graph...");
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
                    await DeleteDeviceConfigurationPolicy(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted device configuration policy with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting device configuration policies: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} device configuration policies.");
                HideLoading();
            }
        }

        /// <summary>
        /// Apple BYOD Enrollment Profiles
        /// </summary>

        private async Task LoadAllAppleBYODEnrollmentProfilesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllAppleBYODEnrollmentContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} Apple BYOD enrollment profiles.");
        }
        private async Task SearchForAppleBYODEnrollmentProfilesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchAppleBYODEnrollmentContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} Apple BYOD enrollment profiles matching '{searchQuery}'.");
        }
        private async Task DeleteAppleBYODEnrollmentProfilesAsync()
        {
            int count = 0;
            ShowLoading("Deleting Apple BYOD enrollment profiles from Microsoft Graph...");
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
                    await DeleteAppleBYODEnrollmentProfile(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted Apple BYOD enrollment profile with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Apple BYOD enrollment profiles: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Apple BYOD enrollment profiles.");
                HideLoading();
            }
        }

        /// <summary>
        /// Assignment Filters
        /// </summary>

        private async Task LoadAllAssignmentFiltersAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllAssignmentFilterContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} assignment filters.");
        }
        private async Task SearchForAssignmentFiltersAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchAssignmentFilterContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} assignment filters matching '{searchQuery}'.");
        }
        private async Task DeleteAssignmentFiltersAsync()
        {
            int count = 0;
            ShowLoading("Deleting assignment filters from Microsoft Graph...");
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
                    await DeleteAssignmentFilter(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted assignment filter with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting assignment filters: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} assignment filters.");
                HideLoading();
            }
        }

        /// <summary>
        /// Entra Groups
        /// </summary>

        private async Task LoadAllEntraGroupsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllGroupContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} Entra groups.");
        }
        private async Task SearchForEntraGroupsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchGroupContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} Entra groups matching '{searchQuery}'.");
        }
        private async Task DeleteEntraGroupsAsync()
        {
            int count = 0;
            ShowLoading("Deleting Entra groups from Microsoft Graph...");
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
                    await DeleteSecurityGroup(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted Entra group with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Entra groups: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Entra groups.");
                HideLoading();
            }
        }

        /// <summary>
        /// Powershell Scripts
        /// </summary>

        private async Task LoadAllPowerShellScriptsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllPowerShellScriptContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} PowerShell scripts.");
        }
        private async Task SearchForPowerShellScriptsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchPowerShellScriptContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} PowerShell scripts matching '{searchQuery}'.");
        }
        private async Task DeletePowerShellScriptsAsync()
        {
            int count = 0;
            ShowLoading("Deleting PowerShell scripts from Microsoft Graph...");
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
                    await DeletePowerShellScript(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted PowerShell script with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting PowerShell scripts: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} PowerShell scripts.");
                HideLoading();
            }
        }

        /// <summary>
        /// Proactive Remediations
        /// </summary>

        private async Task LoadAllProactiveRemediationsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllProactiveRemediationContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} proactive remediations.");
        }
        private async Task SearchForProactiveRemediationsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchProactiveRemediationContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} proactive remediations matching '{searchQuery}'.");
        }
        private async Task DeleteProactiveRemediationsAsync()
        {
            int count = 0;
            ShowLoading("Deleting proactive remediations from Microsoft Graph...");
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
                    await DeleteProactiveRemediationScript(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted proactive remediation with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting proactive remediations: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} proactive remediations.");
                HideLoading();
            }
        }

        /// <summary>
        /// MacOS shell scripts
        /// </summary>

        private async Task LoadAllMacOSShellScriptsAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllMacOSShellScriptContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} MacOS shell scripts.");
        }
        private async Task SearchForMacOSShellScriptsAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchMacOSShellScriptContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} MacOS shell scripts matching '{searchQuery}'.");
        }
        private async Task DeleteMacOSShellScriptsAsync()
        {
            int count = 0;
            ShowLoading("Deleting MacOS shell scripts from Microsoft Graph...");
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
                    await DeleteMacosShellScript(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted MacOS shell script with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting MacOS shell scripts: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} MacOS shell scripts.");
                HideLoading();
            }
        }

        /// <summary>
        /// Windows AutoPilot Profiles
        /// </summary>

        private async Task LoadAllWindowsAutoPilotProfilesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllWindowsAutoPilotContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} Windows AutoPilot profiles.");
        }
        private async Task SearchForWindowsAutoPilotProfilesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchWindowsAutoPilotContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} Windows AutoPilot profiles matching '{searchQuery}'.");
        }
        private async Task DeleteWindowsAutoPilotProfilesAsync()

        {
            int count = 0;
            ShowLoading("Deleting Windows AutoPilot profiles from Microsoft Graph...");
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
                            // Delete the assignments first
                            await DeleteWindowsAutoPilotProfileAssignments(sourceGraphServiceClient, id);
                            LogToFunctionFile(appFunction.Main, $"Deleted assignments for Windows AutoPilot profile with ID: {id}");

                            // Now delete the profile
                            await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                            LogToFunctionFile(appFunction.Main, $"Deleted Windows AutoPilot profile with ID: {id}");
                            UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                            count++;
                        }
                        else
                        {
                            // User chose not to delete assignments, skip deletion of the profile
                            LogToFunctionFile(appFunction.Main, $"Skipped deletion of Windows AutoPilot profile with ID: {id} as it is assigned to devices.", LogLevels.Warning);
                        }
                    }
                    else
                    {
                        await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                        LogToFunctionFile(appFunction.Main, $"Deleted Windows AutoPilot profile with ID: {id}");
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows AutoPilot profiles: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Windows AutoPilot profiles.");
                HideLoading();
            }
        }

        /// <summary>
        /// Windows Driver Updates
        /// </summary>
        private async Task LoadAllWindowsDriverUpdatesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllWindowsDriverUpdateContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} Windows driver updates.");
        }
        private async Task SearchForWindowsDriverUpdatesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchWindowsDriverUpdateContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} Windows driver updates matching '{searchQuery}'.");
        }
        private async Task DeleteWindowsDriverUpdatesAsync()
        {
            int count = 0;
            ShowLoading("Deleting Windows driver updates from Microsoft Graph...");
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
                    await DeleteDriverProfile(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted Windows driver update with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows driver updates: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Windows driver updates.");
                HideLoading();
            }
        }

        /// <summary>
        /// Windows Feature Updates
        /// </summary>

        private async Task LoadAllWindowsFeatureUpdatesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllWindowsFeatureUpdateContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} Windows feature updates.");
        }
        private async Task SearchForWindowsFeatureUpdatesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchWindowsFeatureUpdateContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} Windows feature updates matching '{searchQuery}'.");
        }
        private async Task DeleteWindowsFeatureUpdatesAsync()
        {
            int count = 0;
            ShowLoading("Deleting Windows feature updates from Microsoft Graph...");
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
                    await DeleteWindowsFeatureUpdateProfile(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted Windows feature update with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows feature updates: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Windows feature updates.");
                HideLoading();
            }
        }

        /// <summary>
        /// Windows Quality Update Policy
        /// </summary>

        private async Task LoadAllWindowsQualityUpdatePoliciesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllWindowsQualityUpdatePolicyContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} Windows quality update policies.");
        }
        private async Task SearchForWindowsQualityUpdatePoliciesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchWindowsQualityUpdatePolicyContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} Windows quality update policies matching '{searchQuery}'.");
        }
        private async Task DeleteWindowsQualityUpdatePoliciesAsync()
        {
            int count = 0;
            ShowLoading("Deleting Windows quality updates from Microsoft Graph...");
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
                    await DeleteWindowsQualityUpdatePolicy(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted Windows quality update with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows quality updates: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Windows quality updates.");
                HideLoading();
            }
        }

        /// <summary>
        /// Windows Quality Update Profile
        /// </summary>

        private async Task LoadAllWindowsQualityUpdateProfilesAsync()
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await GetAllWindowsQualityUpdateProfileContentAsync(sourceGraphServiceClient));
            AppendToDetailsRichTextBlock($"Loaded {count} Windows quality update profiles.");
        }
        private async Task SearchForWindowsQualityUpdateProfilesAsync(string searchQuery)
        {
            var count = await UserInterfaceHelper.PopulateCollectionAsync(
                ContentList,
                async () => await SearchWindowsQualityUpdateProfileContentAsync(sourceGraphServiceClient, searchQuery));
            AppendToDetailsRichTextBlock($"Found {count} Windows quality update profiles matching '{searchQuery}'.");
        }
        private async Task DeleteWindowsQualityUpdateProfilesAsync()
        {
            int count = 0;
            ShowLoading("Deleting Windows quality update profiles from Microsoft Graph...");
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
                    await DeleteWindowsQualityUpdateProfile(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted Windows quality update profile with ID: {id}");
                    UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                }
            }
            catch (Exception ex)
            {
                LogToFunctionFile(appFunction.Main, $"Error deleting Windows quality update profiles: {ex.Message}", LogLevels.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Windows quality update profiles.");
                HideLoading();
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


