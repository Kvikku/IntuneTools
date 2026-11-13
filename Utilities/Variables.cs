using System.Collections.Concurrent;
using System.Reflection;

namespace InToolz.Utilities
{
    public class Variables
    {
        // Application version derived from assembly metadata (set via <Version> in csproj)
        public static readonly string appVersion = GetAppVersion();

        private static string GetAppVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                // Strip build metadata, e.g. "1.2.0.0+commitsha" → "1.2.0.0"
                var plusIdx = info.IndexOf('+');
                var clean = plusIdx >= 0 ? info[..plusIdx] : info;

                // Ensure consistent 4-component format (e.g. "1.2.0" → "1.2.0.0")
                if (Version.TryParse(clean, out var parsed))
                {
                    return $"{parsed.Major}.{parsed.Minor}.{Math.Max(0, parsed.Build)}.{Math.Max(0, parsed.Revision)}";
                }

                return clean;
            }

            var version = asm.GetName().Version;
            return version is null
                ? "0.0.0.0"
                : $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        // Log file – use per-user local app data to avoid exposing logs to other local users.
        public static readonly string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static readonly string appFolderName = "InToolz";
        public static string timestampedAppFolder = string.Empty; // Mutable, so keep as is or consider property


        // The Renaming page's ComboBox items carry these names as their Tag (RenameRules.TryParseMode),
        // so the numbers and the order here no longer have to match the XAML.
        public enum RenameMode
        {
            Prefix = 0,
            Suffix = 1,
            FindAndReplace = 2,
            Description = 3,
            RemovePrefix = 4,
            RemoveSuffix = 5,
            RemoveDescription = 6
        }

        public enum appFunction
        {
            Main,           // General / page-level messages
            Summary,        // System info logged at app startup
            Import,         // Import operations
            Assignment,     // Assignment operations
            Rename,         // Rename operations
            Delete,         // Delete operations
            FindUnassigned, // Find Unassigned scan
            FindDuplicates, // Duplicate detection scan
            FindStaleDevices, // Stale device scan
            JsonExport,     // JSON export/import operations
            AuditLog,       // Audit log operations
            ManageAssignment,
        }

        public static string selectedRenameMode = "Prefix"; // Default rename mode
        public static string selectedRemovePrefixString = string.Empty; // Freeform prefix to remove (empty = bracket auto-detect)
        public static string selectedRemoveSuffixString = string.Empty; // Suffix to strip from the end of names
        public static string selectedFindString = string.Empty;         // Find & Replace: text to search for
        public static string selectedReplaceString = string.Empty;      // Find & Replace: replacement text (empty = delete)

        // Group variables
        public static bool IsGroupSelected = false;
        public static Dictionary<string, string> groupNameAndID = new Dictionary<string, string>();
        public static ConcurrentDictionary<string, string> groupIDAndName = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> selectedGroupNameAndID = new Dictionary<string, string>();
        public static readonly string allUsersVirtualGroupID = "acacacac-9df4-4c7d-9d50-4ef0226f57a9"; // Virtual Group ID for "All Users"
        public static readonly string allDevicesVirtualGroupID = "adadadad-808e-44e2-905a-0b7873a8a531"; // Virtual Group ID for "All Devices"

        // Graph authentication variables
        public static string sourceTenantName = string.Empty;
        public static string destinationTenantName = string.Empty;

        // Raised whenever sign-in, sign-out, tenant swap, or Demo Mode changes the tenant
        // state above, so shell-level UI (the top bar's tenant pills, the dashboard) can
        // refresh reactively instead of polling.
        public static event Action? TenantStateChanged;
        public static void RaiseTenantStateChanged() => TenantStateChanged?.Invoke();

        /// <summary>
        /// Swaps the source and destination tenant's full auth state (GraphServiceClient,
        /// token provider, signed-in account) and tenant display names. Shared by
        /// SettingsPage's "Swap Tenants" button and ImportPage's tenant-flow swap button so
        /// both call the same logic instead of duplicating it. Page-local state (e.g.
        /// SettingsPage's permission-warning flags) is swapped separately by the caller.
        /// </summary>
        public static void SwapSourceAndDestinationTenants()
        {
            SwapAuthInstances();

            (sourceTenantName, destinationTenantName) = (destinationTenantName, sourceTenantName);

            AppLogger.Info($"Swapped tenants. Source is now '{sourceTenantName}', Destination is now '{destinationTenantName}'.", appFunction.Main);

            RaiseTenantStateChanged();
        }


        // Filter variables for Graph API
        public static bool IsFilterSelected = false;
        public static string? SelectedFilterID = string.Empty; // null when the user clears the filter selection
        public static DeviceAndAppManagementAssignmentFilterType deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
        public static Dictionary<string, string> filterNameAndID = new Dictionary<string, string>();
        public static string SelectedFilterName = "";


        // Generic App Deployment Options
        public static string _selectedIntent = string.Empty;
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
        public static IosVppAppAssignmentSettings? iOSAppDeploymentSettings; // null until the deployment options dialog is confirmed


        // Time saved variables
        public static int totalTimeSavedInSeconds = 0;
        public static readonly int secondsSavedOnAssignments = 30;
        public static readonly int secondsSavedOnRenaming = 20;
        public static readonly int secondsSavedOnDeleting = 10;
        public static readonly int secondsSavedOnImporting = 90;
        public static readonly int secondsSavedOnFindingUnassigned = 30;
        public static readonly int secondsSavedOnJsonExport = 60;
        public static readonly int secondsSavedOnAuditLog = 45;
        public static readonly int secondsSavedOnManagingAssignments = 30;
        public static int numberOfItemsRenamed = 0;
        public static int numberOfItemsDeleted = 0;
        public static int numberOfItemsAssigned = 0;
        public static int numberOfItemsImported = 0;
        public static int numberOfItemsCheckedForAssignments = 0;
        public static int numberOfItemsJsonExported = 0;
        public static int numberOfAuditLogsRetrieved = 0;
        public static int numberOfAssignmentsManaged = 0;
    }
}
