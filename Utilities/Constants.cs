namespace IntuneTools.Utilities
{
    /// <summary>
    /// Shared constants for Graph API operations.
    /// </summary>
    public static class GraphConstants
    {
        /// <summary>
        /// Default page size for Graph API list/search requests.
        /// Graph Beta supports up to 999 for most endpoints; 1000 is used
        /// because several Intune endpoints accept it without error.
        /// </summary>
        public const int DefaultPageSize = 1000;

        /// <summary>
        /// Fallback display name used when a resource name cannot be retrieved.
        /// </summary>
        public const string FallbackDisplayName = "ERROR GETTING NAME";
    }

    /// <summary>
    /// Shared constants for UI operations and thresholds.
    /// </summary>
    public static class UIConstants
    {
        /// <summary>
        /// Number of items at or above which a bulk-operation warning dialog is shown.
        /// </summary>
        public const int BulkOperationWarningThreshold = 10;
    }

    /// <summary>
    /// Well-known virtual group IDs used by Microsoft Intune.
    /// These are fixed GUIDs that represent built-in "All Users" and "All Devices" groups.
    /// They are not real Entra ID groups; they exist only in Intune's assignment model.
    /// </summary>
    public static class WellKnownGroups
    {
        /// <summary>Virtual group ID for "All Users".</summary>
        public const string AllUsersId = "acacacac-9df4-4c7d-9d50-4ef0226f57a9";

        /// <summary>Virtual group ID for "All Devices".</summary>
        public const string AllDevicesId = "adadadad-808e-44e2-905a-0b7873a8a531";
    }
}
