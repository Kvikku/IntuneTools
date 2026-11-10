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

The page always loads from all supported content types — there is no content-type filter on this page.

---

## Page Layout

The page is divided into three main areas:

1. **Toolbar** — Search controls, staging management buttons, and the **Manage Assignments** action.
2. **Data Grid (Staging Area)** — Displays loaded items with their current assignment information.
3. **Log Console** — A real-time log panel showing operation progress and results.

---

## Step-by-Step Workflow

### 1. Load Items

You have two ways to populate the staging area:

- **List All** — Fetches every supported resource from your tenant.
- **Search** — Type a query into the search box and click Search to find matching resources.

A loading overlay appears while data is being fetched.

### 2. Review Assignments

Each row in the data grid shows the item name, type, platform, and ID. Right-click any row to **View assignments** — this opens a popup showing all current group assignments for that item, including the target group name, assignment type (Include/Exclude), and any applied filter.

### 3. Select Items and Manage Their Assignments

1. Select the rows you want to inspect — click a row to select it, or use **Ctrl+Click** / **Shift+Click** for multi-select. Use **Clear Selected** or **Clear All** to adjust the staging area first if needed.
2. Click the **Manage Assignments** button. The tool retrieves and displays the current assignments for every selected item.
3. From that dialog, you can choose to remove all assignments from the selected items. If there are **10 or more items**, a bulk-operation warning will appear first.

During the removal, a progress bar tracks how many items have been processed. The log console provides real-time feedback on each item.

After completion, a summary shows how many assignments were removed successfully and how many errors occurred.

---

## Toolbar Reference

| Button / Control | Description |
|---|---|
| Search box | Filter items by name |
| **Search** | Execute the search against the tenant |
| **List All** | Load all supported resources from the tenant |
| **Clear Selected** | Remove selected rows from the staging area |
| **Clear All** | Remove all rows from the staging area |
| **Manage Assignments** | View current assignments for selected items, with the option to remove them all |
| **Clear Log** | Clear the log console panel |

---

## Tips

- **Right-click to inspect before acting.** Use the **View assignments** context menu option to see exactly what assignments an item has before removing them.
- **Combine with Search.** Search for a specific policy or naming pattern before loading all content — this is much faster in large tenants.
- **Assignments are removed from all groups.** The remove operation clears all group assignments from the selected items. It does not let you selectively remove one group — use the Intune portal for that level of granularity.
- **Use the log to verify.** Each removed assignment is logged individually, making it easy to audit what was changed.
