# Manage Assignments Page

The Manage Assignments page lets you inspect and remove existing group assignments from Intune and Entra ID resources in bulk. It is useful for cleaning up assignments before decommissioning content, restructuring group targeting, or auditing what is currently assigned and to whom.

---

## Prerequisites

- You must be **authenticated** to a tenant before using this page. Use the Settings page to sign in first.
- Your account must have sufficient permissions to read and modify assignments for the resource types you intend to manage.

---

## Supported Content Types

The Manage Assignments page supports all content types that can have group assignments:

| Category | Content Type |
|---|---|
| Apps | Applications (Win32, Store, LOB, VPP, and more) |
| Configuration | Settings Catalog policies |
| Configuration | Device Configuration policies |
| Compliance | Device Compliance policies |
| Enrollment | Apple BYOD Enrollment Profiles |
| Scripts | PowerShell Scripts |
| Scripts | Proactive Remediations (Remediation Scripts) |
| Scripts | macOS Shell Scripts |
| Deployment | Windows AutoPilot Profiles |
| Updates | Windows Driver Updates |
| Updates | Windows Feature Updates |
| Updates | Windows Quality Update Policies |
| Updates | Windows Quality Update Profiles |

> **Note:** Assignment Filters and Entra Security Groups are not listed because they do not have group assignments.

---

## Page Layout

The page is divided into three main areas:

1. **Toolbar** — Search controls, content type filter, and staging management buttons.
2. **Data Grid (Staging Area)** — Displays loaded items with their current assignment information.
3. **Log Console** — A real-time log panel showing operation progress and results.

---

## Step-by-Step Workflow

### 1. Select Content Types

Before loading data, choose which resource types to include:

1. Click the **Content Types** button in the toolbar.
2. A flyout appears with checkboxes for each content type.
3. Check or uncheck individual types, or use **Select all** to toggle all at once.

### 2. Load Items

You have two ways to populate the staging area:

- **List All** — Fetches every resource of the selected content types from your tenant.
- **Search** — Type a query into the search box and click Search to find matching resources.

A loading overlay appears while data is being fetched.

### 3. Review Assignments

Each row in the data grid shows the item name, type, platform, and ID. Right-click any row to **View assignments** — this opens a popup showing all current group assignments for that item, including the target group name, assignment type (Include/Exclude), and any applied filter.

### 4. Select Items to Unassign

Select the rows you want to remove assignments from:

- Click a row to select it.
- Use **Ctrl+Click** or **Shift+Click** for multi-select.
- Use **Clear Selected** to remove items from the grid that you do not want to act on.
- Use **Clear All** to empty the staging area and start over.

### 5. Remove Assignments

1. Select the items whose assignments you want to remove.
2. Click the **Remove Assignments** button.
3. If there are **10 or more items**, a bulk-operation warning will appear.
4. A confirmation dialog shows how many items will be unassigned. Click **Remove** to proceed or **Cancel** to abort.

During the operation, a progress bar tracks how many items have been processed. The log console provides real-time feedback on each item.

After completion, a summary shows how many assignments were removed successfully and how many errors occurred.

---

## Toolbar Reference

| Button / Control | Description |
|---|---|
| Search box | Filter items by name |
| **Search** | Execute the search against the tenant |
| **List All** | Load all resources of selected content types |
| **Content Types** | Open the content type filter flyout |
| **Clear Selected** | Remove selected rows from the staging area |
| **Clear All** | Remove all rows from the staging area |
| **Remove Assignments** | Remove all group assignments from selected items |
| **Clear Log** | Clear the log console panel |

---

## Tips

- **Right-click to inspect before acting.** Use the **View assignments** context menu option to see exactly what assignments an item has before removing them.
- **Combine with Search.** Search for a specific policy or naming pattern before loading all content — this is much faster in large tenants.
- **Assignments are removed from all groups.** The remove operation clears all group assignments from the selected items. It does not let you selectively remove one group — use the Intune portal for that level of granularity.
- **Use the log to verify.** Each removed assignment is logged individually, making it easy to audit what was changed.
