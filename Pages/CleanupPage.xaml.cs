using CommunityToolkit.WinUI.UI.Controls;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
using static IntuneTools.Graph.IntuneHelperClasses.WindowsQualityUpdateProfileHelper;
//using static IntuneTools.Utilities.SourceTenantGraphClient;


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
        };

        #endregion

        #region Constructor & Configuration

        protected override string UnauthenticatedMessage => "You must authenticate with a tenant before using cleanup features.";

        protected override IEnumerable<string> GetManagedControlNames() => new[]
        {
            "InputTextBox", "SearchButton", "ListAllButton", "ClearSelectedButton",
            "ClearAllButton", "DeleteButton", "CleanupDataGrid", "ClearLogButton"
        };

        public CleanupPage()
        {
            InitializeComponent();
            RightClickMenu.AttachDataGridContextMenu(CleanupDataGrid);
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

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            ShowLoading("Loading data from Microsoft Graph...");
            AppendToDetailsRichTextBlock("Starting to load all content. This could take a while...");
            try
            {
                ContentList.Clear();
                await LoadContentTypesAsync(graphServiceClient, SupportedContentTypes, AppendToDetailsRichTextBlock);
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

        private async Task SearchOrchestrator(GraphServiceClient graphServiceClient, string searchQuery)
        {
            ShowLoading("Searching content in Microsoft Graph...");
            AppendToDetailsRichTextBlock($"Searching for content matching '{searchQuery}'. This may take a while...");
            try
            {
                ContentList.Clear();
                await SearchContentTypesAsync(graphServiceClient, searchQuery, SupportedContentTypes, AppendToDetailsRichTextBlock);
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
        ];

        #endregion

        #region Event Handlers

        private void CleanupDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
        {
            HandleDataGridSorting(sender, e);
        }

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

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var numberOfItems = ContentList.Count;

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

        private async void ListAll_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = InputTextBox.Text;
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


