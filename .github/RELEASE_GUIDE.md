# Release Guide

## Option A: Auto-increment patch version (recommended)

1. Make sure all changes are merged to `master`
2. Run:
   ```
   gh workflow run release.yml
   ```
3. Wait for the workflow to complete (~4 min)
4. Go to [GitHub Releases](https://github.com/Kvikku/IntuneTools/releases) — a **draft** release will be waiting
5. Review the release notes, edit if needed, then click **Publish release**

This auto-bumps the patch version (e.g. `1.3.0.0` → `1.3.1.0`) and commits the updated version back to master.

## Option B: Specify a version (for major/minor bumps)

1. Make sure all changes are merged to `master`
2. Run:
   ```
   gh workflow run release.yml -f version=2.0.0.0
   ```
3. Same as steps 3-5 above

## What happens automatically

- Version is stamped in `IntuneTools.csproj` and `Package.appxmanifest`
- App is built, published as self-contained x64, and zipped
- Version bump is committed back to `master`
- Draft GitHub release is created with:
  - "What's Changed" (auto-generated from PRs) at the top
  - Download/Manual install sections at the bottom
  - Zip artifact attached

## The only manual step

Publishing the draft release — this is intentionally manual so you can review the notes first.
