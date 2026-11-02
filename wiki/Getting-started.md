# Getting Started

![](https://img.shields.io/badge/START%20HERE-red)

This guide covers everything you need before using InToolz.

---

## Prerequisites

### Azure tenant access

You need access to at least one Azure / Entra ID tenant. For cross-tenant import scenarios, you'll need access to both a **source** (read from) and **destination** (write to) tenant.

### Enterprise application

InToolz uses the built-in **Microsoft Graph Command Line Tools** enterprise application in Entra ID — no custom app registration is required.

> **Note:** If the enterprise application hasn't been used in your tenant before, an admin consent prompt may appear on first sign-in.

---

## Required API Permissions

All permissions are **Delegated** (not Application). The scopes are requested automatically at sign-in via the Microsoft Graph Command Line Tools enterprise app.

### Both tenants

Both the source and the destination tenant request the same set of scopes. The source tenant is where InToolz **reads** content from, and also where Cleanup, Renaming and Assignment make their changes. The destination tenant is where imported content is **created**.

| Scope | Purpose |
|-------|---------|
| `openid`, `offline_access` | Authentication & token refresh |
| `User.Read` | Read signed-in user profile |
| `Directory.Read.All` | Resolve tenant name and directory objects |
| `Policy.Read.All` | Read conditional access and auth policies |
| `AuditLog.Read.All` | Read audit logs |
| `Reports.Read.All` | Read usage reports |
| `RoleManagement.Read.All` | Read role assignments |
| `Application.Read.All` | Read app registrations |
| `DelegatedPermissionGrant.Read.All` | Read delegated permission grants |
| `DeviceManagementApps.ReadWrite.All` | Manage Intune app management |
| `DeviceManagementCloudCA.ReadWrite.All` | Manage cloud-attached device config |
| `DeviceManagementConfiguration.ReadWrite.All` | Manage device configuration & policies |
| `DeviceManagementManagedDevices.ReadWrite.All` | Manage managed devices |
| `DeviceManagementRBAC.ReadWrite.All` | Manage Intune RBAC settings |
| `DeviceManagementScripts.ReadWrite.All` | Manage PowerShell & shell scripts |
| `DeviceManagementServiceConfig.ReadWrite.All` | Manage Intune service configuration |
| `Group.ReadWrite.All` | Manage groups for assignment |

### Source tenant only

| Scope | Purpose |
|-------|---------|
| `Device.ReadWrite.All` | Delete stale Entra ID device objects (Cleanup → Stale Devices) |

> **Upgrading from 1.7 or earlier?** `Device.ReadWrite.All` is new in 1.8, so a source tenant you consented to with an earlier version will report it as missing the next time you sign in. Grant the new permission when prompted — in tenants that require admin consent, an admin needs to approve it. Until then, deleting Entra ID devices from Stale Devices will fail.

After you sign in, InToolz checks the permissions that were granted against the ones it needs and opens the permissions dialog if any are missing. See [Settings](Settings) for details.

---

## Next steps

Once you've verified tenant access and permissions, open InToolz and head to **Settings** to sign in to your tenants. See the other wiki pages for feature-specific guides.