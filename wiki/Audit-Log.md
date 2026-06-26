# Audit Log Page

The Audit Log page lets you review recent activity in your Intune tenant — what changes were made, who made them, and whether they succeeded. It is useful for compliance reviews, incident investigation, and getting a quick overview of tenant activity.

---

## Prerequisites

- You must be **authenticated** to a tenant before using the Audit Log page. The logs are fetched from whichever tenant is signed in as the source.
- Your account needs the `AuditLog.Read.All` permission (included in the default sign-in scopes — see [Getting Started](Getting-started)).

---

## Page Layout

The page is divided into three main areas:

1. **Toolbar** — Time range filter and export controls.
2. **Audit Log Grid** — Displays log entries with columns for date, actor, operation, resource type, result, and details.
3. **Summary Panel** — Shows a per-actor breakdown of activity and overall statistics.

---

## Step-by-Step Workflow

### 1. Set the Time Range

Use the **Time Range** control to select how far back to fetch logs. Available options range from **1 day** to **30 days**. The default is 7 days.

### 2. Load Logs

Click **Load Audit Log** to fetch log entries from Microsoft Graph for the selected time range.

A loading overlay appears while data is being fetched. In large tenants with a lot of activity, this can take a moment.

### 3. Review Log Entries

Each row in the audit log grid shows:

| Column | Description |
|---|---|
| **Date / Time** | When the event occurred (local time) |
| **Actor** | The user or service principal that performed the action |
| **Operation** | What action was taken (e.g., Create, Update, Delete) |
| **Resource Type** | The type of resource that was modified |
| **Resource Name** | The name of the specific resource |
| **Result** | Whether the operation succeeded or failed |

You can **sort** any column by clicking its header.

### 4. Review the Summary

The summary panel below the grid shows:

- **Total events** in the selected time range
- **Unique actors** (users or services) who made changes
- A **per-actor breakdown** listing each actor with their event count, making it easy to spot unusual activity

### 5. Export

Two export options are available:

- **Export CSV** — Downloads the full log as a CSV file, suitable for importing into Excel or a reporting tool.
- **Export Report** — Generates a formatted report summarising the log entries and per-actor breakdown.

---

## Toolbar Reference

| Control | Description |
|---|---|
| **Time Range** | Select the lookback period (1–30 days) |
| **Load Audit Log** | Fetch log entries from the tenant |
| **Export CSV** | Download the log as a CSV file |
| **Export Report** | Generate a formatted summary report |

---

## Tips

- **Start with 7 days.** The default range is a good starting point. Extend to 30 days only if you need to investigate something older — larger ranges take longer to fetch.
- **Sort by Actor** to quickly see all actions taken by a specific user.
- **Sort by Result** to surface failed operations — these are often the most actionable entries.
- **Use the per-actor summary** to spot unexpected activity, such as a service account or unfamiliar user making bulk changes.
- **Export to CSV for deeper analysis.** The grid is great for a quick look, but Excel or Power BI lets you filter, pivot, and correlate across columns more flexibly.
