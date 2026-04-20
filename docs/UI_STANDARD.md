# IntuneTools - UI Standard

This document defines the unified visual and UX standard that every page in
IntuneTools should follow. The goal is to make the app feel like a single,
modern, sleek Fluent application rather than a collection of pages that each
made their own decisions.

The shared styles live in **`Styles/PageStyles.xaml`** and are merged into
`App.xaml`, so any page can reference them with `{StaticResource ...}`.

`Pages/RenamingPage.xaml` is the reference implementation - copy its
structure when migrating other pages.

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

| Token                      | Value          | Use                                   |
| -------------------------- | -------------- | ------------------------------------- |
| `PageRootMargin`           | `24,24,24,24`  | Outer margin of the page root `Grid`  |
| `PageHeaderBottomMargin`   | `20`           | Bottom margin under the header block  |
| `PageSectionSpacing`       | `12`           | Spacing between toolbar cards         |
| `CardCornerRadius`         | `8`            | Corner radius of every card           |
| `CardPadding`              | `16,12,16,12`  | Inner padding of every card           |

## 3. Typography

| Style                              | Size / Weight     | Use                                       |
| ---------------------------------- | ----------------- | ----------------------------------------- |
| `PageTitleTextBlockStyle`          | 32 / SemiBold     | Single page title at the top              |
| `PageSubtitleTextBlockStyle`       | 14 / Regular      | One-line description under the title      |
| `CardSectionLabelTextBlockStyle`   | 12 / SemiBold     | Caption above a toolbar card row          |
| `SidePanelHeaderTextBlockStyle`    | 16 / SemiBold     | "Log Console", "Groups", etc.             |

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
  default) and an `OperationStatusBar` containing a `ProgressRing` and a
  `ProgressBar`. Names must stay constant so `BaseDataOperationPage` can
  bind to them.
- Long operations show the `LoadingOverlay` border with the `ProgressRing`
  + `LoadingStatusText`. Do not invent new spinners.
- Use `InfoBar` (not custom yellow rectangles) for staging-area guidance.

## 7. Side panels

- The right-hand side panel uses a `GridSplitter` with `Width="8"` and
  `Background="Transparent"`.
- Default panel width is `340`, `MinWidth="200"`.
- The panel header uses `SidePanelHeaderTextBlockStyle` (16 / SemiBold).
  No more `FontSize="20"` titles or negative-margin alignment hacks.
- The log `ListView` uses `LogListViewItemContainerStyle` and the three
  `Log*TextBlockStyle` styles for timestamp / level / message.

## 8. Naming conventions (controls referenced from code-behind)

These names are part of the implicit contract with `BaseDataOperationPage`
and `BaseMultiTenantPage` and **must not be renamed** when migrating a page:

`TenantInfoBar`, `OperationStatusBar`, `OperationProgressRing`,
`OperationProgressBar`, `LoadingOverlay`, `LoadingProgressRing`,
`LoadingStatusText`, `LogConsole`.

## 9. Migration checklist for a page

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

Track per-page progress in `todo.md` at the repo root.
