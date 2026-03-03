using IntuneTools.Utilities;

namespace IntuneTools.Tests.Utilities
{
    public class HelperClassTests
    {
        #region TranslatePolicyPlatformName Tests

        [Theory]
        [InlineData("Windows10", "Windows")]
        [InlineData("#microsoft.graph.windows10CompliancePolicy", "Windows")]
        [InlineData("#microsoft.graph.win32LobApp", "Windows")]
        [InlineData("#microsoft.graph.winGetApp", "Windows")]
        [InlineData("#microsoft.graph.officeSuiteApp", "Windows")]
        [InlineData("MacOS", "macOS")]
        [InlineData("#microsoft.graph.macOSCompliancePolicy", "macOS")]
        [InlineData("iOS", "iOS")]
        [InlineData("#microsoft.graph.iosCompliancePolicy", "iOS")]
        [InlineData("Android", "Android")]
        [InlineData("#microsoft.graph.androidWorkProfileCompliancePolicy", "Android")]
        [InlineData("#microsoft.graph.androidDeviceOwnerCompliancePolicy", "Android")]
        [InlineData("#microsoft.graph.webApp", "Universal")]
        public void TranslatePolicyPlatformName_KnownPlatforms_ReturnsExpected(string input, string expected)
        {
            var result = HelperClass.TranslatePolicyPlatformName(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("SomeWindowsVariant", "Windows")]
        [InlineData("windows_test", "Windows")]
        [InlineData("macOS_device", "macOS")]
        [InlineData("iOS_device", "iOS")]
        [InlineData("AndroidEnterprise", "Android")]
        public void TranslatePolicyPlatformName_ContainsSubstring_ReturnsExpected(string input, string expected)
        {
            var result = HelperClass.TranslatePolicyPlatformName(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TranslatePolicyPlatformName_NullInput_ReturnsNull()
        {
            var result = HelperClass.TranslatePolicyPlatformName(null);
            Assert.Null(result);
        }

        [Fact]
        public void TranslatePolicyPlatformName_EmptyInput_ReturnsEmpty()
        {
            var result = HelperClass.TranslatePolicyPlatformName(string.Empty);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TranslatePolicyPlatformName_UnknownPlatform_ReturnsOriginal()
        {
            const string unknown = "LinuxPolicy";
            var result = HelperClass.TranslatePolicyPlatformName(unknown);
            Assert.Equal(unknown, result);
        }

        #endregion

        #region TranslateApplicationType Tests

        [Theory]
        [InlineData("#microsoft.graph.win32LobApp", "App - Windows app (Win32)")]
        [InlineData("#microsoft.graph.iosVppApp", "App - iOS VPP app")]
        [InlineData("#microsoft.graph.winGetApp", "App - Windows app (WinGet)")]
        [InlineData("#microsoft.graph.iosiPadOSWebClip", "App - iOS/iPadOS web clip")]
        [InlineData("#microsoft.graph.androidManagedStoreApp", "App - Android Managed store app")]
        [InlineData("#microsoft.graph.macOSOfficeSuiteApp", "App - macOS Microsoft 365 Apps")]
        [InlineData("#microsoft.graph.officeSuiteApp", "App - Windows Microsoft 365 Apps")]
        [InlineData("#microsoft.graph.macOSMicrosoftDefenderApp", "App - macOS Microsoft Defender for Endpoint")]
        [InlineData("#microsoft.graph.macOSMicrosoftEdgeApp", "App - macOS Microsoft Edge")]
        [InlineData("#microsoft.graph.windowsMicrosoftEdgeApp", "App - Windows Microsoft Edge")]
        [InlineData("#microsoft.graph.webApp", "App - Web link")]
        [InlineData("#microsoft.graph.macOSWebClip", "App - macOS web clip")]
        [InlineData("#microsoft.graph.windowsWebApp", "App - Windows web link")]
        [InlineData("#microsoft.graph.androidManagedStoreWebApp", "App - Android Managed store web link")]
        public void TranslateApplicationType_KnownTypes_ReturnsExpected(string odataType, string expected)
        {
            var result = HelperClass.TranslateApplicationType(odataType);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TranslateApplicationType_NullInput_ReturnsNull()
        {
            var result = HelperClass.TranslateApplicationType(null);
            Assert.Null(result);
        }

        [Fact]
        public void TranslateApplicationType_EmptyInput_ReturnsEmpty()
        {
            var result = HelperClass.TranslateApplicationType(string.Empty);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TranslateApplicationType_UnknownType_ReturnsOriginal()
        {
            const string unknown = "#microsoft.graph.unknownApp";
            var result = HelperClass.TranslateApplicationType(unknown);
            Assert.Equal(unknown, result);
        }

        #endregion

        #region TranslateODataTypeFromApplicationType Tests

        [Theory]
        [InlineData("App - Windows app (Win32)", "#microsoft.graph.win32LobApp")]
        [InlineData("App - iOS VPP app", "#microsoft.graph.iosVppApp")]
        [InlineData("App - Windows app (WinGet)", "#microsoft.graph.winGetApp")]
        [InlineData("App - iOS/iPadOS web clip", "#microsoft.graph.iosiPadOSWebClip")]
        [InlineData("App - Android Managed store app", "#microsoft.graph.androidManagedStoreApp")]
        [InlineData("App - macOS Microsoft 365 Apps", "#microsoft.graph.macOSOfficeSuiteApp")]
        [InlineData("App - Windows Microsoft 365 Apps", "#microsoft.graph.officeSuiteApp")]
        [InlineData("App - macOS Microsoft Defender for Endpoint", "#microsoft.graph.macOSMicrosoftDefenderApp")]
        [InlineData("App - macOS Microsoft Edge", "#microsoft.graph.macOSMicrosoftEdgeApp")]
        [InlineData("App - Windows Microsoft Edge", "#microsoft.graph.windowsMicrosoftEdgeApp")]
        [InlineData("App - Web link", "#microsoft.graph.webApp")]
        [InlineData("App - macOS web clip", "#microsoft.graph.macOSWebClip")]
        [InlineData("App - Windows web link", "#microsoft.graph.windowsWebApp")]
        [InlineData("App - Android Managed store web link", "#microsoft.graph.androidManagedStoreWebApp")]
        public void TranslateODataTypeFromApplicationType_KnownTypes_ReturnsExpected(string appType, string expected)
        {
            var result = HelperClass.TranslateODataTypeFromApplicationType(appType);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TranslateODataTypeFromApplicationType_NullInput_ReturnsNull()
        {
            var result = HelperClass.TranslateODataTypeFromApplicationType(null);
            Assert.Null(result);
        }

        [Fact]
        public void TranslateODataTypeFromApplicationType_EmptyInput_ReturnsEmpty()
        {
            var result = HelperClass.TranslateODataTypeFromApplicationType(string.Empty);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TranslateODataTypeFromApplicationType_UnknownType_ReturnsOriginal()
        {
            const string unknown = "App - Unknown type";
            var result = HelperClass.TranslateODataTypeFromApplicationType(unknown);
            Assert.Equal(unknown, result);
        }

        #endregion

        #region Roundtrip Tests

        [Theory]
        [InlineData("#microsoft.graph.win32LobApp")]
        [InlineData("#microsoft.graph.iosVppApp")]
        [InlineData("#microsoft.graph.winGetApp")]
        [InlineData("#microsoft.graph.webApp")]
        [InlineData("#microsoft.graph.macOSWebClip")]
        public void TranslateApplicationType_Roundtrip_ReturnsOriginal(string odataType)
        {
            var friendly = HelperClass.TranslateApplicationType(odataType);
            var roundtripped = HelperClass.TranslateODataTypeFromApplicationType(friendly);
            Assert.Equal(odataType, roundtripped);
        }

        #endregion

        #region GetInstallIntent Tests

        [Theory]
        [InlineData("Available", Microsoft.Graph.Beta.Models.InstallIntent.Available)]
        [InlineData("Required", Microsoft.Graph.Beta.Models.InstallIntent.Required)]
        [InlineData("Uninstall", Microsoft.Graph.Beta.Models.InstallIntent.Uninstall)]
        public void GetInstallIntent_KnownValues_SetsCorrectEnum(string input, Microsoft.Graph.Beta.Models.InstallIntent expected)
        {
            HelperClass.GetInstallIntent(input);
            Assert.Equal(expected, Variables._selectedAppDeploymentIntent);
        }

        [Fact]
        public void GetInstallIntent_UnknownValue_DefaultsToRequired()
        {
            HelperClass.GetInstallIntent("SomethingElse");
            Assert.Equal(Microsoft.Graph.Beta.Models.InstallIntent.Required, Variables._selectedAppDeploymentIntent);
        }

        #endregion

        #region GetWin32AppNotificationValue Tests

        [Theory]
        [InlineData("Show all toast notifications", Microsoft.Graph.Beta.Models.Win32LobAppNotification.ShowAll)]
        [InlineData("Hide toast notifications and show only reboot", Microsoft.Graph.Beta.Models.Win32LobAppNotification.ShowReboot)]
        [InlineData("Hide all toast notifications", Microsoft.Graph.Beta.Models.Win32LobAppNotification.HideAll)]
        public void GetWin32AppNotificationValue_KnownValues_SetsCorrectEnum(string input, Microsoft.Graph.Beta.Models.Win32LobAppNotification expected)
        {
            HelperClass.GetWin32AppNotificationValue(input);
            Assert.Equal(expected, Variables.win32LobAppNotification);
        }

        [Fact]
        public void GetWin32AppNotificationValue_UnknownValue_DefaultsToShowAll()
        {
            HelperClass.GetWin32AppNotificationValue("Unknown");
            Assert.Equal(Microsoft.Graph.Beta.Models.Win32LobAppNotification.ShowAll, Variables.win32LobAppNotification);
        }

        #endregion

        #region GetDeliveryOptimizationPriority Tests

        [Fact]
        public void GetDeliveryOptimizationPriority_Foreground_SetsForeground()
        {
            HelperClass.GetDeliveryOptimizationPriority("Content download in foreground");
            Assert.Equal(Microsoft.Graph.Beta.Models.Win32LobAppDeliveryOptimizationPriority.Foreground, Variables.win32LobAppDeliveryOptimizationPriority);
        }

        [Fact]
        public void GetDeliveryOptimizationPriority_Background_SetsNotConfigured()
        {
            HelperClass.GetDeliveryOptimizationPriority("Content download in background");
            Assert.Equal(Microsoft.Graph.Beta.Models.Win32LobAppDeliveryOptimizationPriority.NotConfigured, Variables.win32LobAppDeliveryOptimizationPriority);
        }

        [Fact]
        public void GetDeliveryOptimizationPriority_Unknown_DefaultsToNotConfigured()
        {
            HelperClass.GetDeliveryOptimizationPriority("Something else");
            Assert.Equal(Microsoft.Graph.Beta.Models.Win32LobAppDeliveryOptimizationPriority.NotConfigured, Variables.win32LobAppDeliveryOptimizationPriority);
        }

        #endregion

        #region GetAndroidManagedStoreAutoUpdateMode Tests

        [Theory]
        [InlineData("High priority", Microsoft.Graph.Beta.Models.AndroidManagedStoreAutoUpdateMode.Priority)]
        [InlineData("Postponed", Microsoft.Graph.Beta.Models.AndroidManagedStoreAutoUpdateMode.Postponed)]
        public void GetAndroidManagedStoreAutoUpdateMode_KnownValues_SetsCorrectEnum(string input, Microsoft.Graph.Beta.Models.AndroidManagedStoreAutoUpdateMode expected)
        {
            HelperClass.GetAndroidManagedStoreAutoUpdateMode(input);
            Assert.Equal(expected, Variables._androidManagedStoreAutoUpdateMode);
        }

        [Fact]
        public void GetAndroidManagedStoreAutoUpdateMode_UnknownValue_DefaultsToDefault()
        {
            HelperClass.GetAndroidManagedStoreAutoUpdateMode("Unknown");
            Assert.Equal(Microsoft.Graph.Beta.Models.AndroidManagedStoreAutoUpdateMode.Default, Variables._androidManagedStoreAutoUpdateMode);
        }

        #endregion
    }
}
