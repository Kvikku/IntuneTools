using InToolz.Pages;

namespace InToolz.Utilities
{
    /// <summary>
    /// Single source of truth for every top-level destination in the app. Backs the
    /// Ctrl+K command palette, the shell breadcrumb's page-title lookup, and the
    /// dashboard's "browse all actions" grid — so none of those three can drift out of
    /// sync with each other the way the old NavigationView tag, HomePage's quick-action
    /// buttons, and the (removed) MainWindow switch could.
    /// </summary>
    public sealed record AppAction(string Title, string Glyph, string Description, string[] Keywords, Type TargetPage);

    public static class AppActions
    {
        public static readonly IReadOnlyList<AppAction> All = new List<AppAction>
        {
            new("Home", "", "Your at-a-glance overview.",
                new[] { "home", "dashboard", "overview" }, typeof(DashboardPage)),
            new("Assignment", "", "Assign policies and apps to groups in bulk.",
                new[] { "assign", "group", "filter" }, typeof(AssignmentPage)),
            new("Manage Assignments", "", "View and remove group assignments from content.",
                new[] { "remove", "assignment", "unassign" }, typeof(ManageAssignmentsPage)),
            new("Import", "", "Export and import configurations between tenants.",
                new[] { "import", "copy", "tenant" }, typeof(ImportPage)),
            new("Cleanup", "", "Remove policies, apps, and other content in bulk. Detect and clean up duplicates.",
                new[] { "cleanup", "delete", "duplicate", "unassigned", "stale", "device" }, typeof(CleanupPage)),
            new("Renaming", "", "Rename objects using prefix, suffix, find & replace, and more.",
                new[] { "rename", "prefix", "suffix" }, typeof(RenamingPage)),
            new("JSON", "", "Back up and restore policies and Entra groups as JSON files.",
                new[] { "json", "export", "backup" }, typeof(JsonPage)),
            new("Audit Log", "", "Review recent changes and who made them.",
                new[] { "audit", "log", "history", "changes" }, typeof(AuditLogPage)),
            new("Settings", "", "Configure tenants and application preferences.",
                new[] { "settings", "tenant", "sign in", "demo" }, typeof(SettingsPage)),
        };
    }
}
