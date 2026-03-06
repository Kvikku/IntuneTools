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
using static IntuneTools.Graph.EntraHelperClasses.ConditionalAccessHelper;
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// Page for cleaning up (deleting) Intune content.
    /// </summary>
    public sealed partial class CleanupPage : BaseDataOperationPage
    {
        #region Fields & Types

        // Progress tracking for delete operations
        private int _deleteTotal;
        private int _deleteCurrent;
        private int _deleteSuccessCount;
        private int _deleteErrorCount;

        // Content type filter support
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        /// <summary>
        /// Maps checkbox names to ContentTypes constants for registry lookup.
        /// </summary>
        private static readonly Dictionary<string, string> CheckboxToContentType = new()
        {
            ["SettingsCatalog"] = ContentTypes.SettingsCatalog,
            ["DeviceCompliance"] = ContentTypes.DeviceCompliancePolicy,
            ["DeviceConfiguration"] = ContentTypes.DeviceConfigurationPolicy,
            ["AppleBYODEnrollmentProfile"] = ContentTypes.AppleBYODEnrollmentProfile,
            ["macOSShellScript"] = ContentTypes.MacOSShellScript,
            ["PowerShellScript"] = ContentTypes.PowerShellScript,
            ["ProactiveRemediation"] = ContentTypes.ProactiveRemediation,
            ["WindowsAutopilot"] = ContentTypes.WindowsAutoPilotProfile,
            ["WindowsDriverUpdate"] = ContentTypes.WindowsDriverUpdate,
            ["WindowsFeatureUpdate"] = ContentTypes.WindowsFeatureUpdate,
            ["WindowsQualityUpdatePolicy"] = ContentTypes.WindowsQualityUpdatePolicy,
            ["WindowsQualityUpdateProfile"] = ContentTypes.WindowsQualityUpdateProfile,
            ["Filters"] = ContentTypes.AssignmentFilter,
            ["EntraGroups"] = ContentTypes.EntraGroup,
            ["ConditionalAccess"] = ContentTypes.ConditionalAccessPolicy,
        };

        /// <summary>
        /// Defines a delete operation for a specific content type.
        /// </summary>
        /// <param name="TypeKey">Content type identifier (e.g., ContentTypes.SettingsCatalog).</param>
        /// <param name="DisplayName">Human-readable name for logging.</param>
        /// <param name="DeleteAsync">Async function that deletes a single item by ID. Returns true if deleted, false if skipped.</param>
        private record DeleteTypeDefinition(
            string TypeKey,
            string DisplayName,
            Func<string, Task<bool>> DeleteAsync);

        /// <summary>
        /// Content types supported by CleanupPage (excludes Application since delete is not supported).
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
            ContentTypes.ConditionalAccessPolicy,
        };

        #endregion

        #region Constructor & Configuration

        public CleanupPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(CleanupDataGrid);
            LogConsole.ItemsSource = LogEntries;
        }

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before using cleanup features.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton", "ClearSelectedButton",
            "ClearAllButton", "DeleteButton", "CleanupDataGrid", "ClearLogButton",
            "ContentTypesButton"
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
        /// Main entry point for delete operations. Iterates through all content types and deletes items.
        /// </summary>
        private async Task DeleteContent()
        {
            _deleteTotal = ContentList.Count;
            _deleteCurrent = 0;
            _deleteSuccessCount = 0;
            _deleteErrorCount = 0;

            if (_deleteTotal == 0)
            {
                AppendToDetailsRichTextBlock("No content to delete.");
                return;
            }

            ShowOperationProgress("Preparing to delete items...", 0, _deleteTotal);

            foreach (var definition in GetDeleteTypeRegistry())
            {
                var ids = GetContentIdsByType(definition.TypeKey);
                if (ids.Count > 0)
                {
                    await DeleteItemsAsync(ids, definition);
                }
            }

            // Show final status
            if (_deleteErrorCount == 0)
            {
                ShowOperationSuccess($"Successfully deleted {_deleteSuccessCount} items");
            }
            else
            {
                ShowOperationError($"Completed with {_deleteErrorCount} error(s). {_deleteSuccessCount} items deleted successfully.");
            }

            AppendToDetailsRichTextBlock("Content deletion completed.");
        }

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
                var selectedTypes = GetSelectedContentTypes().ToList();
                IEnumerable<string> typesToLoad = selectedTypes.Count > 0 ? selectedTypes : SupportedContentTypes;
                await LoadContentTypesAsync(graphServiceClient, typesToLoad, AppendToDetailsRichTextBlock);
                CleanupDataGrid.ItemsSource = ContentList;
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
                var selectedTypes = GetSelectedContentTypes().ToList();
                IEnumerable<string> typesToLoad = selectedTypes.Count > 0 ? selectedTypes : SupportedContentTypes;
                await SearchContentTypesAsync(graphServiceClient, searchQuery, typesToLoad, AppendToDetailsRichTextBlock);
                CleanupDataGrid.ItemsSource = ContentList;
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

        #region Delete Logic

        /// <summary>
        /// Generic helper to delete items, reducing code duplication across all content types.
        /// </summary>
        private async Task DeleteItemsAsync(List<string> ids, DeleteTypeDefinition definition)
        {
            foreach (var id in ids)
            {
                _deleteCurrent++;
                ShowOperationProgress($"Deleting {definition.DisplayName}", _deleteCurrent, _deleteTotal);
                try
                {
                    var deleted = await definition.DeleteAsync(id);
                    if (deleted)
                    {
                        LogToFunctionFile(appFunction.Main, $"Deleted {definition.DisplayName} with ID: {id}");
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    // If not deleted (skipped), don't count as success or error
                }
                catch (Exception ex)
                {
                    _deleteErrorCount++;
                    LogToFunctionFile(appFunction.Main, $"Error deleting {definition.DisplayName} {id}: {ex.Message}", LogLevels.Error);
                }
            }

            if (ids.Count > 0)
            {
                AppendToDetailsRichTextBlock($"Processed {ids.Count} {definition.DisplayName}(s).");
            }
        }

        /// <summary>
        /// Handles AutoPilot profile deletion with special logic for assignment checking.
        /// </summary>
        private async Task<bool> HandleAutoPilotProfileDeletion(string id)
        {
            var isAssigned = await CheckIfAutoPilotProfileHasAssignments(sourceGraphServiceClient, id);

            if (isAssigned)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete AutoPilot Profile",
                    Content = $"The Windows AutoPilot profile with ID: {id} is assigned to devices. Do you want to delete the assignments before deleting the profile?",
                    PrimaryButtonText = "Delete Assignments",
                    SecondaryButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = this.XamlRoot
                };
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await DeleteWindowsAutoPilotProfileAssignments(sourceGraphServiceClient, id);
                    LogToFunctionFile(appFunction.Main, $"Deleted assignments for Windows AutoPilot profile with ID: {id}");
                    await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                    return true;
                }
                else
                {
                    LogToFunctionFile(appFunction.Main, $"Skipped deletion of Windows AutoPilot profile with ID: {id} as it is assigned to devices.", LogLevels.Warning);
                    return false;
                }
            }
            else
            {
                await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                return true;
            }
        }

        /// <summary>
        /// Returns the delete registry with all content types and their delete operations.
        /// </summary>
        private IEnumerable<DeleteTypeDefinition> GetDeleteTypeRegistry() =>
        [
            new(ContentTypes.SettingsCatalog, "Settings Catalog",
                async id => { await DeleteSettingsCatalog(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.DeviceCompliancePolicy, "Device Compliance Policy",
                async id => { await DeleteDeviceCompliancePolicy(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.DeviceConfigurationPolicy, "Device Configuration Policy",
                async id => { await DeleteDeviceConfigurationPolicy(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.AppleBYODEnrollmentProfile, "Apple BYOD Enrollment Profile",
                async id => { await DeleteAppleBYODEnrollmentProfile(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.AssignmentFilter, "Assignment Filter",
                async id => { await DeleteAssignmentFilter(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.EntraGroup, "Entra Group",
                async id => { await DeleteSecurityGroup(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.PowerShellScript, "PowerShell Script",
                async id => { await DeletePowerShellScript(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.ProactiveRemediation, "Proactive Remediation",
                async id => { await DeleteProactiveRemediationScript(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.MacOSShellScript, "macOS Shell Script",
                async id => { await DeleteMacosShellScript(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.WindowsAutoPilotProfile, "Windows AutoPilot Profile",
                HandleAutoPilotProfileDeletion),

            new(ContentTypes.WindowsDriverUpdate, "Windows Driver Update",
                async id => { await DeleteDriverProfile(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.WindowsFeatureUpdate, "Windows Feature Update",
                async id => { await DeleteWindowsFeatureUpdateProfile(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.WindowsQualityUpdatePolicy, "Windows Quality Update Policy",
                async id => { await DeleteWindowsQualityUpdatePolicy(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.WindowsQualityUpdateProfile, "Windows Quality Update Profile",
                async id => { await DeleteWindowsQualityUpdateProfile(sourceGraphServiceClient, id); return true; }),

            new(ContentTypes.ConditionalAccessPolicy, "Conditional Access Policy",
                async id => await DeleteConditionalAccessPolicy(sourceGraphServiceClient, id)),
        ];

        /// <summary>
        /// Exports the current ContentList to JSON files so the user can back up before deleting.
        /// Reuses the export registries from JsonPage.
        /// </summary>
        private async Task<bool> BackupContentToJsonAsync()
        {
            var itemsByType = ContentList
                .Where(c => JsonPage.ContentTypeFileNames.ContainsKey(c.ContentType ?? ""))
                .GroupBy(c => c.ContentType!, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unsupportedCount = ContentList.Count(c => !JsonPage.ContentTypeFileNames.ContainsKey(c.ContentType ?? ""));

            if (itemsByType.Count == 0)
            {
                AppendToDetailsRichTextBlock("No exportable content types to back up.");
                return false;
            }

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
            {
                AppendToDetailsRichTextBlock("Backup cancelled.");
                return false;
            }

            try
            {
                int totalItems = itemsByType.Sum(g => g.Count());
                int currentItem = 0;
                int filesWritten = 0;
                ShowOperationProgress("Backing up content...", 0, totalItems);

                foreach (var group in itemsByType)
                {
                    var contentType = group.Key;
                    var fileName = JsonPage.ContentTypeFileNames[contentType];
                    var items = new List<JsonExportItem>();

                    foreach (var c in group)
                    {
                        currentItem++;
                        ShowOperationProgress($"Backing up '{c.ContentName}'...", currentItem, totalItems);

                        JsonElement? policyData = null;
                        if (sourceGraphServiceClient != null && !string.IsNullOrEmpty(c.ContentId)
                            && JsonPage.JsonContentTypeOperations.TryGetValue(contentType, out var ops))
                        {
                            policyData = await ops.Export(sourceGraphServiceClient, c.ContentId);
                        }

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
                        TenantName = string.IsNullOrEmpty(Variables.sourceTenantName) ? "Unknown" : Variables.sourceTenantName,
                        Items = items
                    };

                    var json = JsonSerializer.Serialize(document, JsonPage.ExportSerializerOptions);
                    var filePath = Path.Combine(folder.Path, fileName);
                    await File.WriteAllTextAsync(filePath, json);
                    filesWritten++;
                }

                var msg = $"Backup complete. {totalItems} item(s) exported across {filesWritten} file(s) to '{folder.Path}'.";
                if (unsupportedCount > 0)
                    msg += $" {unsupportedCount} item(s) of unsupported type(s) were skipped.";

                ShowOperationSuccess(msg);
                AppendToDetailsRichTextBlock(msg);
                return true;
            }
            catch (Exception ex)
            {
                ShowOperationError($"Backup failed: {ex.Message}");
                AppendToDetailsRichTextBlock($"Error during backup: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Content Type Filter

        private IEnumerable<string> GetSelectedContentTypes()
        {
            var checkedNames = GetCheckedOptionNames();
            foreach (var name in checkedNames)
            {
                if (CheckboxToContentType.TryGetValue(name, out var contentType))
                {
                    yield return contentType;
                }
            }
        }

        public List<string> GetCheckedOptionNames()
        {
            var checkedNames = new List<string>();
            foreach (var child in ContentTypesPanel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true)
                {
                    checkedNames.Add(cb.Name);
                }
            }
            return checkedNames;
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var checkbox in ContentTypesPanel.Children.OfType<CheckBox>())
            {
                checkbox.IsChecked = true;
            }
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            foreach (var child in ContentTypesPanel.Children)
            {
                if (child is CheckBox cb && cb.Name != "OptionsAllCheckBox")
                {
                    cb.IsChecked = false;
                }
            }
            _suppressOptionEvents = false;
        }

        private void SelectAll_Indeterminate(object sender, RoutedEventArgs e) { }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        private void Option_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        private void UpdateSelectAllCheckBox()
        {
            var optionCheckBoxes = ContentTypesPanel.Children.OfType<CheckBox>().Where(cb => cb.Name != "OptionsAllCheckBox").ToList();
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

        #endregion

        #region Event Handlers

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            CleanupDataGrid.ItemsSource = null;
            CleanupDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock("All items cleared from the list.");
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = CleanupDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToDetailsRichTextBlock("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }
            CleanupDataGrid.ItemsSource = null;
            CleanupDataGrid.ItemsSource = ContentList;
            AppendToDetailsRichTextBlock($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        private void CleanupDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var numberOfItems = ContentList.Count;

            // Bulk operation safeguard: warn when deleting 10 or more items
            if (numberOfItems >= 10)
            {
                var bulkWarning = new ContentDialog
                {
                    Title = "\u26A0 Large Bulk Delete",
                    Content = $"You are about to delete {numberOfItems} items. This is a large operation and cannot be undone. Are you sure you want to continue?",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var bulkResult = await bulkWarning.ShowAsync().AsTask();
                if (bulkResult != ContentDialogResult.Primary)
                {
                    AppendToDetailsRichTextBlock("Bulk delete cancelled by user.");
                    return;
                }
            }

            var dialog = new ContentDialog
            {
                Title = "Delete content?",
                Content = $"You are about to permanently delete {numberOfItems} item(s). This action cannot be undone.\n\n" +
                          "Have you taken a backup? You can back up these items to JSON right now using the button below.",
                PrimaryButtonText = "Delete",
                SecondaryButtonText = "Backup First",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync().AsTask();
            if (result == ContentDialogResult.Secondary)
            {
                var backupOk = await BackupContentToJsonAsync();
                if (backupOk)
                {
                    // Re-prompt to delete after successful backup
                    var postBackupDialog = new ContentDialog
                    {
                        Title = "Backup complete — proceed with delete?",
                        Content = $"Your backup was saved successfully. Do you want to delete the {numberOfItems} item(s) now?",
                        PrimaryButtonText = "Delete",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };
                    var postResult = await postBackupDialog.ShowAsync().AsTask();
                    if (postResult != ContentDialogResult.Primary) return;
                }
                else
                {
                    return;
                }
            }
            else if (result != ContentDialogResult.Primary)
            {
                return;
            }

            await DeleteContent();
            ContentList.Clear();
            AppendToDetailsRichTextBlock("Cleared the data grid.");
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


