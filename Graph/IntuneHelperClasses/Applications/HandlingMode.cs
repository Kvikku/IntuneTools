namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// How a particular Intune mobile app type can be moved between tenants.
    /// The registry uses this to make sure binary-upload code paths and
    /// tenant-bound code paths can never be confused at runtime.
    /// </summary>
    public enum HandlingMode
    {
        /// <summary>
        /// Metadata-only clone (web links, suite apps, store-sourced metadata
        /// that doesn't carry a tenant-specific token). Handled by the existing
        /// reflection-based clone in <c>ApplicationHelper.ImportMultipleApplications</c>.
        /// </summary>
        Cloneable,

        /// <summary>
        /// Binary round-trip via the Intune content upload protocol — the
        /// installer is downloaded from the source tenant, re-encrypted, and
        /// uploaded to the destination tenant through
        /// <see cref="IntuneContentEngine"/>.
        /// </summary>
        BinaryRoundTrip,

        /// <summary>
        /// Tenant-bound — Apple VPP, Managed Google Play, WinGet manifest
        /// references, etc. cannot be cloned because they require a unique
        /// integration in each destination tenant. The user gets a CSV/XLSX
        /// "manual hand-over" list instead (Phase 4).
        /// </summary>
        ManualHandover,
    }
}
