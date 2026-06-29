# Renaming Page

The Renaming page allows you to bulk-rename Intune and Entra ID resources. Like the Cleanup page, it uses a **staging area** model — items are loaded into a grid for review and then updated in bulk based on the rename mode you select.

---

## Prerequisites

- You must be **authenticated** to a tenant before using the Renaming page. Use the Settings page to sign in first.
- Your account must have sufficient permissions to modify the resource types you intend to rename.

---

## Supported Content Types

The Renaming page supports updating the following resource types:

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
| Scripts | Proactive Remediations |
| Scripts | macOS Shell Scripts |
| Deployment | Windows AutoPilot Profiles |
| Updates | Windows Driver Updates |
| Updates | Windows Feature Updates |
| Updates | Windows Quality Update Policies |
| Updates | Windows Quality Update Profiles |

---

## Page Layout

The page is divided into three main areas:

1. **Toolbar** — Contains search and staging controls, plus the rename configuration options.
2. **Data Grid (Staging Area)** — Displays all items staged for renaming, with columns for Name, Type, Platform, ID, and Description.
3. **Log Console** — A real-time log panel on the right showing operation progress and results.

The data grid and log console are separated by a **draggable splitter** that you can resize.

---

## Rename Modes

The Renaming page offers six modes, selectable from the **Rename Configuration** dropdown:

### Add Prefix

Prepends a prefix to the display name of each staged item. You provide the prefix text and choose a bracket format:

| Format | Example (prefix: `IT`) |
|---|---|
| Parentheses `( )` | `(IT) My Policy Name` |
| Square brackets `[ ]` | `[IT] My Policy Name` |
| Curly brackets `{ }` | `{IT} My Policy Name` |

If an item already has a prefix in brackets, the existing prefix is **replaced** with the new one rather than stacking multiple prefixes.

### Remove Prefix

Strips an existing bracketed prefix from the display name of each staged item. No text input is required — the tool automatically detects prefixes enclosed in `()`, `[]`, or `{}` and removes them.

For example:
- `(IT) My Policy Name` → `My Policy Name`
- `[PROD] Compliance Policy` → `Compliance Policy`
- `{Test} Driver Update` → `Driver Update`

Items that have no detectable prefix are skipped.

### Add Suffix

Appends text to the end of each item's display name. You provide the suffix text — no bracket formatting is applied. Useful for adding environment tags or version markers.

For example, with suffix ` - PROD`:
- `My Compliance Policy` → `My Compliance Policy - PROD`

### Remove Suffix

Strips a specified text suffix from the end of each item's display name. You provide the exact suffix text to remove.

For example, with suffix ` - PROD`:
- `My Compliance Policy - PROD` → `My Compliance Policy`

Items that do not end with the specified suffix are skipped.

### Find & Replace

Replaces all occurrences of a specified string in each item's display name with a replacement string. Useful for renaming conventions that have changed across many items.

For example, find `OLD` replace with `NEW`:
- `[OLD] My Policy` → `[NEW] My Policy`
- `Settings OLD v2` → `Settings NEW v2`

The match is case-sensitive.

### Description

Replaces the **description field** of each staged item with the text you provide. This does not affect the display name.

---

## Step-by-Step Workflow

### 1. Load Items into the Staging Area

You have two ways to populate the staging area:

- **List All** — Click the **List All** button to fetch every supported resource from your tenant.
- **Search** — Type a query into the search box and click **Search** to find resources matching that term.

A loading overlay will appear while data is being fetched from Microsoft Graph.

### 2. Review the Staging Area

Each row in the data grid shows:

| Column | Description |
|---|---|
| **Name** | The current display name of the resource |
| **Type** | The category of the resource (e.g., Settings Catalog, Application) |
| **Platform** | The target platform (e.g., Windows, macOS) |
| **ID** | The unique Microsoft Graph identifier |
| **Description** | The resource's current description |

You can **sort** any column by clicking its header, and **right-click** rows for additional options.

### 3. Refine the List

Remove items you don't want to rename:

- **Clear Selected** — Select one or more rows (Ctrl+Click or Shift+Click for multi-select), then click **Clear Selected**.
- **Clear All** — Click **Clear All** to empty the staging area entirely.

### 4. Configure the Rename Operation

1. **Select a mode** from the dropdown.
2. **Enter the text** in the text box:
   - **Add Prefix**: type the prefix text (e.g., `IT`, `PROD`, `Test`).
   - **Remove Prefix**: no text input needed.
   - **Add Suffix**: type the suffix text.
   - **Remove Suffix**: type the exact suffix to strip.
   - **Find & Replace**: type the text to find and the replacement text.
   - **Description**: type the new description text.
3. If using **Add Prefix**, click the **Format** button to choose between parentheses, square brackets, or curly brackets.

### 5. Apply the Rename

1. Click the **Update** button.
2. If there are **10 or more items**, a bulk-operation warning will appear asking you to confirm.
3. A confirmation dialog will show a preview of what the new names (or description) will look like. Review the preview and click the confirm button to proceed, or **Cancel** to abort.

During the operation, a progress bar shows how many items have been processed. The log console provides real-time feedback on each item, including any errors.

After completion, a summary shows the number of successfully renamed items and any errors.

---

## Toolbar Reference

### Search & Staging Card

| Button | Description |
|---|---|
| Search box | Enter a query to filter items by name |
| **Search** | Execute the search against the tenant |
| **List All** | Load all supported resources from the tenant |
| **Clear Selected** | Remove selected rows from the staging area |
| **Clear All** | Remove all rows from the staging area |
| **Clear Log** | Clear the log console panel |
| **Export CSV** | Export the current grid contents to a CSV file |

### Rename Configuration Card

| Control | Description |
|---|---|
| **Mode dropdown** | Select the rename mode |
| **Text box(es)** | Enter the prefix/suffix/find/replace/description text |
| **Format** | Choose the bracket style for prefixes (Add Prefix mode only) |
| **Update** | Apply the rename operation to all staged items |

---

## Log Console

The log console on the right side provides timestamped entries for every action. Each entry includes:

- **Timestamp** — When the event occurred.
- **Level indicator** — Visual severity indicator (info, success, warning, error).
- **Message** — Description of what happened.

You can select log entries and use **Clear Log** to reset the console.

---

## Tips

- **Preview before committing.** The confirmation dialog shows you exactly what the names will look like after renaming — always review it.
- **Use Search to target specific items.** This is especially useful in large tenants where List All may return hundreds of resources.
- **Combine modes for cleanup.** Use Remove Prefix first to strip old prefixes, then Add Prefix to apply a new consistent naming standard.
- **Prefix replacement is automatic.** If a policy already has a bracketed prefix, Add Prefix replaces it rather than adding a second one — no need to Remove Prefix first.
- **Find & Replace is case-sensitive.** Make sure the search text matches the casing in your policy names exactly.
- **Description mode is independent.** Updating descriptions does not affect display names.
