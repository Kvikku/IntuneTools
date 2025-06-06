using IntuneTools.Utilities;
using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.SourceTenantGraphClient;

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
            LoadTenantSettings();
        }

        private void LoadTenantSettings()
        {
            _sourceTenantSettings = LoadSettingsFromFile(Variables.sourceTenantSettingsFileFullPath);
            _destinationTenantSettings = LoadSettingsFromFile(Variables.destinationTenantSettingsFileFullPath);

            PopulateComboBox(SourceEnvironmentComboBox, _sourceTenantSettings);
            PopulateComboBox(DestinationEnvironmentComboBox, _destinationTenantSettings);
        }

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

        private void SourceEnvironmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTenantFields(SourceEnvironmentComboBox, _sourceTenantSettings, SourceTenantIdTextBox, SourceClientIdTextBox);
        }

        private void DestinationEnvironmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTenantFields(DestinationEnvironmentComboBox, _destinationTenantSettings, DestinationTenantIdTextBox, DestinationClientIdTextBox);
        }

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
            SourceTenantGraphClient.sourceClientID = SourceClientIdTextBox.Text;
            SourceTenantGraphClient.sourceTenantID = SourceTenantIdTextBox.Text;
            SourceTenantGraphClient.sourceAccessToken = null;
            SourceTenantGraphClient.sourceAuthority = $"https://login.microsoftonline.com/{SourceTenantGraphClient.sourceTenantID}";
            var client = await SourceTenantGraphClient.GetSourceGraphClient();
            sourceGraphServiceClient = client;
            await ShowMessageBox("Login info", client.Organization.ToString(), "OK");

        }

        private void DestinationLoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Add your logic here for handling the DestinationLoginButton click event.
            // Example:
            AuthenticateToDestinationTenant();
        }

        private async Task AuthenticateToDestinationTenant()
        {
            // Example logic for authentication
            await Task.Run(() =>
            {

            });
        }
    }
}
