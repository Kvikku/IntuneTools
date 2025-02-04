using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Utilities
{
    public class HelperClass
    {
        public static void CreateLogFile()
        {
            // This method will be used to create the log file if it does not exist

            // Check if the log file directory exists
            if (!Directory.Exists(appDataFolder))
            {
                // If the directory does not exist, create it
                Directory.CreateDirectory(appDataFolder);
            }

            // Create a new log file with date and time appended to the name

            File.Create(primaryLogFile).Close();
        }

        public static void Log(string message, LogLevels level = LogLevels.Info)
        {
            // Create a timestamp
            string timestamp = DateTime.Now.ToString("HH:mm:ss - dd-MM-yyyy");
            string logEntry = $"{timestamp} - [{level}] - {message}";

            // Append the log entry to the file
            try
            {
                using (StreamWriter writer = new StreamWriter(primaryLogFile, true))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }


    }
}
