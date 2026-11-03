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
4. Go to [GitHub Releases](https://github.com/Kvikku/IntuneTools/releases) — a **draft** release will be waiting
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
- Version is stamped in `InToolz.csproj` and `Package.appxmanifest`
- App is built, published as self-contained x64, and zipped
- Version bump is committed back to `master`
- Draft GitHub release is created with:
  - "What's Changed" (auto-generated from PRs) at the top
  - Download/Manual install sections at the bottom
  - Zip artifact attached

## Release checks and dry runs

Before anything is stamped, built or pushed, the workflow checks that the release is likely to be intended (`.github/scripts/ReleaseGuard.ps1`). It stops, with one message per problem, if:

- the commit is not on `master`
- the version is not greater than the latest release tag (a repeated or older version)
- you gave a version, but `InToolz.csproj` at that commit does not already say it. This is what a premature tag looks like - the right number on a commit that does not contain the work - and the reason the version bump has to be merged first
- `ci.yml` has no successful run for the commit

The project-file check does not apply to Option A (no version given), which stamps the version itself.

To try the pipeline without releasing anything, add `dry_run`:

```
gh workflow run release.yml -f version=1.9.0.0 -f dry_run=true
```

A dry run reports the checks as warnings, so you can see what would have stopped a real release, then builds and zips the app and previews the release notes in the run summary. It does not push the version bump or create the draft release. Add `--ref <branch>` to run it from a branch, which is how to test changes to the pipeline itself.

The checks have their own tests, which CI also runs: `pwsh -NoProfile -File .github/scripts/ReleaseGuard.Tests.ps1`.

## Release notes

The workflow auto-generates "What's Changed" from merged PRs. For polished releases, add a **Highlights** section above the auto-generated content summarising the most important user-facing changes in 2-4 bullet points.

## Microsoft Store package

The GitHub release only attaches a self-contained zip. The Store gets a different package, built from the `InToolzPackaged` project and uploaded by hand in Partner Center. The two releases are independent.

Build it from the release commit, after the version in `InToolz.csproj` is the release version. The project needs Visual Studio's MSBuild (`dotnet build` can't build a `.wapproj`), so run this from a Developer Command Prompt for VS:

```
msbuild InToolzPackaged\InToolzPackaged.wapproj /restore /p:Configuration=Release /p:Platform=x64 /p:UapAppxPackageBuildMode=StoreUpload /p:AppxBundle=Always /p:AppxBundlePlatforms=x64 /p:AppxAutoIncrementPackageRevision=False /p:GenerateAppxPackageOnBuild=true
```

The file to upload is `InToolzPackaged\AppPackages\InToolzPackaged_<version>_x64_bundle.msixupload`. Everything under `AppPackages` is gitignored.

Before uploading, check that:

- the version in the file name matches the release and ends in `.0` — the Store reserves the last part, so a `.1` means something auto-incremented it (`AppxAutoIncrementPackageRevision` must be `False`)
- the package identity name and publisher match `InToolzPackaged\Package.StoreAssociation.xml`
- `git status` is clean afterwards (the build should not modify tracked files)

The build also writes an `..._Test` folder with an `Install.ps1` and a temporary certificate. That is a sideload package with the **same identity as the Store app**, so installing it over a Store install means uninstalling the Store version first and losing its local settings.

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
