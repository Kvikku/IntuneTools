using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace IntuneTools.Utilities
{
    public class Variables
    {
        
        // Log file
        public static string appDataPath = @"C:\ProgramData\";
        public static string appFolderName = "IntuneTools";
        public static string appDataFolder = Path.Combine(appDataPath, appFolderName);
        public static string timestamp = DateTime.Now.ToString("HH-mm-ss_dd-MM-yyyy");
        public static string logFileName = "IntuneTools.log";
        public static string appSettingsFileName = "AppSettings.json";
        public static string primaryLogFile = appDataPath + appFolderName + "\\" + timestamp + "_" + logFileName;
        public static string appSettingsFile = Path.Combine(appDataPath, appFolderName, appSettingsFileName);


        public enum LogLevels
        {
            Info,
            Warning,
            Error
        };
    }
}
