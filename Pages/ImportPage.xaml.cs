using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

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

        public void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            CreateImportStatusFile(); // Ensure the import status file is created before importing
        }        // Show loading overlay - TODO: Uncomment when XAML controls are available
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
        }// Example usage in your List All button click
        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading("Retrieving all applications...");
            try
            {
                // Your API call here
                await LoadAllDataFromGraph();
            }
            finally
            {
                HideLoading();
            }
        }

        // Placeholder method for loading data from Graph API
        private async Task LoadAllDataFromGraph()
        {
            // TODO: Implement actual Graph API call
            // For now, simulate some delay
            await Task.Delay(2000);
            
            // Example: Add some sample data
            ContentList.Clear();
            ContentList.Add(new ContentInfo { ContentName = "Sample App 1", ContentType = "Application", ContentPlatform = "Windows" });
            ContentList.Add(new ContentInfo { ContentName = "Sample App 2", ContentType = "Application", ContentPlatform = "iOS" });
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {

        }        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the selected items in the DataGrid - TODO: Uncomment when XAML controls are available
            // ContentDataGrid.SelectedItems.Clear();
        }
    }
} // End of namespace IntuneTools.Pages
