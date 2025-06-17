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

        public static string primaryLogFile = Path.Combine(logFileFolder, timestamp + "_" + logFileName); // Corrected to use logFileFolder
        public static string ImportStatusFileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm}-ImportStatus.log";
        public static string ImportStatusFilePath = Path.Combine(logFileFolder, ImportStatusFileName); // Full path for the import status log file
        public enum LogLevels
        {
            Info,
            Warning,
            Error
        };


        // Graph authentication variables
        public static string sourceTenantName = string.Empty;
        public static string destinationTenantName = string.Empty;



        // Graph API variables

        public static string SelectedFilterID = null;
        public static DeviceAndAppManagementAssignmentFilterType deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;


    }
}
