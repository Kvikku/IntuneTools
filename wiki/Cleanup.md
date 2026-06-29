# Cleanup Page

The Cleanup page allows you to bulk-delete Intune and Entra ID resources from your tenant. It uses a **staging area** model — items are first loaded into a grid where you can review and refine the list before committing to deletion.

> **Warning:** Deletion is permanent and cannot be undone. Always review the staging area carefully before clicking **Delete All**.

---

## Prerequisites

- You must be **authenticated** to a tenant before using the Cleanup page. Use the Settings page to sign in first.
- Your account must have sufficient permissions to delete the resource types you intend to remove.

---

## Supported Content Types

The Cleanup page supports deleting the following resource types:

| Category | Content Type |
|---|---|
| Apps | Applications (Win32, Store, LOB, VPP, and more) |
| Configuration | Settings Catalog policies |
| Configuration | Device Configuration policies |
| Compliance | Device Compliance policies |
| Enrollment | Apple BYOD Enrollment Profiles |
| Filters | Assignment Filters |
| Identity | Entra Security Groups |
| Scripts | PowerShell Scripts |
| Scripts | Proactive Remediations |
| Scripts | macOS Shell Scripts |
| Deployment | Windows AutoPilot Profiles |
| Updates | Windows Driver Updates |
| Updates | Windows Feature Updates |
| Updates | Windows Quality Update Policies |
| Updates | Windows Quality Update Profiles |

> **Note:** Some application types (such as VPP apps with active user licenses or Store apps still assigned to groups) may fail to delete. The log console will report the specific error for any item that could not be removed.

---

## Page Layout

The page is divided into three main areas:

1. **Toolbar** — Contains search controls, staging management buttons, and the delete action.
2. **Data Grid (Staging Area)** — Displays all items that are staged for deletion, with columns for Name, Type, Platform, ID, and Description.
3. **Log Console** — A real-time log panel on the right side showing operation progress and results.

The data grid and log console are separated by a **draggable splitter** — you can resize either panel by dragging the divider left or right.

---

## Step-by-Step Workflow

### 1. Load Items into the Staging Area

You have four ways to populate the staging area:

- **List All** — Click the **List All** button to fetch every supported resource from your tenant. This may take some time depending on how many resources exist.
- **Search** — Type a query into the search box and click **Search** to find resources matching that term.
- **Find Unassigned** — Click the **Find Unassigned** button to discover content that has no group assignments. See [Finding Unassigned Content](#finding-unassigned-content) below.
- **Find Duplicates** — Click the **Find Duplicates** button to detect content with duplicate names. See [Finding Duplicate Content](#finding-duplicate-content) below.

A loading overlay will appear while data is being fetched from Microsoft Graph.

### 2. Review the Staging Area

Once items are loaded, they appear in the data grid. Each row represents a single resource with the following information:

| Column | Description |
|---|---|
| **Name** | The display name of the resource |
| **Type** | The category of the resource (e.g., Settings Catalog, PowerShell Script) |
| **Platform** | The target platform (e.g., Windows, macOS) |
| **ID** | The unique Microsoft Graph identifier |
| **Description** | The resource's description, if any |

You can **sort** any column by clicking its header, and you can **right-click** rows to access a context menu with additional options including **View assignments** and **Copy cell**.

### 3. Refine the List

Before deleting, remove any items you want to keep:

- **Clear Selected** — Select one or more rows in the grid (use Ctrl+Click or Shift+Click for multi-select), then click **Clear Selected** to remove only those items from the staging area.
- **Clear All** — Click **Clear All** to empty the entire staging area and start over.

> **Tip:** Use Search to load a targeted set of items, then use Clear Selected to further narrow down the list. This gives you precise control over what gets deleted.

### 4. Delete

When you are satisfied that the staging area contains only the items you want to remove:

1. Click the red **Delete All** button.
2. If there are **10 or more items**, an additional bulk-delete warning will appear asking you to confirm the large operation.
3. A final confirmation dialog will ask you to confirm deletion. Click **Delete** to proceed or **Cancel** to abort.

During deletion, a progress bar in the status area shows how many items have been processed. The log console provides real-time feedback on each item being deleted, including any errors encountered.

After the operation completes, a summary is displayed showing how many items were successfully deleted and how many errors occurred.

### Special Case: Windows AutoPilot Profiles

If a Windows AutoPilot profile has active device assignments, you will be prompted with an additional dialog asking whether to **delete the assignments first** before removing the profile, or to **skip** that profile entirely.

---

## Finding Unassigned Content

The **Find Unassigned** button helps you identify policies, scripts, and profiles that exist in your tenant but are not assigned to any group. This is useful for cleaning up stale or forgotten configurations that are no longer in use.

### How It Works

1. Click **Find Unassigned**. All toolbar and action buttons are disabled while the operation runs.
2. The tool fetches all assignable content types from Microsoft Graph. A progress bar tracks the operation.
3. Each item is individually checked for group assignments. Only items with **no assignments** are added to the staging area — items that have assignments are filtered out.
4. When complete, the staging area contains only unassigned content, ready for review and optional deletion.

### Supported Content Types

Find Unassigned checks all content types listed in [Supported Content Types](#supported-content-types) **except** Assignment Filters and Entra Security Groups, since those resource types do not have group assignments.

### Tips

- **This operation can take a while** in large tenants, because each item requires a separate API call to check its assignments. The progress bar shows how many items have been checked.
- **Review carefully before deleting.** An unassigned policy is not necessarily unused — it may be kept as a template or backup.
- **Right-click any row** and choose **View assignments** to double-check an item before staging it for deletion.

---

## Finding Duplicate Content

The **Find Duplicates** button scans your tenant for content that has duplicate display names within the same content type. This is useful for identifying accidental duplicates, stale copies from imports, or redundant configurations that have built up over time.

### How It Works

1. Click **Find Duplicates**. All toolbar and action buttons are disabled while the scan runs.
2. The tool fetches all supported content types from Microsoft Graph and groups items by name within each content type.
3. Any group with more than one item with the same name is considered a set of duplicates. All items in a duplicate group are loaded into the **Duplicates** tab.
4. Duplicates are **automatically selected** in the grid, with one item per duplicate group left unselected (the one to keep). You can adjust this selection before deleting.

### Reviewing Duplicates

The duplicates grid shows items grouped by name. Items that are pre-selected for deletion are highlighted. Before proceeding:

- **Review the selection.** Deselect any items you want to keep.
- **Right-click** to view assignments — if one copy is assigned and the other is not, you likely want to keep the assigned one.
- **Compare descriptions and platforms** to make sure you are keeping the correct version.

### Deleting Duplicates

Once you are happy with the selection, click **Delete Selected** in the Duplicates tab. The same confirmation and progress flow applies as for the main Delete All operation.

---

## Toolbar Reference

| Button | Description |
|---|---|
| **Search** | Search for resources matching the text in the search box |
| **List All** | Load all supported resources from the tenant |
| **Find Unassigned** | Find content with no group assignments |
| **Find Duplicates** | Detect content with duplicate display names |
| **Clear Selected** | Remove selected rows from the staging area |
| **Clear All** | Remove all rows from the staging area |
| **Clear Log** | Clear the log console panel |
| **Delete All** (red) | Permanently delete all items in the staging area |
| **Export CSV** | Export the current grid contents to a CSV file |

---

## Log Console

The log console provides timestamped entries for every operation. Each entry includes:

- **Timestamp** — When the event occurred.
- **Level indicator** — Visual indicator of the log severity (info, warning, error).
- **Message** — Description of what happened, including the item's display name.

You can select log entries and use **Clear Log** to reset the console.

---

## Tips

- **Start small.** Use Search with a specific query before using List All, especially in large tenants.
- **Find stale content.** Use Find Unassigned to discover policies and scripts that may no longer be needed.
- **Clean up duplicates.** Use Find Duplicates after bulk imports to catch any accidental double-imports.
- **Double-check the grid** before clicking Delete All. Remember, everything in the staging area will be deleted.
- **Watch the log console** during deletion to catch any errors or skipped items in real time.
- **Use multi-select** (Ctrl+Click or Shift+Click) to efficiently remove items you want to keep from the staging area.
