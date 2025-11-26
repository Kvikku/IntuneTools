using IntuneTools.Graph;
using IntuneTools.Utilities;
using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation; // Added for NavigationEventArgs
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static IntuneTools.Graph.DestinationUserAuthentication;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;
using static IntuneTools.Graph.SourceUserAuthentication;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private Dictionary<string, Dictionary<string, string>>? _sourceTenantSettings;
        private Dictionary<string, Dictionary<string, string>>? _destinationTenantSettings;

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Reflect persisted variables in UI when arriving on the page.
            // If you keep Graph clients alive elsewhere, you can also check those to decide the icon.
            var sourceSignedIn = !string.IsNullOrWhiteSpace(Variables.sourceTenantName);
            var destinationSignedIn = !string.IsNullOrWhiteSpace(Variables.destinationTenantName);

            // Source
            if (SourceLoginStatusText != null)
            {
                SourceLoginStatusText.Text = sourceSignedIn
                    ? $"Signed in: {Variables.sourceTenantName}"
                    : "Not signed in";
            }
            UpdateImage(SourceLoginStatusImage, sourceSignedIn ? "GreenCheck.png" : "RedCross.png");

            // Destination
            if (DestinationLoginStatusText != null)
            {
                DestinationLoginStatusText.Text = destinationSignedIn
                    ? $"Signed in: {Variables.destinationTenantName}"
                    : "Not signed in";
            }
            UpdateImage(DestinationLoginStatusImage, destinationSignedIn ? "GreenCheck.png" : "RedCross.png");
        }

        //private void LoadTenantSettings()
        //{
        //    _sourceTenantSettings = LoadSettingsFromFile(Variables.sourceTenantSettingsFileFullPath);
        //    _destinationTenantSettings = LoadSettingsFromFile(Variables.destinationTenantSettingsFileFullPath);

        //    PopulateComboBox(SourceEnvironmentComboBox, _sourceTenantSettings);
        //    PopulateComboBox(DestinationEnvironmentComboBox, _destinationTenantSettings);

        //    // Populate the login information for source and destination tenants

        //    if (_sourceTenantSettings != null && _sourceTenantSettings.Count > 0)
        //    {
        //        SourceEnvironmentComboBox.SelectedIndex = 0; // Select the first item by default
        //    }

        //    if (sourceGraphServiceClient != null)
        //    {
        //        UpdateImage(SourceLoginStatusImage, "GreenCheck.png");
        //    }
        //    else
        //    {
        //        UpdateImage(SourceLoginStatusImage, "RedCross.png");
        //    }

        //    if (_destinationTenantSettings != null && _destinationTenantSettings.Count > 0)
        //    {
        //        DestinationEnvironmentComboBox.SelectedIndex = 0; // Select the first item by default
        //    }

        //    if (destinationGraphServiceClient != null)
        //    {
        //        UpdateImage(DestinationLoginStatusImage, "GreenCheck.png");
        //    }
        //    else
        //    {
        //        UpdateImage(DestinationLoginStatusImage, "RedCross.png");
        //    }

        //}

        private Dictionary<string, Dictionary<string, string>>? LoadSettingsFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            }
            return null;
        }

        private void PopulateComboBox(ComboBox comboBox, Dictionary<string, Dictionary<string, string>>? settings)
        {
            if (settings != null)
            {
                foreach (var tenantKey in settings.Keys)
                {
                    comboBox.Items.Add(tenantKey);
                }
            }
        }

        //private void SourceEnvironmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    UpdateTenantFields(SourceEnvironmentComboBox, _sourceTenantSettings, SourceTenantIdTextBox, SourceClientIdTextBox);
        //}

        //private void DestinationEnvironmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    UpdateTenantFields(DestinationEnvironmentComboBox, _destinationTenantSettings, DestinationTenantIdTextBox, DestinationClientIdTextBox);
        //}

        private void UpdateTenantFields(ComboBox comboBox, Dictionary<string, Dictionary<string, string>>? settings, TextBox tenantIdTextBox, TextBox clientIdTextBox)
        {
            if (comboBox.SelectedItem is string selectedTenantKey && settings != null && settings.TryGetValue(selectedTenantKey, out var tenantDetails))
            {
                tenantIdTextBox.Text = tenantDetails.TryGetValue("TenantID", out var tenantId) ? tenantId : string.Empty;
                clientIdTextBox.Text = tenantDetails.TryGetValue("ClientID", out var clientId) ? clientId : string.Empty;
            }
            else
            {
                tenantIdTextBox.Text = string.Empty;
                clientIdTextBox.Text = string.Empty;
            }
        }

        private async void SourceLoginButton_Click(object sender, RoutedEventArgs e)
        {
            //await Utilities.HelperClass.ShowMessageBox("Source Tenant Login", "Authenticating to the source tenant. Please wait...","NO");
            await AuthenticateToSourceTenant();
        }

        private async Task AuthenticateToSourceTenant()
        {
            var Client = await SourceUserAuthentication.GetGraphClientAsync();
            if (Client != null)
            {
                sourceGraphServiceClient = Client;
                sourceTenantName = await GetAzureTenantName(Client);
                Variables.sourceTenantName = sourceTenantName ?? string.Empty;

                Log($"Source Tenant Name: {sourceTenantName}");
                UpdateImage(SourceLoginStatusImage, "GreenCheck.png");
                SourceLoginStatusText.Text = $"Signed in: {sourceTenantName}";
            }
            else
            {
                Log("Failed to authenticate to source tenant.");
                UpdateImage(SourceLoginStatusImage, "RedCross.png");
                SourceLoginStatusText.Text = "Not signed in";
                Variables.sourceTenantName = string.Empty;
            }
        }

        private void DestinationLoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Add your logic here for handling the DestinationLoginButton click event.
            // Example:
            AuthenticateToDestinationTenant();
        }

        private async Task AuthenticateToDestinationTenant()
        {
            var client = await DestinationUserAuthentication.GetGraphClientAsync();
            if (client != null)
            {
                destinationGraphServiceClient = client;
                destinationTenantName = await GetAzureTenantName(client);
                Variables.destinationTenantName = destinationTenantName ?? string.Empty;

                Log($"Destination Tenant Name: {destinationTenantName}");
                UpdateImage(DestinationLoginStatusImage, "GreenCheck.png");
                DestinationLoginStatusText.Text = $"Signed in: {destinationTenantName}";
            }
            else
            {
                Log("Failed to authenticate to destination tenant.");
                UpdateImage(DestinationLoginStatusImage, "RedCross.png");
                DestinationLoginStatusText.Text = "Not signed in";
                Variables.destinationTenantName = string.Empty;
            }
        }

        private void OpenLogFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(Variables.logFileFolder))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = Variables.logFileFolder,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
            else
            {
                Log($"Invalid log file folder path: {Variables.logFileFolder}");
            }
        }

        private async void SourceClearTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cleared = await SourceUserAuthentication.ClearSessionAsync();
                if (cleared)
                {
                    sourceGraphServiceClient = null;
                    sourceTenantName = null;
                    Variables.sourceTenantName = string.Empty;

                    UpdateImage(SourceLoginStatusImage, "RedCross.png");
                    SourceLoginStatusText.Text = "Not signed in";
                    Log("Source token/session cleared.");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to clear source token: {ex.Message}");
            }
        }

        private async void DestinationClearTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cleared = await DestinationUserAuthentication.ClearSessionAsync();
                if (cleared)
                {
                    destinationGraphServiceClient = null;
                    destinationTenantName = null;
                    Variables.destinationTenantName = string.Empty;

                    UpdateImage(DestinationLoginStatusImage, "RedCross.png");
                    DestinationLoginStatusText.Text = "Not signed in";
                    Log("Destination token/session cleared.");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to clear destination token: {ex.Message}");
            }
        }
    }
}
