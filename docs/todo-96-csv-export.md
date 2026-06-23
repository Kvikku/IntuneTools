# Issue #96 — Export to CSV

**Branch:** `feat/csv-export`

Export the currently loaded datagrid content to a CSV file from any page. The requester wants all sections covered, with ManageAssignments showing human-readable group names rather than GUIDs.

---

## Current state

- No CSV/Excel export exists anywhere in the app.
- No CSV/Excel NuGet package is needed — standard .NET `StreamWriter` + WinUI `FileSavePicker` is sufficient.
- `AuditLogReportGenerator.cs` exists as a precedent for a dedicated report-generation utility class.
- `CustomContentInfo` (Name, Platform, Type, ID, Description) covers most pages.
- `AssignmentInfo` (TargetType, GroupId, FilterId, FilterType) is what ManageAssignments holds per row.

---

## Group name resolution

`Variables.groupNameAndID` is a session-level dictionary populated whenever groups are loaded (e.g. on AssignmentPage or ImportPage). It maps **display name → ID**, which is the wrong direction for resolving IDs back to names.

`ManageAssignmentsPage` already has a private `ResolveGroupNamesAsync` that does individual Graph calls to resolve a list of IDs → display names. It needs to be promoted to a shared utility.

---

## Steps

### Step 1 — Add reverse lookup to `Variables`
**File:** `Utilities/Variables.cs`

Add a companion dictionary alongside the existing `groupNameAndID`:

```csharp
public static Dictionary<string, string> groupIDAndName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
```

This will be the fast-path cache: if a group was already fetched during the session (via AssignmentPage or ImportPage), its ID → name mapping is already available without hitting Graph again.

---

### Step 2 — Populate `groupIDAndName` wherever `groupNameAndID` is populated
**File:** `Graph/EntraHelperClasses/GroupHelperClass.cs`

`GetAllGroups` and `SearchForGroups` both iterate groups and write into `groupNameAndID`. Add the reverse write in the same loops:

```csharp
// existing
groupNameAndID[group.DisplayName] = group.Id;
// add
groupIDAndName[group.Id] = group.DisplayName;
```

Also clear `groupIDAndName` wherever `groupNameAndID` is cleared (top of both methods).

---

### Step 3 — Promote `ResolveGroupNamesAsync` to `GroupHelperClass`
**File:** `Graph/EntraHelperClasses/GroupHelperClass.cs`

Move the private method from `ManageAssignmentsPage` to `GroupHelperClass` as a `public static` method, and add a fast-path cache check:

```csharp
/// <summary>
/// Resolves a list of group IDs to their display names.
/// Checks the session cache first; falls back to individual Graph API calls for any misses.
/// Falls back to the raw ID if resolution fails.
/// </summary>
public static async Task<Dictionary<string, string>> ResolveGroupNamesAsync(
    GraphServiceClient graphServiceClient,
    IEnumerable<string> groupIds)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    var misses = new List<string>();
    foreach (var id in groupIds)
    {
        if (groupIDAndName.TryGetValue(id, out var cached))
            result[id] = cached;
        else
            misses.Add(id);
    }

    foreach (var id in misses)
    {
        try
        {
            var group = await graphServiceClient.Groups[id].GetAsync(config =>
                config.QueryParameters.Select = new[] { "displayName" });

            var name = group?.DisplayName ?? id;
            result[id] = name;
            groupIDAndName[id] = name; // populate cache for future lookups
        }
        catch
        {
            result[id] = id; // fall back to raw ID
        }
    }

    return result;
}
```

Then update `ManageAssignmentsPage.ResolveGroupNamesAsync` to just delegate to the shared method (or remove it entirely and update the call site to reference `GroupHelperClass.ResolveGroupNamesAsync`).

---

### Step 4 — Create `CsvExporter` utility class
**File:** `Utilities/CsvExporter.cs` (new file)

A single static class with two public methods:

**Generic export** — for all standard `CustomContentInfo` pages:
```csharp
public static async Task ExportContentListAsync(
    IEnumerable<CustomContentInfo> items,
    string suggestedFileName)
```
Columns: `Name`, `Type`, `Platform`, `Description`, `ID`

**Assignment-aware export** — for ManageAssignments, where each row may have multiple assignment targets:
```csharp
public static async Task ExportAssignmentListAsync(
    IEnumerable<CustomContentInfo> items,
    IEnumerable<AssignmentInfo> assignments,       // flat list of all assignments
    Dictionary<string, string> groupNames,         // pre-resolved ID → name map
    string suggestedFileName)
```
Columns: `Name`, `Type`, `Platform`, `Assignment Target`, `Target Group`, `Filter`, `Filter Type`, `ID`

For ManageAssignments, each content item is joined to its assignment rows — if a policy has 3 assignment targets, it produces 3 CSV rows (one per target), with name/type repeated. This matches what the requester asked for ("list what it is assigned to").

**CSV writing rules:**
- UTF-8 with BOM (so Excel opens it correctly without import wizard)
- Quote any field that contains a comma, double-quote, or newline
- First row is a header
- Use `FileSavePicker` with `.csv` filter; default file name includes page name + timestamp (e.g. `Cleanup_2026-06-23.csv`)

Internal helper:
```csharp
private static string EscapeCsvField(string? value)
{
    if (string.IsNullOrEmpty(value)) return string.Empty;
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        return $"\"{value.Replace("\"", "\"\"")}\"";
    return value;
}
```

For the `FileSavePicker`, use the existing pattern from `JsonPage` (which already does file save dialogs) to get the correct `WindowHandle` wiring.

---

### Step 5 — Add Export button to each page toolbar
**Files:** `Pages/CleanupPage.xaml`, `Pages/RenamingPage.xaml`, `Pages/ManageAssignmentsPage.xaml`, `Pages/AssignmentPage.xaml`, `Pages/JsonPage.xaml`, `Pages/ImportPage.xaml`

Add an Export button to each page's toolbar using `SecondaryActionButtonStyle` (consistent with existing toolbar buttons). Place it to the right of the existing action buttons, before the search area.

XAML snippet (adjust per page):
```xml
<Button Content="Export CSV"
        Style="{StaticResource SecondaryActionButtonStyle}"
        Click="ExportCsvButton_Click" />
```

Each page's click handler:
- **CleanupPage / RenamingPage / JsonPage / ImportPage**: call `CsvExporter.ExportContentListAsync(ContentList, "PageName")`
- **ManageAssignmentsPage**: resolve group names first, then call `CsvExporter.ExportAssignmentListAsync(...)` — see Step 6
- **AssignmentPage**: uses `AssignmentList` (not `ContentList`) — call `CsvExporter.ExportContentListAsync(AssignmentList, "Assignments")`

Disable the Export button when the list is empty (bind `IsEnabled` to `ContentList.Count > 0`, or handle in the click handler with an early return and a log message).

---

### Step 6 — ManageAssignments export: resolve group names then export
**File:** `Pages/ManageAssignmentsPage.xaml.cs`

The export for this page is slightly more involved because each `CustomContentInfo` row links to one or more `AssignmentInfo` entries. The page already has a collection of assignments — check how it stores the link between content items and their assignment targets (likely a separate `_allAssignments` list or similar), and pass both to `CsvExporter.ExportAssignmentListAsync`.

Before calling the exporter:
1. Collect all unique `GroupId` values from the current assignment list (skip nulls and virtual group IDs like All Users / All Devices)
2. Call `GroupHelperClass.ResolveGroupNamesAsync(graphServiceClient, groupIds)` to get the name map
3. Pass the name map into the exporter so it can substitute names for IDs in the output

For `TargetType` values like "All Users" and "All Devices", no group name lookup is needed — use the `TargetType` string directly in the "Target Group" column.

---

### Step 7 — AuditLogPage export (optional / stretch)
**File:** `Pages/AuditLogPage.xaml.cs`

The audit log page already has its own HTML report export. Adding a CSV export here would follow the same pattern as other pages but use `AuditEventViewModel` fields (Date/Time, Actor, Activity, Category, Result, Component, Operation, Resources) instead of `CustomContentInfo`. Lower priority — the HTML report already covers this use case well.

---

## Pages covered

| Page | Data model | Notes |
|------|-----------|-------|
| CleanupPage | `CustomContentInfo` | Standard export |
| RenamingPage | `CustomContentInfo` | Standard export |
| JsonPage | `CustomContentInfo` | Standard export |
| ImportPage | `CustomContentInfo` | Standard export |
| AssignmentPage | `AssignmentInfo`-style | Uses `AssignmentList`, not `ContentList` |
| ManageAssignmentsPage | `CustomContentInfo` + `AssignmentInfo` | Multi-row per item; requires group name resolution |
| AuditLogPage | `AuditEventViewModel` | Stretch goal; HTML report already exists |

---

## What NOT to do

- Do not add a NuGet package for CSV — standard `StreamWriter` is sufficient and avoids a dependency.
- Do not add an Excel (.xlsx) export at this stage — CSV opens fine in Excel and the requester listed it as an option, not a requirement.
- Do not resolve group names eagerly on page load — only resolve at export time to avoid unnecessary Graph calls.
