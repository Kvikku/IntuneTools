using Microsoft.UI.Xaml.Controls;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models.Security;
using Microsoft.UI.Xaml;
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
    public sealed partial class RenamingPage : Page
    {
        public class ContentInfo
        {
            public string? ContentName { get; set; }
            public string? ContentPlatform { get; set; }
            public string? ContentType { get; set; }
            public string? ContentId { get; set; }
        }

        public ObservableCollection<ContentInfo> ContentList { get; set; } = new ObservableCollection<ContentInfo>();

        public RenamingPage()
        {
            this.InitializeComponent();
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
                RenamingDataGrid.ItemsSource = ContentList;
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
                RenamingDataGrid.ItemsSource = ContentList;
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
            try
            {
                // Rename each content item based on its type
                await RenameSettingsCatalogs(contentIDs, newName);
                //await RenameDeviceCompliancePolicies(contentIDs, newName);
                //await RenameDeviceConfigurationPolicies(contentIDs, newName);
                //await RenameAppleBYODEnrollmentProfiles(contentIDs, newName);
                //await RenameAssignmentFilters(contentIDs, newName);
                //await RenameEntraGroups(contentIDs, newName);
                //await RenamePowerShellScripts(contentIDs, newName);
                //await RenameProactiveRemediations(contentIDs, newName);
                //await RenameMacOSShellScripts(contentIDs, newName);
                //await RenameWindowsAutoPilotProfiles(contentIDs, newName);
                //await RenameWindowsDriverUpdates(contentIDs, newName);
                //await RenameWindowsFeatureUpdates(contentIDs, newName);
                //await RenameWindowsQualityUpdatePolicies(contentIDs, newName);
                //await RenameWindowsQualityUpdateProfiles(contentIDs, newName);
                AppendToDetailsRichTextBlock($"Renamed {contentIDs.Count} items to '{newName}'.");
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Error during renaming: {ex.Message}");
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

        private async Task RenameSettingsCatalogs(List<string> settingsCatalogIDs, string newName)
        {
            foreach (var id in settingsCatalogIDs)
            {
                try
                {
                    await RenameSettingsCatalogPolicy(sourceGraphServiceClient, id, newName);
                    AppendToDetailsRichTextBlock($"Renamed Settings Catalog with ID {id} to '{newName}'.");
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error renaming Settings Catalog with ID {id}: {ex.Message}");
                }
            }
        }



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
                    ContentId = update.Id
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
                    ContentId = update.Id
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
                    ContentId = policy.Id
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
                    ContentId = policy.Id
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
                    ContentId = profile.Id
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
                    ContentId = profile.Id
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









        /// <summary
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
            var selectedItems = RenamingDataGrid.SelectedItems?.Cast<ContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
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
            var selectedItems = RenamingDataGrid.SelectedItems?.Cast<ContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items selected for renaming.");
                return;
            }
            string newName = NewNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                AppendToDetailsRichTextBlock("Please enter a new name.");
                return;
            }
            await RenameContent(selectedItems.Select(i => i.ContentId).ToList(), newName);
        }



    }

}
