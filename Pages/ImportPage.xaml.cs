using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph.Beta;
using IntuneTools.Graph;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Utilities.SourceTenantGraphClient;
using System.Net.Mime;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{

    public class ContentInfo
    {
        public string? ContentName { get; set; }
        public string? ContentPlatform { get; set; }
        public string? ContentType { get; set; }
        //public string? ContentId { get; set; }
    }

    public sealed partial class ImportPage : Page
    {
        public ObservableCollection<ContentInfo> ContentList { get; set; } = new ObservableCollection<ContentInfo>();

        private bool _suppressUpdateSelectAll = false;
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        public ImportPage()
        {
            this.InitializeComponent();
            SelectAll_Checked(LoadingOverlay, null); // Initialize the 'Select all' checkbox to checked state
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

                // Load all data from Graph API

                await LoadAllSettingsCatalogPoliciesAsync();
                await LoadAllDeviceCompliancePoliciesAsync();




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
                        //ContentId = policy.Id
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
                        //ContentId = policy.Id
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




        /// BUTTON HANDLERS ///
        /// Buttons should be defined in the XAML file and linked to these methods.
        /// Buttons should call other methods to perform specific actions.
        /// Buttons should not directly perform actions themselves.
        public void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            CreateImportStatusFile(); // Ensure the import status file is created before importing
        }
     
        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            // This method is called when the "List All" button is clicked
            await ListAllOrchestrator(sourceGraphServiceClient);
        }  
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {

        }        
        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the selected items in the DataGrid - TODO: Uncomment when XAML controls are available
            // ContentDataGrid.SelectedItems.Clear();
        }

        // Handler for the 'Select all' checkbox Checked event
        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            Option1CheckBox.IsChecked = true;
            Option2CheckBox.IsChecked = true;
            Option3CheckBox.IsChecked = true;
            _suppressOptionEvents = false;
        }

        // Handler for the 'Select all' checkbox Unchecked event
        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)      
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            Option1CheckBox.IsChecked = false;
            Option2CheckBox.IsChecked = false;
            Option3CheckBox.IsChecked = false;
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
            if (Option1CheckBox == null || Option2CheckBox == null || Option3CheckBox == null)
                return;

            bool?[] states = { Option1CheckBox.IsChecked, Option2CheckBox.IsChecked, Option3CheckBox.IsChecked };
            _suppressSelectAllEvents = true;
            if (states.All(x => x == true))
                OptionsAllCheckBox.IsChecked = true;
            else if (states.All(x => x == false))
                OptionsAllCheckBox.IsChecked = false;
            else
                OptionsAllCheckBox.IsChecked = null;
            _suppressSelectAllEvents = false;
        }
    }
} 
