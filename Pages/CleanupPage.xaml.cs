using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models.Security;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static IntuneTools.Graph.DestinationTenantGraphClient;
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
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.SourceTenantGraphClient;
using static IntuneTools.Utilities.Variables;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CleanupPage : Page
    {
        public CleanupPage()
        {
            InitializeComponent();
        }

        public class ContentInfo
        {
            public string? ContentName { get; set; }
            public string? ContentPlatform { get; set; }
            public string? ContentType { get; set; }
            public string? ContentId { get; set; }
        }

        public ObservableCollection<ContentInfo> ContentList { get; set; } = new ObservableCollection<ContentInfo>();


        /// <summary>
        /// Data Grid methods
        /// </summary>

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
        }
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
            var policies = await GetAllSettingsCatalogPolicies(sourceGraphServiceClient);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.Name,
                    ContentType = "Settings Catalog",
                    ContentPlatform = policy.Platforms?.ToString() ?? string.Empty,
                    ContentId = policy.Id
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {policies.Count()} settings catalog policies.");
        }
        private async Task SearchForSettingsCatalogPoliciesAsync(string searchQuery)
        {
            var policies = await SearchForSettingsCatalog(sourceGraphServiceClient, searchQuery);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.Name,
                    ContentType = "Settings Catalog",
                    ContentPlatform = policy.Platforms?.ToString() ?? string.Empty,
                    ContentId = policy.Id
                });
            }
            AppendToDetailsRichTextBlock($"Found {policies.Count()} settings catalog policies matching '{searchQuery}'.");
        }
        private List<string> GetSettingsCatalogIDs()
        {
            // This method retrieves the IDs of all settings catalog policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Settings Catalog")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task DeleteSettingsCatalogsAsync()
        {
            int count = 0;
            ShowLoading("Deleting settings catalog policies from Microsoft Graph...");
            try
            {
                // Get all settings catalog IDs
                var settingsCatalogIDs = GetSettingsCatalogIDs();
                if (settingsCatalogIDs.Count == 0)
                {
                    WriteToImportStatusFile("No settings catalog policies found to delete.");
                    return;
                }
                
                count = settingsCatalogIDs.Count;

                WriteToImportStatusFile($"Found {count} settings catalog policies to delete.");

                // Delete each settings catalog policy

                foreach (var id in settingsCatalogIDs)
                {
                    await DeleteSettingsCatalog(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted settings catalog policy with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting settings catalog policies: {ex.Message}", LogType.Error);
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
            var policies = await GetAllDeviceCompliancePolicies(sourceGraphServiceClient);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Device Compliance Policy",
                    ContentPlatform = policy.OdataType?.ToString() ?? string.Empty,
                    ContentId = policy.Id
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {policies.Count()} device compliance policies.");
        }
        private async Task SearchForDeviceCompliancePoliciesAsync(string searchQuery)
        {
            var policies = await SearchForDeviceCompliancePolicies(sourceGraphServiceClient, searchQuery);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Device Compliance Policy",
                    ContentPlatform = policy.OdataType?.ToString() ?? string.Empty,
                    ContentId = policy.Id
                });
            }
            AppendToDetailsRichTextBlock($"Found {policies.Count()} device compliance policies matching '{searchQuery}'.");
        }
        private List<string> GetDeviceCompliancePolicyIDs()
        {
            // This method retrieves the IDs of all device compliance policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Device Compliance Policy")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task DeleteDeviceCompliancePoliciesAsync()
        {
            int count = 0;
            ShowLoading("Deleting device compliance policies from Microsoft Graph...");
            try
            {
                // Get all device compliance policy IDs
                var deviceCompliancePolicyIDs = GetDeviceCompliancePolicyIDs();
                if (deviceCompliancePolicyIDs.Count == 0)
                {
                    WriteToImportStatusFile("No device compliance policies found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {deviceCompliancePolicyIDs.Count} device compliance policies to delete.");
                // Delete each device compliance policy
                foreach (var id in deviceCompliancePolicyIDs)
                {
                    await DeleteDeviceCompliancePolicy(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted device compliance policy with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting device compliance policies: {ex.Message}", LogType.Error);
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
            var policies = await GetAllDeviceConfigurations(sourceGraphServiceClient);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Device Configuration Policy",
                    ContentPlatform = policy.OdataType?.ToString() ?? string.Empty,
                    ContentId = policy.Id
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {policies.Count()} device configuration policies.");
        }
        private async Task SearchForDeviceConfigurationPoliciesAsync(string searchQuery)
        {
            var policies = await SearchForDeviceConfigurations(sourceGraphServiceClient, searchQuery);
            foreach (var policy in policies)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = policy.DisplayName,
                    ContentType = "Device Configuration Policy",
                    ContentPlatform = policy.OdataType?.ToString() ?? string.Empty,
                    ContentId = policy.Id
                });
            }
            AppendToDetailsRichTextBlock($"Found {policies.Count()} device configuration policies matching '{searchQuery}'.");
        }
        private List<string> GetDeviceConfigurationPolicyIDs()
        {
            // This method retrieves the IDs of all device configuration policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Device Configuration Policy")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task DeleteDeviceConfigurationPoliciesAsync()
        {
            int count = 0;
            ShowLoading("Deleting device configuration policies from Microsoft Graph...");
            try
            {
                // Get all device configuration policy IDs
                var deviceConfigurationPolicyIDs = GetDeviceConfigurationPolicyIDs();
                if (deviceConfigurationPolicyIDs.Count == 0)
                {
                    WriteToImportStatusFile("No device configuration policies found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {deviceConfigurationPolicyIDs.Count} device configuration policies to delete.");
                // Delete each device configuration policy
                foreach (var id in deviceConfigurationPolicyIDs)
                {
                    await DeleteDeviceConfigurationPolicy(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted device configuration policy with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting device configuration policies: {ex.Message}", LogType.Error);
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
            var profiles = await GetAllAppleBYODEnrollmentProfiles(sourceGraphServiceClient);
            foreach (var profile in profiles)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Apple BYOD Enrollment Profile",
                    ContentPlatform = "iOS/iPadOS",
                    ContentId = profile.Id
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {profiles.Count()} Apple BYOD enrollment profiles.");
        }
        private async Task SearchForAppleBYODEnrollmentProfilesAsync(string searchQuery)
        {
            var profiles = await SearchForAppleBYODEnrollmentProfiles(sourceGraphServiceClient, searchQuery);
            foreach (var profile in profiles)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Apple BYOD Enrollment Profile",
                    ContentPlatform = "iOS/iPadOS",
                    ContentId = profile.Id
                });
            }
            AppendToDetailsRichTextBlock($"Found {profiles.Count()} Apple BYOD enrollment profiles matching '{searchQuery}'.");
        }
        private List<string> GetAppleBYODEnrollmentProfileIDs()
        {
            // This method retrieves the IDs of all Apple BYOD enrollment profiles in ContentList
            return ContentList
                .Where(c => c.ContentType == "Apple BYOD Enrollment Profile")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task DeleteAppleBYODEnrollmentProfilesAsync()
        {
            int count = 0;
            ShowLoading("Deleting Apple BYOD enrollment profiles from Microsoft Graph...");
            try
            {
                // Get all Apple BYOD enrollment profile IDs
                var appleBYODEnrollmentProfileIDs = GetAppleBYODEnrollmentProfileIDs();
                if (appleBYODEnrollmentProfileIDs.Count == 0)
                {
                    WriteToImportStatusFile("No Apple BYOD enrollment profiles found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {appleBYODEnrollmentProfileIDs.Count} Apple BYOD enrollment profiles to delete.");
                // Delete each Apple BYOD enrollment profile
                foreach (var id in appleBYODEnrollmentProfileIDs)
                {
                    await DeleteAppleBYODEnrollmentProfile(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted Apple BYOD enrollment profile with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting Apple BYOD enrollment profiles: {ex.Message}", LogType.Error);
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
            var filters = await GetAllAssignmentFilters(sourceGraphServiceClient);
            foreach (var filter in filters)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = filter.DisplayName,
                    ContentType = "Assignment Filter",
                    ContentPlatform = filter.OdataType?.ToString() ?? string.Empty,
                    ContentId = filter.Id
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
                    ContentPlatform = filter.OdataType?.ToString() ?? string.Empty,
                    ContentId = filter.Id
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
        private async Task DeleteAssignmentFiltersAsync()
        {
            int count = 0;
            ShowLoading("Deleting assignment filters from Microsoft Graph...");
            try
            {
                // Get all assignment filter IDs
                var assignmentFilterIDs = GetAssignmentFilterIDs();
                if (assignmentFilterIDs.Count == 0)
                {
                    WriteToImportStatusFile("No assignment filters found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {assignmentFilterIDs.Count} assignment filters to delete.");
                // Delete each assignment filter
                foreach (var id in assignmentFilterIDs)
                {
                    await DeleteAssignmentFilter(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted assignment filter with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting assignment filters: {ex.Message}", LogType.Error);
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
            var groups = await GetAllGroups(sourceGraphServiceClient);
            foreach (var group in groups)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = group.DisplayName,
                    ContentType = "Entra Group",
                    ContentPlatform = "Entra ID",
                    ContentId = group.Id
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
                    ContentPlatform = "Entra ID",
                    ContentId = group.Id
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
        private async Task DeleteEntraGroupsAsync()
        {
            int count = 0;
            ShowLoading("Deleting Entra groups from Microsoft Graph...");
            try
            {
                // Get all Entra group IDs
                var entraGroupIDs = GetEntraGroupIDs();
                if (entraGroupIDs.Count == 0)
                {
                    WriteToImportStatusFile("No Entra groups found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {entraGroupIDs.Count} Entra groups to delete.");
                // Delete each Entra group
                foreach (var id in entraGroupIDs)
                {
                    await DeleteSecurityGroup(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted Entra group with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting Entra groups: {ex.Message}", LogType.Error);
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
            var scripts = await GetAllPowerShellScripts(sourceGraphServiceClient);
            foreach (var script in scripts)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "PowerShell Script",
                    ContentPlatform = "Windows",
                    ContentId = script.Id
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {scripts.Count()} PowerShell scripts.");
        }
        private async Task SearchForPowerShellScriptsAsync(string searchQuery)
        {
            var scripts = await SearchForPowerShellScripts(sourceGraphServiceClient, searchQuery);
            foreach (var script in scripts)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "PowerShell Script",
                    ContentPlatform = "Windows",
                    ContentId = script.Id
                });
            }
            AppendToDetailsRichTextBlock($"Found {scripts.Count()} PowerShell scripts matching '{searchQuery}'.");
        }
        private List<string> GetPowerShellScriptIDs()
        {
            // This method retrieves the IDs of all PowerShell scripts in ContentList
            return ContentList
                .Where(c => c.ContentType == "PowerShell Script")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task DeletePowerShellScriptsAsync()
        {
            int count = 0;
            ShowLoading("Deleting PowerShell scripts from Microsoft Graph...");
            try
            {
                // Get all PowerShell script IDs
                var powerShellScriptIDs = GetPowerShellScriptIDs();
                if (powerShellScriptIDs.Count == 0)
                {
                    WriteToImportStatusFile("No PowerShell scripts found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {powerShellScriptIDs.Count} PowerShell scripts to delete.");
                // Delete each PowerShell script
                foreach (var id in powerShellScriptIDs)
                {
                    await DeletePowerShellScript(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted PowerShell script with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting PowerShell scripts: {ex.Message}", LogType.Error);
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
            var scripts = await GetAllProactiveRemediations(sourceGraphServiceClient);
            foreach (var script in scripts)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "Proactive Remediation",
                    ContentPlatform = "Windows",
                    ContentId = script.Id
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {scripts.Count()} proactive remediations.");
        }
        private async Task SearchForProactiveRemediationsAsync(string searchQuery)
        {
            var scripts = await SearchForProactiveRemediations(sourceGraphServiceClient, searchQuery);
            foreach (var script in scripts)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "Proactive Remediation",
                    ContentPlatform = "Windows",
                    ContentId = script.Id
                });
            }
            AppendToDetailsRichTextBlock($"Found {scripts.Count()} proactive remediations matching '{searchQuery}'.");
        }
        private List<string> GetProactiveRemediationIDs()
        {
            // This method retrieves the IDs of all proactive remediations in ContentList
            return ContentList
                .Where(c => c.ContentType == "Proactive Remediation")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task DeleteProactiveRemediationsAsync()
        {
            int count = 0;
            ShowLoading("Deleting proactive remediations from Microsoft Graph...");
            try
            {
                // Get all proactive remediation IDs
                var proactiveRemediationIDs = GetProactiveRemediationIDs();
                if (proactiveRemediationIDs.Count == 0)
                {
                    WriteToImportStatusFile("No proactive remediations found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {proactiveRemediationIDs.Count} proactive remediations to delete.");
                // Delete each proactive remediation
                foreach (var id in proactiveRemediationIDs)
                {
                    await DeleteProactiveRemediationScript(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted proactive remediation with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting proactive remediations: {ex.Message}", LogType.Error);
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
            var scripts = await GetAllmacOSShellScripts(sourceGraphServiceClient);
            foreach (var script in scripts)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "MacOS Shell Script",
                    ContentPlatform = "macOS",
                    ContentId = script.Id
                });
            }
            AppendToDetailsRichTextBlock($"Loaded {scripts.Count()} MacOS shell scripts.");
        }
        private async Task SearchForMacOSShellScriptsAsync(string searchQuery)
        {
            var scripts = await SearchForShellScriptmacOS(sourceGraphServiceClient, searchQuery);
            foreach (var script in scripts)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = script.DisplayName,
                    ContentType = "MacOS Shell Script",
                    ContentPlatform = "macOS",
                    ContentId = script.Id
                });
            }
            AppendToDetailsRichTextBlock($"Found {scripts.Count()} MacOS shell scripts matching '{searchQuery}'.");
        }
        private List<string> GetMacOSShellScriptIDs()
        {
            // This method retrieves the IDs of all MacOS shell scripts in ContentList
            return ContentList
                .Where(c => c.ContentType == "MacOS Shell Script")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task DeleteMacOSShellScriptsAsync()
        {
            int count = 0;
            ShowLoading("Deleting MacOS shell scripts from Microsoft Graph...");
            try
            {
                // Get all MacOS shell script IDs
                var macOSShellScriptIDs = GetMacOSShellScriptIDs();
                if (macOSShellScriptIDs.Count == 0)
                {
                    WriteToImportStatusFile("No MacOS shell scripts found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {macOSShellScriptIDs.Count} MacOS shell scripts to delete.");
                // Delete each MacOS shell script
                foreach (var id in macOSShellScriptIDs)
                {
                    await DeleteMacosShellScript(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted MacOS shell script with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting MacOS shell scripts: {ex.Message}", LogType.Error);
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
            var profiles = await GetAllWindowsAutoPilotProfiles(sourceGraphServiceClient);
            foreach (var profile in profiles)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = profile.DisplayName,
                    ContentType = "Windows AutoPilot Profile",
                    ContentPlatform = "Windows",
                    ContentId = profile.Id
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
                    ContentId = profile.Id
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
        private async Task DeleteWindowsAutoPilotProfilesAsync()

        {
            int count = 0;
            ShowLoading("Deleting Windows AutoPilot profiles from Microsoft Graph...");
            try
            {
                // Get all Windows AutoPilot profile IDs
                var windowsAutoPilotProfileIDs = GetWindowsAutoPilotProfileIDs();
                if (windowsAutoPilotProfileIDs.Count == 0)
                {
                    WriteToImportStatusFile("No Windows AutoPilot profiles found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {windowsAutoPilotProfileIDs.Count} Windows AutoPilot profiles to delete.");
                // Delete each Windows AutoPilot profile
                foreach (var id in windowsAutoPilotProfileIDs)
                {
                    await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted Windows AutoPilot profile with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting Windows AutoPilot profiles: {ex.Message}", LogType.Error);
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
            var updates = await GetAllDriverProfiles(sourceGraphServiceClient);
            foreach (var update in updates)
            {
                ContentList.Add(new ContentInfo
                {
                    ContentName = update.DisplayName,
                    ContentType = "Windows Driver Update",
                    ContentPlatform = "Windows",
                    ContentId = update.Id
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
                    ContentId = update.Id
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
        private async Task DeleteWindowsDriverUpdatesAsync()
        {
            int count = 0;
            ShowLoading("Deleting Windows driver updates from Microsoft Graph...");
            try
            {
                // Get all Windows driver update IDs
                var windowsDriverUpdateIDs = GetWindowsDriverUpdateIDs();
                if (windowsDriverUpdateIDs.Count == 0)
                {
                    WriteToImportStatusFile("No Windows driver updates found to delete.");
                    return;
                }
                WriteToImportStatusFile($"Found {windowsDriverUpdateIDs.Count} Windows driver updates to delete.");
                // Delete each Windows driver update
                foreach (var id in windowsDriverUpdateIDs)
                {
                    await DeleteDriverProfile(sourceGraphServiceClient, id);
                    WriteToImportStatusFile($"Deleted Windows driver update with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"Error deleting Windows driver updates: {ex.Message}", LogType.Error);
            }
            finally
            {
                AppendToDetailsRichTextBlock($"Deleted {count} Windows driver updates.");
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
            var selectedItems = CleanupDataGrid.SelectedItems?.Cast<ContentInfo>().ToList();
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
