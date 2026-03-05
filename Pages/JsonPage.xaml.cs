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
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.IntuneHelperClasses.SettingsCatalogHelper;

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
        /// Cache of full policy data keyed by source content ID.
        /// Populated during export (from Graph) or import (from JSON file).
        /// Used when importing to a destination tenant.
        /// </summary>
        private readonly Dictionary<string, JsonElement> _policyDataCache = new();

        /// <summary>
        /// Content types supported by JsonPage.
        /// </summary>
        private static readonly string[] SupportedContentTypes = new[]
        {
            ContentTypes.SettingsCatalog,
            ContentTypes.DeviceCompliancePolicy,
        };

        /// <summary>
        /// Maps content type constants to their JSON file names.
        /// </summary>
        private static readonly Dictionary<string, string> ContentTypeFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { ContentTypes.SettingsCatalog, "settingscatalog.json" },
            { ContentTypes.DeviceCompliancePolicy, "devicecompliance.json" },
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
            ImportToTenantButton.IsEnabled = !string.IsNullOrEmpty(Variables.destinationTenantName);
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
        /// Fetches full policy data from the source tenant for supported content types.
        /// </summary>
        private async Task ExportToJsonAsync()
        {
            if (ContentList.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items to export. Load items first using 'List All' or 'Search'.");
                return;
            }

            // Warn if source tenant is not authenticated — export will lack policy data
            if (sourceGraphServiceClient == null)
            {
                var noAuthDialog = new ContentDialog
                {
                    Title = "No Source Tenant Authenticated",
                    Content = "Without an authenticated source tenant, the exported JSON will only contain item metadata (names, types, IDs) and will NOT include full policy data.\n\nThe resulting files cannot be used to import policies into another tenant. To include full policy data, authenticate with a source tenant first.",
                    PrimaryButtonText = "Export Anyway",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                if (await noAuthDialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    AppendToDetailsRichTextBlock("Export cancelled.");
                    return;
                }
            }

            // Group staged items by content type
            var itemsByType = ContentList
                .Where(c => ContentTypeFileNames.ContainsKey(c.ContentType ?? ""))
                .GroupBy(c => c.ContentType!, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unsupportedItems = ContentList.Where(c => !ContentTypeFileNames.ContainsKey(c.ContentType ?? "")).ToList();

            var typeList = string.Join("\n", itemsByType.Select(g => $"  • {g.Key}: {g.Count()} item(s) → {ContentTypeFileNames[g.Key]}"));
            var unsupportedNote = unsupportedItems.Count > 0
                ? $"\n\n{unsupportedItems.Count} item(s) of unsupported type(s) will be skipped."
                : "";

            // Confirm export
            var confirmDialog = new ContentDialog
            {
                Title = "Export to Folder",
                Content = $"This will fetch full policy data and save one JSON file per content type to the selected folder:\n\n{typeList}{unsupportedNote}\n\nExisting files in the folder with the same names will be overwritten.",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                AppendToDetailsRichTextBlock("Export cancelled.");
                return;
            }

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
            {
                AppendToDetailsRichTextBlock("Export cancelled.");
                return;
            }

            try
            {
                int totalItems = itemsByType.Sum(g => g.Count());
                int currentItem = 0;
                int totalWithData = 0;
                int filesWritten = 0;
                ShowOperationProgress("Exporting to folder...", 0, totalItems);

                foreach (var group in itemsByType)
                {
                    var contentType = group.Key;
                    var fileName = ContentTypeFileNames[contentType];
                    var items = new List<JsonExportItem>();

                    foreach (var c in group)
                    {
                        currentItem++;
                        ShowOperationProgress($"Exporting '{c.ContentName}'...", currentItem, totalItems);

                        JsonElement? policyData = null;

                        if (sourceGraphServiceClient != null && !string.IsNullOrEmpty(c.ContentId))
                        {
                            if (string.Equals(contentType, ContentTypes.SettingsCatalog, StringComparison.OrdinalIgnoreCase))
                            {
                                policyData = await ExportSettingsCatalogPolicyDataAsync(sourceGraphServiceClient, c.ContentId);
                            }
                            else if (string.Equals(contentType, ContentTypes.DeviceCompliancePolicy, StringComparison.OrdinalIgnoreCase))
                            {
                                policyData = await ExportDeviceCompliancePolicyDataAsync(sourceGraphServiceClient, c.ContentId);
                            }
                        }

                        if (policyData.HasValue) totalWithData++;

                        items.Add(new JsonExportItem
                        {
                            Name = c.ContentName,
                            Type = c.ContentType,
                            Platform = c.ContentPlatform,
                            Id = c.ContentId,
                            Description = c.ContentDescription,
                            PolicyData = policyData
                        });
                    }

                    var document = new JsonExportDocument
                    {
                        ExportedAt = DateTime.UtcNow.ToString("o"),
                        TenantName = string.IsNullOrEmpty(sourceTenantName) ? "Unknown" : sourceTenantName,
                        Items = items
                    };

                    var json = JsonSerializer.Serialize(document, ExportSerializerOptions);
                    var filePath = Path.Combine(folder.Path, fileName);
                    await File.WriteAllTextAsync(filePath, json);
                    filesWritten++;
                    AppendToDetailsRichTextBlock($"Wrote {items.Count} item(s) to '{fileName}'.");
                }

                if (unsupportedItems.Count > 0)
                {
                    AppendToDetailsRichTextBlock($"Skipped {unsupportedItems.Count} item(s) of unsupported content type(s).");
                }

                ShowOperationSuccess($"Exported {totalItems} items ({totalWithData} with full data) across {filesWritten} file(s) to '{folder.Name}'");
                AppendToDetailsRichTextBlock($"Export complete. {filesWritten} file(s) written to '{folder.Path}'.");
            }
            catch (Exception ex)
            {
                ShowOperationError($"Export failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error exporting to folder: {ex.Message}");
            }
        }

        #endregion

        #region JSON Import

        /// <summary>
        /// Imports content from a JSON file into the staging area.
        /// Preserves full policy data in the cache for later import to a tenant.
        /// </summary>
        private async Task ImportFromJsonAsync()
        {
            // Warn if staging area has items
            if (ContentList.Count > 0)
            {
                var replaceDialog = new ContentDialog
                {
                    Title = "Replace Staging Area?",
                    Content = $"The staging area currently contains {ContentList.Count} item(s). Importing from a folder will replace all current items.\n\nDo you want to continue?",
                    PrimaryButtonText = "Replace",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                if (await replaceDialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    AppendToDetailsRichTextBlock("Import from folder cancelled.");
                    return;
                }
            }

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
            {
                AppendToDetailsRichTextBlock("Import cancelled.");
                return;
            }

            try
            {
                ShowOperationProgress("Importing from folder...");

                ContentList.Clear();
                _policyDataCache.Clear();

                int totalItems = 0;
                int totalWithData = 0;
                int filesRead = 0;

                // Build reverse lookup: filename → content type
                var fileNameToContentType = ContentTypeFileNames
                    .ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in ContentTypeFileNames)
                {
                    var filePath = Path.Combine(folder.Path, kvp.Value);
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    var json = await File.ReadAllTextAsync(filePath);
                    var document = JsonSerializer.Deserialize<JsonExportDocument>(json, ImportSerializerOptions);

                    if (document?.Items == null || document.Items.Count == 0)
                    {
                        AppendToDetailsRichTextBlock($"'{kvp.Value}' contains no items, skipping.");
                        continue;
                    }

                    filesRead++;

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

                        if (item.PolicyData.HasValue && !string.IsNullOrEmpty(item.Id))
                        {
                            _policyDataCache[item.Id] = item.PolicyData.Value;
                            totalWithData++;
                        }

                        totalItems++;
                    }

                    var tenantInfo = !string.IsNullOrEmpty(document.TenantName) ? $" (tenant: {document.TenantName})" : "";
                    AppendToDetailsRichTextBlock($"Loaded {document.Items.Count} item(s) from '{kvp.Value}'{tenantInfo}.");
                }

                if (totalItems == 0)
                {
                    ShowOperationError("No supported JSON files found in the selected folder.");
                    AppendToDetailsRichTextBlock($"No files matching known content types found in '{folder.Path}'. Expected files: {string.Join(", ", ContentTypeFileNames.Values)}");
                    return;
                }

                JsonDataGrid.ItemsSource = ContentList;

                ShowOperationSuccess($"Imported {totalItems} items from {filesRead} file(s) in '{folder.Name}'");
                if (totalWithData > 0)
                {
                    AppendToDetailsRichTextBlock($"{totalWithData} item(s) have full policy data and can be imported to a destination tenant.");
                }
                else
                {
                    AppendToDetailsRichTextBlock("No items contain full policy data. Use 'Export to Folder' from a source tenant to include importable data.");
                }
            }
            catch (JsonException ex)
            {
                ShowOperationError("Invalid JSON format.");
                AppendToDetailsRichTextBlock($"Error parsing JSON file: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowOperationError($"Import failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error importing from folder: {ex.Message}");
            }
        }

        #endregion

        #region Import to Tenant

        /// <summary>
        /// Imports all staged items that have full policy data into the destination tenant.
        /// </summary>
        private async Task ImportToTenantAsync()
        {
            if (ContentList.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items to import. Load items from a JSON file first.");
                return;
            }

            // Filter to items that have cached policy data
            var importableItems = ContentList
                .Where(c => !string.IsNullOrEmpty(c.ContentId) && _policyDataCache.ContainsKey(c.ContentId!))
                .ToList();

            if (importableItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items have full policy data for import. Export from a source tenant first to include policy data.");
                return;
            }

            if (destinationGraphServiceClient == null)
            {
                AppendToDetailsRichTextBlock("No destination tenant authenticated. Please authenticate with a destination tenant first.");
                return;
            }

            // Confirm with user
            var dialog = new ContentDialog
            {
                Title = "Import to Tenant",
                Content = $"You are about to import {importableItems.Count} item(s) to the destination tenant ({destinationTenantName}). Continue?",
                PrimaryButtonText = "Import",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var dialogResult = await dialog.ShowAsync();
            if (dialogResult != ContentDialogResult.Primary)
            {
                AppendToDetailsRichTextBlock("Import to tenant cancelled.");
                return;
            }

            int total = importableItems.Count;
            int current = 0;
            int successCount = 0;
            int errorCount = 0;

            ShowOperationProgress("Importing to tenant...", 0, total);
            AppendToDetailsRichTextBlock($"Starting import of {total} item(s) to {destinationTenantName}...");

            foreach (var item in importableItems)
            {
                current++;
                ShowOperationProgress($"Importing '{item.ContentName}'...", current, total);

                try
                {
                    var policyData = _policyDataCache[item.ContentId!];

                    string? importedName = null;

                    if (string.Equals(item.ContentType, ContentTypes.SettingsCatalog, StringComparison.OrdinalIgnoreCase))
                    {
                        importedName = await ImportSettingsCatalogFromJsonDataAsync(destinationGraphServiceClient, policyData);
                    }
                    else if (string.Equals(item.ContentType, ContentTypes.DeviceCompliancePolicy, StringComparison.OrdinalIgnoreCase))
                    {
                        importedName = await ImportDeviceComplianceFromJsonDataAsync(destinationGraphServiceClient, policyData);
                    }
                    else
                    {
                        AppendToDetailsRichTextBlock($"Skipped '{item.ContentName}' — content type '{item.ContentType}' not yet supported for JSON import.");
                        continue;
                    }

                    if (importedName != null)
                    {
                        AppendToDetailsRichTextBlock($"Imported: {importedName}");
                        successCount++;
                    }
                    else
                    {
                        AppendToDetailsRichTextBlock($"Failed to import: {item.ContentName}");
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    AppendToDetailsRichTextBlock($"Error importing '{item.ContentName}': {ex.Message}");
                    errorCount++;
                }
            }

            if (errorCount == 0)
            {
                ShowOperationSuccess($"Import completed: {successCount} item(s) imported successfully");
            }
            else
            {
                ShowOperationError($"Import completed with errors: {successCount} succeeded, {errorCount} failed");
            }

            AppendToDetailsRichTextBlock("Import to tenant finished.");
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

        private async void ImportToTenantButton_Click(object sender, RoutedEventArgs e)
        {
            await ImportToTenantAsync();
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
