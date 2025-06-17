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
using static IntuneTools.Graph.IntuneHelperClasses.FilterHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.EntraHelperClasses.GroupHelperClass;
using static IntuneTools.Utilities.SourceTenantGraphClient;
using System.Net.Mime;
using Microsoft.UI.Xaml.Documents; // Added for Paragraph and Run
using Windows.Foundation; // Added for IAsyncOperation

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


        /// <summary
        /// Groups
        /// </summary>

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
                var groups = await SearchForGroups(sourceGraphServiceClient, searchQuery);
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
                var groups = await GetAllGroups(sourceGraphServiceClient);
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

        private async Task LoadAllAssignmentFiltersAsync()
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
                    FilterOptions.Add(filter.DisplayName); // Add filter display name to ComboBox options
                    
                    // Also add to the dictionary for later use
                    filterNameAndID[filter.DisplayName] = filter.Id; // Store name and ID in the dictionary
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

        private void LogContentToImport()
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
        }

        private void LogGroupsToBeAssigned()
        {
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
                    }
                }
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
            LogToImportStatusFile("Applying the following filters:", LogLevels.Info);
            AppendToDetailsRichTextBlock("Applying the following filters:\n");
            if (FilterSelectionComboBox.SelectedItem != null)
            {
                string selectedFilter = FilterSelectionComboBox.SelectedItem.ToString();
                LogToImportStatusFile($"- {selectedFilter}", LogLevels.Info);
                AppendToDetailsRichTextBlock($"- {selectedFilter}\n");
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

            if (ContentList.Count == 0)
            {
                LogToImportStatusFile("No content to import.", LogLevels.Warning);
                AppendToDetailsRichTextBlock("No content to import.\n");
                return;
            }

            // Retrieve source and tenant names
            CreateImportStatusFile(); // Ensure the import status file is created before importing

            // Log the start of the import process
            LogToImportStatusFile("Starting import process...", LogLevels.Info);
            LogToImportStatusFile($"Source Tenant: {sourceTenantName}", LogLevels.Info);
            LogToImportStatusFile($"Destination Tenant: {destinationTenantName}", LogLevels.Info);
            AppendToDetailsRichTextBlock($"Source Tenant: {sourceTenantName}\n");
            AppendToDetailsRichTextBlock($"Destination Tenant: {destinationTenantName}\n");


            // Log what content is being imported
            LogContentToImport();

            // Log which group(s) are being assigned
            LogGroupsToBeAssigned();

            // Log which filter(s) are being applied
            LogFiltersToBeApplied();
            AppendToDetailsRichTextBlock("Import process finished.\n");

            // Perform the import process

           

        }




        /// BUTTON HANDLERS ///
        /// Buttons should be defined in the XAML file and linked to these methods.
        /// Buttons should call other methods to perform specific actions.
        /// Buttons should not directly perform actions themselves.
        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            await MainImportProcess();
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

           
        }
    }
}