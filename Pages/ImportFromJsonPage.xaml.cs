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
    /// Page for importing Intune content inventory from a previously exported JSON file.
    /// </summary>
    public sealed partial class ImportFromJsonPage : BaseDataOperationPage
    {
        #region Fields

        /// <summary>
        /// JSON serializer options for import (must match export options).
        /// </summary>
        private static readonly JsonSerializerOptions ImportJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Constructor & Configuration

        public ImportFromJsonPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(ImportFromJsonDataGrid);
            LogConsole.ItemsSource = LogEntries;
        }

        /// <summary>
        /// This page does not require tenant authentication since it loads from a local file.
        /// </summary>
        protected override void ValidateAuthenticationState()
        {
            // No authentication needed for importing from a local JSON file.
            // All controls remain enabled.
        }

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "ImportButton", "ClearSelectedButton", "ClearAllButton",
            "ImportFromJsonDataGrid", "ClearLogButton"
        };

        #endregion

        #region Core Operations

        /// <summary>
        /// Opens a file picker and loads a JSON export file.
        /// </summary>
        private async Task ImportFromJsonAsync()
        {
            try
            {
                var openPicker = new FileOpenPicker();

                // Get the window handle for the picker
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                openPicker.FileTypeFilter.Add(".json");

                var file = await openPicker.PickSingleFileAsync();

                if (file == null)
                {
                    AppendToLog("Import cancelled by user.");
                    return;
                }

                ShowLoading("Reading JSON file...");
                AppendToLog($"Loading file: {file.Name}");

                var json = await Windows.Storage.FileIO.ReadTextAsync(file);

                if (string.IsNullOrWhiteSpace(json))
                {
                    HideLoading();
                    ShowOperationError("The selected file is empty.");
                    AppendToLog("Error: The selected file is empty.");
                    return;
                }

                var document = JsonSerializer.Deserialize<JsonExportDocument>(json, ImportJsonOptions);

                if (document == null)
                {
                    HideLoading();
                    ShowOperationError("Failed to parse the JSON file. The file format may be invalid.");
                    AppendToLog("Error: Failed to parse the JSON file.");
                    return;
                }

                if (document.Items == null || document.Items.Count == 0)
                {
                    HideLoading();
                    ShowOperationError("The JSON file contains no items.");
                    AppendToLog("The JSON file contains no items.");
                    return;
                }

                // Log metadata from the export
                if (!string.IsNullOrWhiteSpace(document.TenantName))
                {
                    AppendToLog($"Source tenant: {document.TenantName}");
                }
                if (!string.IsNullOrWhiteSpace(document.ExportDate))
                {
                    AppendToLog($"Export date: {document.ExportDate}");
                }

                // Convert items to CustomContentInfo and populate ContentList
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

                ImportFromJsonDataGrid.ItemsSource = ContentList;

                HideLoading();
                ShowOperationSuccess($"Successfully loaded {ContentList.Count} items from {file.Name}");
                AppendToLog($"Loaded {ContentList.Count} items from: {file.Path}");
                LogToFunctionFile(appFunction.Main, $"Imported {ContentList.Count} items from JSON: {file.Path}");
            }
            catch (JsonException ex)
            {
                HideLoading();
                ShowOperationError($"Invalid JSON format: {ex.Message}");
                AppendToLog($"JSON parsing error: {ex.Message}");
                LogToFunctionFile(appFunction.Main, $"Error parsing JSON import file: {ex.Message}", LogLevels.Error);
            }
            catch (Exception ex)
            {
                HideLoading();
                ShowOperationError($"Import failed: {ex.Message}");
                AppendToLog($"Error during import: {ex.Message}");
                LogToFunctionFile(appFunction.Main, $"Error during JSON import: {ex.Message}", LogLevels.Error);
            }
        }

        #endregion

        #region Event Handlers

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            await ImportFromJsonAsync();
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            ImportFromJsonDataGrid.ItemsSource = null;
            ImportFromJsonDataGrid.ItemsSource = ContentList;
            AppendToLog("All items cleared from the list.");
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ImportFromJsonDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }
            ImportFromJsonDataGrid.ItemsSource = null;
            ImportFromJsonDataGrid.ItemsSource = ContentList;
            AppendToLog($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        private void ImportFromJsonDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        #endregion
    }
}
