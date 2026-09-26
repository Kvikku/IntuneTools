namespace IntuneTools.Utilities
{
    /// <summary>
    /// A stale-device scan result for an Entra ID device object.
    /// </summary>
    public class StaleEntraDeviceInfo
    {
        public string? DeviceObjectId { get; set; }
        public string? DisplayName { get; set; }
        public string? OperatingSystem { get; set; }
        public string? OperatingSystemVersion { get; set; }
        public string? TrustType { get; set; }
        public bool? AccountEnabled { get; set; }
        public DateTimeOffset? ApproximateLastSignInDateTime { get; set; }

        public string LastSignInDisplay =>
            ApproximateLastSignInDateTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "Never";

        public string DaysInactiveDisplay => ApproximateLastSignInDateTime.HasValue
            ? $"{(DateTimeOffset.UtcNow - ApproximateLastSignInDateTime.Value).Days}"
            : "Never signed in";

        public string EnabledDisplay => AccountEnabled switch
        {
            true => "Yes",
            false => "No",
            null => "—"
        };

        /// <summary>
        /// Human-readable join type, mapped from the Graph trustType value
        /// ("AzureAd", "ServerAd", "Workplace").
        /// </summary>
        public string JoinTypeDisplay => TrustType switch
        {
            "AzureAd" => "Entra Joined",
            "ServerAd" => "Hybrid Entra Joined",
            "Workplace" => "Entra Registered",
            _ => TrustType ?? "Unknown"
        };
    }
}
