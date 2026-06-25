using System.Collections.ObjectModel;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static IntuneTools.Graph.EntraHelperClasses.GroupHelperClass;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.ApplicationHelper;
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



namespace IntuneTools.Pages
{
    /// <summary>
    /// Page for cleaning up (deleting) Intune content.
    /// </summary>
    public sealed partial class CleanupPage : BaseDataOperationPage
    {
        #region Fields & Types

        // Duplicate detection results
        private readonly ObservableCollection<DuplicateContentInfo> DuplicateContentList = new();

        // Content type filter for duplicate scan
        private readonly HashSet<string> _selectedContentTypes = new(SupportedContentTypes);

        private static readonly (string TypeKey, string DisplayName)[] ContentTypeOptions =
        [
            (ContentTypes.SettingsCatalog,           "Settings Catalog"),
            (ContentTypes.DeviceCompliancePolicy,    "Device Compliance Policy"),
            (ContentTypes.DeviceConfigurationPolicy, "Device Configuration Policy"),
            (ContentTypes.AppleBYODEnrollmentProfile,"Apple BYOD Enrollment Profile"),
            (ContentTypes.AssignmentFilter,          "Assignment Filter"),
            (ContentTypes.EntraGroup,                "Entra Group"),
            (ContentTypes.PowerShellScript,          "PowerShell Script"),
            (ContentTypes.ProactiveRemediation,      "Proactive Remediation"),
            (ContentTypes.MacOSShellScript,          "macOS Shell Script"),
            (ContentTypes.WindowsAutoPilotProfile,   "Windows AutoPilot Profile"),
            (ContentTypes.WindowsDriverUpdate,       "Windows Driver Update"),
            (ContentTypes.WindowsFeatureUpdate,      "Windows Feature Update"),
            (ContentTypes.WindowsQualityUpdatePolicy,"Quality Update Policy"),
            (ContentTypes.WindowsQualityUpdateProfile,"Quality Update Profile"),
            (ContentTypes.Application,               "Application"),
        ];

        // Progress tracking for delete operations
        private int _deleteTotal;
        private int _deleteCurrent;
        private int _deleteSuccessCount;
        private int _deleteErrorCount;

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
        /// Content types supported by CleanupPage.
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

        public CleanupPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(CleanupDataGrid);
            RightClickMenu.AttachDataGridContextMenu(DuplicatesDataGrid);
            LogConsole.ItemsSource = LogEntries;
            DuplicatesDataGrid.ItemsSource = DuplicateContentList;
            PopulateContentTypeFilter();
        }

        private void PopulateContentTypeFilter()
        {
            foreach (var (typeKey, displayName) in ContentTypeOptions)
            {
                var cb = new CheckBox { Content = displayName, IsChecked = true, Tag = typeKey };
                cb.Checked += ContentTypeFilter_Changed;
                cb.Unchecked += ContentTypeFilter_Changed;
                ContentTypeFilterPanel.Children.Add(cb);
            }
        }

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before using cleanup features.";

        protected override appFunction PageLogFunction => appFunction.Delete;

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton", "FindUnassignedButton",
            "ClearSelectedButton", "ClearAllButton", "DeleteButton", "CleanupDataGrid", "ClearLogButton", "ExportCsvButton",
            "ScanDuplicatesButton", "ContentTypeFilterButton", "SelectOlderButton", "SelectUnassignedButton",
            "ClearDuplicateSelectionButton", "DeleteDuplicatesButton", "DuplicatesDataGrid",
            "DuplicatesClearLogButton", "DuplicatesExportCsvButton"
        };

        #endregion

        #region Base Class Overrides

        protected override void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            base.ShowLoading(message);
            ListAllButton.IsEnabled = false;
            SearchButton.IsEnabled = false;
            FindUnassignedButton.IsEnabled = false;
        }

        protected override void HideLoading()
        {
            base.HideLoading();
            ListAllButton.IsEnabled = true;
            SearchButton.IsEnabled = true;
            FindUnassignedButton.IsEnabled = true;
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
                LogWarning("No content to delete.");
                return;
            }

            ShowOperationProgress("Preparing to delete items...", 0, _deleteTotal);

            foreach (var definition in GetDeleteTypeRegistry())
            {
                // Applications have per-app ContentType values (e.g., "App - Windows app (Win32)"),
                // so use the dedicated helper that matches any application content type.
                var ids = definition.TypeKey == ContentTypes.Application
                    ? GetApplicationContentIds()
                    : GetContentIdsByType(definition.TypeKey);
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
                await LoadContentTypesAsync(graphServiceClient, SupportedContentTypes);
                CleanupDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                LogError($"Error during loading: {ex.Message}");
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
                await SearchContentTypesAsync(graphServiceClient, searchQuery, SupportedContentTypes);
                CleanupDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                LogError($"Error during search: {ex.Message}");
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
                        AppLogger.Info($"Deleted {definition.DisplayName} with ID: {id}", appFunction.Delete);
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        _deleteSuccessCount++;
                    }
                    // If not deleted (skipped), don't count as success or error
                }
                catch (Exception ex)
                {
                    _deleteErrorCount++;
                    AppLogger.Error($"Error deleting {definition.DisplayName} {id}: {ex.Message}", appFunction.Delete);
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

            if (isAssigned == null)
            {
                AppendToDetailsRichTextBlock($"Failed to check assignments for AutoPilot profile {id}. Skipping deletion to be safe.");
                return false;
            }

            if (isAssigned.Value)
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
                    AppLogger.Info($"Deleted assignments for Windows AutoPilot profile with ID: {id}", appFunction.Delete);
                    await DeleteWindowsAutopilotProfile(sourceGraphServiceClient, id);
                    return true;
                }
                else
                {
                    AppLogger.Warning($"Skipped deletion of Windows AutoPilot profile with ID: {id} as it is assigned to devices.", appFunction.Delete);
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

            new(ContentTypes.Application, "Application",
                async id => { await DeleteApplication(sourceGraphServiceClient, id); return true; }),
        ];

        #endregion

        #region Unassigned Content Detection

        /// <summary>
        /// Content types that support group assignments (excludes Assignment Filter and Entra Group).
        /// </summary>
        private static readonly string[] AssignableContentTypes = new[]
        {
            ContentTypes.SettingsCatalog,
            ContentTypes.DeviceCompliancePolicy,
            ContentTypes.DeviceConfigurationPolicy,
            ContentTypes.AppleBYODEnrollmentProfile,
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
        /// Returns a mapping of content type to its assignment-checking function.
        /// </summary>
        private Dictionary<string, Func<GraphServiceClient, string, Task<bool?>>> GetAssignmentCheckRegistry() => new()
        {
            [ContentTypes.SettingsCatalog] = HasSettingsCatalogAssignmentsAsync,
            [ContentTypes.DeviceCompliancePolicy] = HasDeviceCompliancePolicyAssignmentsAsync,
            [ContentTypes.DeviceConfigurationPolicy] = HasDeviceConfigurationAssignmentsAsync,
            [ContentTypes.AppleBYODEnrollmentProfile] = HasAppleBYODEnrollmentProfileAssignmentsAsync,
            [ContentTypes.PowerShellScript] = HasPowerShellScriptAssignmentsAsync,
            [ContentTypes.ProactiveRemediation] = HasProactiveRemediationAssignmentsAsync,
            [ContentTypes.MacOSShellScript] = HasMacOSShellScriptAssignmentsAsync,
            [ContentTypes.WindowsAutoPilotProfile] = CheckIfAutoPilotProfileHasAssignments,
            [ContentTypes.WindowsDriverUpdate] = HasWindowsDriverUpdateAssignmentsAsync,
            [ContentTypes.WindowsFeatureUpdate] = HasWindowsFeatureUpdateAssignmentsAsync,
            [ContentTypes.WindowsQualityUpdatePolicy] = HasWindowsQualityUpdatePolicyAssignmentsAsync,
            [ContentTypes.WindowsQualityUpdateProfile] = HasWindowsQualityUpdateProfileAssignmentsAsync,
            [ContentTypes.Application] = HasApplicationAssignmentsAsync,
        };

        /// <summary>
        /// Loads all assignable content types and filters to show only items without assignments.
        /// </summary>
        private async Task FindUnassignedOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading content from Microsoft Graph...");
            DeleteButton.IsEnabled = false;
            ClearSelectedButton.IsEnabled = false;
            ClearAllButton.IsEnabled = false;
            AppendToDetailsRichTextBlock("Loading all assignable content types. This may take a while...");
            try
            {
                // Load into a temporary list so items don't appear in the grid before being checked
                ContentList.Clear();
                await LoadContentTypesAsync(graphServiceClient, AssignableContentTypes);
                var allItems = ContentList.ToList();
                ContentList.Clear();

                var totalItems = allItems.Count;
                AppendToDetailsRichTextBlock($"Loaded {totalItems} items. Checking assignments...");

                ShowOperationProgress("Checking assignments...", 0, totalItems);

                var assignmentChecks = GetAssignmentCheckRegistry();
                var checkedCount = 0;

                foreach (var item in allItems)
                {
                    checkedCount++;
                    ShowOperationProgress($"Checking assignments ({checkedCount}/{totalItems})", checkedCount, totalItems);

                    if (item.ContentType == null || item.ContentId == null)
                    {
                        AppendToDetailsRichTextBlock($"Skipping item with missing type or ID.");
                        continue;
                    }

                    // Applications have per-app ContentType values (e.g., "App - Windows app (Win32)"),
                    // so normalize them to ContentTypes.Application for registry lookup.
                    var lookupKey = UserInterfaceHelper.IsApplicationContentType(item.ContentType)
                        ? ContentTypes.Application
                        : item.ContentType;

                    if (assignmentChecks.TryGetValue(lookupKey, out var checkFunc))
                    {
                        var hasAssignments = await checkFunc(graphServiceClient, item.ContentId);
                        UpdateTotalTimeSaved(secondsSavedOnFindingUnassigned, appFunction.FindUnassigned);
                        if (hasAssignments == null)
                        {
                            AppendToDetailsRichTextBlock($"Failed to check assignments for '{item.ContentName}'. Skipping to be safe.");
                        }
                        else if (!hasAssignments.Value)
                        {
                            ContentList.Add(item);
                        }
                    }
                    else
                    {
                        AppendToDetailsRichTextBlock($"No assignment check available for type '{item.ContentType}'. Skipping.");
                    }
                }

                CleanupDataGrid.ItemsSource = ContentList;
                AppendToDetailsRichTextBlock($"Found {ContentList.Count} unassigned item(s) out of {totalItems} total.");
                ShowOperationSuccess($"Found {ContentList.Count} unassigned item(s)");
            }
            catch (Exception ex)
            {
                AppendToDetailsRichTextBlock($"Error finding unassigned content: {ex.Message}");
                ShowOperationError($"Error: {ex.Message}");
            }
            finally
            {
                HideLoading();
                DeleteButton.IsEnabled = true;
                ClearSelectedButton.IsEnabled = true;
                ClearAllButton.IsEnabled = true;
            }
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
                ContentList.Clear();
                AppendToDetailsRichTextBlock("Cleared the data grid.");
            }
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private async void FindUnassignedButton_Click(object sender, RoutedEventArgs e)
        {
            await FindUnassignedOrchestrator(sourceGraphServiceClient);
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

        private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentList.Count == 0)
            {
                LogWarning("Nothing to export — the list is empty.");
                return;
            }
            try
            {
                var savedPath = await CsvExporter.ExportContentListAsync(ContentList, "Cleanup");
                if (savedPath != null)
                    ShowOperationSuccess($"Exported {ContentList.Count} items to CSV.", savedPath);
            }
            catch (Exception ex)
            {
                LogError($"CSV export failed: {ex.Message}");
            }
        }

        #endregion

        #region Mode Selector

        private void CleanupModeSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeletePanel is null || DuplicatesPanel is null) return;

            var isDuplicates = CleanupModeSegmented.SelectedIndex == 1;
            DeletePanel.Visibility = isDuplicates ? Visibility.Collapsed : Visibility.Visible;
            DuplicatesPanel.Visibility = isDuplicates ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Duplicate Detection

        private async void ScanDuplicatesButton_Click(object sender, RoutedEventArgs e)
        {
            await ScanDuplicatesOrchestrator(sourceGraphServiceClient);
        }

        private void SelectOlderButton_Click(object sender, RoutedEventArgs e)
        {
            if (DuplicateContentList.Count == 0)
            {
                LogWarning("No scan results to select from. Run a scan first.");
                return;
            }

            DuplicatesDataGrid.SelectedItems.Clear();

            var toSelect = new List<DuplicateContentInfo>();
            foreach (var group in DuplicateContentList
                .GroupBy(i => (Name: i.ContentName?.Trim().ToUpperInvariant(), Type: i.ContentType))
                .Where(g => g.Count() >= 2))
            {
                toSelect.AddRange(group
                    .OrderByDescending(i => i.CreatedDateTime ?? DateTimeOffset.MinValue)
                    .Skip(1));
            }

            foreach (var item in toSelect)
                DuplicatesDataGrid.SelectedItems.Add(item);

            LogInfo($"Selected {toSelect.Count} older item(s) — the newest in each group is kept.");
        }

        private void SelectUnassignedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DuplicateContentList.Count == 0)
            {
                LogWarning("No scan results to select from. Run a scan first.");
                return;
            }

            DuplicatesDataGrid.SelectedItems.Clear();

            var toSelect = new List<DuplicateContentInfo>();
            foreach (var group in DuplicateContentList
                .GroupBy(i => (Name: i.ContentName?.Trim().ToUpperInvariant(), Type: i.ContentType))
                .Where(g => g.Count() >= 2 && g.Any(i => i.HasAssignments == true)))
            {
                toSelect.AddRange(group.Where(i => i.HasAssignments == false));
            }

            if (toSelect.Count == 0)
            {
                LogWarning("No unassigned items found in groups where another item is assigned. Assignment data may not be available for all types.");
                return;
            }

            foreach (var item in toSelect)
                DuplicatesDataGrid.SelectedItems.Add(item);

            LogInfo($"Selected {toSelect.Count} unassigned item(s) — assigned items in each group are kept.");
        }

        private void ContentTypeFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not string typeKey) return;

            if (cb.IsChecked == true)
                _selectedContentTypes.Add(typeKey);
            else
                _selectedContentTypes.Remove(typeKey);

            var selected = _selectedContentTypes.Count;
            var total = ContentTypeOptions.Length;
            ContentTypeFilterButton.Label = selected == total ? "All Types" : $"{selected} of {total} types";
        }

        private void ClearDuplicateSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = DuplicatesDataGrid.SelectedItems?.Cast<DuplicateContentInfo>().ToList();
            if (selected == null || selected.Count == 0)
            {
                LogWarning("No items selected to clear.");
                return;
            }
            foreach (var item in selected)
                DuplicateContentList.Remove(item);
        }

        private async void DeleteDuplicatesButton_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedDuplicatesAsync();
        }

        private async void DuplicatesExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (DuplicateContentList.Count == 0)
            {
                LogWarning("Nothing to export — run a scan first.");
                return;
            }
            try
            {
                var exportList = DuplicateContentList
                    .Select(d => new CustomContentInfo
                    {
                        ContentName = d.ContentName,
                        ContentType = d.ContentType,
                        ContentId = d.ContentId,
                        ContentPlatform = d.ContentPlatform,
                        ContentDescription = $"Created: {d.CreatedDisplay} | Modified: {d.ModifiedDisplay} | Assigned: {d.AssignedDisplay}"
                    })
                    .ToList();
                var path = await CsvExporter.ExportContentListAsync(exportList, "Duplicates");
                if (path != null)
                    ShowOperationSuccess($"Exported {DuplicateContentList.Count} items to CSV.", path);
            }
            catch (Exception ex)
            {
                LogError($"CSV export failed: {ex.Message}");
            }
        }

        private void DuplicatesDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        private void DuplicatesDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is DuplicateContentInfo info && info.IsOddGroup)
                e.Row.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(18, 128, 128, 128));
            else
                e.Row.Background = null;
        }

        private async Task ScanDuplicatesOrchestrator(GraphServiceClient graphServiceClient)
        {
            if (ContentList.Count > 0)
            {
                var warn = new ContentDialog
                {
                    Title = "Clear Staging Area?",
                    Content = $"Scanning requires loading all content, which will clear the {ContentList.Count} item(s) currently in the delete staging area. Continue?",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                if (await warn.ShowAsync() != ContentDialogResult.Primary) return;
            }

            ScanDuplicatesButton.IsEnabled = false;
            DuplicatesLoadingOverlay.Show("Loading all content types...");
            DuplicateContentList.Clear();

            try
            {
                var typesToScan = SupportedContentTypes.Where(_selectedContentTypes.Contains).ToArray();
                ContentList.Clear();
                await LoadContentTypesAsync(graphServiceClient, typesToScan);
                var allItems = ContentList.ToList();
                ContentList.Clear();
                CleanupDataGrid.ItemsSource = ContentList;

                AppLogger.Info($"Loaded {allItems.Count} total items across {typesToScan.Length} content type(s).", appFunction.FindDuplicates);

                var duplicateGroups = allItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.ContentName) && !string.IsNullOrWhiteSpace(i.ContentType))
                    .GroupBy(i => (
                        Name: i.ContentName!.Trim().ToUpperInvariant(),
                        Type: UserInterfaceHelper.IsApplicationContentType(i.ContentType!)
                            ? ContentTypes.Application : i.ContentType!))
                    .Where(g => g.Count() >= 2)
                    .ToList();

                if (duplicateGroups.Count == 0)
                {
                    ShowOperationSuccess("No duplicates found.");
                    AppLogger.Info("Scan complete — no duplicates found.", appFunction.FindDuplicates);
                    return;
                }

                var totalDuplicateItems = duplicateGroups.Sum(g => g.Count());
                AppLogger.Info($"Found {duplicateGroups.Count} duplicate group(s) with {totalDuplicateItems} items. Fetching metadata...", appFunction.FindDuplicates);

                var metadataRegistry = GetDuplicateMetadataRegistry();
                var assignmentChecks = GetAssignmentCheckRegistry();
                var processed = 0;
                var groupIndex = 0;

                foreach (var group in duplicateGroups)
                {
                    var isOddGroup = groupIndex % 2 != 0;
                    groupIndex++;

                    foreach (var item in group)
                    {
                        processed++;
                        DuplicatesLoadingOverlay.Show($"Fetching metadata... ({processed}/{totalDuplicateItems})");
                        ShowOperationProgress("Fetching duplicate metadata", processed, totalDuplicateItems);

                        var normalizedType = UserInterfaceHelper.IsApplicationContentType(item.ContentType!)
                            ? ContentTypes.Application : item.ContentType!;

                        var dupInfo = new DuplicateContentInfo
                        {
                            ContentName = item.ContentName,
                            ContentType = item.ContentType,
                            ContentId = item.ContentId,
                            ContentPlatform = item.ContentPlatform,
                            IsOddGroup = isOddGroup,
                        };

                        if (!string.IsNullOrEmpty(item.ContentId))
                        {
                            if (metadataRegistry.TryGetValue(normalizedType, out var getMetadata))
                            {
                                try
                                {
                                    var (created, modified) = await getMetadata(graphServiceClient, item.ContentId);
                                    dupInfo.CreatedDateTime = created;
                                    dupInfo.LastModifiedDateTime = modified;
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.Warning($"Could not fetch metadata for '{item.ContentName}': {ex.Message}", appFunction.FindDuplicates);
                                }
                            }

                            if (assignmentChecks.TryGetValue(normalizedType, out var checkAssignment))
                            {
                                try
                                {
                                    dupInfo.HasAssignments = await checkAssignment(graphServiceClient, item.ContentId);
                                }
                                catch (Exception ex)
                                {
                                    AppLogger.Warning($"Could not check assignments for '{item.ContentName}': {ex.Message}", appFunction.FindDuplicates);
                                }
                            }
                        }

                        DuplicateContentList.Add(dupInfo);
                    }
                }

                var assignedCount = DuplicateContentList.Count(d => d.HasAssignments == true);
                DuplicatesInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success;
                DuplicatesInfoBar.Title = "Scan Complete";
                DuplicatesInfoBar.Message = $"{duplicateGroups.Count} duplicate group(s)  ·  {totalDuplicateItems} items  ·  {assignedCount} assigned";

                ShowOperationSuccess($"Found {duplicateGroups.Count} duplicate name(s) — {totalDuplicateItems} items total.");
                AppLogger.Info($"Scan complete. {duplicateGroups.Count} duplicate group(s) found.", appFunction.FindDuplicates);
            }
            catch (Exception ex)
            {
                LogError($"Scan failed: {ex.Message}");
                ShowOperationError($"Scan failed: {ex.Message}");
            }
            finally
            {
                DuplicatesLoadingOverlay.Hide();
                ScanDuplicatesButton.IsEnabled = true;
            }
        }

        private async Task DeleteSelectedDuplicatesAsync()
        {
            var selected = DuplicatesDataGrid.SelectedItems?.Cast<DuplicateContentInfo>().ToList();
            if (selected == null || selected.Count == 0)
            {
                LogWarning("No items selected for deletion.");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Delete selected duplicates?",
                Content = $"Are you sure you want to permanently delete {selected.Count} item(s)? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var deleteRegistry = GetDeleteTypeRegistry().ToDictionary(d => d.TypeKey);
            var total = selected.Count;
            var current = 0;
            var success = 0;
            var errors = 0;

            DuplicatesLoadingOverlay.Show("Deleting selected duplicates...");
            try
            {
                foreach (var item in selected)
                {
                    current++;
                    ShowOperationProgress($"Deleting duplicate ({current}/{total})", current, total);

                    if (string.IsNullOrEmpty(item.ContentId) || string.IsNullOrEmpty(item.ContentType)) continue;

                    var typeKey = UserInterfaceHelper.IsApplicationContentType(item.ContentType)
                        ? ContentTypes.Application : item.ContentType;

                    if (!deleteRegistry.TryGetValue(typeKey, out var definition)) continue;

                    try
                    {
                        await definition.DeleteAsync(item.ContentId);
                        DuplicateContentList.Remove(item);
                        AppLogger.Info($"Deleted duplicate '{item.ContentName}' (ID: {item.ContentId})", appFunction.Delete);
                        UpdateTotalTimeSaved(secondsSavedOnDeleting, appFunction.Delete);
                        success++;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Error deleting '{item.ContentName}': {ex.Message}", appFunction.Delete);
                        errors++;
                    }
                }

                if (errors == 0)
                    ShowOperationSuccess($"Successfully deleted {success} duplicate(s).");
                else
                    ShowOperationError($"Completed with {errors} error(s). {success} deleted successfully.");
            }
            finally
            {
                DuplicatesLoadingOverlay.Hide();
            }
        }

        private Dictionary<string, Func<GraphServiceClient, string, Task<(DateTimeOffset? Created, DateTimeOffset? Modified)>>> GetDuplicateMetadataRegistry() => new()
        {
            [ContentTypes.SettingsCatalog] = async (c, id) =>
            {
                var p = await c.DeviceManagement.ConfigurationPolicies[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.DeviceCompliancePolicy] = async (c, id) =>
            {
                var p = await c.DeviceManagement.DeviceCompliancePolicies[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.DeviceConfigurationPolicy] = async (c, id) =>
            {
                var p = await c.DeviceManagement.DeviceConfigurations[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.AppleBYODEnrollmentProfile] = async (c, id) =>
            {
                var p = await c.DeviceManagement.AppleUserInitiatedEnrollmentProfiles[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.AssignmentFilter] = async (c, id) =>
            {
                var p = await c.DeviceManagement.AssignmentFilters[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.EntraGroup] = async (c, id) =>
            {
                var p = await c.Groups[id].GetAsync();
                return (p?.CreatedDateTime, null);
            },
            [ContentTypes.PowerShellScript] = async (c, id) =>
            {
                var p = await c.DeviceManagement.DeviceManagementScripts[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.ProactiveRemediation] = async (c, id) =>
            {
                var p = await c.DeviceManagement.DeviceHealthScripts[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.MacOSShellScript] = async (c, id) =>
            {
                var p = await c.DeviceManagement.DeviceShellScripts[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.WindowsAutoPilotProfile] = async (c, id) =>
            {
                var p = await c.DeviceManagement.WindowsAutopilotDeploymentProfiles[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.WindowsDriverUpdate] = async (c, id) =>
            {
                var p = await c.DeviceManagement.WindowsDriverUpdateProfiles[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.WindowsFeatureUpdate] = async (c, id) =>
            {
                var p = await c.DeviceManagement.WindowsFeatureUpdateProfiles[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.WindowsQualityUpdatePolicy] = async (c, id) =>
            {
                var p = await c.DeviceManagement.WindowsQualityUpdatePolicies[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.WindowsQualityUpdateProfile] = async (c, id) =>
            {
                var p = await c.DeviceManagement.WindowsQualityUpdateProfiles[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
            [ContentTypes.Application] = async (c, id) =>
            {
                var p = await c.DeviceAppManagement.MobileApps[id].GetAsync();
                return (p?.CreatedDateTime, p?.LastModifiedDateTime);
            },
        };

        #endregion
    }
}



