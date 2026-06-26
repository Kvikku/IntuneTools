# Logging

InToolz writes a log file for each major operation. These logs are useful for auditing what the app did, diagnosing errors, and understanding exactly which items were created, renamed, deleted, or assigned.

## Log file location

```
C:\ProgramData\IntuneTools\
```

Each operation type writes to its own file:

| File | Contents |
|---|---|
| `assignment.log` | Assignment operations — which items were assigned to which groups |
| `import.log` | Import operations — which items were copied from source to destination |
| `cleanup.log` | Delete operations — which items were deleted and any errors |
| `renaming.log` | Rename operations — old and new names for each item |
| `json_export.log` | JSON export operations |
| `json_import.log` | JSON import operations |
| `manage_assignments.log` | Assignment removal operations |
| `main.log` | General application activity |

## Log format

Each entry includes a timestamp, severity level, and message:

```
2026-06-15 14:32:01 [INFO]  Deleted Settings Catalog policy: 'My Policy Name'
2026-06-15 14:32:02 [ERROR] Failed to delete PowerShell Script 'Old Script': Forbidden
```

Severity levels: `INFO`, `SUCCESS`, `WARNING`, `ERROR`.

## Tips

- If an operation fails silently in the UI, check the relevant log file — the error message from Microsoft Graph is always written there.
- Log files are appended to across sessions, so they build up a history over time. You can open them in any text editor.
- The `C:\ProgramData\IntuneTools` folder is shared across all Windows user profiles on the machine.
