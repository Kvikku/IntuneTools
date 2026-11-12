using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using static InToolz.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static InToolz.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static InToolz.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
using static InToolz.Graph.IntuneHelperClasses.macOSShellScript;
using static InToolz.Graph.IntuneHelperClasses.PowerShellScriptsHelper;
using static InToolz.Graph.IntuneHelperClasses.ProactiveRemediationsHelper;
using static InToolz.Graph.IntuneHelperClasses.SettingsCatalogHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsAutoPilotHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsDriverUpdateHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsFeatureUpdateHelper;
using static InToolz.Graph.IntuneHelperClasses.WindowsQualityUpdatePolicyHandler;
using static InToolz.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;
using InToolz.Graph.EntraHelperClasses;
using InToolz.Graph.IntuneHelperClasses;

namespace InToolz.Pages
{
    /// <summary>
    /// Page for viewing and removing group assignments from Intune content.
    /// </summary>
    public sealed partial class ManageAssignmentsPage : BaseDataOperationPage
    {
        #region Fields & Types

        // Progress tracking for remove operations
        private int _removeTotal;
        private int _removeCurrent;
        private int _removeSuccessCount;
        private int _removeErrorCount;

        /// <summary>
        /// Content types that support group assignments.
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

        #endregion

        #region Constructor & Configuration

        public ManageAssignmentsPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(AssignmentsDataGrid, () => sourceGraphServiceClient);
            LogConsole.ItemsSource = LogEntries;

            // Restore the last search query so it doesn't need to be retyped after every restart.
            SearchStagingBar.SearchText = PageStatePersistence.LoadLastSearchQuery(PageStateKey);

            UpdateSelectedCount();
        }

        private void AssignmentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectedCount();

        private void UpdateSelectedCount() =>
            ManageAssignmentsActionBar.CountText = $"{AssignmentsDataGrid.SelectedItems.Count} selected";

        // Key used to persist this page's last search query.
        private const string PageStateKey = "ManageAssignments";

        /// <summary>
        /// Auto-runs the remembered search the first time this page is shown while
        /// authenticated in a session, if nothing is staged yet.
        /// </summary>
        protected override async void OnFirstAuthenticatedEntry()
        {
            if (ContentList.Count > 0) return;

            var savedQuery = PageStatePersistence.LoadLastSearchQuery(PageStateKey);
            if (string.IsNullOrWhiteSpace(savedQuery)) return;

            AppendToLog($"Auto-loading remembered search: '{savedQuery}'.");
            await SearchOrchestrator(RequireSourceClient(), savedQuery);
        }

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before managing assignments.";

        protected override appFunction PageLogFunction => appFunction.ManageAssignment;

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "SearchStagingBar", "ViewAssignmentsButton", "AssignmentsDataGrid"
        };

        #endregion

        #region Base Class Overrides

        protected override void ShowLoading(string message = "Loading data from Microsoft Graph...")
        {
            base.ShowLoading(message);
            SearchStagingBar.SetSearchAndListEnabled(false);
        }

        protected override void HideLoading()
        {
            base.HideLoading();
            SearchStagingBar.SetSearchAndListEnabled(true);
        }

        #endregion

        #region Core Operations

        /// <summary>
        /// Loads all assignable content types from Microsoft Graph.
        /// </summary>
        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading assignable content from Microsoft Graph...");
            AppendToLog("Starting to load all assignable content types. This could take a while...");
            try
            {
                ContentList.Clear();
                await LoadContentTypesAsync(graphServiceClient, AssignableContentTypes);
                AssignmentsDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                AppendToLog($"Error during loading: {ex.Message}");
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
            AppendToLog($"Searching for content matching '{searchQuery}'. This may take a while...");
            try
            {
                ContentList.Clear();
                await SearchContentTypesAsync(graphServiceClient, searchQuery, AssignableContentTypes);
                AssignmentsDataGrid.ItemsSource = ContentList;
            }
            catch (Exception ex)
            {
                AppendToLog($"Error during search: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        /// <summary>
        /// Views assignment details for selected content items and displays them in a dialog.
        /// </summary>
        private async Task ViewAssignmentsOrchestrator(GraphServiceClient graphServiceClient, List<CustomContentInfo> selectedItems)
        {
            var viewRegistry = GraphOperationRegistries.GetAssignmentDetailsRegistry();
            var checkedCount = 0;
            var totalItems = selectedItems.Count;
            var results = new List<AssignmentResult>();

            ShowOperationProgress("Retrieving assignment details...", 0, totalItems);

            foreach (var item in selectedItems)
            {
                checkedCount++;
                ShowOperationProgress($"Checking assignments ({checkedCount}/{totalItems})", checkedCount, totalItems);

                if (item.ContentType == null || item.ContentId == null)
                {
                    LogWarning($"Skipping item with missing type or ID.");
                    continue;
                }

                if (!viewRegistry.TryGetValue(item.ContentType, out var getDetailsFunc))
                {
                    if (UserInterfaceHelper.IsApplicationContentType(item.ContentType))
                        getDetailsFunc = ApplicationHelper.GetApplicationAssignmentDetailsAsync;
                    else
                    {
                        LogWarning($"No assignment viewer available for type '{item.ContentType}'. Skipping.");
                        continue;
                    }
                }

                try
                {
                    var details = await getDetailsFunc(graphServiceClient, item.ContentId);
                    results.Add(new AssignmentResult(
                        item.ContentName ?? "Unknown",
                        item.ContentType ?? "Unknown",
                        item.ContentId ?? string.Empty,
                        details,
                        details == null ? "Failed to retrieve assignments." : null));
                }
                catch (Exception ex)
                {
                    results.Add(new AssignmentResult(
                        item.ContentName ?? "Unknown",
                        item.ContentType ?? "Unknown",
                        item.ContentId ?? string.Empty,
                        null,
                        ex.Message));
                }
            }

            int totalAssignments = results.Where(r => r.Assignments != null).Sum(r => r.Assignments!.Count);

            // Resolve group IDs to display names
            var groupIds = results
                .Where(r => r.Assignments != null)
                .SelectMany(r => r.Assignments!)
                .Where(a => !string.IsNullOrEmpty(a.GroupId))
                .Select(a => a.GroupId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, string> groupNames = new();
            if (groupIds.Count > 0)
            {
                ShowOperationProgress("Resolving group names...");
                groupNames = await GroupHelperClass.ResolveGroupNamesAsync(graphServiceClient, groupIds);
            }

            ShowOperationSuccess($"Checked assignments for {totalItems} item(s) — {totalAssignments} assignment(s) found");
            LogInfo($"Retrieved assignments for {totalItems} item(s): {totalAssignments} total assignment(s).");

            if (results.Count > 0)
            {
                var removeAll = await AssignmentViewerDialog.ShowAsync(
                    this.XamlRoot, results, groupNames,
                    GraphOperationRegistries.GetRemoveSingleAssignmentRegistry(),
                    LogSuccess, LogError);
                if (removeAll)
                {
                    // Bulk safeguard for large operations
                    if (UserInterfaceHelper.RequiresBulkConfirmation(selectedItems.Count))
                    {
                        var bulkWarning = new ContentDialog
                        {
                            Title = "\u26A0 Large Bulk Operation",
                            Content = $"You are about to remove all assignments from {selectedItems.Count} items. Are you sure you want to continue?",
                            PrimaryButtonText = "Continue",
                            CloseButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Close,
                            XamlRoot = this.XamlRoot
                        };

                        if (await bulkWarning.ShowAsync() != ContentDialogResult.Primary)
                        {
                            AppendToLog("Bulk assignment removal cancelled by user.");
                            return;
                        }
                    }

                    await RemoveAssignmentsOrchestrator(graphServiceClient, selectedItems);
                }
            }
        }

        /// <summary>
        /// Removes all assignments from selected content items.
        /// </summary>
        private async Task RemoveAssignmentsOrchestrator(GraphServiceClient graphServiceClient, List<CustomContentInfo> selectedItems)
        {
            _removeTotal = selectedItems.Count;
            _removeCurrent = 0;
            _removeSuccessCount = 0;
            _removeErrorCount = 0;

            ShowOperationProgress("Removing assignments...", 0, _removeTotal);

            foreach (var definition in GraphOperationRegistries.GetRemoveAssignmentRegistry())
            {
                var itemsOfType = selectedItems
                    .Where(i => string.Equals(i.ContentType, definition.TypeKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in itemsOfType)
                {
                    _removeCurrent++;
                    ShowOperationProgress($"Removing assignments from {definition.DisplayName}", _removeCurrent, _removeTotal);

                    if (string.IsNullOrEmpty(item.ContentId))
                    {
                        LogWarning($"Skipping item '{item.ContentName}' with missing ID.");
                        continue;
                    }

                    try
                    {
                        await definition.RemoveAsync(graphServiceClient, item.ContentId);
                        _removeSuccessCount++;
                        LogSuccess($"Removed assignments from '{item.ContentName}'.");
                        UpdateTotalTimeSaved(secondsSavedOnManagingAssignments, appFunction.ManageAssignment);
                    }
                    catch (Exception ex)
                    {
                        _removeErrorCount++;
                        LogError($"Error removing assignments from '{item.ContentName}': {ex.Message}");
                    }
                }
            }

            if (_removeErrorCount == 0)
            {
                ShowOperationSuccess($"Successfully removed assignments from {_removeSuccessCount} item(s)");
            }
            else
            {
                ShowOperationError($"Completed with {_removeErrorCount} error(s). {_removeSuccessCount} item(s) processed successfully.");
            }
        }

        #endregion


        #region Event Handlers

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            ContentList.Clear();
            AssignmentsDataGrid.ItemsSource = null;
            AssignmentsDataGrid.ItemsSource = ContentList;
            AppendToLog("All items cleared from the list.");
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = AssignmentsDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected to clear.");
                return;
            }
            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }
            AssignmentsDataGrid.ItemsSource = null;
            AssignmentsDataGrid.ItemsSource = ContentList;
            AppendToLog($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        private void AssignmentsDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(RequireSourceClient());
        }

        private async void SearchStagingBar_SearchRequested(object sender, RoutedEventArgs e)
        {
            var searchQuery = SearchStagingBar.SearchText.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                AppendToLog("Please enter a search query.");
                return;
            }
            PageStatePersistence.SaveLastSearchQuery(PageStateKey, searchQuery);
            await SearchOrchestrator(RequireSourceClient(), searchQuery);
        }

        private async void ViewAssignmentsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = AssignmentsDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected. Please select one or more items to view their assignments.");
                return;
            }

            await ViewAssignmentsOrchestrator(RequireSourceClient(), selectedItems);
        }

        private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentList.Count == 0)
            {
                LogWarning("Nothing to export — the list is empty.");
                return;
            }

            var viewRegistry = GraphOperationRegistries.GetAssignmentDetailsRegistry();
            var rows = new List<(CustomContentInfo, IEnumerable<AssignmentInfo>)>();
            var allGroupIds = new List<string>();
            var client = RequireSourceClient();

            ShowLoading("Fetching assignments for export...");
            try
            {
                foreach (var item in ContentList)
                {
                    if (item.ContentType == null || item.ContentId == null ||
                        !viewRegistry.TryGetValue(item.ContentType, out var getDetailsFunc))
                    {
                        rows.Add((item, Enumerable.Empty<AssignmentInfo>()));
                        continue;
                    }

                    try
                    {
                        var assignments = await getDetailsFunc(client, item.ContentId) ?? new List<AssignmentInfo>();
                        rows.Add((item, assignments));
                        allGroupIds.AddRange(assignments
                            .Where(a => !string.IsNullOrEmpty(a.GroupId))
                            .Select(a => a.GroupId!));
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Could not fetch assignments for '{item.ContentName}': {ex.Message}");
                        rows.Add((item, Enumerable.Empty<AssignmentInfo>()));
                    }
                }

                var groupNames = allGroupIds.Count > 0
                    ? await GroupHelperClass.ResolveGroupNamesAsync(client, allGroupIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList())
                    : new Dictionary<string, string>();

                var savedPath = await CsvExporter.ExportWithAssignmentsAsync(rows, groupNames, "ManageAssignments");
                if (savedPath != null)
                    ShowOperationSuccess($"Exported {rows.Count} items to CSV.", savedPath);
            }
            catch (Exception ex)
            {
                LogError($"CSV export failed: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        #endregion
    }
}
