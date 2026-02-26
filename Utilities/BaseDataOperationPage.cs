using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Base class for pages that perform data operations on content collections.
    /// Extends BaseMultiTenantPage with ContentList management, DataGrid sorting, and generic ID retrieval.
    /// </summary>
    public abstract class BaseDataOperationPage : BaseMultiTenantPage
    {
        /// <summary>
        /// The collection of content items displayed in the DataGrid.
        /// </summary>
        public ObservableCollection<CustomContentInfo> ContentList { get; set; } = new ObservableCollection<CustomContentInfo>();

        /// <summary>
        /// Gets all content IDs of a specific content type from the ContentList.
        /// Replaces the need for individual GetXxxIDs() methods.
        /// </summary>
        /// <param name="contentType">The content type to filter by (e.g., "Settings Catalog", "Device Compliance Policy")</param>
        /// <returns>List of content IDs matching the specified type</returns>
        protected List<string> GetContentIdsByType(string contentType)
        {
            return ContentList
                .Where(c => string.Equals(c.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.ContentId ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }

        /// <summary>
        /// Gets all content IDs for application content types.
        /// Uses UserInterfaceHelper.IsApplicationContentType for matching.
        /// </summary>
        protected List<string> GetApplicationContentIds()
        {
            return ContentList
                .Where(c => UserInterfaceHelper.IsApplicationContentType(c.ContentType))
                .Select(c => c.ContentId ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
        }

        /// <summary>
        /// Checks if the ContentList contains any items of the specified content type.
        /// </summary>
        protected bool HasContentType(string contentType)
        {
            return ContentList.Any(c => string.Equals(c.ContentType, contentType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if the ContentList contains any application content.
        /// </summary>
        protected bool HasApplicationContent()
        {
            return ContentList.Any(c => UserInterfaceHelper.IsApplicationContentType(c.ContentType));
        }

        /// <summary>
        /// Clears the ContentList and rebinds a DataGrid.
        /// </summary>
        protected void ClearContentList(DataGrid? dataGrid = null)
        {
            ContentList.Clear();
            if (dataGrid != null)
            {
                UserInterfaceHelper.RebindDataGrid(dataGrid, ContentList);
            }
            AppendToLog("All items cleared from the list.");
        }

        /// <summary>
        /// Removes selected items from the ContentList.
        /// </summary>
        protected void RemoveSelectedItems(DataGrid dataGrid)
        {
            var selectedItems = dataGrid.SelectedItems?.Cast<CustomContentInfo>().ToList();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                AppendToLog("No items selected to clear.");
                return;
            }

            foreach (var item in selectedItems)
            {
                ContentList.Remove(item);
            }

            UserInterfaceHelper.RebindDataGrid(dataGrid, ContentList);
            AppendToLog($"Cleared {selectedItems.Count} selected item(s) from the list.");
        }

        /// <summary>
        /// Generic DataGrid sorting handler for CustomContentInfo collections.
        /// Wire this to your DataGrid's Sorting event.
        /// </summary>
        protected void HandleDataGridSorting(object sender, DataGridColumnEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null || ContentList == null || ContentList.Count == 0)
                return;

            // Get the property name from the column binding
            var textColumn = e.Column as DataGridTextColumn;
            var binding = textColumn?.Binding as Binding;
            string? sortProperty = binding?.Path?.Path;

            if (string.IsNullOrEmpty(sortProperty))
            {
                AppendToLog("Sorting error: Unable to determine property name from column binding.");
                return;
            }

            // Check if property exists on CustomContentInfo
            var propInfo = typeof(CustomContentInfo).GetProperty(sortProperty);
            if (propInfo == null)
            {
                AppendToLog($"Sorting error: Property '{sortProperty}' not found on CustomContentInfo.");
                return;
            }

            // Toggle sort direction
            ListSortDirection direction;
            if (e.Column.SortDirection.HasValue && e.Column.SortDirection.Value == DataGridSortDirection.Ascending)
            {
                direction = ListSortDirection.Descending;
            }
            else
            {
                direction = ListSortDirection.Ascending;
            }

            // Sort the ContentList in place
            List<CustomContentInfo> sorted;
            try
            {
                if (direction == ListSortDirection.Ascending)
                {
                    sorted = ContentList.OrderBy(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
                else
                {
                    sorted = ContentList.OrderByDescending(x => propInfo.GetValue(x, null) ?? string.Empty).ToList();
                }
            }
            catch (Exception ex)
            {
                AppendToLog($"Sorting error: {ex.Message}");
                return;
            }

            // Update ContentList
            ContentList.Clear();
            foreach (var item in sorted)
            {
                ContentList.Add(item);
            }

            // Update sort direction indicators
            foreach (var col in dataGrid.Columns)
            {
                col.SortDirection = null;
            }
            e.Column.SortDirection = direction == ListSortDirection.Ascending
                ? DataGridSortDirection.Ascending
                : DataGridSortDirection.Descending;
        }
    }

    /// <summary>
    /// Common content type constants to avoid magic strings.
    /// </summary>
    public static class ContentTypes
    {
        public const string SettingsCatalog = "Settings Catalog";
        public const string DeviceCompliancePolicy = "Device Compliance Policy";
        public const string DeviceConfigurationPolicy = "Device Configuration Policy";
        public const string AppleBYODEnrollmentProfile = "Apple BYOD Enrollment Profile";
        public const string AssignmentFilter = "Assignment Filter";
        public const string EntraGroup = "Entra Group";
        public const string PowerShellScript = "PowerShell Script";
        public const string ProactiveRemediation = "Proactive Remediation";
        public const string MacOSShellScript = "MacOS Shell Script";
        public const string WindowsAutoPilotProfile = "Windows AutoPilot Profile";
        public const string WindowsDriverUpdate = "Windows Driver Update";
        public const string WindowsFeatureUpdate = "Windows Feature Update";
        public const string WindowsQualityUpdatePolicy = "Windows Quality Update Policy";
        public const string WindowsQualityUpdateProfile = "Windows Quality Update Profile";
    }
}
