# Release Guide

> **Copilot agent skill:** You can ask Copilot to prepare a release for you.
> It knows the versioning scheme, which files to update, and how to trigger the pipeline.
> See [`.github/agents/release.md`](agents/release.md) for the full agent instructions.

## Versioning scheme

The project uses **4-part versions**: `major.minor.patch.build` (e.g. `1.4.0.0`).

| Segment | When to increment |
|---|---|
| **Major** | Breaking changes, major new feature sets, or significant UI overhauls |
| **Minor** | New features or meaningful enhancements (e.g. a new page) |
| **Patch** | Bug fixes, small improvements, dependency bumps |
| **Build** | Conventionally `0` (reserved); the workflow preserves whatever value is in `<Version>` |

The source of truth is `InToolz.csproj` (`<Version>` element). `Package.appxmanifest` is kept in sync automatically by the `SyncAppxManifestVersion` MSBuild target during build/publish.

## Option A: Auto-increment patch version (recommended for quick fixes)

1. Make sure all changes are merged to `master`
2. Run:
   ```
   gh workflow run release.yml
   ```
3. Wait for the workflow to complete (~4 min)
4. Go to [GitHub Releases](https://github.com/Kvikku/InToolz/releases) — a **draft** release will be waiting
5. Review the release notes, edit if needed, then click **Publish release**

This auto-bumps the patch version (e.g. `1.4.0.0` → `1.4.1.0`) and commits the updated version back to master.

## Option B: Specify a version (for minor/major bumps)

1. Make sure all changes are merged to `master`
2. Bump `<Version>` in `InToolz.csproj`, commit (include `Package.appxmanifest` if it changed), and push to `master`
3. Run:
   ```
   gh workflow run release.yml -f version=2.0.0.0
   ```
4. Same as steps 3-5 from Option A

**The bump has to come first.** When you give a version, the workflow checks that `InToolz.csproj` at that commit already says it, and stops if not (see *Release checks and dry runs*).

## What happens automatically

- Release checks run first and stop the release if the commit or version looks unintended
- Version is stamped in `InToolz.csproj` and both `Package.appxmanifest` files
- App is built, published as self-contained for x64 and for ARM64 (ARM64 is cross-built on the x64 runner), and zipped, one zip per architecture
- In parallel, the Store package and a sideload package are built from `InToolzPackaged`, each holding both an x64 and an ARM64 package, and the Store package is checked (see *Microsoft Store package*)
- Version bump is committed back to `master`
- Draft GitHub release is created with:
  - "What's Changed" (auto-generated from PRs) at the top
  - Download/Manual install sections at the bottom
  - The x64 and ARM64 zips, the sideload package and its certificate attached

## Release checks and dry runs

Before anything is stamped, built or pushed, the workflow checks that the release is likely to be intended (`.github/scripts/ReleaseGuard.ps1`). It stops, with one message per problem, if:

- the commit is not on `master`
- the version is not greater than the latest release tag (a repeated or older version)
- that version is already published (a draft with the same version is fine)
- you gave a version, but `InToolz.csproj` at that commit does not already say it. This is what a premature tag looks like - the right number on a commit that does not contain the work - and the reason the version bump has to be merged first
- `ci.yml` has no successful run for the commit

The project-file check does not apply to Option A (no version given), which stamps the version itself.

Publishing a draft release creates its tag, and that tag starts this workflow again. That run notices the release is already published and ends without doing anything, so it never rebuilds or overwrites the published release.

To try the pipeline without releasing anything, add `dry_run`:

```
gh workflow run release.yml -f version=1.9.0.0 -f dry_run=true
```

A dry run reports the checks as warnings, so you can see what would have stopped a real release, then builds and zips the app, builds and checks the Store package, and previews the release notes in the run summary. It does not push the version bump or create the draft release. Add `--ref <branch>` to run it from a branch, which is how to test changes to the pipeline itself.

The checks have their own tests, which CI also runs: `pwsh -NoProfile -File .github/scripts/ReleaseGuard.Tests.ps1`.

## Release notes

The workflow auto-generates "What's Changed" from merged PRs. For polished releases, add a **Highlights** section above the auto-generated content summarising the most important user-facing changes in 2-4 bullet points.

## Microsoft Store package

The GitHub release attaches self-contained zips (x64 and ARM64), a signed sideload package, and its certificate. The Store gets a different package, built from the same `InToolzPackaged` project. The release run builds all of it for you, so nobody builds packages by hand any more; uploading the Store package in Partner Center is still a manual step. The GitHub release and the Store submission are independent.

Every release run, dry runs included, produces three workflow artifacts:

| Artifact | Contents |
|---|---|
| `InToolz-store` | `InToolzPackaged_<version>_x64_ARM64_bundle.msixupload`, the file to upload in Partner Center |
| `InToolz-sideload` | `InToolz_<version>_sideload.zip`: a signed `.msixbundle` with both architectures, `Install.ps1`, the certificate and the framework dependencies |
| `InToolz-sideload-cert` | `InToolz_<version>.cer`, the same certificate on its own |

The self-contained zips are artifacts too: `InToolz-x64` (`InToolz_<version>_x64.zip`) and `InToolz-arm64` (`InToolz_<version>_arm64.zip`).

The real release (not a dry run) also attaches the sideload zip and the `.cer` to the GitHub release itself, alongside the two zips of the self-contained app. Before it creates the draft, it checks that exactly those four files are there.

Download the Store package from the run:

```
gh run download <run-id> -n InToolz-store
```

Artifacts are kept for 90 days, so submit before then or run the release again.

Before the package can be uploaded, the run checks it (`.github/scripts/StorePackage.ps1`, which has its own tests). The identity name and publisher must match `InToolzPackaged/Package.StoreAssociation.xml`, the version must be the release version, the bundle must hold exactly one x64 and one ARM64 application package, the `Application Id` must be `App` (part of the app's identity, so pins and shortcuts survive an update), and the `runFullTrust` capability must be present. Each application package is checked on its own, and its executable must be in the package **and built for that package's architecture** (read from the executable's PE header): a package labelled ARM64 around an x64 executable would still install and run, emulated, so nothing else would notice. A package that fails stops the run, even a dry run.

### ARM64

ARM64 is part of the same Store submission and package family, so ARM64 PCs get the native build as an ordinary update and existing x64 users are unaffected. The pipeline builds it on the x64 runner (a cross-build) and checks the result as described above. CI also builds the app and runs the whole unit-test suite natively on a `windows-11-arm` runner.

What none of that proves is that the app looks right and signs in on a real ARM64 PC. The sign-in risk is small, because nothing in it is architecture-specific: the app references only the core `Microsoft.Identity.Client` package, with no embedded-browser (WebView2) or Windows-broker support, so MSAL uses the system browser with a loopback (`http://localhost`) listener on every architecture ([MSAL.NET: using web browsers](https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/using-web-browsers)). It is still worth one real check before the first ARM64 submission.

#### Checking a build on an ARM64 PC

Take the `InToolz-arm64` artifact of the release run (`InToolz_<version>_arm64.zip`), extract it and run `InToolz.exe`. No certificate is needed. (To check the Store-package path instead, run `Install.ps1` from the `InToolz-sideload` artifact: it installs the ARM64 package on an ARM64 PC.)

1. **It runs natively.** Open the newest folder under `%LOCALAPPDATA%\InToolz` and read `Summary.log`: it should say `Process Architecture: Arm64`. If it says `X64`, the x64 build is running under emulation.
2. **Demo Mode.** In Settings choose *Enable Demo Mode*, then open every page. The data grids fill, sorting and the right-click menu work, and CSV export works. This exercises the whole UI natively without a tenant.
3. **One real sign-in.** In Settings sign in to a test tenant as the source. The default browser opens, sign-in completes, and the app shows the tenant name and the permission check.
4. Note anything odd on issue #112.

The sideload package has the **same identity as the Store app**, so installing it over a Store install means uninstalling the Store version first and losing its local settings.

### Sideload signing certificate

The sideload package is signed with a persistent self-signed certificate (`CN=548133C8-71E9-434E-BD81-2DCFE6F02BD1`, matching the app's `Publisher` — a signing certificate's subject must match the manifest's `Publisher` or the package won't validate). Because it's the same certificate on every release, users who installed one release only ever need to trust its `.cer` once.

The certificate lives as two secrets, `SIDELOAD_CERT_PFX_BASE64` and `SIDELOAD_CERT_PASSWORD`, in the **`release`** GitHub Environment, which is restricted to deployments from `master` or a version tag (`*.*.*.*`). The `package` job only requests that environment on those refs (`environment: ${{ (github.ref_name == 'master' || github.ref_type == 'tag') && 'release' || '' }}`), so the secrets are never reachable from a feature branch, and a dry run from a branch falls back to a certificate generated for that build only — the same thing that happened before this certificate existed.

The certificate itself (generated 2026-11-03, expires 2029-11-03) is **exportable**, so it can be rotated before it expires: generate a new one with the same `Subject`, export it to a `.pfx`, and overwrite the two secrets. The release after a rotation signs with the new certificate, and prior installs need to trust its new `.cer` once more — call that out in the release notes when it happens.

### Building it locally

The release run uses this same command. It needs Visual Studio's MSBuild (`dotnet build` can't build a `.wapproj`), so run it from a Developer Command Prompt for VS, at the release commit, once the version in `InToolz.csproj` is the release version:

```
msbuild InToolzPackaged\InToolzPackaged.wapproj /restore /p:Configuration=Release /p:Platform=x64 /p:UapAppxPackageBuildMode=StoreUpload /p:AppxBundle=Always "/p:AppxBundlePlatforms=x64|ARM64" /p:AppxAutoIncrementPackageRevision=False /p:GenerateAppxPackageOnBuild=true
```

The result is `InToolzPackaged\AppPackages\InToolzPackaged_<version>_x64_ARM64_bundle.msixupload` (gitignored). `AppxAutoIncrementPackageRevision` must stay `False`: the Store reserves the last part of the version, so a `.1` means something auto-incremented it. `git status` should be clean afterwards.

## Submitting to the Microsoft Store

`.github/workflows/store-submit.yml` submits an already-built `InToolz-store` artifact to Partner Center using the [Microsoft Store Developer CLI](https://learn.microsoft.com/windows/apps/publish/msstore-dev-cli/overview) (`msstore`). It's deliberately independent of `release.yml`: the package is built once per release, and submitting it is a separate, re-runnable act — nothing is rebuilt or re-signed to submit it.

```
gh workflow run store-submit.yml -f run_id=<release run ID> -f version=<version> -f dry_run=false
```

- `run_id` — the `release.yml` run whose `InToolz-store` artifact to submit (same run ID you'd pass to `gh run download`). Artifacts are kept for 90 days.
- `version` — checked against the downloaded package via the same `StorePackage.ps1` check `release.yml` already runs, so a mismatched or stale artifact is refused.
- `dry_run` — **defaults to `true`**. A dry run only validates the four Partner Center credentials and the package (`msstore info`, `msstore apps get`) and makes no Partner Center changes at all. Only `dry_run=false` touches anything.

### The two-step submission, and why

| Step | Command | Reversible? |
|---|---|---|
| Stage | `msstore publish . -i <package> -id <appId> --noCommit` | Yes — uploads the package into the draft submission, but the draft can still be discarded or edited in Partner Center. |
| Commit | `msstore submission publish <appId>` | No — starts certification. This is the point of no easy return. |

Both steps only run when `dry_run=false`.

### Two independent gates before a real submission

1. **The `store-submit` GitHub Environment** is restricted to the `master` branch and has a required reviewer (currently `Kvikku`) — every run, dry run or not, pauses for that approval before the job can even start, since that's also when the environment's secrets become reachable.
2. **`dry_run` defaults to `true`** — submitting for real needs an explicit `-f dry_run=false`, on top of the approval above.

### One-time setup

The app's first publication to the Store must already have happened through the Partner Center UI (it has — 1.8.0.0 is live). After that, the CLI handles every subsequent update. To get the four credentials:

1. **Register a Microsoft Entra ID application** (if one doesn't already exist for this): [entra.microsoft.com](https://entra.microsoft.com/) → Identity → Applications → App registrations → New registration. No redirect URI is needed.
2. **Tenant ID**: Identity → Overview → "Tenant ID".
3. **Client ID**: the app registration's "Application (client) ID".
4. **Client secret**: the app registration → Certificates & secrets → New client secret. Copy the value immediately — it's only shown once.
5. **Associate the app with Partner Center**: [partner.microsoft.com/dashboard](https://partner.microsoft.com/dashboard) → Account settings → User management → Microsoft Entra applications → Add Azure AD application → select the app from step 1 → assign it the **Manager** role.
6. **Seller ID**: Partner Center → Account settings → Developer settings (or Organization profile → Identifiers) → "Seller ID" / "Publisher ID".

Store all four as secrets in the `store-submit` environment (never as plain repo secrets — that would skip the branch restriction):

```
gh secret set PARTNER_CENTER_TENANT_ID --env store-submit
gh secret set PARTNER_CENTER_SELLER_ID --env store-submit
gh secret set PARTNER_CENTER_CLIENT_ID --env store-submit
gh secret set PARTNER_CENTER_CLIENT_SECRET --env store-submit
```

The Store product ID doesn't need a secret — it's public (it's the same ID in the Store listing URL) and already set as the `STORE_APP_ID` variable on the `store-submit` environment, read from `InToolzPackaged/Package.StoreAssociation.xml`'s `LandingUrl`.

## Wiki / user documentation

Wiki pages live in `wiki/` in the main repo and are automatically synced to the GitHub Wiki on every push to `master` (via `.github/workflows/wiki-sync.yml`). The wiki should be kept in sync with the code — update it in the same PR as the feature.

**For each minor or major release, review:**

- Do any page descriptions mention capabilities that have changed?
- Are all new content types listed in the relevant pages' Supported Content Types tables?
- Are any new pages or major features missing a wiki page?
- Does `wiki/Home.md` link to all current pages?

Pages most likely to need updates when adding features:

| Wiki page | Update when… |
|---|---|
| `Home.md` | A new wiki page is added |
| `Cleanup.md` | New content types, new scan modes |
| `Renaming.md` | New rename modes |
| `Assignment.md` | Assignment dialog or workflow changes |
| `JSON-Import-Export.md` | New exportable content types |
| `Manage-Assignments.md` | New content types |

## Manual steps before publishing

1. Update the wiki in the same PR as the feature (or in the release branch PR).
2. Publish the draft GitHub release after reviewing the auto-generated notes.
