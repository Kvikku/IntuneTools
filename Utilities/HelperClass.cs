using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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

            //CreateAppSettingsFile(); // Ensure app settings file is created


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
            string logEntry = $"{timestamp} - [{level.ToString().ToUpper()}] - {message}";

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

        public static string CreateTimestampedAppFolder()
        {
            var folderName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm");
            var fullPath = Path.Combine(appDataPath, appFolderName, folderName);
            Directory.CreateDirectory(fullPath);
            return fullPath;
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
                    sw.WriteLine($"{DateTime.Now:HH:mm:ss - dd-MM-yyyy} - [{logType.ToString().ToUpper()}] - {data}");
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
            Log($"App Version: {appVersion}", LogLevels.Info);
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

        public static void UpdateImage(Microsoft.UI.Xaml.Controls.Image image, string imageFileName)
        {
            try
            {
                image.Source = new BitmapImage(new Uri("ms-appx:///Assets/" + imageFileName));
            }
            catch (Exception ex)
            {
                Log($"Failed to update image source. Image: {image.Name}, FileName: {imageFileName}, Error: {ex.Message}", LogLevels.Error);
            }
        }

        /// Graph helper methods ///

        public static string TranslatePolicyPlatformName(string platformName)
        {
            // This method translates the platform name to a user-friendly format

            if (string.IsNullOrEmpty(platformName))
            {
                return platformName;
            }

            // Check for substrings to handle variations

            if (platformName.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows";
            }
            if (platformName.Contains("macOS", StringComparison.OrdinalIgnoreCase))
            {
                return "macOS";
            }
            if (platformName.Contains("iOS", StringComparison.OrdinalIgnoreCase))
            {
                return "iOS";
            }
            if (platformName.Contains("Android", StringComparison.OrdinalIgnoreCase))
            {
                return "Android";
            }

            // Fallback to exact matches if no substrings matched
            return platformName switch
            {
                // Windows
                "Windows10" => "Windows",
                "#microsoft.graph.windows10CompliancePolicy" => "Windows",
                "#microsoft.graph.win32LobApp" => "Windows",
                "#microsoft.graph.winGetApp" => "Windows",
                "#microsoft.graph.officeSuiteApp" => "Windows",


                // macOS
                "MacOS" => "macOS",
                "#microsoft.graph.macOSCompliancePolicy" => "macOS",


                // iOS and iPadOS
                "#microsoft.graph.iosCompliancePolicy" => "iOS",
                "iOS" => "iOS",


                // Android
                "Android" => "Android",
                "#microsoft.graph.androidWorkProfileCompliancePolicy" => "Android",
                "#microsoft.graph.androidDeviceOwnerCompliancePolicy" => "Android",

                // Universal
                "#microsoft.graph.webApp" => "Universal",
                _ => platformName // Return the original name if no translation is found
            };
        }

        public static string TranslateApplicationType(string odataType)
        {
            if (string.IsNullOrEmpty(odataType))
            {
                return odataType;
            }

            return odataType switch
            {
                "#microsoft.graph.win32LobApp" => "App - Windows app (Win32)",
                "#microsoft.graph.iosVppApp" => "App - iOS VPP app",
                "#microsoft.graph.winGetApp" => "App - Windows app (WinGet)",
                "#microsoft.graph.iosiPadOSWebClip" => "App - iOS/iPadOS web clip",
                "#microsoft.graph.androidManagedStoreApp" => "App - Android Managed store app",
                "#microsoft.graph.macOSOfficeSuiteApp" => "App - macOS Microsoft 365 Apps",
                "#microsoft.graph.officeSuiteApp" => "App - Windows Microsoft 365 Apps",
                "#microsoft.graph.macOSMicrosoftDefenderApp" => "App - macOS Microsoft Defender for Endpoint",
                "#microsoft.graph.macOSMicrosoftEdgeApp" => "App - macOS Microsoft Edge",
                "#microsoft.graph.windowsMicrosoftEdgeApp" => "App - Windows Microsoft Edge",
                "#microsoft.graph.webApp" => "App - Web link",
                "#microsoft.graph.macOSWebClip" => "App - macOS web clip",
                "#microsoft.graph.windowsWebApp" => "App - Windows web link",
                "#microsoft.graph.androidManagedStoreWebApp" => "App - Android Managed store web link",
                _ => odataType
            };
        }

        public static string TranslateODataTypeFromApplicationType(string applicationType)
        {
            if (string.IsNullOrEmpty(applicationType))
            {
                return applicationType;
            }

            return applicationType switch
            {
                "App - Windows app (Win32)" => "#microsoft.graph.win32LobApp",
                "App - iOS VPP app" => "#microsoft.graph.iosVppApp",
                "App - Windows app (WinGet)" => "#microsoft.graph.winGetApp",
                "App - iOS/iPadOS web clip" => "#microsoft.graph.iosiPadOSWebClip",
                "App - Android Managed store app" => "#microsoft.graph.androidManagedStoreApp",
                "App - macOS Microsoft 365 Apps" => "#microsoft.graph.macOSOfficeSuiteApp",
                "App - Windows Microsoft 365 Apps" => "#microsoft.graph.officeSuiteApp",
                "App - macOS Microsoft Defender for Endpoint" => "#microsoft.graph.macOSMicrosoftDefenderApp",
                "App - macOS Microsoft Edge" => "#microsoft.graph.macOSMicrosoftEdgeApp",
                "App - Windows Microsoft Edge" => "#microsoft.graph.windowsMicrosoftEdgeApp",
                "App - Web link" => "#microsoft.graph.webApp",
                "App - macOS web clip" => "#microsoft.graph.macOSWebClip",
                "App - Windows web link" => "#microsoft.graph.windowsWebApp",
                "App - Android Managed store web link" => "#microsoft.graph.androidManagedStoreWebApp",
                _ => applicationType
            };
        }

        public static void GetWin32AppNotificationValue(string input)
        {
            // Method to get the Win32LobAppNotification enum value based on input string
            win32LobAppNotification = input switch
            {
                "Show all toast notifications" => Win32LobAppNotification.ShowAll,
                "Hide toast notifications and show only reboot" => Win32LobAppNotification.ShowReboot,
                "Hide all toast notifications" => Win32LobAppNotification.HideAll,
                _ => Win32LobAppNotification.ShowAll
            };
        }

        public static void GetDeploymentMode(string input)
        {
            // TODO when fixing exclusion assignments
            _selectedDeploymentMode = input switch
            {

            };
        }

        public static void GetInstallIntent(string input)
        {
            _selectedAppDeploymentIntent = input switch
            {
                "Available" => InstallIntent.Available,
                "Required" => InstallIntent.Required,
                "Uninstall" => InstallIntent.Uninstall,
                _ => InstallIntent.Required
            };
        }

        public static void GetDeliveryOptimizationPriority(string input)
        {
            win32LobAppDeliveryOptimizationPriority = input switch
            {
                "Content download in foreground" => Win32LobAppDeliveryOptimizationPriority.Foreground,
                "Content download in background" => Win32LobAppDeliveryOptimizationPriority.NotConfigured,
                _ => Win32LobAppDeliveryOptimizationPriority.NotConfigured
            };
        }

        public static void GetAndroidManagedStoreAutoUpdateMode(string input)
        {
            _androidManagedStoreAutoUpdateMode = input switch
            {
                "High priority" => AndroidManagedStoreAutoUpdateMode.Priority,
                "Postponed" => AndroidManagedStoreAutoUpdateMode.Postponed,
                _ => AndroidManagedStoreAutoUpdateMode.Default
            };
        }

        public static async Task<string?> GetAzureTenantName(GraphServiceClient graphServiceClient)
        {
            // Method to get the Azure tenant name
            try
            {
                var tenantInfo = await graphServiceClient.Organization.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "displayName" };
                });


                return tenantInfo.Value[0].DisplayName;
            }
            catch (Exception ex)
            {
                return "UNKNOWN"; // Return "UNKNOWN" if the tenant name cannot be retrieved
            }

        }

        /// <summary>
        /// Writes text to a RichTextBlock, either replacing or appending to its content.
        /// </summary>
        /// <param name="richTextBlock">The RichTextBlock to write to.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="append">If true, appends the text; otherwise, replaces all content. Default is true.</param>
        public static void WriteToRichTextBlock(RichTextBlock richTextBlock, string text, bool append = true)
        {
            if (richTextBlock == null)
                return;

            if (!append || richTextBlock.Blocks.Count == 0)
            {
                richTextBlock.Blocks.Clear();
                var paragraph = new Microsoft.UI.Xaml.Documents.Paragraph();
                paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = text });
                richTextBlock.Blocks.Add(paragraph);
            }
            else
            {
                // Append to the first paragraph
                var paragraph = richTextBlock.Blocks.FirstOrDefault() as Microsoft.UI.Xaml.Documents.Paragraph;
                if (paragraph != null)
                {
                    paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = text });
                }
                else
                {
                    // Fallback: create a new paragraph if none exists
                    paragraph = new Microsoft.UI.Xaml.Documents.Paragraph();
                    paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = text });
                    richTextBlock.Blocks.Add(paragraph);
                }
            }
        }

        /// <summary>
        /// Generic async helper to search for policies, map them to content, update a collection, and bind to a UI control.
        /// </summary>
        /// <typeparam name="TPolicy">The type of the policy returned by the search function.</typeparam>
        /// <typeparam name="TContent">The type of the content to be displayed in the UI collection.</typeparam>
        /// <param name="searchFunc">A function that takes a search query and returns a Task of IEnumerable of TPolicy.</param>
        /// <param name="searchQuery">The search query string.</param>
        /// <param name="contentList">The ObservableCollection to update with mapped content.</param>
        /// <param name="mapFunc">A function to map TPolicy to TContent.</param>
        /// <param name="showLoading">Action to show loading UI.</param>
        /// <param name="hideLoading">Action to hide loading UI.</param>
        /// <param name="bindToGrid">Action to bind the collection to the UI control (e.g., DataGrid).</param>
        public static async Task SearchAndBindAsync<TPolicy, TContent>(
            Func<string, Task<IEnumerable<TPolicy>>> searchFunc,
            string searchQuery,
            ObservableCollection<TContent> contentList,
            Func<TPolicy, TContent> mapFunc,
            Action showLoading,
            Action hideLoading,
            Action<IEnumerable<TContent>> bindToGrid)
        {
            showLoading();
            try
            {
                var policies = await searchFunc(searchQuery);
                contentList.Clear();
                foreach (var policy in policies)
                {
                    contentList.Add(mapFunc(policy));
                }
                bindToGrid(contentList);
            }
            finally
            {
                hideLoading();
            }
        }

        /// <summary>
        /// Generic async helper to load all items, map them to content, update a collection, and bind to a UI control.
        /// </summary>
        /// <typeparam name="TPolicy">The type of the policy/item returned by the loader function.</typeparam>
        /// <typeparam name="TContent">The type of the content to be displayed in the UI collection.</typeparam>
        /// <param name="loaderFunc">A function that returns a Task of IEnumerable of TPolicy.</param>
        /// <param name="contentList">The ObservableCollection to update with mapped content.</param>
        /// <param name="mapFunc">A function to map TPolicy to TContent.</param>
        /// <param name="showLoading">Action to show loading UI.</param>
        /// <param name="hideLoading">Action to hide loading UI.</param>
        /// <param name="bindToGrid">Action to bind the collection to the UI control (e.g., DataGrid).</param>
        public static async Task LoadAndBindAsync<TPolicy, TContent>(
            Func<Task<IEnumerable<TPolicy>>> loaderFunc,
            ObservableCollection<TContent> contentList,
            Func<TPolicy, TContent> mapFunc,
            Action showLoading,
            Action hideLoading,
            Action<IEnumerable<TContent>> bindToGrid)
        {
            showLoading();
            try
            {
                var items = await loaderFunc();
                contentList.Clear();
                foreach (var item in items)
                {
                    contentList.Add(mapFunc(item));
                }
                bindToGrid(contentList);
            }
            finally
            {
                hideLoading();
            }
        }
        public static string FindPreFixInPolicyName(string policyName, string newPolicyName)
        {
            if (string.IsNullOrWhiteSpace(policyName))
            {
                return newPolicyName;
            }

            // Trim leading/trailing whitespace from the policy name.
            policyName = policyName.Trim();

            // Clean up double brackets like [[...]] or ((...)) or {{...}}
            if (policyName.StartsWith("[[") && policyName.Contains("]]"))
            {
                int doubleBracketClosingIndex = policyName.IndexOf("]]");
                if (doubleBracketClosingIndex > 1)
                {
                    policyName = policyName.Remove(doubleBracketClosingIndex, 1).Remove(0, 1);
                }
            }
            else if (policyName.StartsWith("((") && policyName.Contains("))"))
            {
                int doubleBracketClosingIndex = policyName.IndexOf("))");
                if (doubleBracketClosingIndex > 1)
                {
                    policyName = policyName.Remove(doubleBracketClosingIndex, 1).Remove(0, 1);
                }
            }
            else if (policyName.StartsWith("{{") && policyName.Contains("}}"))
            {
                int doubleBracketClosingIndex = policyName.IndexOf("}}");
                if (doubleBracketClosingIndex > 1)
                {
                    policyName = policyName.Remove(doubleBracketClosingIndex, 1).Remove(0, 1);
                }
            }

            char firstChar = policyName[0];
            char expectedClosingChar;

            switch (firstChar)
            {
                case '(': expectedClosingChar = ')'; break;
                case '[': expectedClosingChar = ']'; break;
                case '{': expectedClosingChar = '}'; break;
                default:
                    // The policy name does not start with a recognized prefix bracket.
                    // Prepend the new prefix to the original name, ensuring a single space
                    return newPolicyName + " " + policyName.TrimStart();
            }

            int closingIndex = policyName.IndexOf(expectedClosingChar);

            if (closingIndex > 0)
            {
                // Extract the rest of the string after the prefix.
                string restOfName = policyName.Substring(closingIndex + 1).TrimStart();
                // Return the new name combined with the rest of the original name, ensuring a single space
                return newPolicyName + " " + restOfName;
            }

            // If no valid closing bracket is found, prepend the new prefix, ensuring a single space
            return newPolicyName + " " + policyName.TrimStart();
        }
    }
}
