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

## The only manual step

Publishing the draft release — this is intentionally manual so you can review the notes first.
