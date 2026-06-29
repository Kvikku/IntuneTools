# InToolz

**Bulk management for Microsoft Intune — stop clicking a million times.**

InToolz lets Intune administrators assign, rename, clean up, import, and export policies, scripts, apps, and groups across one or more tenants — operations that would take hours through the Intune portal done in seconds.

---

## New to InToolz?

Start with **[Getting Started](Getting-started)** — it covers the prerequisites, required API permissions, and how to sign in to your tenant for the first time.

After that, head to **[Settings](Settings)** to authenticate, then pick the feature you need from the list below.

---

## What can InToolz do?

### Assign policies and apps to groups → [Assignment](Assignment)

Load any mix of content types into a staging area, then assign them all to one or more Entra groups in a single operation. Supports Include/Exclude targeting, assignment filters, and app-specific options (Required/Available/Uninstall, delivery optimization, end-user notifications). Right-click any item to inspect its current assignments before acting.

### Copy content between tenants → [Import](Import)

Read policies and profiles from a source tenant and recreate them in a destination tenant. Optionally apply group assignments and filters during import. Supports all major Intune content types.

### Delete in bulk → [Cleanup](Cleanup)

Stage items for deletion and remove them in a single confirmed operation. Three ways to find what to delete:
- **List All** — load everything and pick what to remove
- **Find Unassigned** — surface policies and scripts with no group assignments
- **Find Duplicates** — detect items with the same name in the same content type, auto-select the extras, and clean them up in one go

### Rename with precision → [Renaming](Renaming)

Apply consistent naming conventions across hundreds of items at once. Available modes: **Add Prefix**, **Remove Prefix**, **Add Suffix**, **Remove Suffix**, **Find & Replace**, and **Update Description**.

### Back up and restore as JSON → [JSON Import / Export](JSON-Import-Export)

Export policies to JSON files for version control or offline backup, then reimport them into any tenant. Supports all major policy types and Entra Security Groups (including dynamic membership rules).

### Inspect and remove existing assignments → [Manage Assignments](Manage-Assignments)

See what groups each policy or app is assigned to, and remove assignments in bulk. Useful before decommissioning content or reorganising group structure.

### Review tenant activity → [Audit Log](Audit-Log)

Browse recent Intune changes — who made them, what resource was affected, and whether the operation succeeded. Filter by time range (1–30 days), review a per-actor breakdown, and export to CSV.

---

## Supported content types

InToolz works with the following resource types across its pages:

- Settings Catalog policies
- Device Compliance policies
- Device Configuration (OMA-URI) policies
- Windows Quality Update policies and profiles
- Windows Feature Update policies
- Windows Driver Update policies
- Windows AutoPilot enrollment profiles
- PowerShell scripts
- Proactive Remediations
- macOS Shell scripts
- Apple BYOD enrollment profiles
- Assignment Filters
- Entra Security Groups (including dynamic)
- Applications (Win32, Store, LOB, VPP, and more)

Not every content type is available on every page — see the **Supported Content Types** section of each page for specifics.

---

## Get the app

Available on the **[Microsoft Store](https://apps.microsoft.com/detail/9phqrcx3gkxd)** or as a manual install from the [Releases page](https://github.com/Kvikku/IntuneTools/releases).

---

## Questions or feedback?

- **Bug or unexpected behaviour?** [Open an issue](https://github.com/Kvikku/IntuneTools/issues)
- **Feature request?** Post in the [Discussions tab](https://github.com/Kvikku/IntuneTools/discussions)
- **Something missing from this wiki?** Issues and PRs for docs are equally welcome
