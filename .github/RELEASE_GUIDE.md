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

The source of truth is `IntuneTools.csproj` (`<Version>` element). `Package.appxmanifest` is kept in sync automatically by the `SyncAppxManifestVersion` MSBuild target during build/publish.

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
2. Bump `<Version>` in `IntuneTools.csproj`, commit (include `Package.appxmanifest` if it changed), and push to `master`
3. Run:
   ```
   gh workflow run release.yml -f version=2.0.0.0
   ```
4. Same as steps 3-5 from Option A

## What happens automatically

- Version is stamped in `IntuneTools.csproj` and `Package.appxmanifest`
- App is built, published as self-contained x64, and zipped
- Version bump is committed back to `master`
- Draft GitHub release is created with:
  - "What's Changed" (auto-generated from PRs) at the top
  - Download/Manual install sections at the bottom
  - Zip artifact attached

## Release notes

The workflow auto-generates "What's Changed" from merged PRs. For polished releases, add a **Highlights** section above the auto-generated content summarising the most important user-facing changes in 2-4 bullet points.

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
