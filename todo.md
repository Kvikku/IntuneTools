# Unified Page UI/UX Standard - Rollout Tracker

We are migrating every page in IntuneTools to a single, modern, sleek
visual + UX standard. The standard, design tokens, and per-page migration
checklist live in **[`docs/UI_STANDARD.md`](docs/UI_STANDARD.md)**. The
shared styles live in **`Styles/PageStyles.xaml`**.

Each page should be migrated in its own PR so reviews stay small and any
visual regression is easy to bisect.

## Pages

- [x] **`Pages/RenamingPage.xaml`** - reference implementation of the
      unified standard (initial migration PR).
- [x] `Pages/CleanupPage.xaml` - same data-operation layout, swap to
      shared styles, replace the hard-coded `#C42B1C` Delete button with
      `DestructiveActionButtonStyle`.
- [x] `Pages/JsonPage.xaml` - swap to shared styles; both Export and
      Import buttons should use `PrimaryActionButtonStyle` /
      `SecondaryActionButtonStyle`.
- [x] `Pages/AssignmentPage.xaml` - largest page; migrate header,
      toolbar cards, action buttons, and the right-hand "Groups" panel
      header. Audit the long content-types `CheckBox` flyout for
      consistent indentation.
- [x] `Pages/ManageAssignmentsPage.xaml` - swap to shared styles.
- [x] `Pages/ImportPage.xaml` - swap to shared styles.
- [x] `Pages/AuditLogPage.xaml` - swap to shared styles; align the
      summary stat cards (Total Events, Unique Actors, ...) to the card
      style and standardize their typography.
- [x] `Pages/SettingsPage.xaml` - special-case landing layout; adopt the
      typography tokens (`PageTitleTextBlockStyle`,
      `PageSubtitleTextBlockStyle`) and `CardBorderStyle` only.
- [x] `Pages/HomePage.xaml` - special-case landing layout; adopt
      `CardBorderStyle` for the hero/feature cards and switch headings
      to the shared typography styles.

## Cross-cutting follow-ups (do as you migrate)

- [x] Remove all hard-coded font sizes, weights, paddings, and corner
      radii from page XAML once all pages are migrated. *Documented
      exceptions remain (see PR #81): icon glyphs / weights inside
      primary action buttons, `AppBarButton` icon sizes, inline
      `HyperlinkButton` padding, the 6 px `ProgressBar` corner radius,
      `AuditLogPage`'s full-screen `LoadingOverlay`, the
      `SettingsPage` "Swap" mini-label (`FontSize="11"`) and the
      `HomePage` `VersionStatusText` body line (`FontSize="14"`).*
- [x] Remove the per-page duplicated `LogConsole` `ListView.ItemTemplate`
      definitions in favour of a shared `DataTemplate` resource.
- [x] Consider moving the `LoadingOverlay` and `OperationStatusBar`
      blocks into a reusable `UserControl` so each page just declares
      `<utilities:OperationStatus />` instead of copy-pasting ~40 lines.
      *Done: shared `Utilities/OperationStatusBar.xaml` and
      `Utilities/LoadingOverlay.xaml` UserControls; `BaseMultiTenantPage`
      dispatches to them with a legacy fallback for `AuditLogPage`'s
      bespoke full-screen overlay.*
- [ ] Add a screenshot of the migrated Renaming page to
      `docs/UI_STANDARD.md` once the first PR ships.

## How to claim a page

1. Open a PR titled `UI: migrate <PageName> to unified standard`.
2. Follow the checklist in `docs/UI_STANDARD.md` section 9.
3. Tick the page above in the same PR.
4. Do **not** rename `x:Name`s referenced by `BaseDataOperationPage` or
   `BaseMultiTenantPage` (see UI_STANDARD section 8).

---

# Future Intune feature ideas

Backlog of ideas captured from the "what new features could we add"
brainstorm. Items are unordered within each section. Open an issue per
item before starting work so we can scope and discuss.

## Already on the README roadmap (prioritise these)

- [ ] **Import applications** — _in progress_ (initial cut: clone
      no-binary apps such as web links, store-sourced apps, and
      curated suite apps; LOB / Win32 / MSI binary upload to follow).
- [ ] Delete duplicate policies/apps — detect by display name +
      settings hash, offer merge/remove.
- [ ] Delete group assignments — bulk unassign across selected
      content types.
- [ ] Bulk add objects (devices/users) to Entra groups — pairs with
      the Assignment workflow.

## Expand supported content types

- [ ] Endpoint Security policies (ASR, BitLocker, Defender AV,
      Firewall, EDR, Account Protection, Disk Encryption for macOS).
- [ ] Conditional Access policies (import/export/clone).
- [ ] App Configuration policies (managed devices + managed apps).
- [ ] App Protection policies (MAM for iOS / Android / Windows).
- [ ] iOS / iPadOS & Android device configuration profiles.
- [ ] Android Enterprise enrollment profiles.
- [ ] Apple ADE (Automated Device Enrollment) profiles.
- [ ] VPP token-managed apps.
- [ ] macOS configuration profiles (custom `.mobileconfig`,
      FileVault, Platform SSO).
- [ ] Update rings for Windows 10/11.
- [ ] Microsoft 365 Apps update channels.
- [ ] Driver / firmware update profiles for macOS.
- [ ] Custom compliance scripts (beyond device compliance policies).
- [ ] Remediation script packages (beyond Proactive Remediation
      metadata).
- [ ] Terms & Conditions.
- [ ] Notification message templates.
- [ ] Branding / Company Portal customization.
- [ ] Role definitions and scope tags (RBAC clone).
- [ ] Enrollment Status Page (ESP) profiles.
- [ ] Autopilot device identifier import (CSV upload).

## New top-level operations

- [ ] **Backup / Restore tenant** — point-in-time snapshot of all
      supported objects to a single archive (zip of JSON), with
      diff-based restore.
- [ ] **Tenant diff / compare** — side-by-side comparison of two
      tenants (or two snapshots) showing added/removed/changed
      settings; export as HTML/Markdown report.
- [ ] **Drift detection** — compare current tenant state against a
      saved baseline and flag deviations.
- [ ] **Policy templates / baselines library** — apply CIS,
      Microsoft Security Baselines, or custom templates in one click.
- [ ] **Search across tenant** — global search box for any object by
      name / setting / assignment / group.
- [ ] **What-if assignment evaluator** — pick a user/device and show
      which policies/apps would apply (resolves include/exclude/filter
      logic locally).
- [ ] **Assignment filter editor & tester** — author filters and
      dry-run them against device inventory.
- [ ] **Stale / unused cleanup** — extend "Find Unassigned" to find
      empty Entra groups used in assignments, filters not referenced
      by anything, Autopilot devices never enrolled, apps with 0
      installs over N days.
- [ ] **Duplicate group / filter detection** with merge.

## Reporting & operations

- [ ] Device & compliance reports — list non-compliant devices, by
      policy, exportable to CSV / Excel.
- [ ] App install status report across tenants / groups.
- [ ] Assignment matrix view — group × policy heat-map.
- [ ] Audit log enhancements — saved filters, scheduled CSV export,
      cross-tenant audit comparison.
- [ ] License / seat insights for Intune SKUs.

## Scripting & automation

- [ ] PowerShell script library — curated repo of community scripts,
      one-click upload as Platform Script or Remediation.
- [ ] Schedule operations (e.g. nightly JSON backup) via Windows
      Task Scheduler integration.
- [ ] Run remediation on demand against a device or group (Graph
      `initiateOnDemandProactiveRemediation`).
- [ ] Device actions in bulk — sync, restart, wipe, retire, rename,
      rotate BitLocker / LAPS keys, collect diagnostics, with
      confirmation gates.
- [ ] LAPS password retrieval UI with audit trail.

## Migration & multi-tenant quality-of-life

- [ ] Group mapping wizard — when importing assignments, auto-map
      source group names to destination groups (with manual
      overrides), persist mapping per tenant pair.
- [ ] Filter mapping alongside group mapping during import.
- [ ] Selective import — pick specific settings within a Settings
      Catalog policy to merge into an existing destination policy
      instead of overwriting.
- [ ] Dependency resolution on import — automatically pull in
      referenced filters, scope tags, and apps required by a policy.
- [ ] Dry-run / preview mode for every write operation, with a
      downloadable change report.
- [ ] Rollback — every write produces an undo bundle (pre-change
      JSON) you can restore in one click.
- [ ] Multi-destination fan-out — push a policy to N tenants at once.

## Integrations

- [ ] Git-backed configuration — point InToolz at a Git repo;
      export changes commit them automatically (Intune-as-Code lite).
- [ ] Azure DevOps / GitHub PR integration for the JSON export
      workflow.
- [ ] Webhook / Teams notification when scheduled jobs run.
- [ ] CSV import for Autopilot hardware hashes and corporate device
      identifiers.

## UX & platform

- [ ] Per-page favorites, recent operations history, command palette
      (Ctrl+K).
- [ ] Read-only "viewer" mode for the destination tenant for safe
      demos.
- [ ] Diff viewer for JSON import showing what will change before
      applying.
- [ ] Localization (the project is currently English-only).
- [ ] CLI companion (`intooltz.exe`) reusing the Graph helpers for
      headless / CI usage.
