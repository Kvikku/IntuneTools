# Logging

InToolz writes a log file for each major operation. These logs are useful for auditing what the app did, diagnosing errors, and understanding exactly which items were created, renamed, deleted, or assigned.

## Log file location

Each session writes to its own timestamped folder, for example:

```
%LOCALAPPDATA%\InToolz\2026-06-15_14-30-00\
```

The quickest way to get there is **Settings → Open Log File Location**. After an operation fails, the status bar also shows an **Open logs** button. The Store version of the app can keep its files under its package folder, so use the button rather than browsing to the path above.

Each area of the app writes to its own file in that folder. A file is created the first time something is written to it, so you only see the logs for what you actually did in that session.

| File | Contents |
|---|---|
| `Main.log` | General application activity |
| `Summary.log` | System information logged at startup (OS, .NET and app version) |
| `Import.log` | Import operations — which items were copied from source to destination |
| `Assignment.log` | Assignment operations — which items were assigned to which groups |
| `Rename.log` | Rename operations — old and new names for each item |
| `Delete.log` | Delete operations — which items were deleted and any errors |
| `FindUnassigned.log` | Find Unassigned scans |
| `FindDuplicates.log` | Find Duplicates scans |
| `FindStaleDevices.log` | Stale Devices scans |
| `JsonExport.log` | JSON export and import operations |
| `AuditLog.log` | Audit Log operations |
| `ManageAssignment.log` | Assignment removal operations |

## Log format

Each entry includes a timestamp, severity level, and message:

```
2026-06-15 14:32:01 - [Info] - Deleted Settings Catalog policy: 'My Policy Name'
2026-06-15 14:32:02 - [Error] - Failed to delete PowerShell Script 'Old Script': Forbidden
```

Severity levels: `Info`, `Warning`, `Error`. Success messages are written as `Info`.

## Tips

- If an operation fails silently in the UI, check the relevant log file — the error message from Microsoft Graph is always written there.
- Every session gets a new folder, so older sessions' logs are kept until you delete them. You can open the files in any text editor.
- Logs are stored per Windows user, not shared between profiles on the same machine.
