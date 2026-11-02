# Introduction

On the settings page you will authenticate to your Azure tenant(s).

The source tenant section will be used for most operations in the app. This includes
* Cleanup
* Renaming
* Assignments
* Source tenant for import

The destination tenant will only be used as a destination for importing. It is therefore not necessary to set up and authenticate to it if you do not plan to use the import feature.

---

## Signing in

Each tenant has its own section with three actions:

| Action | Description |
|---|---|
| **Login** | Sign in to the tenant |
| **Clear Token** | Clear the cached sign-in token for that tenant |
| **View Permissions** | Compare the Graph permissions that were granted with the ones this version needs |

**Swap Tenants** exchanges the source and destination sign-ins, which is useful if you signed in to the wrong one.

---

## Permission checks

Right after a successful sign-in, InToolz checks the permissions that were granted against the ones it needs (listed in [Getting Started](Getting-started)).

- **Everything granted** — nothing pops up; the result is only written to the log.
- **Something missing** — the permissions dialog opens by itself and the tenant's status row is flagged, so a gap shows up here and not as a confusing failure on another page.

You can open the same dialog at any time with **View Permissions**.

The dialog can also list **"Also granted, but not used by this version of the tool"**. These are permissions your tenant consented to for an earlier version (or granted more broadly than needed). They are only a note — they are not counted as missing or granted, and nothing breaks because of them. If InToolz isn't allowed to read the tenant's consent grants, this list is left out and the missing-permissions result is still shown.

---

## Demo Mode

No tenant available? **Enable Demo Mode** loads fictitious sample data into both the source and the destination tenant, so you can click through every page without signing in anywhere. This is handy for trying the app out, or for rehearsing a bulk operation before you run it for real.

- The sample data lives in memory only, so any changes you make are gone when the app closes.
- Graph permissions don't apply: **View Permissions** shows a Demo Mode notice instead.
- Signing in to a real tenant switches that tenant back to live data.
