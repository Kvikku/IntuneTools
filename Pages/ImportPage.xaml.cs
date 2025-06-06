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
using static IntuneTools.Utilities.SourceTenantGraphClient;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{

    public class ContentInfo
    {
        public string? ContentName { get; set; }
        public string? ContentPlatform { get; set; }
        public string? ContentType { get; set; }
    }

    public sealed partial class ImportPage : Page
    {
        public ObservableCollection<ContentInfo> ContentList { get; set; } = new ObservableCollection<ContentInfo>();

        public ImportPage()
        {
            this.InitializeComponent();
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
                ContentList.Clear();
                foreach (var policy in policies)
                {
                    ContentList.Add(new ContentInfo
                    {
                        ContentName = policy.Name,
                        ContentType = "Settings Catalog",
                        ContentPlatform = policy.Platforms?.ToString() ?? string.Empty
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



    }
} 
