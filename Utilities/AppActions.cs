using InToolz.Pages;

namespace InToolz.Utilities
{
    /// <summary>
    /// Single source of truth for every top-level destination in the app. Backs the
    /// Ctrl+K command palette and the shell breadcrumb's page-title lookup today; from
    /// Phase 2 it also backs the dashboard's "browse all actions" grid — so none of
    /// those three can drift out of sync with each other the way the old NavigationView
    /// tag, HomePage's quick-action buttons, and the (removed) MainWindow switch could.
    /// </summary>
    public sealed record AppAction(string Title, string Glyph, string[] Keywords, Type TargetPage);

    public static class AppActions
    {
        public static readonly IReadOnlyList<AppAction> All = new List<AppAction>
        {
            new("Home", "", new[] { "home", "dashboard", "overview" }, typeof(HomePage)),
            new("Assignment", "", new[] { "assign", "group", "filter" }, typeof(AssignmentPage)),
            new("Manage Assignments", "", new[] { "remove", "assignment", "unassign" }, typeof(ManageAssignmentsPage)),
            new("Import", "", new[] { "import", "copy", "tenant" }, typeof(ImportPage)),
            new("Cleanup", "", new[] { "cleanup", "delete", "duplicate", "unassigned", "stale", "device" }, typeof(CleanupPage)),
            new("Renaming", "", new[] { "rename", "prefix", "suffix" }, typeof(RenamingPage)),
            new("JSON", "", new[] { "json", "export", "backup" }, typeof(JsonPage)),
            new("Audit Log", "", new[] { "audit", "log", "history", "changes" }, typeof(AuditLogPage)),
            new("Settings", "", new[] { "settings", "tenant", "sign in", "demo" }, typeof(SettingsPage)),
        };
    }
}
