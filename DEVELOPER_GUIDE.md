# IntuneTools Developer Guide — Adding New Pages

This guide documents the design patterns, base classes, and reusable components in the IntuneTools codebase. Reference it when adding new pages to ensure consistency and maximize code reuse.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Choose Your Base Class](#choose-your-base-class)
3. [Step-by-Step: Add a New Page](#step-by-step-add-a-new-page)
4. [Base Class Reference](#base-class-reference)
5. [XAML Templates](#xaml-templates)
6. [Adding a New Content Type](#adding-a-new-content-type)
7. [Adding a New Application Type to the Import Engine](#adding-a-new-application-type-to-the-import-engine)
8. [Graph Helper Patterns](#graph-helper-patterns)
9. [Reusable Utilities](#reusable-utilities)
10. [Naming Conventions](#naming-conventions)
11. [Checklist](#checklist)

---

## Architecture Overview

```
Page (WinUI 3)
├── BaseMultiTenantPage              ← Pages needing tenant auth + logging
│   └── BaseDataOperationPage        ← Pages with DataGrid + content collections
│       ├── ImportPage
│       ├── CleanupPage
│       ├── RenamingPage
│       └── JsonPage
│   └── AssignmentPage               ← Auth + logging, but custom data handling
├── HomePage                         ← Standalone (no auth needed)
└── SettingsPage                     ← Standalone (manages auth itself)
```

**Key directories:**

| Directory | Purpose |
|-----------|---------|
| `Pages/` | All page XAML and code-behind files |
| `Utilities/` | Base classes, helpers, models, and shared code |
| `Graph/IntuneHelperClasses/` | One helper per Intune content type (Graph API calls) |
| `Graph/EntraHelperClasses/` | Entra ID helpers (groups, etc.) |
| `Graph/` | Authentication classes (Source + Destination tenants) |

---

## Choose Your Base Class

| Scenario | Base Class | Examples |
|----------|-----------|----------|
| Page displays a **DataGrid of content** loaded from Graph | `BaseDataOperationPage` | ImportPage, CleanupPage, RenamingPage, JsonPage |
| Page needs **tenant auth and logging** but no DataGrid content list | `BaseMultiTenantPage` | AssignmentPage |
| Page has **no auth requirement** | `Page` (WinUI default) | HomePage, SettingsPage |

**Decision tree:**

1. Does the page need Graph API access? → **Yes**: Use `BaseMultiTenantPage` or `BaseDataOperationPage`
2. Does the page show a collection of content items in a DataGrid? → **Yes**: Use `BaseDataOperationPage`
3. Does the page only need auth + logging? → Use `BaseMultiTenantPage`
4. No auth at all? → Use `Page` directly

---

## Step-by-Step: Add a New Page

### 1. Create the XAML file

Create `Pages/MyNewPage.xaml`. See [XAML Templates](#xaml-templates) below for the full boilerplate.

### 2. Create the code-behind

Create `Pages/MyNewPage.xaml.cs`:

```csharp
namespace IntuneTools.Pages;

public sealed partial class MyNewPage : BaseDataOperationPage
{
    public MyNewPage()
    {
        InitializeComponent();
        LogConsole.ItemsSource = LogEntries;
        RightClickMenu.AttachDataGridContextMenu(ContentDataGrid);
    }

    // List the x:Name of controls that should be disabled when not authenticated
    protected override IEnumerable<string> GetManagedControlNames() =>
        ["SearchBox", "SearchButton", "ListAllButton", "ActionButton"];

    // Override if both source AND destination tenants are required
    // protected override bool RequiresBothTenants => true;

    private async void ListAllButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteWithLoadingAsync(
            operation: async () =>
            {
                ContentList.Clear();
                await LoadAllContentTypesAsync(sourceGraphServiceClient, AppendToLog);
                ContentDataGrid.ItemsSource = ContentList;
            },
            loadingMessage: "Loading content...",
            successMessage: $"Loaded {ContentList.Count} items.");
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text?.Trim();
        if (string.IsNullOrEmpty(query)) return;

        await ExecuteWithLoadingAsync(
            operation: async () =>
            {
                ContentList.Clear();
                await SearchAllContentTypesAsync(sourceGraphServiceClient, query, AppendToLog);
                ContentDataGrid.ItemsSource = ContentList;
            },
            loadingMessage: $"Searching for '{query}'...",
            successMessage: $"Found {ContentList.Count} items.");
    }
}
```

### 3. Register navigation in MainWindow

**MainWindow.xaml** — Add a `NavigationViewItem`:

```xml
<muxc:NavigationViewItem Content="My Page" Tag="MyPage" Icon="Page2"/>
```

**MainWindow.xaml.cs** — Add a case to `NavigateToPage()`:

```csharp
case "MyPage":
    ContentFrame.Navigate(typeof(MyNewPage));
    break;
```

### 4. Done!

The base class handles authentication validation, loading overlays, logging, progress tracking, and control state management automatically.

---

## Base Class Reference

### BaseMultiTenantPage

**Provides:** Authentication validation, logging, loading overlays, progress tracking, and control state management.

| Member | Type | Purpose |
|--------|------|---------|
| `LogEntries` | `ObservableCollection<LogEntry>` | Bind to `LogConsole.ItemsSource` |
| `RequiresBothTenants` | `virtual bool` | Override → `true` if destination tenant also required |
| `UnauthenticatedMessage` | `virtual string` | Customize the auth warning text |
| `GetManagedControlNames()` | `virtual IEnumerable<string>` | Controls to disable when unauthenticated |
| `ValidateAuthenticationState()` | method | Auto-called on navigation; checks auth, toggles controls |
| `ShowLoading(message)` | method | Show modal overlay with spinner |
| `HideLoading()` | method | Hide the loading overlay |
| `LogInfo(message)` | method | Log info to console (white, • prefix) |
| `LogSuccess(message)` | method | Log success (green, ✔ prefix) |
| `LogWarning(message)` | method | Log warning (orange, ⚠ prefix) |
| `LogError(message)` | method | Log error (red, ✖ prefix) |
| `AppendToLog(text)` | method | Alias for `LogInfo` (backward compat) |
| `ClearLog()` | method | Clear the log console |
| `ShowOperationProgress(message)` | method | Indeterminate progress bar |
| `ShowOperationProgress(msg, current, total)` | method | Determinate progress bar |
| `ShowOperationSuccess(message)` | method | Green InfoBar |
| `ShowOperationError(message)` | method | Red InfoBar |
| `ExecuteWithLoadingAsync(operation, loadingMsg, successMsg, errorPrefix)` | method | Run async work with automatic loading/error/completion handling |

**Expected XAML control names** (looked up by `x:Name`):

- `TenantInfoBar` — InfoBar for auth status
- `LoadingOverlay` — Grid overlay container
- `LoadingStatusText` — TextBlock in the overlay
- `LoadingProgressRing` — ProgressRing in the overlay
- `LogConsole` — ListView bound to `LogEntries`
- `ClearLogButton` — Button (auto-wired to `ClearLogButton_Click`)
- `OperationStatusBar` — InfoBar for progress (optional)
- `OperationProgressRing` — Indeterminate spinner (optional)
- `OperationProgressBar` — Determinate bar (optional)

### BaseDataOperationPage (extends BaseMultiTenantPage)

**Adds:** Content collection management, DataGrid helpers, and content type-aware loading/searching.

| Member | Type | Purpose |
|--------|------|---------|
| `ContentList` | `ObservableCollection<CustomContentInfo>` | Master content collection for DataGrid binding |
| `LoadAllContentTypesAsync(client, log)` | method | Load all registered content types into `ContentList` |
| `LoadContentTypesAsync(client, types, log)` | method | Load specific content types into `ContentList` |
| `SearchAllContentTypesAsync(client, query, log)` | method | Search all registered content types |
| `SearchContentTypesAsync(client, query, types, log)` | method | Search specific content types |
| `GetContentIdsByType(contentType)` | method | Get IDs filtered by content type |
| `HasContentType(contentType)` | method | Check if content type exists in list |
| `ClearContentList(dataGrid?)` | method | Clear list and optionally rebind grid |
| `RemoveSelectedItems(dataGrid)` | method | Remove selected rows from list |
| `HandleDataGridSorting(sender, e)` | method | Wire to `DataGrid.Sorting` for generic column sorting |

---

## XAML Templates

### Full page template for BaseDataOperationPage

```xml
<local:BaseDataOperationPage
    x:Class="IntuneTools.Pages.MyNewPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:IntuneTools"
    xmlns:toolkit="using:CommunityToolkit.WinUI.UI.Controls"
    Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">

    <Grid RowDefinitions="Auto,Auto,*,Auto">

        <!-- Row 0: Auth Status -->
        <InfoBar x:Name="TenantInfoBar" IsOpen="False" Severity="Warning" Margin="10,10,10,0"/>

        <!-- Row 1: Action Bar -->
        <Grid Grid.Row="1" Margin="10" ColumnDefinitions="*,Auto,Auto,Auto">
            <TextBox x:Name="SearchBox" PlaceholderText="Search..." Margin="0,0,8,0"/>
            <Button x:Name="SearchButton" Content="Search" Grid.Column="1" Click="SearchButton_Click" Margin="0,0,4,0"/>
            <Button x:Name="ListAllButton" Content="List All" Grid.Column="2" Click="ListAllButton_Click" Margin="0,0,4,0"/>
            <Button x:Name="ActionButton" Content="Do Something" Grid.Column="3" Click="ActionButton_Click"/>
        </Grid>

        <!-- Row 2: DataGrid -->
        <toolkit:DataGrid x:Name="ContentDataGrid"
                          Grid.Row="2"
                          Margin="10,0"
                          AutoGenerateColumns="False"
                          CanUserSortColumns="True"
                          Sorting="HandleDataGridSorting"
                          IsReadOnly="True"
                          SelectionMode="Extended"
                          GridLinesVisibility="Horizontal"
                          ItemsSource="{x:Bind ContentList}">
            <toolkit:DataGrid.Columns>
                <toolkit:DataGridTextColumn Header="Name" Binding="{Binding ContentName}" Width="2*"/>
                <toolkit:DataGridTextColumn Header="Type" Binding="{Binding ContentType}" Width="*"/>
                <toolkit:DataGridTextColumn Header="Platform" Binding="{Binding ContentPlatform}" Width="*"/>
                <toolkit:DataGridTextColumn Header="ID" Binding="{Binding ContentId}" Width="*"/>
                <toolkit:DataGridTextColumn Header="Description" Binding="{Binding ContentDescription}" Width="2*"/>
            </toolkit:DataGrid.Columns>
        </toolkit:DataGrid>

        <!-- Row 3: Log Console -->
        <Grid Grid.Row="3" Margin="10" RowDefinitions="Auto,*">
            <Grid ColumnDefinitions="Auto,*">
                <TextBlock Text="Log" FontWeight="SemiBold" VerticalAlignment="Center"/>
                <Button x:Name="ClearLogButton" Content="Clear" Grid.Column="1"
                        HorizontalAlignment="Right" Click="ClearLogButton_Click"/>
            </Grid>
            <ListView x:Name="LogConsole" Grid.Row="1" Height="150"
                      ItemsSource="{x:Bind LogEntries}" SelectionMode="None">
                <ListView.ItemTemplate>
                    <DataTemplate x:DataType="local:LogEntry">
                        <TextBlock TextWrapping="Wrap" Foreground="{x:Bind Foreground}">
                            <Run Text="{x:Bind TimestampText}" FontSize="11" Foreground="Gray"/>
                            <Run Text=" "/>
                            <Run Text="{x:Bind LevelIndicator}"/>
                            <Run Text=" "/>
                            <Run Text="{x:Bind Message}"/>
                        </TextBlock>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>
        </Grid>

        <!-- Loading Overlay (spans all rows) -->
        <Grid x:Name="LoadingOverlay" Grid.RowSpan="4" Visibility="Collapsed"
              Background="#80000000">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Spacing="12">
                <ProgressRing x:Name="LoadingProgressRing" IsActive="False" Width="48" Height="48"/>
                <TextBlock x:Name="LoadingStatusText" TextAlignment="Center"
                           Foreground="White" FontSize="14"/>
            </StackPanel>
        </Grid>

        <!-- Operation Status (optional, spans all rows) -->
        <InfoBar x:Name="OperationStatusBar" Grid.Row="3" IsOpen="False" Margin="10,0,10,5">
            <InfoBar.Content>
                <StackPanel Orientation="Horizontal" Spacing="8">
                    <ProgressRing x:Name="OperationProgressRing" IsActive="False" Width="20" Height="20"/>
                    <ProgressBar x:Name="OperationProgressBar" Width="200" Visibility="Collapsed"/>
                </StackPanel>
            </InfoBar.Content>
        </InfoBar>
    </Grid>
</local:BaseDataOperationPage>
```

> **Note:** The root element must match your base class — use `<local:BaseMultiTenantPage>` or `<local:BaseDataOperationPage>` instead of `<Page>`.

---

## Adding a New Content Type

When you need to support a new Intune content type (e.g., a new policy category):

### 1. Create a Graph helper

Create `Graph/IntuneHelperClasses/MyNewPolicyHelper.cs`:

```csharp
namespace IntuneTools;

public class MyNewPolicyHelper
{
    // Load all items
    public static async Task<List<MyNewPolicy>> GetAllMyNewPolicies(GraphServiceClient client)
    {
        var result = await client.DeviceManagement.MyNewPolicies.GetAsync();
        var policies = new List<MyNewPolicy>();

        var pageIterator = PageIterator<MyNewPolicy, MyNewPolicyCollectionResponse>
            .CreatePageIterator(client, result, (policy) =>
            {
                policies.Add(policy);
                return true;
            });
        await pageIterator.IterateAsync();

        LogToFunctionFile(appFunction.Main, $"Found {policies.Count} MyNewPolicy items.");
        return policies;
    }

    // Search by display name
    public static async Task<List<MyNewPolicy>> SearchForMyNewPolicies(
        GraphServiceClient client, string query)
    {
        var result = await client.DeviceManagement.MyNewPolicies.GetAsync(cfg =>
        {
            cfg.QueryParameters.Filter = $"contains(displayName,'{query}')";
        });
        // Same page iterator pattern...
    }

    // Return as CustomContentInfo (for ContentTypeRegistry)
    public static async Task<List<CustomContentInfo>> GetAllMyNewPolicyContentAsync(
        GraphServiceClient client)
    {
        var policies = await GetAllMyNewPolicies(client);
        return policies.Select(p => new CustomContentInfo
        {
            ContentName = p.DisplayName,
            ContentType = ContentTypes.MyNewPolicy,
            ContentPlatform = TranslatePolicyPlatformName(p.Platform),
            ContentId = p.Id,
            ContentDescription = p.Description
        }).ToList();
    }

    public static async Task<List<CustomContentInfo>> SearchMyNewPolicyContentAsync(
        GraphServiceClient client, string query)
    {
        var policies = await SearchForMyNewPolicies(client, query);
        return policies.Select(p => new CustomContentInfo
        {
            ContentName = p.DisplayName,
            ContentType = ContentTypes.MyNewPolicy,
            ContentPlatform = TranslatePolicyPlatformName(p.Platform),
            ContentId = p.Id,
            ContentDescription = p.Description
        }).ToList();
    }

    // Optional: Assign, Delete, Rename, Import, Export
    // Follow the same patterns as existing helpers
}
```

### 2. Add a content type constant

In `Utilities/ContentTypeRegistry.cs`, add to the `ContentTypes` class:

```csharp
public const string MyNewPolicy = "My New Policy";
```

### 3. Register in ContentTypeRegistry

In the `_registry` dictionary in `ContentTypeRegistry`:

```csharp
[ContentTypes.MyNewPolicy] = new ContentTypeOperations(
    ContentTypes.MyNewPolicy,
    "My New Policies",
    MyNewPolicyHelper.GetAllMyNewPolicyContentAsync,
    MyNewPolicyHelper.SearchMyNewPolicyContentAsync
)
```

### 4. Add right-click lookup URL (optional)

In `Utilities/RightClickMenu.cs`, add a URL template so users can look up items in the Intune portal:

```csharp
["My New Policy"] = "https://intune.microsoft.com/#blade/.../policyId/{0}"
```

After these steps, every existing page that uses `LoadAllContentTypesAsync` or `SearchAllContentTypesAsync` will automatically include the new content type.

---

## Adding a New Application Type to the Import Engine

Application import is intentionally split between metadata-only ("cloneable") and binary-installer ("LOB-style") app types. The split is enforced by `Graph/IntuneHelperClasses/Applications/AppContentHandlerRegistry.cs` so the upload code path can never accidentally fire on a tenant-bound app type (Apple VPP, Managed Google Play, etc.).

### File layout

```
Graph/IntuneHelperClasses/Applications/
├── AppContentHandlerRegistry.cs   ← single place to register a new app type
├── HandlingMode.cs                ← Cloneable | BinaryRoundTrip | ManualHandover
├── IAppContentHandler.cs          ← per-type "shape" interface
├── ApplicationCloneHelper.cs      ← shared reflection clone + property strip
├── IntuneAppContentCrypto.cs      ← AES-256-CBC + HMAC-SHA256 streaming codec
├── IntuneContentEngine.cs         ← transport-only: download, encrypt, upload, commit
├── ContentTransferOptions.cs      ← chunk size, polling, SAS-renewal threshold
├── AppTransferProgress.cs         ← IProgress<T> payload for the UI
└── Win32LobAppContentHandler.cs   ← Phase 1 reference implementation
```

### Adding a new binary-upload app type (e.g. macOSPkgApp)

1. Create `<NewType>ContentHandler.cs` next to `Win32LobAppContentHandler.cs` and implement `IAppContentHandler`.
   - Wire each Graph operation through the SDK's per-type request builder (e.g. `MobileApps[id].GraphMacOSPkgApp.ContentVersions...`).
   - Override `BuildContentFileMetadata` if the type uses a derived content-file model (e.g. `MacOSPkgAppFile`) and needs extra fields beyond `Name` / `Size` / `SizeEncrypted`.
   - Set `RequiresUserMetadata = true` if the local-file import flow needs extra fields (bundle ID, install context, etc.).
2. Register it in `AppContentHandlerRegistry.Build()` — one line.
3. Remove the OData type from `BinaryUploadAppODataTypesPendingHandler` in `ApplicationHelper.cs`.
4. Add a deterministic round-trip test for any new type-specific logic if you touched the engine or the crypto layer.

### Adding a new tenant-bound type (e.g. a future store integration)

1. Add an entry to `BuildManualHandovers()` in `AppContentHandlerRegistry.cs` with a one-line `Hint` describing what the user has to do in the destination tenant.
2. The import loop will surface it in the manual hand-over CSV (Phase 4) automatically — no other code changes needed.

### What the engine handles for you

* Streaming download from the source's Azure Storage SAS URL.
* AES-256-CBC + HMAC-SHA256 decrypt + re-encrypt without buffering multi-GB payloads in memory.
* `azureStorageUriRequest` and `commit` polling state machines with exponential back-off and a configurable timeout.
* Mid-flight SAS renewal via `renewUpload` when an upload outlasts the original SAS expiration.
* Block-blob chunked upload to Azure Storage (the one leg the Graph SDK does not model).
* `IProgress<AppTransferProgress>` callbacks for the UI.
* Per-app `try/catch` so one failed app does not abort the batch.

---

## Graph Helper Patterns

All Graph helpers in `Graph/IntuneHelperClasses/` follow these method patterns:

| Method Pattern | Signature | Purpose |
|----------------|-----------|---------|
| `GetAll[Type]s()` | `(GraphServiceClient) → List<T>` | Fetch all items with pagination |
| `SearchFor[Type]s()` | `(GraphServiceClient, string) → List<T>` | Server-side filter by display name |
| `GetAll[Type]ContentAsync()` | `(GraphServiceClient) → List<CustomContentInfo>` | Load all as generic content (for registry) |
| `Search[Type]ContentAsync()` | `(GraphServiceClient, string) → List<CustomContentInfo>` | Search as generic content (for registry) |
| `AssignGroupsToSingle[Type]()` | `(string id, List<string> groupIds, GraphServiceClient)` | Assign groups (preserves existing) |
| `Delete[Type]()` | `(GraphServiceClient, string id)` | Delete by ID |
| `Rename[Type]()` | `(GraphServiceClient, string id, string newName)` | Rename by ID |
| `Import[Type]()` | `(GraphServiceClient, JsonElement)` | Create new item from JSON |
| `Export[Type]Data()` | `(GraphServiceClient, string id) → JsonElement` | Export full policy data |

**Pagination pattern** (reuse everywhere):

```csharp
var pageIterator = PageIterator<T, TCollectionResponse>
    .CreatePageIterator(client, result, (item) =>
    {
        items.Add(item);
        return true;
    });
await pageIterator.IterateAsync();
```

**Assignment pattern** (preserves existing assignments):

1. Build new assignment list from provided group IDs
2. Handle virtual groups (`allUsersVirtualGroupID`, `allDevicesVirtualGroupID`)
3. Fetch existing assignments and merge (skip duplicates via `HashSet`)
4. POST the combined assignment list

---

## Reusable Utilities

### UserInterfaceHelper (static methods)

| Method | Use When |
|--------|----------|
| `RebindDataGrid(dataGrid, collection)` | Refreshing DataGrid binding after changes |
| `PopulateCollectionAsync(collection, loader)` | Loading items into an ObservableCollection |
| `SearchCollectionAsync(collection, search, query, map)` | Search + populate with mapping |
| `IsApplicationContentType(contentType)` | Checking if a content type is an app type |
| `ExecuteBatchOperationAsync(ids, operation, ...)` | Running batch operations with progress + error handling + time tracking |
| `ExecuteBatchOperationWithNameAsync(...)` | Same but logs item names |

### HelperClass (static methods)

| Method | Use When |
|--------|----------|
| `LogToFunctionFile(function, message, level)` | Writing to disk log files |
| `TranslatePolicyPlatformName(name)` | Normalizing platform names for display |
| `TranslateApplicationType(odataType)` | Converting OData types to friendly app names |
| `RemovePrefixFromPolicyName(name)` | Stripping `(prefix)`, `[prefix]`, `{prefix}` |
| `SearchAndBindAsync(...)` | Generic search → bind to collection + DataGrid |
| `LoadAndBindAsync(...)` | Generic load → bind to collection + DataGrid |
| `WriteToRichTextBlock(rtb, text, append)` | Writing to RichTextBlock controls |

### RightClickMenu

Call once in your page constructor to get copy-cell and portal-lookup context menus on any DataGrid:

```csharp
RightClickMenu.AttachDataGridContextMenu(MyDataGrid);
```

### TimeSaved

Track time savings after operations:

```csharp
UpdateTotalTimeSaved(itemCount * secondsSavedOnImporting, appFunction.Import);
```

### ContentTypeRegistry

Access content type operations generically:

```csharp
// Get operations for a single content type
var ops = ContentTypeRegistry.Get(ContentTypes.SettingsCatalog);
var items = await ops.LoadAll(client);

// Iterate all registered types
foreach (var ops in ContentTypeRegistry.All)
{
    var items = await ops.LoadAll(client);
}
```

### CustomContentInfo (shared data model)

The universal content container used across all pages and DataGrids:

```csharp
public class CustomContentInfo
{
    public string? ContentName { get; set; }
    public string? ContentPlatform { get; set; }
    public string? ContentType { get; set; }
    public string? ContentId { get; set; }
    public string? ContentDescription { get; set; }
}
```

### LogEntry (log display model)

Factory methods create color-coded log entries:

```csharp
LogEntry.Info("Processing...");     // White, • prefix
LogEntry.Success("Done!");          // Green, ✔ prefix
LogEntry.Warning("Check this");     // Orange, ⚠ prefix
LogEntry.Error("Failed");           // Red, ✖ prefix
```

### Global usings

Common namespaces are pre-imported globally via `<Using Include="…" />` items
in `IntuneTools.csproj` (and mirrored in `IntuneTools.Tests.csproj`). There is
no longer a `Utilities/GlobalUsing.cs` file — the project file is the single
source of truth, alongside `<ImplicitUsings>` (which already pulls in the
SDK-default BCL set: `System`, `System.Collections.Generic`, `System.IO`,
`System.Linq`, `System.Net.Http`, `System.Threading`, `System.Threading.Tasks`).

You don't need to add `using` statements for:

- `IntuneTools.Utilities`
- `Microsoft.Graph.Beta` and `Microsoft.Graph.Beta.Models`
- `System.Collections.ObjectModel`, `System.Text`, `System.Text.Json`
- `Microsoft.Kiota.Serialization.Json`
- The static helpers `HelperClass`, `TimeSaved`, `Variables`,
  `SourceUserAuthentication`, `DestinationUserAuthentication`

See `CONTRIBUTING.md` for the full convention and the `IDE0005` enforcement
rules in `.editorconfig`.

---

## Naming Conventions

| Category | Convention | Example |
|----------|-----------|---------|
| XAML control names | PascalCase + control type suffix | `SearchButton`, `ContentDataGrid` |
| Event handlers | `ControlName_EventName` | `SearchButton_Click` |
| Content type constants | Human-readable display name | `"Settings Catalog"`, `"Device Compliance Policy"` |
| Async methods | Suffix with `Async` | `LoadContentTypesAsync()` |
| Orchestrator methods | Suffix with `Orchestrator` | `ListAllOrchestrator()` |
| Graph helper methods | `[Verb][Count][Type]s()` | `GetAllSettingsCatalogPolicies()` |
| Graph content methods | `[Verb][Type]ContentAsync()` | `GetAllSettingsCatalogContentAsync()` |
| Nav item tags | PascalCase identifier | `Tag="MyPage"` |

---

## Checklist

Use this checklist when adding a new page:

- [ ] **Base class chosen** — `BaseDataOperationPage`, `BaseMultiTenantPage`, or `Page`
- [ ] **XAML created** — Root element matches base class, required named controls present
- [ ] **Code-behind created** — Constructor wires `LogConsole.ItemsSource` and `RightClickMenu`
- [ ] **`GetManagedControlNames()` overridden** — Lists controls to disable when unauthenticated
- [ ] **Navigation registered** — `NavigationViewItem` in `MainWindow.xaml` + case in `MainWindow.xaml.cs`
- [ ] **Uses `ExecuteWithLoadingAsync`** — For all async operations (handles loading/errors/completion)
- [ ] **Uses base class methods** — `LoadContentTypesAsync`, `SearchContentTypesAsync`, `ContentList`, etc.
- [ ] **Logging via `LogInfo/Success/Warning/Error`** — Not manual TextBlock writes

If adding a new **content type**:

- [ ] **Graph helper created** — In `Graph/IntuneHelperClasses/`, follows existing patterns
- [ ] **Content type constant added** — In `ContentTypes` class
- [ ] **Registered in `ContentTypeRegistry`** — With `LoadAll` and `Search` delegates
- [ ] **Right-click lookup URL added** — In `RightClickMenu.cs` (optional)
