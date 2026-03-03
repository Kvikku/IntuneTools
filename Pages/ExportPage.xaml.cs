using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace IntuneTools.Pages
{
    /// <summary>
    /// Page for exporting Intune content inventory to JSON files.
    /// </summary>
    public sealed partial class ExportPage : BaseDataOperationPage
    {
        #region Fields

        /// <summary>
        /// Content types supported by ExportPage (all content types).
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

        /// <summary>
        /// JSON serializer options for export.
        /// </summary>
        private static readonly JsonSerializerOptions ExportJsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        #endregion

        #region Constructor & Configuration

        public ExportPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(ExportDataGrid);
            LogConsole.ItemsSource = LogEntries;
        }

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before using export features.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton", "ClearSelectedButton",
            "ClearAllButton", "ExportButton", "ExportDataGrid", "ClearLogButton"
        };

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

        // Convenience method for logging - calls base class AppendToLog
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
                ExportDataGrid.ItemsSource = ContentList;
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
                ExportDataGrid.ItemsSource = ContentList;
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

        /// <summary>
        /// Exports the staged content to a JSON file.
        /// </summary>
        private async Task ExportContentToJsonAsync()
        {
            if (ContentList.Count == 0)
            {
                AppendToDetailsRichTextBlock("No content to export. Use 'List All' or 'Search' to load content first.");
                return;
            }

            try
            {
                var savePicker = new FileSavePicker();

                // Get the window handle for the picker
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("JSON File", new List<string> { ".json" });
                savePicker.SuggestedFileName = $"IntuneExport_{DateTime.Now:yyyyMMdd_HHmmss}";

                var file = await savePicker.PickSaveFileAsync();

                if (file == null)
                {
                    AppendToDetailsRichTextBlock("Export cancelled by user.");
                    return;
                }

                ShowOperationProgress("Exporting content to JSON...");

                var exportData = new ExportDocument
                {
                    ExportDate = DateTime.UtcNow.ToString("o"),
                    TenantName = sourceTenantName,
                    TenantId = sourceTenantID,
                    TotalItems = ContentList.Count,
                    Items = ContentList.Select(c => new ExportItem
                    {
                        Name = c.ContentName,
                        Type = c.ContentType,
                        Platform = c.ContentPlatform,
                        Id = c.ContentId,
                        Description = c.ContentDescription
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(exportData, ExportJsonOptions);

                // Write using CachedFileManager to respect the picker's deferred access
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                await Windows.Storage.FileIO.WriteTextAsync(file, json);
                var status = await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);

                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    ShowOperationSuccess($"Successfully exported {ContentList.Count} items to {file.Name}");
                    AppendToDetailsRichTextBlock($"Exported {ContentList.Count} items to: {file.Path}");
                    LogToFunctionFile(appFunction.Main, $"Exported {ContentList.Count} items to JSON: {file.Path}");
                }
                else
                {
                    ShowOperationError("Export could not be completed. The file may not have been saved.");
                    AppendToDetailsRichTextBlock("Export could not be completed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                ShowOperationError($"Export failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error during export: {ex.Message}");
                LogToFunctionFile(appFunction.Main, $"Error during JSON export: {ex.Message}", LogLevels.Error);
            }
        }

        #endregion

        #region Export Data Model

        /// <summary>
        /// Root document for JSON export.
        /// </summary>
        private sealed class ExportDocument
        {
            public string? ExportDate { get; set; }
            public string? TenantName { get; set; }
            public string? TenantId { get; set; }
            public int TotalItems { get; set; }
            public List<ExportItem> Items { get; set; } = new();
        }

        /// <summary>
        /// Individual item in the JSON export.
        /// </summary>
        private sealed class ExportItem
        {
            public string? Name { get; set; }
            public string? Type { get; set; }
            public string? Platform { get; set; }
            public string? Id { get; set; }
            public string? Description { get; set; }
        }

        #endregion

        #region Event Handlers

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            ExportDataGrid.ItemsSource = null;
            ExportDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock("All items cleared from the list.");
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ExportDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }
            ExportDataGrid.ItemsSource = null;
            ExportDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        private void ExportDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportContentToJsonAsync();
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
