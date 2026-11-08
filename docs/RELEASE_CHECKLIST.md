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

- [x] Sign in to **source** with an account that consented under the *previous*
      released version. If this release added a scope, expect the post-sign-in
      check to auto-open and report it missing.
- [x] Grant consent, confirm the warning clears and the tenant status row
      returns to normal
- [ ] Sign in as a **non-admin in a tenant that requires admin consent** —
      confirm a readable failure, no crash, no half-initialised client
- [x] Sign in to **destination** and confirm its scope list is as intended
      (it deliberately differs from source — see `DestinationUserAuthentication.DefaultScopes`)
- [ ] Confirm a permission warning survives navigating away from Settings and
      back (the page is `NavigationCacheMode="Required"`)

### Destructive operations

- [x] For each delete path touched this release: delete **one throwaway object**
      and confirm only what was intended was removed
- [ ] Confirm the confirmation dialog states the actual consequence, and that
      the bulk warning fires above 10 items
- [ ] Attempt one delete *without* the required scope consented — confirm the
      failure is reported, not silent

### Scale

- [ ] Run against a tenant with **more than 1000 objects** in at least one
      collection being listed. Paging bugs only appear past the first page.

### Packaging and Store

- [ ] Version stamped correctly in `InToolz.csproj`, and `Package.appxmanifest`
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

## Current release: 1.9.0.0

A backend and packaging release: the rename to InToolz, a native ARM64 build, and
a rebuilt release pipeline. **No new Graph scope** (the scope lists are identical to
1.8's), so there is no re-consent heads-up this time.

### The rename and the in-place upgrade — highest risk

The package identity (`JrgenSundet.IntuneTools`), `Application Id="App"` and the
publisher are unchanged on purpose, so the Store treats 1.9 as an update of the same
app. That is checked from the package (the release run's Store package check) but has
never been seen on a real upgrade.

- [ ] **Upgrade a 1.8 Store install to 1.9** — through a Partner Center package
      flight to your own account, or a staged rollout. A sideload can't test this: it
      has the same identity as the Store app, so installing it means uninstalling
      first. Confirm the app updates in place, the Start menu pin or tile survives,
      and saved page state and the `%LOCALAPPDATA%\InToolz` data are still there.
- [ ] The Start menu entry, tile and window title say **InToolz**; the executable
      is `InToolz.exe`
- [ ] Home page: the GitHub and Issues links open the renamed repository, and the
      update indicator still works (it now asks `kvikku/InToolz`)
- [ ] After the GitHub repository is renamed, and **before releasing**, shipped
      1.8 installs can still see updates: `curl -sL https://api.github.com/repos/kvikku/IntuneTools/releases/latest`
      still returns the latest release (they follow GitHub's redirect). Nobody may
      ever create a new repository called `Kvikku/IntuneTools`.

### ARM64 — a new architecture

- [ ] The release run's Store package check passes: one x64 and one ARM64 package,
      each with an executable built for its architecture
- [ ] On a **native ARM64 PC**, run the script in `.github/RELEASE_GUIDE.md`
      ("Checking a build on an ARM64 PC"): `Process Architecture: Arm64` in
      `Summary.log`, every page in Demo Mode, one real sign-in
- [ ] x64 is unaffected: the same Demo Mode pass on an x64 PC from the Store
      package. The Store picks the package per device, so this is the architecture
      most users are on.
- [ ] Partner Center, after uploading: the submission lists both packages

### Sideload package and zips — attached to the release for the first time

1.8 attached only a zip. 1.9 attaches an x64 zip, an ARM64 zip, a signed sideload
package for both and its certificate.

- [ ] `InToolz_<version>_x64.zip` and `_arm64.zip`: extract, run `InToolz.exe`
- [ ] `InToolz_<version>_sideload.zip`: trust the `.cer` once, run `Install.ps1`,
      the app installs with a Start menu entry. (Uninstall the Store version first.)
- [ ] The **Manual install** section of the release notes matches the files that
      are actually attached

### Page rules moved out of code-behind (#116)

These had no tests and the UI can't run in CI, so click through them once. Demo Mode
covers all but the duplicates.

- [ ] **Renaming**, each of the 7 modes in turn: the controls shown match the mode
      (prefix style button, replace box, placeholder text), and a rename on one
      throwaway object does what the label says. *Remove Prefix* with the box blank
      still removes a bracket-style prefix; *Remove Suffix* with the box blank asks
      for text instead of doing anything.
- [ ] **Bulk prompts**: assign, rename, import, remove assignments and delete (both
      paths) each warn at 10 items and not at 9
- [ ] **Assignment** with no group selected and neither All Users nor All Devices:
      the dialog refuses to close and says why
- [ ] **Cleanup, duplicates** (needs two same-named throwaway policies in a test
      tenant): *Select older* keeps the newest in each group; *Select unassigned*
      selects only items known to be unassigned, and only in groups where another
      item is assigned

### Fixed since 1.8

- [ ] Listing or searching groups when Graph returns an error (a source account
      without `Group.Read.All` consent is one way) logs a warning and shows an empty
      list instead of ending in an unhandled error

### Release notes

- [ ] **Highlights** written (a draft is in the version-bump PR)
- [ ] A **heads-up for zip users**: the executable is now `InToolz.exe` (it was
      `IntuneTools.exe`) and the zip is `InToolz_<version>_x64.zip`, so an AppLocker
      or Defender rule keyed on the old file name needs updating

## Previous release: 1.8.0.0

Branch: `test/1.8-live-tenant-verification`

### New Graph scope — highest risk, affects every existing user

`Device.ReadWrite.All` was added to `SourceUserAuthentication.DefaultScopes` for
Entra device deletion. Everyone who consented under 1.7 must re-consent.

- [x] Returning 1.7 user signs in to source → **missing 1 of 17** scopes (dialog: 16 granted, 1 missing)
- [ ] Non-admin in a tenant requiring admin consent → graceful failure
- [x] Destination shows **16 of 16** granted and never prompts for `Device.ReadWrite.All`
      (correct: `CleanupPage` only ever uses `sourceGraphServiceClient`)

### Stale device cleanup — destructive, no undo

- [x] Reconcile both scan counts against the portal. Intune uses
      `lastSyncDateTime`, Entra uses `approximateLastSignInDateTime`, and
      **null/never counts as stale**.
- [ ] All threshold presets (30/60/90/120/180) move the counts sensibly
- [ ] Threshold persists across an app restart
- [x] Delete one throwaway **Intune** device record → record removed, device
      **not** retired or wiped
- [x] Delete one throwaway **Entra** device object → this is the call needing
      the new scope
- [ ] Tenant with 1000+ devices — paging through `managedDevices` and `devices`
      is untested

### App assignment — issue #104

- [x] All Devices + **Required** → succeeds
- [x] All Devices + **Uninstall** → succeeds
- [x] All Devices + **Available** → still blocked by the validation dialog
- [x] **iOS VPP app, device licensing True, All Devices, Required** — the
      reporter's exact case. Confirms Graph accepts it, not just that the
      client-side guard passes.
- [x] Assign to an app that **already has assignments** → existing ones survive
      (`assign` is a full replace, so the merge step is the risk)
- [x] Log reads `- Intent: Required`, not `Available`

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
