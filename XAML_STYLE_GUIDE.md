# IntuneTools XAML Style Guide

This guide locks in the visual and interaction conventions every page in InToolz must follow so users feel "at home" no matter where they navigate. If you are adding a new page, read this first and start from the canonical skeleton at the bottom.

> **Goal:** same skeleton, same controls in the same places, same visual weight, same keyboard behavior on every page.

---

## 1. Page skeleton

Every feature page is a `Grid` with `Margin="20"` and three rows:

| Row | Height | Purpose | Bottom margin |
|-----|--------|---------|---------------|
| 0 | `Auto` | **Header** — title, subtitle, InfoBar stack | 16 |
| 1 | `Auto` | **Toolbar** — one or more `ToolbarCard`s (search, actions) | 12 |
| 2 | `*`    | **Content** — DataGrid, side panel, log console | — |

Exceptions:
- **HomePage** is a landing page; it may use a `ScrollViewer + StackPanel`, but should still use `PageTitleTextBlockStyle` for its title and `ToolbarCardStyle` for any tile cards so it visually matches feature pages.
- **SettingsPage** uses the same `Margin="20"` root, the same `PageTitleTextBlockStyle` title, and the shared `TenantPill` user control.

### Side panel layout

When a page splits into a main grid plus a side panel, use these column widths:

```
<ColumnDefinition Width="*"/>      <!-- Main content -->
<ColumnDefinition Width="8"/>      <!-- GridSplitter -->
<ColumnDefinition Width="340"/>    <!-- Side panel (or log console) -->
```

The `GridSplitter` width is **always 8**. The side-panel default width is **340**.

---

## 2. Header

Always use the shared `PageHeader` user control:

- `Title` → renders with `PageTitleTextBlockStyle` (FontSize 38, Bold).
- `Subtitle` → renders with `PageSubtitleTextBlockStyle` (FontSize 14, secondary foreground, 4 px top margin).
- `InstructionText` → optional; renders an `InfoBar` ("How this page works") underneath.
- Slot for additional `InfoBar`s (e.g., `TenantInfoBar`, `OperationStatusBar`) goes after the instruction.

The order is non-negotiable: **Title → Subtitle → Instruction InfoBar → Tenant InfoBar → Operation InfoBar.**

---

## 3. Typography scale

| Token | Style | Use for |
|-------|-------|---------|
| 38 / Bold | `PageTitleTextBlockStyle` | Page title |
| 14 / Regular / secondary | `PageSubtitleTextBlockStyle` | Subtitle under the title |
| 20 / SemiBold | (inline) | Section heading inside content (e.g., "Log Console") |
| 12 / SemiBold | `SectionLabelTextBlockStyle` | Card header label ("Search & Staging", "JSON Actions") |
| 13 / Regular / secondary | `StatCardLabelStyle` | Label inside a stat tile |
| 28 / Bold | `StatCardValueStyle` | Value inside a stat tile |
| 12 / Consolas | (inline) | Log console rows; tenant ID display |

Do not introduce new font sizes. If you need one, add it to this table first.

---

## 4. Spacing scale

| Value | When |
|-------|------|
| 6  | Intra-group spacing (e.g., items inside a tight `StackPanel`) |
| 8  | Between sibling controls in a row (`Spacing="8"` on horizontal `StackPanel`) |
| 12 | Between cards in the toolbar row; padding inside `ToolbarCard` |
| 16 | Bottom margin of the header (Row 0); between top-level sections |
| 20 | Root page margin |

Do not introduce new spacing values without updating this table.

---

## 5. Button taxonomy

Every button on every page falls into exactly one of these buckets. Use the style; do not set `Height`/`Padding`/`Background` inline.

| Role | Style | Examples |
|------|-------|----------|
| **Primary** (one per card, accent-colored) | `PrimaryActionButtonStyle` | Export, Import, Update names, Create in tenant, Assign |
| **Destructive** (red) | `DestructiveActionButtonStyle` | Delete All, Clear All |
| **Secondary** | default `Button` | View Details, Open Folder, Cancel |
| **Toolbar / icon-only** | `AppBarButton` (with `Label`, `Icon`, and `ToolTipService.ToolTip`) | List All, Clear Selected, Select All |

**Primary action placement rule:** the primary action lives **right-aligned inside the rightmost toolbar card** (Row 1) and is wired to **Ctrl+Enter**.

**Destructive action rule:** the action must require a `ContentDialog` confirmation. Buttons in the dialog are `PrimaryButtonText="Delete"` and `CloseButtonText="Cancel"` in that order. The dialog must **not** default-focus the destructive button (`DefaultButton="Close"`).

---

## 6. Search vs. Filter

These are different concepts; use different controls.

| Concept | What it does | Control | Event |
|---------|--------------|---------|-------|
| **Search** | Sends a query to Microsoft Graph (server-side) | `AutoSuggestBox` with `ToolbarSearchBoxStyle` | `QuerySubmitted` |
| **Filter** | Narrows what is already loaded (client-side) | `TextBox` | `TextChanged` |

Standard `AutoSuggestBox` width: **280** (set by `ToolbarSearchBoxStyle`). Standard `QueryIcon`: **Find**.

---

## 7. Keyboard accelerators

Every feature page must wire these accelerators in this exact form:

| Shortcut | Action | Handler name |
|----------|--------|--------------|
| `Ctrl+F` | Focus the search box | `FocusSearch_Accelerator` |
| `Ctrl+L` | List all (populate grid) | `ListAll_Accelerator` |
| `Ctrl+A` | Select all rows in the grid | `SelectAll_Accelerator` |
| `Ctrl+Shift+A` | Deselect all rows | `DeselectAll_Accelerator` |
| `Ctrl+Enter` | Invoke the page's primary action | `PrimaryAction_Accelerator` |

App-wide:

| Shortcut | Action |
|----------|--------|
| `Ctrl+,` | Open Settings |
| `Ctrl+1`…`Ctrl+8` | Jump to navigation item by index |

---

## 8. Tenant context

There is one source of truth for which tenants are connected: `MainWindow`'s `NavigationView.PaneFooter`.

| Surface | Shows tenant info | How |
|---------|-------------------|-----|
| `MainWindow` PaneFooter | **Always** | Two `TenantPill`s (Source + Destination) |
| `SettingsPage` | **Always** | Full tenant cards using the shared `TenantPill` for the status pill |
| Feature pages (Cleanup, Renaming, Import, …) | **Only when something is wrong** | `TenantInfoBar` shows when not signed in or wrong tenant; otherwise hidden |

Status pill background must always be `SubtleFillColorSecondaryBrush` (matches MainWindow). Status is conveyed by the dot color (green / yellow / red / gray), not the pill background.

---

## 9. Empty states

When a grid or list has no items, show an `EmptyState` user control centered in the content area, with these opacity rules:

- Glyph: opacity **0.5**
- Title (FontSize 14, SemiBold): opacity **0.75**
- Message (FontSize 12, wrapping, `MaxWidth="380"`): opacity **0.6**

Use the shared `EmptyStateGlyphStyle`, `EmptyStateTitleStyle`, and `EmptyStateMessageStyle` (or the `EmptyState` user control which sets them automatically).

---

## 10. Loading overlay

Use the shared `LoadingOverlay` user control. Bind `IsLoading` to your busy state and set `StatusText` to a short verb-led phrase ("Loading…", "Deleting…", "Exporting…"). Do not roll your own acrylic `Border`.

---

## 11. Log console

Use the shared `LogConsole` user control. It owns the 58 / 18 / * column template, the Consolas timestamp, and the level indicator. Bind it to the `LogEntries` collection that `BaseMultiTenantPage` already exposes.

---

## 12. ContentDialog conventions

- Use `PrimaryButtonText` for the affirmative action and `CloseButtonText` for "Cancel".
- Use `SecondaryButtonText` only when there is a real third option.
- For destructive actions: `DefaultButton="Close"`.
- Title is short ("Delete 12 items?"); body is one or two sentences explaining consequences.

---

## 13. Tab order

On every feature page, focus moves in this order:

```
header → search box → primary action → grid → side panel → log console
```

If you add controls, keep them on this path. Use `TabIndex` only when the visual order does not match the desired focus order.

---

## 14. Accessibility

- Every icon-only `AppBarButton` must have both `Label` and `ToolTipService.ToolTip` (and the tooltip should mention the keyboard shortcut, e.g. `"List All (Ctrl+L)"`).
- Every interactive control without visible text must have `AutomationProperties.Name`.
- Status colors (green/yellow/red) are always paired with text or an icon — never color-only.

---

## 15. Canonical page skeleton

Copy this as the starting point for any new feature page:

```xaml
<utilities:BaseDataOperationPage
    x:Class="IntuneTools.Pages.MyNewPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:utilities="using:IntuneTools.Utilities"
    xmlns:controls="using:IntuneTools.Pages.Controls"
    xmlns:sizers="using:CommunityToolkit.WinUI.Controls"
    Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">

    <Grid Margin="20">
        <Grid.KeyboardAccelerators>
            <KeyboardAccelerator Modifiers="Control"        Key="F"     Invoked="FocusSearch_Accelerator"/>
            <KeyboardAccelerator Modifiers="Control"        Key="L"     Invoked="ListAll_Accelerator"/>
            <KeyboardAccelerator Modifiers="Control"        Key="A"     Invoked="SelectAll_Accelerator"/>
            <KeyboardAccelerator Modifiers="Control,Shift"  Key="A"     Invoked="DeselectAll_Accelerator"/>
            <KeyboardAccelerator Modifiers="Control"        Key="Enter" Invoked="PrimaryAction_Accelerator"/>
        </Grid.KeyboardAccelerators>

        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Header  -->
            <RowDefinition Height="Auto"/>  <!-- Toolbar -->
            <RowDefinition Height="*"/>     <!-- Content -->
        </Grid.RowDefinitions>

        <!-- Row 0: Header -->
        <controls:PageHeader Grid.Row="0"
                             Title="My New Page"
                             Subtitle="One short sentence describing what this page does."
                             InstructionText="1. Search…  2. Pick items…  3. Click the primary action."/>

        <!-- Row 1: Toolbar cards -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="12" Margin="0,0,0,12">
            <controls:ToolbarCard HeaderText="Search &amp; Staging">
                <StackPanel Orientation="Horizontal" Spacing="8">
                    <AutoSuggestBox x:Name="SearchBox"
                                    Style="{StaticResource ToolbarSearchBoxStyle}"
                                    PlaceholderText="Search by name…"
                                    QuerySubmitted="SearchBox_QuerySubmitted"/>
                    <AppBarButton Label="List All" Icon="ViewAll" Click="ListAll_Click"
                                  ToolTipService.ToolTip="List All (Ctrl+L)"/>
                </StackPanel>
            </controls:ToolbarCard>

            <controls:ToolbarCard HeaderText="Actions">
                <Button Style="{StaticResource PrimaryActionButtonStyle}"
                        Content="Do the thing"
                        Click="PrimaryAction_Click"/>
            </controls:ToolbarCard>
        </StackPanel>

        <!-- Row 2: Content -->
        <Grid Grid.Row="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="8"/>
                <ColumnDefinition Width="340"/>
            </Grid.ColumnDefinitions>

            <!-- Main content goes here (DataGrid, etc.) -->
            <controls:LoadingOverlay Grid.Column="0"
                                     IsLoading="{x:Bind IsLoading, Mode=OneWay}"
                                     StatusText="Loading…"/>

            <sizers:GridSplitter Grid.Column="1" Width="8"
                                 ResizeBehavior="BasedOnAlignment"
                                 ResizeDirection="Columns"
                                 Background="Transparent"/>

            <controls:LogConsole Grid.Column="2"
                                 Entries="{x:Bind LogEntries, Mode=OneWay}"/>
        </Grid>
    </Grid>
</utilities:BaseDataOperationPage>
```

---

## 16. Checklist before opening a PR for a new page

- [ ] Root is `Grid Margin="20"` with the three-row structure.
- [ ] Header uses `PageHeader` (or `PageTitleTextBlockStyle` + `PageSubtitleTextBlockStyle`).
- [ ] Toolbar uses `ToolbarCard` with a `SectionLabelTextBlockStyle` header label.
- [ ] Search uses `AutoSuggestBox` with `ToolbarSearchBoxStyle`; client-side filter uses `TextBox`.
- [ ] Primary action uses `PrimaryActionButtonStyle` and is wired to `Ctrl+Enter`.
- [ ] Destructive action uses `DestructiveActionButtonStyle` and a confirmation `ContentDialog` with `DefaultButton="Close"`.
- [ ] Empty state uses `EmptyState`.
- [ ] Loading uses `LoadingOverlay`.
- [ ] Log console uses `LogConsole`.
- [ ] All five keyboard accelerators (Ctrl+F/L/A/Shift+A/Enter) are wired.
- [ ] Every icon-only button has `Label` + `ToolTipService.ToolTip` mentioning its shortcut.
- [ ] Tab order is header → search → action → grid → side panel → log.
