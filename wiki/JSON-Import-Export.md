# JSON Import / Export Page

The JSON Import / Export page lets you back up Intune policies and Entra groups to local JSON files and restore them into any tenant later. It uses a staging area model — items are loaded from a tenant or from JSON files into a grid where you can review them, then either export them to disk or import them into a destination tenant.

## Prerequisites

- **Exporting from a tenant** requires authentication to a source tenant via the Settings page.
- **Importing from JSON files** into the staging area does **not** require authentication — you can work entirely offline.
- **Importing to a destination tenant** requires authentication to a destination tenant via the Settings page. Your account must have sufficient permissions to create the resource types you intend to import.

## Supported Content Types

The JSON page supports backing up and restoring the following resource types:

| Category | Content Type | JSON File Name |
|---|---|---|
| Configuration | Settings Catalog | `settingscatalog.json` |
| Configuration | Device Configuration | `deviceconfiguration.json` |
| Compliance | Device Compliance | `devicecompliance.json` |
| Enrollment | Apple BYOD Enrollment Profiles | `applebyodenrollment.json` |
| Filters | Assignment Filters | `assignmentfilter.json` |
| Identity | Entra Security Groups | `entragroup.json` |
| Scripts | PowerShell Scripts | `powershellscript.json` |
| Scripts | Proactive Remediations (Remediation Scripts) | `proactiveremediation.json` |
| Scripts | macOS Shell Scripts | `macosshellscript.json` |
| Deployment | Windows AutoPilot Profiles | `windowsautopilot.json` |
| Updates | Windows Driver Updates | `windowsdriverupdate.json` |
| Updates | Windows Feature Updates | `windowsfeatureupdate.json` |
| Updates | Windows Quality Update Policies | `windowsqualityupdatepolicy.json` |
| Updates | Windows Quality Update Profiles | `windowsqualityupdateprofile.json` |

Each content type is stored in its own JSON file when exporting.

> **Note on Entra groups:** Export captures the group's display name, description, and dynamic membership rule (if applicable). Group members and existing assignments are **not** exported. When importing, a new group is created — it will not have any members until you add them.

## Page Layout

The page is divided into four main areas:

1. **Header** — Shows the page title, a short description, and a tenant status bar indicating the authenticated source and destination tenants.
2. **Toolbar** — Two grouped cards: *Search & Staging* (search, list all, clear) and *JSON Actions* (export to folder, import from folder, import to tenant).
3. **Data Grid (Staging Area)** — Displays all items currently staged, with columns for Name, Type, Platform, ID, and Description.
4. **Log Console** — A real-time log panel showing timestamped operation progress and results.

The Data Grid and Log Console are separated by a draggable splitter.

## Step-by-Step Workflow

### 1. Load Items into the Staging Area

There are two ways to populate the staging area:

**From a tenant (online):**
- **List All** — Click the **List All** button to fetch every supported resource from the authenticated source tenant.
- **Search** — Type a query into the search box and click the search icon or press Enter to filter resources by name.

**From JSON files (offline):**
- Click **Import from Folder** and select a folder containing previously exported JSON files. The page reads all recognized files (e.g. `settingscatalog.json`, `entragroup.json`) and loads their items into the staging area.

> **Note:** If the staging area already contains items when importing from a folder, you will be prompted to confirm replacing them.

### 2. Review the Staging Area

Each row in the data grid shows:

| Column | Description |
|---|---|
| Name | The display name of the resource |
| Type | The category (e.g., Settings Catalog, Entra Security Group) |
| Platform | The target platform (e.g., Windows, macOS) |
| ID | The unique Microsoft Graph identifier |
| Description | The resource description, if available |

You can sort any column by clicking its header, and right-click rows for additional options.

### 3. Refine the List

Remove items you don't want to export or import:

- **Clear Selected** — Select one or more rows (Ctrl+Click or Shift+Click for multi-select), then click **Clear Selected**.
- **Clear All** — Click **Clear All** to remove every item from the staging area.

### 4. Export to Folder

1. Click the **Export to Folder** button.
2. A confirmation dialog shows the number of items per content type, the file names that will be created, and warns that existing files will be overwritten.
3. Click **Export** and select a destination folder.
4. The page fetches full policy data from Microsoft Graph for each staged item and writes one JSON file per content type to the selected folder.

If no source tenant is authenticated, you will be warned that the export will only contain item metadata (names, types, IDs) without full policy data — these files cannot be used to import policies into another tenant.

A progress bar and the log console show real-time status during the export.

### 5. Import from Folder

1. Click the **Import from Folder** button.
2. Select a folder containing previously exported JSON files.
3. The page scans for all recognized file names (see [Supported Content Types](#supported-content-types)) and loads matching items into the staging area.
4. Items that include full policy data are cached and marked as importable to a destination tenant.

The log console reports how many items were loaded from each file and whether they contain full policy data.

### 6. Import to Tenant

1. Ensure a destination tenant is authenticated (shown in the tenant status bar).
2. Click the **Import to [tenant name]** button.
3. A confirmation dialog shows the number of items to import and the destination tenant name.
4. Click **Import** to proceed.
5. Each item with cached policy data is created as a new resource in the destination tenant via Microsoft Graph.

During the operation, a progress bar shows how many items have been processed. The log console provides real-time feedback on each import, including any errors.

After completion:
- A success or error banner summarizes the results.
- If any items failed, a **failure summary** is printed to the log console listing each failed item with its content type and error reason.

> **Windows Update types** (Driver Updates, Feature Updates, Quality Update Policies, Quality Update Profiles) require the destination tenant to have Windows E3 or E5 licensing. If the license is missing, the import for those items will fail with a licensing error.

## Toolbar Reference

### Search & Staging Card

| Button | Description |
|---|---|
| Search box | Enter a query to filter items by name |
| **Search** | Execute the search against the source tenant |
| **List All** | Load all resources of all supported content types from the source tenant |
| **Clear Selected** | Remove selected rows from the staging area |
| **Clear All** | Remove all items from the staging area |
| **Clear Log** | Clear the log console panel |
| **Export CSV** | Export the current staging area to a CSV file |

### JSON Actions Card

| Button | Description |
|---|---|
| **Export to Folder** | Fetch full policy data and save one JSON file per content type to a selected folder |
| **Import from Folder** | Load items from a folder of previously exported JSON files into the staging area |
| **Import to [tenant]** | Create policies in the destination tenant from the staged JSON data |

## Tenant Status Bar

The info bar below the page header shows:

| Field | Description |
|---|---|
| Source | The currently authenticated source tenant, or "Not authenticated" |
| Destination | The currently authenticated destination tenant, or "Not authenticated" |

The **Import to Tenant** button label dynamically updates to show the destination tenant name (e.g., "Import to Contoso") so it is always clear which tenant will receive the imported policies.

## JSON File Format

Each exported JSON file follows this structure:

```json
{
  "exportedAt": "2026-03-05T12:00:00.0000000Z",
  "tenantName": "Contoso",
  "items": [
    {
      "name": "My Policy",
      "type": "Settings Catalog",
      "platform": "Windows",
      "id": "00000000-0000-0000-0000-000000000000",
      "description": "A sample policy",
      "policyData": { ... }
    }
  ]
}
```

| Field | Description |
|---|---|
| `exportedAt` | UTC timestamp of when the export was performed |
| `tenantName` | The source tenant name at the time of export |
| `items[].policyData` | The full Graph API representation of the policy, used during import |

Files without `policyData` contain only metadata and cannot be used to import to a tenant.

## Log Console

The log console on the right side provides timestamped entries for every operation. Each entry includes:

- **Timestamp** — When the event occurred.
- **Level indicator** — Visual severity indicator (info, warning, error).
- **Message** — Description of what happened.

You can select log entries and use **Clear Log** to reset the console.

## Tips

- **No tenant needed to browse exports.** You can use *Import from Folder* to load and review previously exported JSON files without authenticating to any tenant.
- **Export includes full policy data.** The JSON export fetches the complete Graph API representation of each policy, so it can be faithfully recreated in another tenant.
- **One file per content type.** Exports create separate files so you can selectively import only the types you need.
- **Entra groups: members are not exported.** When you import an Entra group, a new empty group is created. Membership must be managed separately.
- **Overwrite warning.** Exporting to a folder that already contains JSON files will overwrite files with the same names.
- **Check the failure summary.** After importing to a tenant, scroll to the bottom of the log console for a detailed failure summary listing every item that failed and why.
- **Windows Update licensing.** If importing Windows Update policies fails, verify that the destination tenant has Windows E3 or E5 licensing enabled.
- **Use this page for backup and disaster recovery.** Export your tenant's policies regularly to maintain an offline backup that can be restored at any time.
