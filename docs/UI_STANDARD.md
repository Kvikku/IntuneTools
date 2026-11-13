# InToolz - UI Standard

This document defines the unified visual and UX standard that every page in
InToolz should follow. The goal is to make the app feel like a single,
modern, sleek Fluent application rather than a collection of pages that each
made their own decisions.

The shared styles live in **`Styles/PageStyles.xaml`** and are merged into
`App.xaml`, so any page can reference them with `{StaticResource ...}`.

`Pages/RenamingPage.xaml` is the reference implementation - copy its
structure when migrating other pages.

> **2.0 "Command Center" shell.** The `NavigationView` sidebar described in
> this document has been replaced by a persistent top bar (logo, Ctrl+K
> command palette, tenant pills) plus a shell-level breadcrumb — see
> **§14** below for what changed and what every page can now assume the
> shell already provides. Sections 1-13 below still describe each page's
> *internal* layout (header, toolbar cards, grid + side panel), which is
> unchanged; only the chrome around it moved up into `MainWindow`.

---

## 1. Page anatomy

Every "data operation" page (Renaming, Cleanup, Assignment, JSON, Import,
Manage Assignments, Audit Log) follows the same three-row layout:

```
+-----------------------------------------------------------------+
| Row 0  Header                                                   |
|        - Title              (PageTitleTextBlockStyle)           |
|        - Subtitle           (PageSubtitleTextBlockStyle)        |
|        - TenantInfoBar      (Informational, hidden by default)  |
|        - OperationStatusBar (with ProgressRing/ProgressBar)     |
+-----------------------------------------------------------------+
| Row 1  Toolbar cards (StackPanel, Orientation=Horizontal,       |
|        Spacing=12)                                              |
|        +------------------+  +-----------------------+          |
|        | Search & Staging |  | <Page-specific actions|>         |
|        +------------------+  +-----------------------+          |
+-----------------------------------------------------------------+
| Row 2  Main content                                             |
|        +-------------------+ || +------------------+            |
|        | DataGrid + Info   | || | Side panel       |            |
|        | + Loading overlay | || | (Log Console,    |            |
|        |                   | || |  Groups, etc.)   |            |
|        +-------------------+ || +------------------+            |
+-----------------------------------------------------------------+
```

Settings and Home are landing/configuration pages and have their own
single-purpose layouts; they should still consume the typography and card
styles from this standard, but are not bound to the three-row data layout.

## 2. Spacing tokens

Defined in `Styles/PageStyles.xaml`. Pages should reference the tokens
instead of hard-coding numbers.

| Token                      | Type / Value          | Use                                                     |
| -------------------------- | --------------------- | ------------------------------------------------------- |
| `PageRootMargin`           | `Thickness 24,24,24,24` | Outer margin of the page root `Grid`                  |
| `PageHeaderBottomMargin`   | `Thickness 0,0,0,20`  | `Margin` of the header `StackPanel` (Row 0)             |
| `PageSectionSpacing`       | `Double 12`           | `Spacing` between toolbar cards (Row 1 `StackPanel`)    |
| `CardCornerRadius`         | `CornerRadius 8`      | `CornerRadius` of every card (consumed by `CardBorderStyle`) |
| `CardPadding`              | `Thickness 16,12,16,12` | Inner `Padding` of every card (consumed by `CardBorderStyle`) |

## 3. Typography

| Style                              | Size / Weight     | Use                                       |
| ---------------------------------- | ----------------- | ----------------------------------------- |
| `PageTitleTextBlockStyle`          | 32 / SemiBold     | Single page title at the top              |
| `PageSubtitleTextBlockStyle`       | 14 / Regular      | One-line description under the title      |
| `SectionTitleTextBlockStyle`       | 20 / SemiBold     | Prominent in-card section title (e.g. SettingsPage tenant cards) |
| `SidePanelHeaderTextBlockStyle`    | 16 / SemiBold     | "Log Console", "Groups", etc.             |
| `SubsectionLabelTextBlockStyle`    | 14 / SemiBold     | Sub-section label inside flyouts / option lists ("Prefix Format") |
| `CardSectionLabelTextBlockStyle`   | 12 / SemiBold     | Caption above a toolbar card row          |

Rules:

- **Only one page title per page.** Avoid centred or `Bold` titles outside
  this style.
- Subtitles are sentence case, end with a period, and describe what the page
  does in one short line.
- Card section labels are Title Case ("Search & Staging", "Rename
  Configuration", "Destructive Action").

## 4. Surfaces

- **`CardBorderStyle`** is the only acceptable container for toolbar groups
  and configuration groups. Do not roll a custom `Border` with manually
  copied brushes/padding.
- Cards are arranged horizontally with `Spacing="12"`. They wrap naturally
  because each card sizes to its content.
- The DataGrid sits on the page background, not inside a card.

## 5. Buttons

| Style                          | When to use                                                                  |
| ------------------------------ | ---------------------------------------------------------------------------- |
| `PrimaryActionButtonStyle`     | The single most important action of a card (Update, Export, Assign...)       |
| `SecondaryActionButtonStyle`   | Supporting actions of equal shape but lower emphasis                         |
| `DestructiveActionButtonStyle` | Permanent / dangerous actions (Delete All)                                   |
| `AppBarButton`                 | Icon-first toolbar verbs (Search, List All, Clear Selected, Clear All, ...)  |

All custom buttons are 36px high with 16px horizontal padding. Icon buttons
embed a `FontIcon` (`FontSize="16"`) plus a `TextBlock`, separated by a
`Spacing="8"` `StackPanel`. Do **not** use `Height="40"` or ad-hoc
`Background="#C42B1C"` colours - use `DestructiveActionButtonStyle`.

## 6. Status & feedback

- Every data page exposes a `TenantInfoBar` (informational, hidden by
  default) and an `OperationStatusBar` (the shared
  `InToolz.Utilities.OperationStatusBar` `UserControl`) for bulk-operation
  feedback. Names must stay constant so `BaseDataOperationPage` /
  `BaseMultiTenantPage` can find and dispatch to them.
- Long operations show the `LoadingOverlay` (the shared
  `InToolz.Utilities.LoadingOverlay` `UserControl`). Pages with bespoke
  overlay needs (e.g. `AuditLogPage`'s full-screen overlay with a Cancel
  button) keep their hand-rolled overlay and rely on the legacy fallback in
  `BaseMultiTenantPage`. Do not invent new spinners.
- Use `InfoBar` (not custom yellow rectangles) for staging-area guidance.

## 7. Search & Staging toolbar

Every data page that supports search plus staged bulk actions (Renaming,
Cleanup's Delete panel, Manage Assignments, Import, JSON) uses the shared
`InToolz.Utilities.SearchStagingBar` `UserControl` instead of hand-rolling
the TextBox + Search / List All / Clear Selected / Clear All / Clear Log /
Export CSV button cluster. Drop it into the "Search & Staging" card:

```xml
<utilities:SearchStagingBar x:Name="SearchStagingBar"
                             SearchRequested="SearchStagingBar_SearchRequested"
                             ListAllRequested="ListAllButton_Click"
                             ClearSelectedRequested="ClearSelectedButton_Click"
                             ClearAllRequested="ClearAllButton_Click"
                             ClearLogRequested="ClearLogButton_Click"
                             ExportCsvRequested="ExportCsvButton_Click"/>
```

The control raises events rather than performing any Graph or staging logic
itself — each page keeps its own validation, `PageStatePersistence` calls,
and orchestrator calls in the event handler, exactly as before. Useful
members:

- `SearchText` — get/set the search box text (used to restore the last
  saved query on page load).
- `SetSearchAndListEnabled(bool)` — replaces the old
  `SearchButton.IsEnabled = ...; ListAllButton.IsEnabled = ...;` pairs in
  `ShowLoading`/`HideLoading` overrides.
- `SetClearButtonsEnabled(bool)` — for pages that lock staging edits during
  a multi-step scan (e.g. Cleanup's Find Unassigned).
- `ExtraButtons` — a `UIElement` inserted between "List All" and "Clear
  Selected" for a page-specific verb that belongs in this cluster (Cleanup's
  "Find Unassigned" button, Import's "Content Types" flyout). Set it via
  property-element syntax (`<utilities:SearchStagingBar.ExtraButtons>`).

Because `SearchStagingBar` is a single named control, list just
`"SearchStagingBar"` in `GetManagedControlNames()` instead of each individual
button — `IsEnabled = false` on the control cascades to every child
automatically.

Pages with a fundamentally different search UX (Assignment's `AutoSuggestBox`,
which filters already-loaded content client-side rather than calling Graph)
keep their own toolbar and are not migrated to this control.

## 8. Side panels

- The right-hand side panel uses a `GridSplitter` with `Width="8"` and
  `Background="Transparent"`.
- Default panel width is `340`, `MinWidth="200"`.
- The panel header uses `SidePanelHeaderTextBlockStyle` (16 / SemiBold).
  No more `FontSize="20"` titles or negative-margin alignment hacks.
- The log `ListView` uses `LogListViewItemContainerStyle` and the three
  `Log*TextBlockStyle` styles for timestamp / level / message.

## 9. Naming conventions (controls referenced from code-behind)

These names are part of the implicit contract with `BaseDataOperationPage`
and `BaseMultiTenantPage` and **must not be renamed** when migrating a page:

`TenantInfoBar`, `OperationStatusBar`, `LoadingOverlay`, `LogConsole`.
(`OperationStatusBar` and `LoadingOverlay` are the shared `UserControl`s
under `InToolz.Utilities`; pages that still use a hand-rolled overlay
must additionally keep `LoadingProgressRing` and `LoadingStatusText` so the
legacy fallback in `BaseMultiTenantPage` still works.)

`SearchStagingBar` follows the same rule on pages that use it (see section 7)
— its name is referenced directly in `GetManagedControlNames()` and in
`ShowLoading`/`HideLoading` overrides.

## 10. Migration checklist for a page

When migrating an existing page to this standard:

1. Replace the root `Grid Margin="20"` with
   `Grid Margin="{StaticResource PageRootMargin}"`.
2. Replace the title `TextBlock` with `Style="{StaticResource PageTitleTextBlockStyle}"`
   (drop `FontSize`, `FontWeight`, `HorizontalAlignment`).
3. Replace the subtitle `TextBlock` with `Style="{StaticResource PageSubtitleTextBlockStyle}"`.
4. Replace every toolbar `Border` with `Style="{StaticResource CardBorderStyle}"`.
5. Replace card section labels with `Style="{StaticResource CardSectionLabelTextBlockStyle}"`.
6. Convert primary buttons to `Style="{StaticResource PrimaryActionButtonStyle}"`,
   destructive buttons to `Style="{StaticResource DestructiveActionButtonStyle}"`.
7. Replace the side-panel header with `Style="{StaticResource SidePanelHeaderTextBlockStyle}"`
   and remove any `Margin="0,-20,0,0"` alignment hacks.
8. Apply `LogListViewItemContainerStyle` and the `Log*TextBlockStyle`
   styles to the log console.
9. Confirm the page still builds and that all `x:Name` references in the
   code-behind resolve unchanged.

Track per-page progress in `docs/todo.md`.

## 11. HomePage feature cards

The HomePage "Get started" tiles are clickable `Button`s styled to look like
cards. Use `FeatureCardButtonStyle` instead of hand-rolling the
`Padding` / `CornerRadius` / `Background` / `BorderBrush` quartet. Inside,
use `FeatureCardTitleTextBlockStyle` for the card title and
`FeatureCardDescriptionTextBlockStyle` for the one-line description.

For dashboard widgets, prefer `StatHeroValueTextBlockStyle` /
`StatHeroLabelTextBlockStyle` for the big number + label pair, and
`StatBreakdownValueTextBlockStyle` / `StatBreakdownLabelTextBlockStyle`
for compact key/value rows. Use `MutedCaptionTextBlockStyle` for small,
low-emphasis labels (e.g. "Tenant" above a tenant name).

## 12. SettingsPage pattern

`Pages/SettingsPage.xaml` uses the WinUI Community Toolkit's
`SettingsCard`/`SettingsExpander` controls (`CommunityToolkit.WinUI.Controls.SettingsControls`
package, `xmlns:ctk="using:CommunityToolkit.WinUI.Controls"`) instead of
hand-rolled cards, matching the native Windows 11 Settings app row pattern.
This is the extensibility point for future settings:

- Content sits in a `ScrollViewer` (settings lists grow over time) wrapping a
  `StackPanel Spacing="32"` of **groups**.
- Each group is a `TextBlock Style="{StaticResource SectionTitleTextBlockStyle}"`
  label (e.g. "Tenants", "Application") followed by a `StackPanel Spacing="12"`
  of `SettingsCard`/`SettingsExpander` rows.
- A `SettingsCard` is a single-action row: `Header`, `Description`, optional
  `HeaderIcon` (`FontIcon`), and one control (typically a `Button` styled with
  `PrimaryActionButtonStyle`/`SecondaryActionButtonStyle`) as its direct child.
- A `SettingsExpander` is for a row with sub-actions: the same `Header`/
  `Description`/`HeaderIcon`, a compact "at a glance" status as its direct
  child (shown even when collapsed — e.g. the signed-in/not-signed-in
  indicator), and one `SettingsCard` per sub-action under
  `<ctk:SettingsExpander.Items>`.

To add a new setting: drop another `SettingsCard` into an existing group's
`StackPanel`, or add a new `TextBlock` + `StackPanel` group for a new
category. No layout rework needed — the `ScrollViewer` absorbs growth.

## 13. Contextual help (HelpTip)

`InToolz.Utilities.HelpTip` is a small "?" icon that shows a `TeachingTip`
(WinUI's native flyout-with-a-dismiss-button control) with a short
explanation on click. It exists because the wiki documents every page in
depth, but most users never open it — guidance only changes behavior when
it shows up *in the workflow*, at the moment someone's confused, without
making them leave the page.

```xml
<utilities:HelpTip Title="Search &amp; Staging"
                    Text="Short 1-3 sentence explanation..."/>
```

Rules for using it:

- **Sparingly.** One per genuinely non-obvious concept, not one per button —
  a single-control tooltip via `ToolTipService.ToolTip` already covers a
  one-liner on hover. `HelpTip` is for things a tooltip can't explain: the
  relationship between several controls, a page's overall mental model, a
  non-obvious consequence of an action.
- **Next to the thing it explains**, inline in a horizontal `StackPanel`
  with the label it's attached to (a page title, or a
  `CardSectionLabelTextBlockStyle` section label) — never floating alone.
- **Keep `Text` to 1-3 sentences.** It's a quick answer, not a copy of the
  wiki page.

First tried out on `Pages/CleanupPage.xaml` (page-level "About this page"
overview, plus one per non-obvious card section) before rolling out to
other pages.

---

## 14. The 2.0 shell (`MainWindow`)

`MainWindow.xaml`/`.xaml.cs` no longer hosts a `muxc:NavigationView`. The
shell is now:

- **A slim drag strip** (`CustomTitleBar`, 32px) — unchanged in spirit from
  v1, just restyled. Still the only element passed to `SetTitleBar`, so it
  stays free of interactive controls.
- **The Command Center bar** (56px) — logo, a command-box `Button` that
  opens the palette, and the source/destination tenant pills. The pills
  read `Variables.sourceTenantName`/`destinationTenantName` directly and
  refresh via `Variables.TenantStateChanged` (see §17) rather than polling.
- **A `BreadcrumbBar`** — shell-level, not per-page. `MainWindow` resolves
  the current page's title from `Utilities/AppActions.cs` on every
  `Frame.Navigated` and sets `ItemsSource` to `["Home"]` or
  `["Home", "<page title>"]`. Pages never render their own breadcrumb.
- **`ContentFrame`** — same `Frame.Navigate(Type)` mechanism as before;
  there is still no parameter-passing between pages.
- **`Utilities/CommandPaletteOverlay`** — a Ctrl+K palette (bound via a
  `KeyboardAccelerator` on the root `Grid`) listing every `AppAction`,
  filterable by title/keywords. Fires `ActionInvoked(Type)`; `MainWindow`
  does the actual `ContentFrame.Navigate`. Holds no state itself, same
  "fires events" convention as `SearchStagingBar`.

### `Utilities/AppActions.cs`

The single source of truth for every top-level destination — one
`AppAction(Title, Glyph, Description, Keywords, TargetPage)` record per
page. Backs the command palette, the breadcrumb's title lookup, and
`DashboardPage`'s "Browse all actions" grid. **Add a page here and it
appears in all three automatically** — don't hand-wire a new page into
the palette or breadcrumb separately.

## 15. Theme (`Styles/BrandTheme.xaml`)

A second resource dictionary, merged into `App.xaml` **after**
`PageStyles.xaml`, so it wins on duplicate keys. It overrides a handful of
Fluent token names pages already consume via `{ThemeResource ...}`
(`CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`,
`TextFillColorPrimaryBrush`/`SecondaryBrush`, `SystemFillColorCriticalBrush`,
`ApplicationPageBackgroundThemeBrush`) inside `ResourceDictionary.
ThemeDictionaries` (`Light`/`Dark` keys), plus a flat (non-themed)
`SystemAccentColor` + `Light1-3`/`Dark1-3` ramp for the brand purple accent.

**Deliberately scoped**: deep Fluent system brushes (`TextBox`, `ComboBox`,
`ContentDialog` chrome, etc.) are not overridden and keep the stock Fluent
look in both themes. Only add a new override here if a page-visible card,
text, or accent color is wrong — don't go hunting for more tokens to
reskin pre-emptively.

`Utilities/ThemeService.cs` applies/persists the user's choice
(`ElementTheme.Default`/`Light`/`Dark`) via the unpackaged-safe JSON-file
pattern `PageStatePersistence` already established. Settings → Application
→ Theme is the only UI surface for it (`ComboBoxItem.Tag` = the enum name).

## 16. `Utilities/SelectionActionBar`

The shared bottom action-bar chrome: a live count label (`CountText`) plus
a `ExtraButtons` content slot, following `SearchStagingBar`'s "fires
events/holds no state" convention — except here the host supplies
finished buttons rather than the control raising click events, since the
action set genuinely differs per page (Rename's Update, Assign's Configure
& Assign, JSON's three buttons, ...).

Used on Renaming, Assignment, Manage Assignments, and JSON. **What the
count means is the host's call, not a fixed rule** — pick whichever matches
how the page actually works:

- **Staged-items count** (`ContentList`/`AssignmentList`
  `.CollectionChanged`) for pages where the grid *is* the thing being
  acted on (Renaming, Assignment, JSON) — bulk action applies to
  everything currently staged.
- **Selected-rows count** (`DataGrid.SelectionChanged`) for pages where
  the action applies only to whatever's highlighted within an already-
  loaded list (Manage Assignments).

Not every page needs it: Import and Cleanup already had (or gained) a
persistent, always-visible primary action — don't add `SelectionActionBar`
just for consistency if a page's existing layout already satisfies "the
action isn't hidden in a dialog or toolbar flyout."

## 17. Tenant state notifications

`Utilities/Variables.cs` exposes `event Action? TenantStateChanged` and
`RaiseTenantStateChanged()`. `SettingsPage` raises it after sign-in,
sign-out, swap, and Demo Mode activation. Anything showing live tenant
state outside of a page's own `OnNavigatedTo` (the shell's tenant pills,
`ImportPage`'s tenant-flow card) subscribes in its constructor:

```csharp
Variables.TenantStateChanged += () => DispatcherQueue.TryEnqueue(RefreshMyTenantUi);
```

`Variables.SwapSourceAndDestinationTenants()` is the shared core of
"swap source and destination" (auth instances + tenant names + log +
raise the event above) — `SettingsPage`'s swap button and `ImportPage`'s
tenant-flow swap button both call it; page-local state (e.g. Settings'
permission-warning flags) is swapped separately by the caller.

## 18. Destructive confirmations

`BaseMultiTenantPage.ShowDestructiveConfirmationAsync(title, consequenceBody,
confirmText = "Delete")` builds on `ShowConfirmationDialogAsync`, styling
the `ContentDialog`'s primary button with `DestructiveActionButtonStyle`
(red) so permanent/dangerous actions are visually distinct from routine
confirmations. Use it — not a hand-rolled `ContentDialog` — for any delete/
remove-assignments/irreversible flow.

`consequenceBody` must say what's actually lost and whether it's
recoverable — never a generic "are you sure?". `CleanupPage`'s stale-device
dialog (Entra 30-day-recoverable vs. Intune record-only-delete framing)
and duplicate-delete dialog (assignment/member-aware framing) are the
reference examples.

## 19. `Utilities/DashboardStatsCache`

A small unpackaged-safe JSON cache (`DashboardStatsCache.SaveResult(scanKey,
count)` / `GetResult(scanKey)`) that lets `DashboardPage`'s stat tiles show
"last scanned Xh ago — N found" without running a live scan on every
dashboard load. `CleanupPage` is the only current writer — it calls
`SaveResult` at the same point each scan already reports success via
`ShowOperationSuccess`. A tile reads `null` as "not yet scanned," not zero.
If a future page adds its own scan-and-report flow that the dashboard
should surface, add a new `ScanKeys` constant and call `SaveResult` at its
completion point — don't invent a second cache.

