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
    }
}
