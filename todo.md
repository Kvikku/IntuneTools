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
- [ ] `Pages/AuditLogPage.xaml` - swap to shared styles; align the
      summary stat cards (Total Events, Unique Actors, ...) to the card
      style and standardize their typography.
- [ ] `Pages/SettingsPage.xaml` - special-case landing layout; adopt the
      typography tokens (`PageTitleTextBlockStyle`,
      `PageSubtitleTextBlockStyle`) and `CardBorderStyle` only.
- [ ] `Pages/HomePage.xaml` - special-case landing layout; adopt
      `CardBorderStyle` for the hero/feature cards and switch headings
      to the shared typography styles.

## Cross-cutting follow-ups (do as you migrate)

- [ ] Remove all hard-coded font sizes, weights, paddings, and corner
      radii from page XAML once all pages are migrated.
- [ ] Remove the per-page duplicated `LogConsole` `ListView.ItemTemplate`
      definitions in favour of a shared `DataTemplate` resource.
- [ ] Consider moving the `LoadingOverlay` and `OperationStatusBar`
      blocks into a reusable `UserControl` so each page just declares
      `<utilities:OperationStatus />` instead of copy-pasting ~40 lines.
- [ ] Add a screenshot of the migrated Renaming page to
      `docs/UI_STANDARD.md` once the first PR ships.

## How to claim a page

1. Open a PR titled `UI: migrate <PageName> to unified standard`.
2. Follow the checklist in `docs/UI_STANDARD.md` section 9.
3. Tick the page above in the same PR.
4. Do **not** rename `x:Name`s referenced by `BaseDataOperationPage` or
   `BaseMultiTenantPage` (see UI_STANDARD section 8).
