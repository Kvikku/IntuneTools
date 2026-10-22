# Release Checklist

What to verify before tagging a release and submitting to the Microsoft Store.

CI covers the build and the unit tests on every branch, so this document is only
about what CI *cannot* cover: behaviour that needs a real tenant, and behaviour
that only appears in a packaged build. For how to actually cut the release, see
[`.github/RELEASE_GUIDE.md`](../.github/RELEASE_GUIDE.md).

> Store certification is the real per-release cost, so releases get batched.
> That makes this checklist the gate — a rejected or broken submission is
> expensive to redo.

## How to use this

1. Work through **Standing checks** — these recur every release.
2. Work through the **current release's** section for anything version-specific.
3. Do a Demo Mode pass first (see below) so live tenant time goes only to things
   that genuinely need a tenant.

## Demo Mode first

Demo Mode (Settings → Enable Demo Mode) backs both tenants with in-memory sample
data and no sign-in, so a lot can be checked before touching a tenant:

- Every page loads, searches and lists content
- Destructive flows end to end, including confirmation dialogs and bulk warnings
- Client-side validation rules — these never reach Graph, so Demo Mode is a
  complete test of them
- Any new UI

What Demo Mode cannot tell you: whether Graph accepts a request, whether a scope
is consented, how large tenants paginate, or how the packaged app behaves.

## Standing checks

### Consent and permissions

- [ ] Sign in to **source** with an account that consented under the *previous*
      released version. If this release added a scope, expect the post-sign-in
      check to auto-open and report it missing.
- [ ] Grant consent, confirm the warning clears and the tenant status row
      returns to normal
- [ ] Sign in as a **non-admin in a tenant that requires admin consent** —
      confirm a readable failure, no crash, no half-initialised client
- [ ] Sign in to **destination** and confirm its scope list is as intended
      (it deliberately differs from source — see `DestinationUserAuthentication.DefaultScopes`)
- [ ] Confirm a permission warning survives navigating away from Settings and
      back (the page is `NavigationCacheMode="Required"`)

### Destructive operations

- [ ] For each delete path touched this release: delete **one throwaway object**
      and confirm only what was intended was removed
- [ ] Confirm the confirmation dialog states the actual consequence, and that
      the bulk warning fires above 10 items
- [ ] Attempt one delete *without* the required scope consented — confirm the
      failure is reported, not silent

### Scale

- [ ] Run against a tenant with **more than 1000 objects** in at least one
      collection being listed. Paging bugs only appear past the first page.

### Packaging and Store

- [ ] Version stamped correctly in `IntuneTools.csproj`, and `Package.appxmanifest`
      picked it up via the `SyncAppxManifestVersion` target
- [ ] **Fresh install with no prior app data** — first-run paths, persisted-state
      defaults, log folder creation under `LocalApplicationData`
- [ ] **Upgrade over the previous installed version** — persisted settings,
      search filters and grid state still load rather than throwing
- [ ] Exercise filesystem-touching features in the **packaged** build
      specifically: file pickers, CSV/JSON export, "Open log file location".
      Package identity behaves differently from a loose `dotnet run` build.
- [ ] Confirm MSAL interactive sign-in works from the packaged app — redirect
      URI behaviour differs under package identity
- [ ] Confirm the attached release artifact matches what the release notes tell
      users to do with it

## Current release: 1.8.0.0

Branch: `test/1.8-live-tenant-verification`

### New Graph scope — highest risk, affects every existing user

`Device.ReadWrite.All` was added to `SourceUserAuthentication.DefaultScopes` for
Entra device deletion. Everyone who consented under 1.7 must re-consent.

- [ ] Returning 1.7 user signs in to source → **missing 1 of 18** scopes
- [ ] Non-admin in a tenant requiring admin consent → graceful failure
- [ ] Destination shows **17** scopes and never prompts for `Device.ReadWrite.All`
      (correct: `CleanupPage` only ever uses `sourceGraphServiceClient`)

### Stale device cleanup — destructive, no undo

- [ ] Reconcile both scan counts against the portal. Intune uses
      `lastSyncDateTime`, Entra uses `approximateLastSignInDateTime`, and
      **null/never counts as stale**.
- [ ] All threshold presets (30/60/90/120/180) move the counts sensibly
- [ ] Threshold persists across an app restart
- [ ] Delete one throwaway **Intune** device record → record removed, device
      **not** retired or wiped
- [ ] Delete one throwaway **Entra** device object → this is the call needing
      the new scope
- [ ] Tenant with 1000+ devices — paging through `managedDevices` and `devices`
      is untested

### App assignment — issue #104

- [ ] All Devices + **Required** → succeeds
- [ ] All Devices + **Uninstall** → succeeds
- [ ] All Devices + **Available** → still blocked by the validation dialog
- [ ] **iOS VPP app, device licensing True, All Devices, Required** — the
      reporter's exact case. Confirms Graph accepts it, not just that the
      client-side guard passes.
- [ ] Assign to an app that **already has assignments** → existing ones survive
      (`assign` is a full replace, so the merge step is the risk)
- [ ] Log reads `- Intent: Required`, not `Available`

### Permission reporting

- [ ] Tenant with complete consent → "All Permissions Granted"
- [ ] Tenant with **leftover consent from an older version** → extras listed
      under "Also granted, but not used", not folded into the counts
- [ ] Tenant where the check itself lacks `Application.Read.All` /
      `DelegatedPermissionGrant.Read.All` → unnecessary-scope lookup degrades
      quietly, missing-permission result still shows

### Settings page (rebuilt UI)

- [ ] Sign in, sign out, clear token and swap tenants on the new
      `SettingsCard`/`SettingsExpander` layout
