using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntuneTools.Pages
{
    /// <summary>
    /// Page for exporting content to JSON files and importing content from JSON files.
    /// </summary>
    public sealed partial class JsonPage : BaseDataOperationPage
    {
        #region Fields

        private static readonly JsonSerializerOptions ExportSerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions ImportSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Content types supported by JsonPage.
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
            ContentTypes.Application,
        };

        #endregion

        #region Constructor & Configuration

        public JsonPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(JsonDataGrid);
            LogConsole.ItemsSource = LogEntries;
        }

        protected override string UnauthenticatedMessage => "Authenticate with a tenant to load items, or use 'Import from JSON' to load from a file.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton",
            "ClearSelectedButton", "ClearAllButton", "ClearLogButton"
        };

        /// <summary>
        /// Allows the page to load without authentication so that JSON import still works.
        /// Export-to-JSON from the staging area and import-from-JSON do not require a tenant connection.
        /// </summary>
        protected override void ValidateAuthenticationState()
        {
            base.ValidateAuthenticationState();

            // Always keep the JSON action buttons and import button enabled regardless of auth
            ImportButton.IsEnabled = true;
            ExportButton.IsEnabled = true;
        }

        #endregion

        #region Base Class Overrides

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

        private void AppendToDetailsRichTextBlock(string text) => AppendToLog(text);

        #endregion

        #region Core Operations

        /// <summary>
        /// Loads all content types from Microsoft Graph.
        /// </summary>
        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            AppendToDetailsRichTextBlock("Starting to load all content. This could take a while...");
            try
            {
                ContentList.Clear();
                await LoadContentTypesAsync(graphServiceClient, SupportedContentTypes, AppendToDetailsRichTextBlock);
                JsonDataGrid.ItemsSource = ContentList;
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

        /// <summary>
        /// Searches for content matching the specified query.
        /// </summary>
        private async Task SearchOrchestrator(GraphServiceClient graphServiceClient, string searchQuery)
        {
            ShowLoading("Searching content in Microsoft Graph...");
            AppendToDetailsRichTextBlock($"Searching for content matching '{searchQuery}'. This may take a while...");
            try
            {
                ContentList.Clear();
                await SearchContentTypesAsync(graphServiceClient, searchQuery, SupportedContentTypes, AppendToDetailsRichTextBlock);
                JsonDataGrid.ItemsSource = ContentList;
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

        #endregion

        #region JSON Export

        /// <summary>
        /// Exports the current staging area content to a JSON file.
        /// </summary>
        private async Task ExportToJsonAsync()
        {
            if (ContentList.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items to export. Load items first using 'List All', 'Search', or 'Import from JSON'.");
                return;
            }

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("JSON File", new List<string> { ".json" });
            savePicker.SuggestedFileName = $"IntuneExport_{DateTime.Now:yyyyMMdd_HHmmss}";

            // Initialize the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file == null)
            {
                AppendToDetailsRichTextBlock("Export cancelled.");
                return;
            }

            try
            {
                ShowOperationProgress("Exporting to JSON...");

                var document = new JsonExportDocument
                {
                    ExportedAt = DateTime.UtcNow.ToString("o"),
                    TenantName = string.IsNullOrEmpty(sourceTenantName) ? "Unknown" : sourceTenantName,
                    Items = ContentList.Select(c => new JsonExportItem
                    {
                        Name = c.ContentName,
                        Type = c.ContentType,
                        Platform = c.ContentPlatform,
                        Id = c.ContentId,
                        Description = c.ContentDescription
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(document, ExportSerializerOptions);
                await File.WriteAllTextAsync(file.Path, json);

                ShowOperationSuccess($"Exported {document.Items.Count} items to {file.Name}");
                AppendToDetailsRichTextBlock($"Successfully exported {document.Items.Count} item(s) to '{file.Path}'.");
            }
            catch (Exception ex)
            {
                ShowOperationError($"Export failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error exporting to JSON: {ex.Message}");
            }
        }

        #endregion

        #region JSON Import

        /// <summary>
        /// Imports content from a JSON file into the staging area.
        /// </summary>
        private async Task ImportFromJsonAsync()
        {
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".json");

            // Initialize the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

            var file = await openPicker.PickSingleFileAsync();
            if (file == null)
            {
                AppendToDetailsRichTextBlock("Import cancelled.");
                return;
            }

            try
            {
                ShowOperationProgress("Importing from JSON...");

                var json = await File.ReadAllTextAsync(file.Path);
                var document = JsonSerializer.Deserialize<JsonExportDocument>(json, ImportSerializerOptions);

                if (document?.Items == null || document.Items.Count == 0)
                {
                    ShowOperationError("The JSON file contains no items.");
                    AppendToDetailsRichTextBlock("The selected JSON file contains no items to import.");
                    return;
                }

                ContentList.Clear();
                foreach (var item in document.Items)
                {
                    ContentList.Add(new CustomContentInfo
                    {
                        ContentName = item.Name,
                        ContentType = item.Type,
                        ContentPlatform = item.Platform,
                        ContentId = item.Id,
                        ContentDescription = item.Description
                    });
                }

                JsonDataGrid.ItemsSource = ContentList;

                var tenantInfo = !string.IsNullOrEmpty(document.TenantName) ? $" (from tenant: {document.TenantName})" : "";
                var exportDate = !string.IsNullOrEmpty(document.ExportedAt) ? $" exported at {document.ExportedAt}" : "";
                ShowOperationSuccess($"Imported {document.Items.Count} items from {file.Name}");
                AppendToDetailsRichTextBlock($"Successfully imported {document.Items.Count} item(s) from '{file.Name}'{tenantInfo}{exportDate}.");
            }
            catch (JsonException ex)
            {
                ShowOperationError("Invalid JSON format.");
                AppendToDetailsRichTextBlock($"Error parsing JSON file: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowOperationError($"Import failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error importing from JSON: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            JsonDataGrid.ItemsSource = null;
            JsonDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock("All items cleared from the list.");
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = JsonDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }
            JsonDataGrid.ItemsSource = null;
            JsonDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        private void JsonDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportToJsonAsync();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            await ImportFromJsonAsync();
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = InputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                AppendToDetailsRichTextBlock("Please enter a search query.");
                return;
            }
            await SearchOrchestrator(sourceGraphServiceClient, searchQuery);
        }

        #endregion
    }
}
