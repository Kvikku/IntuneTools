# Assignment Page

The Assignment page allows you to bulk-assign Intune and Entra ID resources to groups. It uses a **staging area** model — resources are loaded into a grid where you can review and refine the list, then assigned to selected groups via a configuration dialog.

---

## Prerequisites

- You must be **authenticated** to a tenant before using the Assignment page. Use the Settings page to sign in first.
- Your account must have sufficient permissions to create or modify assignments for the resource types you intend to assign.

---

## Supported Content Types

The Assignment page supports assigning the following resource types. You can select which types to include using the **Content Types** filter.

| Category | Content Type |
|---|---|
| Apps | Applications |
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

The page is divided into three main areas:

1. **Toolbar** — Contains search controls (with auto-suggest), content type filter, and staging management buttons.
2. **Data Grid (Staging Area)** — Displays all items staged for assignment, with columns for Name, Type, Platform, and ID.
3. **Log Console** — A real-time log panel showing operation progress and results.

---

## Step-by-Step Workflow

### 1. Select Content Types

Before loading data, choose which resource types to include:

1. Click the **Content Types** button in the toolbar.
2. A flyout appears with checkboxes for each content type.
3. Check or uncheck individual types, or use **Select all** to toggle all at once.

All content types are selected by default when the page loads.

### 2. Load Items into the Staging Area

You have two ways to populate the staging area:

- **List All** — Click the **List All** button to fetch every resource of the selected content types from your tenant.
- **Search** — Type a query into the search box. The search filters items by name, type, or platform. Press Enter or click the search icon to filter. Clearing the search box restores the full list.

A loading overlay will appear while data is being fetched from Microsoft Graph.

### 3. Review the Staging Area

Each row in the data grid shows:

| Column | Description |
|---|---|
| **Name** | The display name of the resource |
| **Type** | The category of the resource (e.g., Settings Catalog, App - Win32) |
| **Platform** | The target platform (e.g., Windows, macOS) |
| **ID** | The unique Microsoft Graph identifier |

You can **sort** any column by clicking its header. **Right-click** any row to access options including **View assignments** (shows the item's current group assignments) and **Copy cell**.

### 4. Refine the List

Remove items you don't want to assign:

- **Remove Selected** — Select one or more rows (Ctrl+Click or Shift+Click for multi-select), then click **Remove Selected**.
- **Remove All** — Click **Remove All** to clear the staging area entirely (requires confirmation).

### 5. Configure and Assign

1. Click the **Configure Assignment** button in the toolbar.
2. The **Assignment Configuration** dialog opens. It combines group selection and assignment options in one place.

#### Step A — Select Groups

In the dialog's group selection panel:

- Use **Search groups** to find a group by name, or **List all groups** to load all groups from the tenant.
- Select one or more groups from the group grid (Ctrl+Click or Shift+Click for multi-select).
- Optionally select **All Users** or **All Devices** virtual groups using the checkboxes provided.

> **You must select at least one group or virtual group before proceeding.** The dialog will show a validation message if none are selected.

#### Step B — Configure Assignment Options

The dialog also contains platform-specific assignment settings:

**General**

| Setting | Description |
|---|---|
| **Deploy mode** | Choose **Include** (add to group) or **Exclude** (exclude from group) |
| **Assignment Intent** | For applications only — choose **Required**, **Available**, or **Uninstall** |
| **Assignment Filter** | Optionally apply an assignment filter with Include or Exclude mode |

> **Note:** The "All Devices" virtual group is not supported for the "Available" intent. A warning is shown when this intent is selected.

> **Warning:** Assignment filters are platform-specific. Only assign one platform at a time when using filters.

**Windows tab**

| Setting | Options |
|---|---|
| **End user notifications** | Show all toast notifications / Hide all / Hide and show only reboot |
| **Delivery Optimization priority** | Background download / Foreground download |

**iOS tab**

| Setting | Options |
|---|---|
| **Use device licensing** | True / False |
| **Uninstall on device removal** | True / False |
| **Is removable** | True / False |
| **Prevent managed app backup** | True / False |
| **Prevent auto app update** | True / False |

**Android tab**

| Setting | Options |
|---|---|
| **Update priority** | Default / High priority / Postponed |

**macOS tab** — No specific settings are currently available for macOS.

#### Step C — Confirm and Assign

3. Click **Assign** in the dialog to proceed.
4. If there are **10 or more items**, a bulk-assignment warning will appear asking you to confirm.
5. A final confirmation dialog shows a summary of the assignment: number of items, groups, filter, and intent.
6. Click **Assign** to perform the assignment or **Cancel** to abort.

During the operation, a progress bar shows how many items have been processed. The log console provides real-time feedback on each assignment, including any errors.

After completion, a summary dialog shows the number of successful and failed assignments.

---

## Toolbar Reference

| Button / Control | Description |
|---|---|
| **Search box** | Auto-suggest search — filters items by name, type, or platform |
| **List All** | Load all resources of selected content types |
| **Content Types** | Open the content type filter flyout |
| **Remove Selected** | Remove selected rows from the staging area |
| **Remove All** | Clear all items from the staging area |
| **Configure Assignment** | Open the assignment dialog to select groups, configure options, and assign |
| **Clear Log** | Clear the log console panel |
| **Export CSV** | Export the current staging area to a CSV file |

---

## Log Console

The log console on the right side provides timestamped entries for every operation. Each entry includes:

- **Timestamp** — When the event occurred.
- **Level indicator** — Visual severity indicator (info, warning, error).
- **Message** — Description of what happened.

You can select log entries and use **Clear Log** to reset the console.

---

## Tips

- **Right-click to view current assignments.** Before assigning, right-click any staged item and choose **View assignments** to see what groups it is already assigned to. This helps avoid creating duplicate assignments.
- **Use content type filters** to reduce clutter. If you only need to assign Settings Catalog policies, uncheck everything else before loading.
- **Platform-specific settings only apply to applications.** The Windows, iOS, Android, and macOS tabs in the dialog only affect application deployments.
- **Assignment filters are platform-specific.** When using filters, only stage items of a single platform to avoid cross-platform filter conflicts.
- **The search box is live.** Clearing the search box restores the full list without needing to re-fetch from Graph.
- **At least one group must be selected** in the dialog before the assignment can proceed.
