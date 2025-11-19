using Microsoft.Graph.Beta.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntuneTools.Utilities
{
    public class Variables
    {
        
        // Log file
        public static string appDataPath = @"C:\ProgramData\";
        public static string appFolderName = "IntuneTools";
        public static string logFileDirectoryName = "Logs";
        public static string appSettingsDirectoryName = "Settings";

        public static string appDataFolder = Path.Combine(appDataPath, appFolderName);
        public static string logFileFolder = Path.Combine(appDataPath, appFolderName, logFileDirectoryName);
        public static string appSettingsFolder = Path.Combine(appDataPath, appFolderName, appSettingsDirectoryName);

        public static string timestamp = DateTime.Now.ToString("HH-mm-ss_dd-MM-yyyy");
        public static string logFileName = "IntuneTools.log";
        
        // Specific settings files - now using full paths
        public static string sourceTenantSettingsFileFullPath = Path.Combine(appSettingsFolder, "SourceTenantSettings.json");
        public static string destinationTenantSettingsFileFullPath = Path.Combine(appSettingsFolder, "DestinationTenantSettings.json");

        // Generic appSettingsFileName and appSettingsFile might need review for their purpose now.
        // For now, they are kept as is, but their usage should be clarified or refactored
        // if they are not intended for a different purpose than the specific tenant settings.
        public static string appSettingsFileName = "AppSettings.json"; // General settings file name
        public static string appSettingsFile = Path.Combine(appSettingsFolder, appSettingsFileName); // Full path for the general settings file

        public static string primaryLogFile = Path.Combine(logFileFolder, $"{DateTime.Now:yyyy-MM-dd}-" + logFileName); // Corrected to use logFileFolder
        public static string ImportStatusFileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm}-ImportStatus.log";
        public static string ImportStatusFilePath = Path.Combine(logFileFolder, ImportStatusFileName); // Full path for the import status log file
        public enum LogLevels
        {
            Info,
            Warning,
            Error
        };

        // Group variables
        public static bool IsGroupSelected = false;
        public static string SelectedGroupID = null;
        public static string SelectedGroupName = null;
        public static Dictionary<string, string> groupNameAndID = new Dictionary<string, string>();
        public static Dictionary<string, string> selectedGroupNameAndID = new Dictionary<string, string>();
        public static string allUsersVirtualGroupID = "acacacac-9df4-4c7d-9d50-4ef0226f57a9"; // Virtual Group ID for "All Users"
        public static string allDevicesVirtualGroupID = "adadadad-808e-44e2-905a-0b7873a8a531"; // Virtual Group ID for "All Devices"

        // Graph authentication variables
        public static string sourceTenantName = string.Empty;
        public static string destinationTenantName = string.Empty;
        public static string sourceTenantID = string.Empty;
        public static string destinationTenantID = string.Empty;
        public static string sourceClientID = string.Empty;
        public static string destinationClientID = string.Empty;


        // Filter variables for Graph API
        public static bool IsFilterSelected = false;
        public static string SelectedFilterID = null;
        public static DeviceAndAppManagementAssignmentFilterType deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
        public static Dictionary<string, string> filterNameAndID = new Dictionary<string, string>();
        public static string SelectedFilterName = "";

        //public static Microsoft.Graph.Beta.GraphServiceClient? sourceGraphServiceClient;
        //public static Microsoft.Graph.Beta.GraphServiceClient? destinationGraphServiceClient;
    }
}
