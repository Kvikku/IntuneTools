using Microsoft.Graph.Beta.Models;
using Microsoft.Identity.Client;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using static IntuneTools.Utilities.Variables;
using static IntuneTools.Utilities.HelperClass;

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
                Directory.CreateDirectory(logFileFolder);
                Directory.CreateDirectory(appSettingsFolder);
                
            }

            CreateAppSettingsFile(); // Ensure app settings file is created


            // Create a new log file with date and time appended to the name

            File.Create(primaryLogFile).Close();
        }

        public static void CreateImportStatusFile()
        {
            // Create a new import status file with date and time appended to the name
            File.Create(ImportStatusFilePath).Close();
        }

        public static void LogToImportStatusFile(string message, LogLevels level = LogLevels.Info)
        {
            // Create a timestamp
            string timestamp = DateTime.Now.ToString("HH:mm:ss - dd-MM-yyyy");
            string logEntry = $"{timestamp} - [{level}] - {message}";

            // Append the log entry to the file
            try
            {
                using (StreamWriter writer = new StreamWriter(ImportStatusFilePath, true))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to import status file: {ex.Message}");
            }
        }

        public static void CreateAppSettingsFile()
        {
            try
            {
                // Ensure the app settings directory exists
                if (!Directory.Exists(appSettingsFolder))
                {
                    Directory.CreateDirectory(appSettingsFolder);
                    Log($"App settings directory created at {appSettingsFolder}", LogLevels.Info);
                }

                // Default content for the settings files
                var appSettingsContent = new Dictionary<string, Dictionary<string, string>>
                {
                    ["Tenant #1"] = new Dictionary<string, string>
                    {
                        ["TenantID"] = "ABC123", // Placeholder
                        ["ClientID"] = "ABC123"  // Placeholder
                    },
                    ["Tenant #2"] = new Dictionary<string, string>
                    {
                        ["TenantID"] = "ABC123", // Placeholder
                        ["ClientID"] = "ABC123"  // Placeholder
                    }
                };
                var options = new JsonSerializerOptions { WriteIndented = true };
                // Serialize the content once to be reused
                string jsonString = JsonSerializer.Serialize(appSettingsContent, options);

                // Helper action to create/populate a specific settings file
                Action<string, string> createSpecificFileIfNeeded = (filePath, description) =>
                {
                    try
                    {
                        bool createFile = false;
                        if (!File.Exists(filePath))
                        {
                            createFile = true;
                        }
                        else
                        {
                            FileInfo fileInfo = new FileInfo(filePath);
                            if (fileInfo.Length == 0)
                            {
                                createFile = true;
                            }
                        }

                        if (createFile)
                        {
                            File.WriteAllText(filePath, jsonString);
                            Log($"{description} settings file created/populated at {filePath}", LogLevels.Info);
                        }
                        else
                        {
                            Log($"{description} settings file already exists at {filePath} and contains data. No changes made.", LogLevels.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error processing {description} settings file {filePath}: {ex.Message}", LogLevels.Error);
                        // Optionally, rethrow or handle more gracefully depending on application requirements
                        Console.WriteLine($"Error processing {description} settings file {filePath}: {ex.Message}");
                    }
                };

                // Process source tenant settings file
                createSpecificFileIfNeeded(sourceTenantSettingsFileFullPath, "Source tenant");

                // Process destination tenant settings file
                createSpecificFileIfNeeded(destinationTenantSettingsFileFullPath, "Destination tenant");
            }
            catch (Exception ex) // Catch errors from directory creation or other general issues not caught by the helper
            {
                Log($"Overall error in CreateAppSettingsFile: {ex.Message}", LogLevels.Error);
                // Optionally, rethrow or handle more gracefully depending on application requirements
                Console.WriteLine($"Error in CreateAppSettingsFile method: {ex.Message}");
            }
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

        public enum LogType
        {
            Info,
            Warning,
            Error
        }
        public static void WriteToImportStatusFile(string data, LogType logType = LogType.Info)
        {
            try
            {
                // Use the using statement to ensure proper disposal of StreamWriter
                using (StreamWriter sw = new StreamWriter(ImportStatusFilePath, true))
                {
                    // Write the data to the import status file with log type
                    sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logType}] - {data}");
                }
                // StreamWriter is automatically closed and disposed of when leaving the using block
            }
            catch (IOException ex)
            {
                // Handle the exception
                
            }
        }

        public static void LogApplicationStart()
        {
            // Log the application start time
            Log("Application started", LogLevels.Info);

            // Log the machine name
            Log($"Machine Name: {Environment.MachineName}", LogLevels.Info);

            // Log the user name
            Log($"User Name: {Environment.UserName}", LogLevels.Info);

            // Log the OS version
            Log($"OS Version: {Environment.OSVersion}", LogLevels.Info);

            // Log the .NET version
            Log($".NET Version: {Environment.Version}", LogLevels.Info);

            // Log the CPU name
            Log($"CPU Name: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")}", LogLevels.Info);

            // Log the system's processor count
            Log($"Processor Count: {Environment.ProcessorCount}", LogLevels.Info);

            // Log the system's memory usage
            Log($"Memory Usage: {GC.GetTotalMemory(false)} bytes", LogLevels.Info);

            // Log this app version
            Log($"App Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}", LogLevels.Info);
        }

        public static async Task ShowMessageBox(string title, string message, string primaryButtonText = "OK")
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryButtonText,
                XamlRoot = App.MainWindowInstance?.Content.XamlRoot // Use the XamlRoot from the main window.
            };

            if (dialog.XamlRoot != null)
            {
                await dialog.ShowAsync();
            }
            else
            {
                // Fallback or error handling if XamlRoot is not available (e.g., log, throw)
                Log("XamlRoot is null, cannot display ContentDialog.", LogLevels.Error);
                // Consider a non-UI fallback if critical, e.g., writing to console or a log file.
                Console.WriteLine($"Error: XamlRoot is null. Dialog Title: {title}, Message: {message}");
            }
        }

        /// Graph helper methods ///


        public static string TranslatePolicyPlatformName(string platformName)
        {
            // This method translates the platform name to a user-friendly format
            // Add more translations as needed
            return platformName switch
            {
                "Windows10" => "Windows",
                "MacOS" => "macOS",
                "#microsoft.graph.iosCompliancePolicy" => "iOS",
                "iOS" => "iOS",
                "Android" => "Android",
                _ => platformName // Return the original name if no translation is found
            };
        }
    }
}
