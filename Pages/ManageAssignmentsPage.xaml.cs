using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static IntuneTools.Graph.IntuneHelperClasses.AppleBYODEnrollmentProfileHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceCompliancePolicyHelper;
using static IntuneTools.Graph.IntuneHelperClasses.DeviceConfigurationHelper;
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
    /// Page for viewing and removing group assignments from Intune content.
    /// </summary>
    public sealed partial class ManageAssignmentsPage : BaseDataOperationPage
    {
        #region Fields & Types

        /// <summary>
        /// Defines a remove operation for a specific content type's assignments.
        /// </summary>
        private record RemoveAssignmentDefinition(
            string TypeKey,
            string DisplayName,
            Func<GraphServiceClient, string, Task> RemoveAsync);

        // Progress tracking for remove operations
        private readonly OperationProgressTracker _removeProgress = new();

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
        };

        #endregion

        #region Constructor & Configuration

        public ManageAssignmentsPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(AssignmentsDataGrid);
            LogConsole.ItemsSource = LogEntries;
        }

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before managing assignments.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton", "ViewAssignmentsButton",
            "RemoveAssignmentsButton", "ClearSelectedButton", "ClearAllButton",
            "AssignmentsDataGrid", "ClearLogButton"
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

        #endregion

        #region Registries

        /// <summary>
        /// Returns the registry mapping content types to their assignment detail retrieval functions.
        /// </summary>
        private Dictionary<string, Func<GraphServiceClient, string, Task<List<AssignmentInfo>?>>> GetViewAssignmentRegistry() => new()
        {
            [ContentTypes.SettingsCatalog] = GetSettingsCatalogAssignmentDetailsAsync,
            [ContentTypes.DeviceCompliancePolicy] = GetDeviceComplianceAssignmentDetailsAsync,
            [ContentTypes.DeviceConfigurationPolicy] = GetDeviceConfigurationAssignmentDetailsAsync,
            [ContentTypes.AppleBYODEnrollmentProfile] = GetAppleBYODAssignmentDetailsAsync,
            [ContentTypes.PowerShellScript] = GetPowerShellScriptAssignmentDetailsAsync,
            [ContentTypes.ProactiveRemediation] = GetProactiveRemediationAssignmentDetailsAsync,
            [ContentTypes.MacOSShellScript] = GetMacOSShellScriptAssignmentDetailsAsync,
            [ContentTypes.WindowsAutoPilotProfile] = GetWindowsAutoPilotAssignmentDetailsAsync,
            [ContentTypes.WindowsDriverUpdate] = GetWindowsDriverUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsFeatureUpdate] = GetWindowsFeatureUpdateAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdatePolicy] = GetWindowsQualityUpdatePolicyAssignmentDetailsAsync,
            [ContentTypes.WindowsQualityUpdateProfile] = GetWindowsQualityUpdateProfileAssignmentDetailsAsync,
        };

        /// <summary>
        /// Returns the registry mapping content types to their assignment removal functions.
        /// </summary>
        private IEnumerable<RemoveAssignmentDefinition> GetRemoveAssignmentRegistry() =>
        [
            new(ContentTypes.SettingsCatalog, "Settings Catalog",
                async (client, id) => await RemoveAllSettingsCatalogAssignmentsAsync(client, id)),

            new(ContentTypes.DeviceCompliancePolicy, "Device Compliance Policy",
                async (client, id) => await RemoveAllDeviceComplianceAssignmentsAsync(client, id)),

            new(ContentTypes.DeviceConfigurationPolicy, "Device Configuration Policy",
                async (client, id) => await RemoveAllDeviceConfigurationAssignmentsAsync(client, id)),

            new(ContentTypes.AppleBYODEnrollmentProfile, "Apple BYOD Enrollment Profile",
                async (client, id) => await RemoveAllAppleBYODAssignmentsAsync(client, id)),

            new(ContentTypes.PowerShellScript, "PowerShell Script",
                async (client, id) => await RemoveAllPowerShellScriptAssignmentsAsync(client, id)),

            new(ContentTypes.ProactiveRemediation, "Proactive Remediation",
                async (client, id) => await RemoveAllProactiveRemediationAssignmentsAsync(client, id)),

            new(ContentTypes.MacOSShellScript, "macOS Shell Script",
                async (client, id) => await RemoveAllMacOSShellScriptAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsAutoPilotProfile, "Windows AutoPilot Profile",
                async (client, id) => await RemoveAllWindowsAutoPilotAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsDriverUpdate, "Windows Driver Update",
                async (client, id) => await RemoveAllWindowsDriverUpdateAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsFeatureUpdate, "Windows Feature Update",
                async (client, id) => await RemoveAllWindowsFeatureUpdateAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsQualityUpdatePolicy, "Windows Quality Update Policy",
                async (client, id) => await RemoveAllWindowsQualityUpdatePolicyAssignmentsAsync(client, id)),

            new(ContentTypes.WindowsQualityUpdateProfile, "Windows Quality Update Profile",
                async (client, id) => await RemoveAllWindowsQualityUpdateProfileAssignmentsAsync(client, id)),
        ];

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
                await LoadContentTypesAsync(graphServiceClient, AssignableContentTypes, AppendToLog);
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
                await SearchContentTypesAsync(graphServiceClient, searchQuery, AssignableContentTypes, AppendToLog);
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
        /// Views assignment details for selected content items.
        /// </summary>
        private async Task ViewAssignmentsOrchestrator(GraphServiceClient graphServiceClient, List<CustomContentInfo> selectedItems)
        {
            var viewRegistry = GetViewAssignmentRegistry();
            var checkedCount = 0;
            var totalItems = selectedItems.Count;

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
                    LogWarning($"No assignment viewer available for type '{item.ContentType}'. Skipping.");
                    continue;
                }

                try
                {
                    var details = await getDetailsFunc(graphServiceClient, item.ContentId);

                    if (details == null)
                    {
                        LogError($"Failed to retrieve assignments for '{item.ContentName}'.");
                        continue;
                    }

                    if (details.Count == 0)
                    {
                        LogInfo($"'{item.ContentName}' ({item.ContentType}) — No assignments.");
                    }
                    else
                    {
                        LogInfo($"'{item.ContentName}' ({item.ContentType}) — {details.Count} assignment(s):");
                        foreach (var assignment in details)
                        {
                            LogInfo($"  • {assignment}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error retrieving assignments for '{item.ContentName}' ({item.ContentType}): {ex.Message}");
                    continue;
                }
            }

            ShowOperationSuccess($"Checked assignments for {totalItems} item(s)");
        }

        /// <summary>
        /// Removes all assignments from selected content items.
        /// </summary>
        private async Task RemoveAssignmentsOrchestrator(GraphServiceClient graphServiceClient, List<CustomContentInfo> selectedItems)
        {
            _removeProgress.Reset(selectedItems.Count);

            ShowOperationProgress("Removing assignments...", 0, _removeProgress.Total);

            foreach (var definition in GetRemoveAssignmentRegistry())
            {
                var itemsOfType = selectedItems
                    .Where(i => string.Equals(i.ContentType, definition.TypeKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in itemsOfType)
                {
                    _removeProgress.Advance();
                    ShowOperationProgress($"Removing assignments from {definition.DisplayName}", _removeProgress.Current, _removeProgress.Total);

                    if (string.IsNullOrEmpty(item.ContentId))
                    {
                        LogWarning($"Skipping item '{item.ContentName}' with missing ID.");
                        continue;
                    }

                    try
                    {
                        await definition.RemoveAsync(graphServiceClient, item.ContentId);
                        _removeProgress.RecordSuccess();
                        LogSuccess($"Removed assignments from '{item.ContentName}'.");
                    }
                    catch (Exception ex)
                    {
                        _removeProgress.RecordError();
                        LogError($"Error removing assignments from '{item.ContentName}': {ex.Message}");
                    }
                }
            }

            if (_removeProgress.ErrorCount == 0)
            {
                ShowOperationSuccess($"Successfully removed assignments from {_removeProgress.SuccessCount} item(s)");
            }
            else
            {
                ShowOperationError($"Completed with {_removeProgress.ErrorCount} error(s). {_removeProgress.SuccessCount} item(s) processed successfully.");
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
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = InputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                AppendToLog("Please enter a search query.");
                return;
            }
            await SearchOrchestrator(sourceGraphServiceClient, searchQuery);
        }

        private async void ViewAssignmentsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = AssignmentsDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected. Please select one or more items to view their assignments.");
                return;
            }

            await ViewAssignmentsOrchestrator(sourceGraphServiceClient, selectedItems);
        }

        private async void RemoveAssignmentsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = AssignmentsDataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected. Please select one or more items to remove their assignments.");
                return;
            }

            var numberOfItems = selectedItems.Count;

            // Bulk operation safeguard: warn when removing assignments from many items
            if (!await ShowBulkOperationWarningAsync(numberOfItems, "Operation"))
            {
                AppendToLog("Bulk assignment removal cancelled by user.");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Remove Assignments?",
                Content = $"Are you sure you want to remove all group assignments from the {numberOfItems} selected item(s)? This cannot be undone.",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync().AsTask();
            if (result == ContentDialogResult.Primary)
            {
                await RemoveAssignmentsOrchestrator(sourceGraphServiceClient, selectedItems);
            }
            else
            {
                AppendToLog("Assignment removal cancelled by user.");
            }
        }

        #endregion
    }
}
