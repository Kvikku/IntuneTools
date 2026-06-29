# Import Page

The Import page allows you to copy Intune and Entra ID resources from a **source tenant** to a **destination tenant**. It uses a **staging area** model — resources are loaded from the source tenant into a grid where you can review and refine the list, then import them into the destination tenant.

---

## Prerequisites

- You must be **authenticated to both a source and destination tenant** before using the Import page. Use the Settings page to sign in to each tenant.
- Your account must have read permissions on the source tenant and write permissions on the destination tenant for the resource types you intend to import.

---

## Supported Content Types

The Import page supports importing the following resource types. You can select which types to include using the **Content Types** filter.

| Category | Content Type |
|---|---|
| Configuration | Settings Catalog policies |
| Configuration | Device Configuration policies |
| Compliance | Device Compliance policies |
| Enrollment | Apple BYOD Enrollment Profiles |
| Filters | Assignment Filters |
| Identity | Entra Security Groups |
| Scripts | PowerShell Scripts |
| Scripts | Proactive Remediations (Remediation Scripts) |
| Scripts | macOS Shell Scripts |
| Deployment | Windows AutoPilot Profiles |
| Updates | Windows Driver Updates |
| Updates | Windows Feature Updates |
| Updates | Windows Quality Update Policies |
| Updates | Windows Quality Update Profiles |

---

## Page Layout

The page is divided into four main areas:

1. **Toolbar** — Contains search controls, content type filter, and staging management buttons.
2. **Data Grid (Staging Area)** — Displays all items from the source tenant staged for import.
3. **Import Options Panel** — Side panel with the Import button and optional group/filter assignment settings.
4. **Log Console** — A real-time log panel showing operation progress and results.

The Import Options panel and Log Console are separated by a **draggable splitter**.

---

## Step-by-Step Workflow

### 1. Select Content Types

Before loading data, choose which resource types to include:

1. Click the **Content Types** button in the toolbar.
2. A flyout appears with checkboxes for each content type.
3. Check or uncheck individual types, or use **Select all** to toggle all at once.

Only the selected content types will be fetched when you use Search or List All.

### 2. Load Items into the Staging Area

You have two ways to populate the staging area with resources from the **source tenant**:

- **List All** — Click the **List All** button to fetch every resource of the selected content types.
- **Search** — Type a query into the search box and click **Search** to find matching resources.

A loading overlay will appear while data is being fetched from Microsoft Graph.

### 3. Review the Staging Area

Each row in the data grid shows:

| Column | Description |
|---|---|
| **Name** | The display name of the resource |
| **Type** | The category of the resource (e.g., Settings Catalog, PowerShell Script) |
| **Platform** | The target platform (e.g., Windows, macOS) |
| **ID** | The unique Microsoft Graph identifier |
| **Description** | The resource's description, if any |

You can **sort** any column by clicking its header, and **right-click** rows for additional options.

### 4. Refine the List

Remove items you don't want to import:

- **Clear Selected** — Select one or more rows (Ctrl+Click or Shift+Click for multi-select), then click **Clear Selected**.
- **Clear All** — Click **Clear All** to empty the staging area entirely.

### 5. Configure Import Options (Optional)

The Import Options side panel lets you optionally assign groups and filters to the imported resources during the import process:

#### Include Groups

1. Check the **Include Groups** checkbox to reveal the groups panel.
2. Use **Search groups** or **List all groups** to populate the group list from the **destination tenant**.
3. Select the groups you want the imported resources to be assigned to.

#### Include Filters

1. Check the **Include Filters** checkbox to load assignment filters from the destination tenant.
2. Select a filter from the dropdown to apply it to the imported resources.

> **Note:** Enabling the Filters checkbox also reveals the Groups panel, as group and filter assignment typically go together.

### 6. Import

1. Click the **Import** button.
2. If there are **10 or more items**, a bulk-import warning will appear asking you to confirm.
3. The import process begins — resources are read from the source tenant and created in the destination tenant.

During the import, a progress bar shows how many content types have been processed. The log console provides detailed feedback including:

- The source and destination tenant names
- Which content types are being imported
- Which groups (if any) are being assigned
- Which filters (if any) are being applied
- Success or failure status for each content type

After completion, a summary is displayed showing how many content types were imported successfully and how many had errors.

---

## Toolbar Reference

| Button / Control | Icon | Description |
|---|---|---|
| **Search** | 🔍 | Search for resources matching the text in the search box |
| **List All** | 📋 | Load all resources of selected content types from the source tenant |
| **Content Types** | 📑 | Open the content type filter flyout |
| **Clear Selected** | ➖ | Remove selected rows from the staging area |
| **Clear All** | ✖ | Remove all rows from the staging area |
| **Clear Log** | 🗑 | Clear the log console panel |

---

## Import Options Panel Reference

| Control | Description |
|---|---|
| **Import** button | Start the import process for all staged items |
| **Include Groups** checkbox | Toggle group assignment for imported resources |
| **Include Filters** checkbox | Toggle filter assignment and load filters from the destination tenant |
| **Group search / list** | Find or list groups from the destination tenant |
| **Group DataGrid** | Select which groups to assign to imported resources |
| **Filter dropdown** | Select an assignment filter to apply |

---

## Log Console

The log console on the right side provides timestamped entries for every operation. Each entry includes:

- **Timestamp** — When the event occurred.
- **Level indicator** — Visual severity indicator (info, warning, error).
- **Message** — Description of what happened.

You can select log entries and use **Clear Log** to reset the console.

---

## Tips

- **Filter content types first.** Use the Content Types flyout to narrow down what gets loaded — this speeds up List All and Search in large tenants.
- **Groups and filters are optional.** You can import resources without assigning them to any group or filter, then configure assignments later using the Assignment page.
- **Import creates new resources.** The import process creates copies in the destination tenant — it does not move or delete anything from the source tenant.
- **Review the log carefully.** The log console shows exactly what was imported and any errors encountered, making it easy to identify what needs manual follow-up.
