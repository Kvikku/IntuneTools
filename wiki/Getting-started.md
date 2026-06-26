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

### Source tenant (read-only)

The source tenant is where you **read** existing Intune content from.

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
| `DeviceManagementApps.Read.All` | Read Intune app management |
| `DeviceManagementCloudCA.Read.All` | Read cloud-attached device config |
| `DeviceManagementConfiguration.Read.All` | Read device configuration & policies |
| `DeviceManagementManagedDevices.Read.All` | Read managed device info |
| `DeviceManagementRBAC.Read.All` | Read Intune RBAC settings |
| `DeviceManagementScripts.Read.All` | Read PowerShell & shell scripts |
| `DeviceManagementServiceConfig.Read.All` | Read Intune service configuration |
| `Group.ReadWrite.All` | Read groups for assignment lookups |

### Destination tenant (read-write)

The destination tenant is where InToolz **creates, assigns, renames, and deletes** content.

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

> **Tip:** The only difference between the two tenants is that **DeviceManagement\*** scopes are `Read.All` on the source and `ReadWrite.All` on the destination.

---

## Next steps

Once you've verified tenant access and permissions, open InToolz and head to **Settings** to sign in to your tenants. See the other wiki pages for feature-specific guides.