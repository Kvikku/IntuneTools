using System;
using System.Collections.Generic;
using System.IO;

namespace IntuneTools.Utilities
{
    public class Variables
    {
        // Application version (consider retrieving this dynamically from assembly info in the future)
        public static readonly string appVersion = "1.2.0.0";
        
        // Log file
        public static readonly string appDataPath = @"C:\ProgramData\";
        public static readonly string appFolderName = "InToolz";
        public static string timestampedAppFolder = string.Empty; // Mutable, so keep as is or consider property


        public enum LogLevels
        {
            Info,
            Warning,
            Error
        };

        // Use an enum for clarity and keep integer mapping stable with ComboBox order.
        public enum RenameMode
        {
            Prefix = 0,
            Suffix = 2,
            Description = 1
        }

        public enum appFunction
        {
            Main, // Used for most logging operations for the time being
            Summary, // Used to log system settings upon app launch
            Import,
            Assignment,
            Rename, 
            Delete,
        }

        public static string selectedRenameMode = "Prefix"; // Default rename mode

        // Group variables
        public static bool IsGroupSelected = false;
        public static string SelectedGroupID = null;
        public static string SelectedGroupName = null;
        public static Dictionary<string, string> groupNameAndID = new Dictionary<string, string>();
        public static Dictionary<string, string> selectedGroupNameAndID = new Dictionary<string, string>();
        public static readonly string allUsersVirtualGroupID = "acacacac-9df4-4c7d-9d50-4ef0226f57a9"; // Virtual Group ID for "All Users"
        public static readonly string allDevicesVirtualGroupID = "adadadad-808e-44e2-905a-0b7873a8a531"; // Virtual Group ID for "All Devices"

        // Graph authentication variables
        public static string sourceTenantName = string.Empty;
        public static string destinationTenantName = string.Empty;
        public static string sourceTenantID = string.Empty;
        public static string destinationTenantID = string.Empty;
        public static string sourceClientID = string.Empty;
        public static string destinationClientID = string.Empty;


        // Filter variables for Graph API
        public static bool IsFilterSelected = false;
        public static string SelectedFilterID = string.Empty;
        public static DeviceAndAppManagementAssignmentFilterType deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
        public static Dictionary<string, string> filterNameAndID = new Dictionary<string, string>();
        public static string SelectedFilterName = "";


        // Generic App Deployment Options
        public static string _selectedDeploymentMode = string.Empty;
        public static string _selectedIntent = string.Empty;
        public static InstallIntent _selectedInstallIntent;
        public static InstallIntent _selectedAppDeploymentIntent;

        // Windows specific

        public static string _selectedNotificationSetting = string.Empty;
        public static string _selectedDeliveryOptimizationPriority = string.Empty;
        public static Win32LobAppNotification win32LobAppNotification;
        public static Win32LobAppDeliveryOptimizationPriority win32LobAppDeliveryOptimizationPriority;

        // Android specific

        public static string _selectedAndroidManagedStoreAutoUpdateMode = string.Empty;
        public static AndroidManagedStoreAutoUpdateMode _androidManagedStoreAutoUpdateMode;

        // iOS specific
        public static string _licensingType = string.Empty;
        public static string _deviceRemovalAction = string.Empty;
        public static string _removable = string.Empty;
        public static string _preventManagedAppBackup = string.Empty;
        public static string _preventAutoUpdate = string.Empty;
        public static IosVppAppAssignmentSettings iOSAppDeploymentSettings;

    }
}
