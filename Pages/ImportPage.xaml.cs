using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Windows.Foundation; // Added for IAsyncOperation
using System.Runtime.CompilerServices; // Add this for await support on IAsyncOperation
using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents; // Added for Paragraph and Run
using IntuneTools.Graph;
using Microsoft.Graph.Beta;
using static IntuneTools.Graph.DestinationTenantGraphClient;
using static IntuneTools.Graph.EntraHelperClasses.GroupHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
using static IntuneTools.Graph.IntuneHelperClasses.FilterHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static IntuneTools.Graph.IntuneHelperClasses.PowerShellScriptsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ProactiveRemediationsHelper;
using static IntuneTools.Graph.IntuneHelperClasses.macOSShellScript;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsAutoPilotHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsDriverUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsFeatureUpdateHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdatePolicyHandler;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.SourceTenantGraphClient;
using static IntuneTools.Utilities.Variables;
using System.Collections.ObjectModel; // Add this for ObservableCollection

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{

    public class ContentInfo
    {
        public string? ContentName { get; set; }
        public string? ContentPlatform { get; set; }
        public string? ContentType { get; set; }
        public string? ContentId { get; set; }
    }

    public class GroupInfo
    {
        public string? GroupName { get; set; }
    }

    public class FilterInfo
    {
        public string? FilterName { get; set; }
    }

    public sealed partial class ImportPage : Page
    {
        public ObservableCollection<ContentInfo> ContentList { get; set; } = new ObservableCollection<ContentInfo>();
        public ObservableCollection<GroupInfo> GroupList { get; set; } = new ObservableCollection<GroupInfo>();
        public ObservableCollection<FilterInfo> FilterList { get; set; } = new ObservableCollection<FilterInfo>();
        public ObservableCollection<string> FilterOptions { get; set; } = new ObservableCollection<string>();

        private bool _suppressUpdateSelectAll = false;
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        public ImportPage()
        {
            this.InitializeComponent();
            SelectAll_Checked(LoadingOverlay, null); // Initialize the 'Select all' checkbox to checked state
            // Ensure the new controls panel is not visible by default
            NewControlsPanel.Visibility = Visibility.Collapsed;
            //LoadFilterOptions();
            AppendToDetailsRichTextBlock("Console output");
            
        }

        private void AppendToDetailsRichTextBlock(string text)
        {
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

        private void LoadFilterOptions()
        {
            // Add dummy data for now
            FilterOptions.Add("Filter 1");
            FilterOptions.Add("Filter 2");
            FilterOptions.Add("Filter 3");
            FilterSelectionComboBox.ItemsSource = FilterOptions;
        }

        private void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            LoadingStatusText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingProgressRing.IsActive = true;

            // // Optionally disable buttons during loading
            ListAll.IsEnabled = false;
            Search.IsEnabled = false;
        }        // Hide loading overlay - TODO: Uncomment when XAML controls are available
        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingProgressRing.IsActive = false;

            // Re-enable buttons
            ListAll.IsEnabled = true;
            Search.IsEnabled = true;
        }

        public List<string> GetCheckedOptionNames()
        {
            var checkedNames = new List<string>();
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true)
                {
                    checkedNames.Add(cb.Name); // or cb.Content.ToString() for display text
                }
            }
            return checkedNames;
        }


        /// Graph API Methods ///
        /// These methods should handle the actual API calls to Microsoft Graph.
        /// 

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            try
            {
                // Clear the ContentList before loading new data
                ContentList.Clear();


                // Get the names of checked options
                var selectedContent = GetCheckedOptionNames(); 

                if (selectedContent.Count == 0)
                {
                    // If no options are selected, show a message and return
                    AppendToDetailsRichTextBlock("No content types selected for import.");
                    return;
                }

                if (selectedContent.Contains("SettingsCatalog"))
                {
                    // Load Settings Catalog policies
                    await LoadAllSettingsCatalogPoliciesAsync();
                }
                if (selectedContent.Contains("DeviceCompliance"))
                {
                    // Load Device Compliance policies
                    await LoadAllDeviceCompliancePoliciesAsync();
                }
                if (selectedContent.Contains("DeviceConfiguration"))
                {
                    // Load Device Configuration policies
                    await LoadAllDeviceConfigurationPoliciesAsync();
                }
                if (selectedContent.Contains("AppleBYODEnrollmentProfile"))
                {
                    // Load Apple BYOD Enrollment Profiles
                    await LoadAllAppleBYODEnrollmentProfilesAsync();
                }
                if (selectedContent.Contains("PowerShellScript"))
                {
                    // Load PowerShell Scripts
                    await LoadAllPowerShellScriptsAsync();
                }
                if (selectedContent.Contains("ProactiveRemediation"))
                {
                    // Load Proactive Remediations
                    await LoadAllProactiveRemediationsAsync();
                }
                if (selectedContent.Contains("macOSShellScript"))
                {
                    // Load macOS Shell Scripts
                    await LoadAllmacOSShellScriptsAsync();
                }
                if (selectedContent.Contains("WindowsAutopilot"))
                {
                    // Load Windows AutoPilot Profiles
                    await LoadAllWindowsAutoPilotProfilesAsync();
                }
                if (selectedContent.Contains("WindowsDriverUpdate"))
                {
                    // Load Windows Driver Updates
                    await LoadAllWindowsDriverUpdatesAsync();
                }
                if (selectedContent.Contains("WindowsFeatureUpdate"))
                {
                    // Load Windows Feature Updates
                    await LoadAllWindowsFeatureUpdatesAsync();
                }
                if (selectedContent.Contains("WindowsQualityUpdatePolicy"))
                {
                    // Load Windows Quality Update policies
                    await LoadAllWindowsQualityUpdatePoliciesAsync();
                }
                if (selectedContent.Contains("WindowsQualityUpdateProfile"))
                {
                    // Load Windows Quality Update profiles
                    await LoadAllWindowsQualityUpdateProfilesAsync();
                }
                if (selectedContent.Contains("Filters"))
                {
                    // Load Assignment Filters
                    await LoadAllAssignmentFiltersToBeImportedAsync();
                }
                if (selectedContent.Contains("EntraGroups"))
                {
                    // Load Entra Groups
                    await LoadGroupsOrchestrator();
                }


                // TODO - method to clean up ContentList if needed

                // Clean up content platform value (operating system names) in ContentList
                foreach (var content in ContentList)
                {
                    var cleanedValue = TranslatePolicyPlatformName(content?.ContentPlatform); // Use the method to clean up the platform name
                    content.ContentPlatform = cleanedValue ?? string.Empty; // Ensure no null values

                }
                // More cleanup as needed

                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task SearchOrchestrator(GraphServiceClient graphServiceClient, string searchQuery)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            try
            {
                // Clear the ContentList before loading new data
                ContentList.Clear();


                // Get the names of checked options
                var selectedContent = GetCheckedOptionNames();

                if (selectedContent.Count == 0)
                {
                    // If no options are selected, show a message and return
                    AppendToDetailsRichTextBlock("No content types selected for import.");
                    return;
                }

                if (selectedContent.Contains("SettingsCatalog"))
                {
                    // Load Settings Catalog policies
                    await SearchForSettingsCatalogPoliciesAsync(searchQuery);
                }
                if (selectedContent.Contains("DeviceCompliance"))
                {
                    // Load Device Compliance policies
                    await SearchForDeviceCompliancePoliciesAsync(searchQuery);
                }
                if (selectedContent.Contains("DeviceConfiguration"))
                {
                    // Load Device Configuration policies
                    await SearchForDeviceConfigurationAsync(searchQuery);
                }
                if (selectedContent.Contains("AppleBYODEnrollmentProfile"))
                {
                    // Load Apple BYOD Enrollment Profiles
                    await SearchForAppleBYODEnrollmentProfilesAsync(searchQuery);
                }
                if (selectedContent.Contains("PowerShellScript"))
                {
                    // Load PowerShell Scripts
                    await SearchForPowerShellScriptsAsync(searchQuery);
                }
                if (selectedContent.Contains("ProactiveRemediation"))
                {
                    // Load Proactive Remediations
                    await SearchForProactiveRemediationsAsync(searchQuery);
                }
                if (selectedContent.Contains("macOSShellScript"))
                {
                    // Load macOS Shell Scripts
                    await SearchFormacOSShellScriptsAsync(searchQuery);
                }
                if (selectedContent.Contains("WindowsAutopilot"))
                {
                    // Load Windows AutoPilot Profiles
                    await SearchForWindowsAutoPilotProfilesAsync(searchQuery);
                }
                if (selectedContent.Contains("WindowsDriverUpdate"))
                {
                    // Load Windows Driver Updates
                    await SearchForWindowsDriverUpdatesAsync(searchQuery);
                }
                if (selectedContent.Contains("WindowsFeatureUpdate"))
                {
                    // Load Windows Feature Updates
                    await SearchForWindowsFeatureUpdatesAsync(searchQuery);
                }
                if (selectedContent.Contains("WindowsQualityUpdatePolicy"))
                {
                    // Load Windows Quality Update policies
                    await SearchForWindowsQualityUpdatePoliciesAsync(searchQuery);
                }
                if (selectedContent.Contains("WindowsQualityUpdateProfile"))
                {
                    // Load Windows Quality Update profiles
                    await SearchForWindowsQualityUpdateProfilesAsync(searchQuery);
                }
                if (selectedContent.Contains("Filters"))
                {
                    // Load Assignment Filters
                    await SearchForAssignmentFiltersAsync(searchQuery);
                }
                if (selectedContent.Contains("EntraGroups"))
                {
                    // Load Entra Groups
                    await SearchForGroupsOrchestrator(searchQuery);
                }


                // TODO - method to clean up ContentList if needed

                // Clean up content platform value (operating system names) in ContentList
                foreach (var content in ContentList)
                {
                    var cleanedValue = TranslatePolicyPlatformName(content?.ContentPlatform); // Use the method to clean up the platform name
                    content.ContentPlatform = cleanedValue ?? string.Empty; // Ensure no null values

                }
                // More cleanup as needed

                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
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
            ShowLoading("Loading settings catalog policies from Microsoft Graph...");
            try
            {
                // Retrieve all settings catalog policies
                var policies = await GetAllSettingsCatalogPolicies(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task SearchForSettingsCatalogPoliciesAsync(string searchQuery)
        {
            ShowLoading("Loading settings catalog policies from Microsoft Graph...");
            try
            {
                // Retrieve all settings catalog policies
                var policies = await SearchForSettingsCatalog(sourceGraphServiceClient,searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetSettingsCatalogIDs()
        {
            // This method retrieves the IDs of all settings catalog policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Settings Catalog")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Device Configuration policies
        /// </summary>

        private async Task LoadAllDeviceConfigurationPoliciesAsync()
        {
            ShowLoading("Loading device configuration policies from Microsoft Graph...");
            try
            {
                // Retrieve all device configuration policies
                var policies = await GetAllDeviceConfigurations(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task SearchForDeviceConfigurationAsync(string searchQuery)
        {
            ShowLoading("Loading settings catalog policies from Microsoft Graph...");
            try
            {
                // Retrieve all settings catalog policies
                var policies = await SearchForDeviceConfigurations(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
                foreach (var policy in policies)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = policy.DisplayName,
                        ContentType = "Device Configuration",
                        ContentPlatform = policy.OdataType?.ToString() ?? string.Empty,
                        ContentId = policy.Id
                    });
                }
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetDeviceConfigurationIDs()
        {
            // This method retrieves the IDs of all device configuration policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Device Configuration Policy")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }


        /// <summary>
        ///  Device compliance policies
        /// </summary>
        private async Task LoadAllDeviceCompliancePoliciesAsync()
        {
            ShowLoading("Loading device compliance policies from Microsoft Graph...");
            try
            {
                // Retrieve all device compliance policies
                var policies = await GetAllDeviceCompliancePolicies(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForDeviceCompliancePoliciesAsync(string searchQuery)
        {
            ShowLoading("Loading settings catalog policies from Microsoft Graph...");
            try
            {
                // Retrieve all settings catalog policies
                var policies = await SearchForDeviceCompliancePolicies(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
                foreach (var policy in policies)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = policy.DisplayName,
                        ContentType = "Device Compliance",
                        ContentPlatform = policy.OdataType?.ToString() ?? string.Empty,
                        ContentId = policy.Id
                    });
                }
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private List<string> GetDeviceComplianceIDs()
        {
            // This method retrieves the IDs of all settings catalog policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Device Compliance Policy")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Apple BYOD Enrollment Profiles
        /// </summary>
        
        private async Task LoadAllAppleBYODEnrollmentProfilesAsync()
        {
            ShowLoading("Loading Apple BYOD Enrollment Profiles from Microsoft Graph...");
            try
            {
                // Retrieve all Apple BYOD Enrollment Profiles
                var profiles = await GetAllAppleBYODEnrollmentProfiles(sourceGraphServiceClient);
                // Update ContentList for DataGrid
                foreach (var profile in profiles)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = profile.DisplayName,
                        ContentType = "Apple BYOD Enrollment Profile",
                        ContentPlatform = "iOS",
                        ContentId = profile.Id
                    });
                }
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForAppleBYODEnrollmentProfilesAsync(string searchQuery)
        {
            ShowLoading("Loading Apple BYOD Enrollment Profiles from Microsoft Graph...");
            try
            {
                // Retrieve all Apple BYOD Enrollment Profiles
                var profiles = await SearchForAppleBYODEnrollmentProfiles(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
                foreach (var profile in profiles)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = profile.DisplayName,
                        ContentType = "Apple BYOD Enrollment Profile",
                        ContentPlatform = "iOS",
                        ContentId = profile.Id
                    });
                }
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private List<string> GetAppleBYODEnrollmentProfileIDs()
        {
            // This method retrieves the IDs of all Apple BYOD Enrollment Profiles in ContentList
            return ContentList
                .Where(c => c.ContentType == "Apple BYOD Enrollment Profile")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }


        /// <summary>
        /// PowerShell Scripts
        /// </summary>

        private async Task LoadAllPowerShellScriptsAsync()
        {
            ShowLoading("Loading PowerShell scripts from Microsoft Graph...");
            try
            {
                // Retrieve all PowerShell scripts
                var scripts = await GetAllPowerShellScripts(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task SearchForPowerShellScriptsAsync(string searchQuery)
        {
            ShowLoading("Loading PowerShell scripts from Microsoft Graph...");
            try
            {
                // Retrieve all PowerShell scripts
                var scripts = await SearchForPowerShellScripts(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
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
            ShowLoading("Loading proactive remediations from Microsoft Graph...");
            try
            {
                // Retrieve all proactive remediations
                var scripts = await GetAllProactiveRemediations(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForProactiveRemediationsAsync(string searchQuery)
        {
            ShowLoading("Loading proactive remediations from Microsoft Graph...");
            try
            {
                // Retrieve all proactive remediations
                var scripts = await SearchForProactiveRemediations(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
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
        /// macOS Shell Scripts
        /// </summary>

        private async Task LoadAllmacOSShellScriptsAsync()
        {
            ShowLoading("Loading macOS shell scripts from Microsoft Graph...");
            try
            {
                // Retrieve all macOS shell scripts
                var scripts = await GetAllmacOSShellScripts(sourceGraphServiceClient);
                // Update ContentList for DataGrid
                foreach (var script in scripts)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = script.DisplayName,
                        ContentType = "macOS Shell Script",
                        ContentPlatform = "macOS",
                        ContentId = script.Id
                    });
                }
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchFormacOSShellScriptsAsync(string searchQuery)
        {
            ShowLoading("Loading macOS shell scripts from Microsoft Graph...");
            try
            {
                // Retrieve all macOS shell scripts
                var scripts = await SearchForShellScriptmacOS(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
                foreach (var script in scripts)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = script.DisplayName,
                        ContentType = "macOS Shell Script",
                        ContentPlatform = "macOS",
                        ContentId = script.Id
                    });
                }
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetmacOSShellScriptIDs()
        {
            // This method retrieves the IDs of all macOS shell scripts in ContentList
            return ContentList
                .Where(c => c.ContentType == "macOS Shell Script")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Windows AutoPilot
        /// </summary>

        private async Task LoadAllWindowsAutoPilotProfilesAsync()
        {
            ShowLoading("Loading Windows AutoPilot profiles from Microsoft Graph...");
            try
            {
                // Retrieve all Windows AutoPilot profiles
                var profiles = await GetAllWindowsAutoPilotProfiles(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForWindowsAutoPilotProfilesAsync(string searchQuery)
        {
            ShowLoading("Loading Windows AutoPilot profiles from Microsoft Graph...");
            try
            {
                // Retrieve all Windows AutoPilot profiles
                var profiles = await SearchForWindowsAutoPilotProfiles(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
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
            ShowLoading("Loading Windows Driver Updates from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Driver Updates
                var updates = await GetAllDriverProfiles(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForWindowsDriverUpdatesAsync(string searchQuery)
        {
            ShowLoading("Loading Windows Driver Updates from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Driver Updates
                var updates = await SearchForDriverProfiles(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetWindowsDriverUpdateIDs()
        {
            // This method retrieves the IDs of all Windows Driver Updates in ContentList
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
            ShowLoading("Loading Windows Feature Updates from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Feature Updates
                var updates = await GetAllWindowsFeatureUpdateProfiles(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForWindowsFeatureUpdatesAsync(string searchQuery)
        {
            ShowLoading("Loading Windows Feature Updates from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Feature Updates
                var updates = await SearchForWindowsFeatureUpdateProfiles(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetWindowsFeatureUpdateIDs()
        {
            // This method retrieves the IDs of all Windows Feature Updates in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows Feature Update")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary>
        /// Windows Quality Update policies
        /// Must not be confused with Windows quality update profiles AKA expedite
        /// </summary>

        private async Task LoadAllWindowsQualityUpdatePoliciesAsync()
        {
            ShowLoading("Loading Windows Quality Update policies from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Quality Update policies
                var policies = await GetAllWindowsQualityUpdatePolicies(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForWindowsQualityUpdatePoliciesAsync(string searchQuery)
        {
            ShowLoading("Loading Windows Quality Update policies from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Quality Update policies
                var policies = await SearchForWindowsQualityUpdatePolicies(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetWindowsQualityUpdatePolicyIDs()
        {
            // This method retrieves the IDs of all Windows Quality Update policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows Quality Update Policy")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary
        /// Windows Quality Update profiles
        /// Must not be confused with Windows quality update policies AKA hotpatch
        /// </summary>

        private async Task LoadAllWindowsQualityUpdateProfilesAsync()
        {
            ShowLoading("Loading Windows Quality Update profiles from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Quality Update profiles
                var profiles = await GetAllWindowsQualityUpdateProfiles(sourceGraphServiceClient);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForWindowsQualityUpdateProfilesAsync(string searchQuery)
        {
            ShowLoading("Loading Windows Quality Update profiles from Microsoft Graph...");
            try
            {
                // Retrieve all Windows Quality Update profiles
                var profiles = await SearchForWindowsQualityUpdateProfiles(sourceGraphServiceClient, searchQuery);
                // Update ContentList for DataGrid
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
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetWindowsQualityUpdateProfileIDs()
        {
            // This method retrieves the IDs of all Windows Quality Update profiles in ContentList
            return ContentList
                .Where(c => c.ContentType == "Windows Quality Update Profile")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        /// <summary
        /// Groups
        /// </summary>

        private async Task LoadGroupsOrchestrator()
        {
            ShowLoading("Loading groups from Microsoft Graph...");
            try
            {
                // Clear the GroupList before loading new data
                GroupList.Clear();
                // Load all groups from Graph API
                var groups = await GetAllGroups(sourceGraphServiceClient);
                // Update GroupList for DataGrid
                foreach (var group in groups)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentId = group.Id,
                        ContentName = group.DisplayName,
                        ContentType = "Entra Group",
                        ContentPlatform = group.GroupTypes != null && group.GroupTypes.Contains("Unified") ? "Microsoft 365 Group" : "Security Group"
                    });
                }
                // Bind to DataGrid
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForGroupsOrchestrator(string searchQuery)
        {
            ShowLoading("Searching for groups in Microsoft Graph...");
            try
            {
                // Clear the GroupList before loading new data
                GroupList.Clear();
                // Search for groups using the provided query
                var groups = await SearchForGroups(sourceGraphServiceClient, searchQuery);
                // Update GroupList for DataGrid
                foreach (var group in groups)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentId = group.Id,
                        ContentName = group.DisplayName,
                        ContentType = "Entra Group",
                        ContentPlatform = group.GroupTypes != null && group.GroupTypes.Contains("Unified") ? "Microsoft 365 Group" : "Security Group"
                    });
                }
                // Bind to DataGrid
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();
            }
        }
        private List<string> GetGroupIDs()
        {
            // This method retrieves the IDs of all groups in GroupList
            return ContentList
                .Where(c => c.ContentType == "Entra Group")
                .Select(g => g.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }
        private async Task SearchForGroupsAsync(string searchQuery)
        {
            // Clear the GroupList before loading new data
            GroupList.Clear();

            ShowLoading("Searching for groups in Microsoft Graph...");
            try
            {
                // Clear the GroupList before loading new data
                GroupList.Clear();
                // Search for groups using the provided query
                var groups = await SearchForGroups(destinationGraphServiceClient, searchQuery);
                // Update GroupList for DataGrid
                foreach (var group in groups)
                {
                    GroupList.Add(new GroupInfo
                    {
                        GroupName = group.DisplayName
                    });
                }
                // Bind to DataGrid
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();
                
            }
        }
        private async Task LoadAllGroupsAsync()
        {
            // Clear the GroupList before loading new data
            GroupList.Clear();

            ShowLoading("Loading groups from Microsoft Graph...");
            try
            {
                // Retrieve all groups
                var groups = await GetAllGroups(destinationGraphServiceClient);
                // Update ContentList for DataGrid
                foreach (var group in groups)
                {
                    GroupList.Add(new GroupInfo
                    {
                        GroupName = group.DisplayName
                    });
                }
                // Bind to DataGrid
                GroupDataGrid.ItemsSource = GroupList;
            }
            finally
            {
                HideLoading();
            }
        }


        /// <summary>
        /// Assignment filters
        /// </summary>

        private async Task LoadAllAssignmentFiltersToBeImportedAsync()
        {
            // Clear the dictionary for filter names and IDs
            filterNameAndID.Clear();
            ShowLoading("Loading assignment filters from Microsoft Graph...");
            try
            {
                // Clear existing filter options
                FilterOptions.Clear();
                // Retrieve all assignment filters
                var filters = await GetAllAssignmentFilters(sourceGraphServiceClient);
                // Update FilterOptions for ComboBox
                foreach (var filter in filters)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = filter.DisplayName,
                        ContentType = "Assignment filter",
                        ContentPlatform = filter.Platform.ToString() ?? string.Empty,
                        ContentId = filter.Id
                    });
                }

                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }
        private async Task SearchForAssignmentFiltersAsync(string searchQuery)
        {
            // Clear the dictionary for filter names and IDs
            filterNameAndID.Clear();
            ShowLoading("Searching for assignment filters in Microsoft Graph...");
            try
            {
                // Clear existing filter options
                FilterOptions.Clear();
                // Retrieve all assignment filters
                var filters = await SearchForAssignmentFilters(sourceGraphServiceClient, searchQuery);
                // Update FilterOptions for ComboBox
                foreach (var filter in filters)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = filter.DisplayName,
                        ContentType = "Assignment filter",
                        ContentPlatform = filter.Platform.ToString() ?? string.Empty,
                        ContentId = filter.Id
                    });
                }
                // Bind to DataGrid
                ContentDataGrid.ItemsSource = ContentList;
            }
            finally
            {
                HideLoading();
            }
        }

        private List<string> GetFilterIDs()
        {
            // This method retrieves the IDs of all device configuration policies in ContentList
            return ContentList
                .Where(c => c.ContentType == "Assignment filter")
                .Select(c => c.ContentId ?? string.Empty) // Ensure no nulls are returned
                .ToList();
        }

        private async Task LoadAllAssignmentFiltersAsync()
        {
            // Clear the dictionary for filter names and IDs
            filterNameAndID.Clear();

            // TODO - update filter variables 

            ShowLoading("Loading assignment filters from Microsoft Graph...");
            try
            {
                // Clear existing filter options
                FilterOptions.Clear();

                // Retrieve all assignment filters
                var filters = await GetAllAssignmentFilters(destinationGraphServiceClient);
                // Update FilterOptions for ComboBox
                foreach (var filter in filters)
                {
                    FilterOptions.Add(filter.DisplayName); // Add filter display name to ComboBox options
                    
                }
                // Ensure ComboBox is bound to FilterOptions (though it should be from XAML or initialization)
                if (FilterSelectionComboBox.ItemsSource != FilterOptions)
                {
                    FilterSelectionComboBox.ItemsSource = FilterOptions;
                }
            }
            finally
            {
                HideLoading();
            }
        }




        /// <summary>
        /// Main import process
        /// </summary>

        private List<string> LogContentToImport()
        {
            LogToImportStatusFile("Importing the following content:", LogLevels.Info);
            AppendToDetailsRichTextBlock("Importing the following content:\n");

            List<string> contentTypes = new List<string>();

            foreach (var content in ContentList)
            {
                // add content type to the list if not already present
                if (!contentTypes.Contains(content.ContentType))
                {
                    contentTypes.Add(content.ContentType);
                    LogToImportStatusFile($"- {content.ContentType}", LogLevels.Info);
                    AppendToDetailsRichTextBlock($"- {content.ContentType}\n");
                }
            }
            
            LogToImportStatusFile("--------------------------------------------------", LogLevels.Info);
            AppendToDetailsRichTextBlock("--------------------------------------------------\n");
            return contentTypes;
        }

        private void LogGroupsToBeAssigned()
        {
            IsGroupSelected = false; // Reset group selection status

            LogToImportStatusFile("Assigning to the following groups:", LogLevels.Info);
            AppendToDetailsRichTextBlock("Assigning to the following groups:\n");
            if (GroupDataGrid.SelectedItems != null && GroupDataGrid.SelectedItems.Count > 0)
            {
                foreach (GroupInfo selectedGroup in GroupDataGrid.SelectedItems)
                {
                    if (selectedGroup != null && !string.IsNullOrEmpty(selectedGroup.GroupName))
                    {
                        LogToImportStatusFile($"- {selectedGroup.GroupName}", LogLevels.Info);
                        AppendToDetailsRichTextBlock($"- {selectedGroup.GroupName}\n");
                        // Add the group name and ID to the selectedGroupNameAndID dictionary
                        if (!selectedGroupNameAndID.ContainsKey(selectedGroup.GroupName))
                        {
                            selectedGroupNameAndID[selectedGroup.GroupName] = groupNameAndID[selectedGroup.GroupName];
                        }
                    }
                }
                IsGroupSelected = true; // Set group selection status to true if any groups are selected
            }
            else
            {
                LogToImportStatusFile("No groups selected for assignment.", LogLevels.Info);
                AppendToDetailsRichTextBlock("No groups selected for assignment.\n");
            }
            LogToImportStatusFile("--------------------------------------------------", LogLevels.Info);
            AppendToDetailsRichTextBlock("--------------------------------------------------\n");
        }

        private void LogFiltersToBeApplied()
        {
            IsFilterSelected = false; // Reset filter selection status

            LogToImportStatusFile("Applying the following filters:", LogLevels.Info);
            AppendToDetailsRichTextBlock("Applying the following filters:\n");
            if (FilterSelectionComboBox.SelectedItem != null)
            {
                string selectedFilter = FilterSelectionComboBox.SelectedItem.ToString();
                
                SelectedFilterID = filterNameAndID.ContainsKey(selectedFilter) ? filterNameAndID[selectedFilter] : null;

                LogToImportStatusFile($"- {selectedFilter}", LogLevels.Info);
                AppendToDetailsRichTextBlock($"- {selectedFilter}\n");
                IsFilterSelected = true; // Set filter selection status to true if a filter is selected
            }
            else
            {
                LogToImportStatusFile("No filter selected for assignment.", LogLevels.Info);
                AppendToDetailsRichTextBlock("No filter selected for assignment.\n");
            }
            LogToImportStatusFile("--------------------------------------------------", LogLevels.Info);
            AppendToDetailsRichTextBlock("--------------------------------------------------\n");
        }


        private async Task MainImportProcess()
        {
            AppendToDetailsRichTextBlock("Starting import process...\n");

            // Check if there is content to import
            if (ContentList.Count == 0)
            {
                LogToImportStatusFile("No content to import.", LogLevels.Warning);
                AppendToDetailsRichTextBlock("No content to import.\n");
                return;
            }

            // Ensure the import status file is created before importing
            CreateImportStatusFile();


            // Log the start of the import process
            LogToImportStatusFile("Starting import process...", LogLevels.Info);
            LogToImportStatusFile($"Source Tenant: {sourceTenantName}", LogLevels.Info);
            LogToImportStatusFile($"Destination Tenant: {destinationTenantName}", LogLevels.Info);
            AppendToDetailsRichTextBlock($"Source Tenant: {sourceTenantName}\n");
            AppendToDetailsRichTextBlock($"Destination Tenant: {destinationTenantName}\n");


            // Log what content is being imported
            var contentList = LogContentToImport();

            // Log which group(s) are being assigned
            LogGroupsToBeAssigned();

            // Log which filter(s) are being applied
            LogFiltersToBeApplied();

            // Extract group IDs into a list for later use
            List<string> groupIDs = new List<string>();
            foreach (var group in selectedGroupNameAndID)
            {
                if (!string.IsNullOrEmpty(group.Value))
                {
                    groupIDs.Add(group.Value); // Add the group ID to the list
                }
            }

            // Perform the import process

            // TODO  - Check that all info is available before proceeding with the import

            if (ContentList.Any(c => c.ContentType == "Entra Group"))
            {
                // Import Entra Groups
                AppendToDetailsRichTextBlock("Importing Entra Groups...\n");
                LogToImportStatusFile("Importing Entra Groups...", LogLevels.Info);
                var groups = GetGroupIDs();
                await ImportMultipleGroups(sourceGraphServiceClient, destinationGraphServiceClient, groups);
                AppendToDetailsRichTextBlock("Entra Groups imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Settings Catalog"))
            {
                // Import Settings Catalog policies
                AppendToDetailsRichTextBlock("Importing Settings Catalog policies...\n");
                LogToImportStatusFile("Importing Settings Catalog policies...", LogLevels.Info);
                var policies = GetSettingsCatalogIDs();
                await ImportMultipleSettingsCatalog(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected,groupIDs);
                AppendToDetailsRichTextBlock("Settings Catalog policies imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Device Compliance Policy"))
            {
                // Import Device Compliance policies
                AppendToDetailsRichTextBlock("Importing Device Compliance policies...\n");
                LogToImportStatusFile("Importing Device Compliance policies...", LogLevels.Info);
                var policies = GetDeviceComplianceIDs();
                await ImportMultipleDeviceCompliancePolicies(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Device Compliance policies imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Device Configuration Policy"))
            {
                // Import Device Configuration policies
                AppendToDetailsRichTextBlock("Importing Device Configuration policies...\n");
                LogToImportStatusFile("Importing Device Configuration policies...", LogLevels.Info);
                var policies = GetDeviceConfigurationIDs();
                await ImportMultipleDeviceConfigurations(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Device Configuration policies imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Apple BYOD Enrollment Profile"))
            {
                // Import Apple BYOD Enrollment Profiles
                AppendToDetailsRichTextBlock("Importing Apple BYOD Enrollment Profiles...\n");
                LogToImportStatusFile("Importing Apple BYOD Enrollment Profiles...", LogLevels.Info);
                var profiles = GetAppleBYODEnrollmentProfileIDs();
                await ImportMultipleAppleBYODEnrollmentProfiles(sourceGraphServiceClient, destinationGraphServiceClient, profiles, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Apple BYOD Enrollment Profiles imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Assignment filter"))
            {
                // Import Assignment Filters
                AppendToDetailsRichTextBlock("Importing Assignment Filters...\n");
                LogToImportStatusFile("Importing Assignment Filters...", LogLevels.Info);
                var filters = GetFilterIDs();
                await ImportMultipleAssignmentFilters(sourceGraphServiceClient, destinationGraphServiceClient, filters);
                AppendToDetailsRichTextBlock("Assignment Filters imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "PowerShell Script"))
            {
                // Import PowerShell Scripts
                AppendToDetailsRichTextBlock("Importing PowerShell Scripts...\n");
                LogToImportStatusFile("Importing PowerShell Scripts...", LogLevels.Info);
                var scripts = GetPowerShellScriptIDs();
                await ImportMultiplePowerShellScripts(sourceGraphServiceClient, destinationGraphServiceClient, scripts, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("PowerShell Scripts imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Proactive Remediation"))
            {
                // Import Proactive Remediations
                AppendToDetailsRichTextBlock("Importing Proactive Remediations...\n");
                LogToImportStatusFile("Importing Proactive Remediations...", LogLevels.Info);
                var scripts = GetProactiveRemediationIDs();
                await ImportMultipleProactiveRemediations(sourceGraphServiceClient, destinationGraphServiceClient, scripts, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Proactive Remediations imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "macOS Shell Script"))
            {
                // Import macOS Shell Scripts
                AppendToDetailsRichTextBlock("Importing macOS Shell Scripts...\n");
                LogToImportStatusFile("Importing macOS Shell Scripts...", LogLevels.Info);
                var scripts = GetmacOSShellScriptIDs();
                await ImportMultiplemacOSShellScripts(sourceGraphServiceClient, destinationGraphServiceClient, scripts, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("macOS Shell Scripts imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Windows AutoPilot Profile"))
            {
                // Import Windows AutoPilot Profiles
                AppendToDetailsRichTextBlock("Importing Windows AutoPilot Profiles...\n");
                LogToImportStatusFile("Importing Windows AutoPilot Profiles...", LogLevels.Info);
                var profiles = GetWindowsAutoPilotProfileIDs();
                await ImportMultipleWindowsAutoPilotProfiles(sourceGraphServiceClient, destinationGraphServiceClient, profiles, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Windows AutoPilot Profiles imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Windows Driver Update"))
            {
                // Import Windows Driver Updates
                AppendToDetailsRichTextBlock("Importing Windows Driver Updates...\n");
                LogToImportStatusFile("Importing Windows Driver Updates...", LogLevels.Info);
                var updates = GetWindowsDriverUpdateIDs();
                await ImportMultipleDriverProfiles(sourceGraphServiceClient, destinationGraphServiceClient, updates, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Windows Driver Updates imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Windows Feature Update"))
            {
                // Import Windows Feature Updates
                AppendToDetailsRichTextBlock("Importing Windows Feature Updates...\n");
                LogToImportStatusFile("Importing Windows Feature Updates...", LogLevels.Info);
                var updates = GetWindowsFeatureUpdateIDs();
                await ImportMultipleWindowsFeatureUpdateProfiles(sourceGraphServiceClient, destinationGraphServiceClient, updates, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Windows Feature Updates imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Windows Quality Update Policy"))
            {
                // Import Windows Quality Update Policies
                AppendToDetailsRichTextBlock("Importing Windows Quality Update Policies...\n");
                LogToImportStatusFile("Importing Windows Quality Update Policies...", LogLevels.Info);
                var policies = GetWindowsQualityUpdatePolicyIDs();
                await ImportMultipleWindowsQualityUpdatePolicies(sourceGraphServiceClient, destinationGraphServiceClient, policies, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Windows Quality Update Policies imported successfully.\n");
            }
            if (ContentList.Any(c => c.ContentType == "Windows Quality Update Profile"))
            {
                // Import Windows Quality Update Profiles
                AppendToDetailsRichTextBlock("Importing Windows Quality Update Profiles...\n");
                LogToImportStatusFile("Importing Windows Quality Update Profiles...", LogLevels.Info);
                var profiles = GetWindowsQualityUpdateProfileIDs();
                await ImportMultipleWindowsQualityUpdateProfiles(sourceGraphServiceClient, destinationGraphServiceClient, profiles, IsGroupSelected, IsFilterSelected, groupIDs);
                AppendToDetailsRichTextBlock("Windows Quality Update Profiles imported successfully.\n");
            }

            AppendToDetailsRichTextBlock("Import process finished.\n");
        }




        /// BUTTON HANDLERS ///
        /// Buttons should be defined in the XAML file and linked to these methods.
        /// Buttons should call other methods to perform specific actions.
        /// Buttons should not directly perform actions themselves.
        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            await MainImportProcess();
        }
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchQueryTextBox.Text))
            {
                await SearchOrchestrator(sourceGraphServiceClient, SearchQueryTextBox.Text);
            }
            else
            {
                AppendToDetailsRichTextBlock("Search query cannot be empty.");
            }
        }
        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All" button is clicked
            await ListAllOrchestrator(sourceGraphServiceClient);
        }  

        private async void GroupListAllClick(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All Groups" button is clicked
            await LoadAllGroupsAsync();
        }

        private async void GroupSearchClick(object sender, RoutedEventArgs e)
        {
            // This method is called when the "Search Groups" button is clicked
            await SearchForGroupsAsync(GroupSearchTextBox.Text);
        }

        private async void FilterCheckBoxClick(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All Assignment Filters" button is clicked
            await LoadAllAssignmentFiltersAsync();
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all items from ContentList, which will update the DataGrid
            ContentList.Clear();
        }
        
        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            // Remove only the selected items from ContentList
            if (ContentDataGrid.SelectedItems != null && ContentDataGrid.SelectedItems.Count > 0)
            {
                // To avoid modifying the collection while iterating, copy selected items to a list
                var itemsToRemove = ContentDataGrid.SelectedItems.Cast<ContentInfo>().ToList();
                foreach (var item in itemsToRemove)
                {
                    ContentList.Remove(item);
                }
            }
        }

        // Handler for the 'Select all' checkbox Checked event
        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.Name != "OptionsAllCheckBox")
                {
                    cb.IsChecked = true;
                }
            }
            _suppressOptionEvents = false;
        }

        // Handler for the 'Select all' checkbox Unchecked event
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

        // Handler for the 'Select all' checkbox Indeterminate event
        private void SelectAll_Indeterminate(object sender, RoutedEventArgs e)
        {
            // Do nothing, or optionally set all to null if you want
            // Option1CheckBox.IsChecked = null;
            // Option2CheckBox.IsChecked = null;
            // Option3CheckBox.IsChecked = null;
        }

        // Handler for individual option checkbox Checked event
        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        // Handler for individual option checkbox Unchecked event
        private void Option_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        // Helper to update the 'Select all' checkbox state based on options
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

        private void GroupsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            NewControlsPanel.Visibility = Visibility.Visible;
            // Call the general Option_Checked handler if needed for other logic (like updating SelectAllCheckBox)
            Option_Checked(sender, e);
        }

        private void GroupsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            NewControlsPanel.Visibility = Visibility.Collapsed;
            // Call the general Option_Unchecked handler if needed for other logic
            Option_Unchecked(sender, e);
        }

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
            // Handle filter selection change
            // For now, just a placeholder
            if (FilterSelectionComboBox.SelectedItem != null)
            {
                string selectedFilter = FilterSelectionComboBox.SelectedItem.ToString();
                // You can add logic here to use the selectedFilter
            }
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
    }
}