namespace IntuneTools.Utilities
{
    /// <summary>
    /// A stale-device scan result for an Intune managed device.
    /// </summary>
    public class StaleManagedDeviceInfo
    {
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? OperatingSystem { get; set; }
        public string? OsVersion { get; set; }
        public string? UserPrincipalName { get; set; }
        public string? ComplianceState { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public DateTimeOffset? LastSyncDateTime { get; set; }

        public string LastSyncDisplay =>
            LastSyncDateTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "Never";

        public string DaysInactiveDisplay => LastSyncDateTime.HasValue
            ? $"{(DateTimeOffset.UtcNow - LastSyncDateTime.Value).Days}"
            : "Never synced";
    }
}
