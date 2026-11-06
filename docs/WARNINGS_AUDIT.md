# Build Warnings Audit (#114)

Step 1 of #114 ("Make build warnings meaningful") — categorize what's actually in the 546-warning
count before changing anything. No code changes in this branch, findings only.

**Step 2 (2026-11-04, branch `chore/customcontentinfo-non-nullable`)**: verified pattern 2, found it
was actually pattern 3 in disguise (see the rewritten pattern-2 section below), fixed both together.
Result: **270 → 197 unique warning sites**, verified via a clean rebuild (`rm -rf obj/bin`, full
rebuild, diffed the warning set) and a full test pass (147/147). Zero new warnings, zero behavior
changes — every fix was either an annotation correction matching code that was already null-safe at
runtime, or an `?? string.Empty` fallback for a Graph field that's genuinely optional. Detail in the
"Root causes" section below.

**Step 3 (2026-11-04, branch `fix/graphserviceclient-non-null-guarantee`)**: resolved pattern 1 (the
`graphServiceClient` design question). Result: **197 → 91 unique warning sites**, clean rebuild +
147/147 tests. Detail in the rewritten pattern-1 section below.

**Step 4 (2026-11-06, branch `fix/assign-groups-import-id-nullability`)**: resolved the
`AssignGroupsToSingleX` import-flow cluster found during step 3. Result: **91 → 83 unique warning
sites**, clean rebuild + 147/147 tests. Detail in the new section below.

**Step 5 (2026-11-06, branch `fix/pageiterator-null-guards`)**: guarded all 16 `PageIterator`
call sites. Result: **83 → 67 unique warning sites**, clean rebuild + 147/147 tests. Detail in the
rewritten "Smaller, isolated pattern" section below.

**Step 6 (2026-11-06, branch `fix/obsolete-api-and-unused-vars`)**: the two mechanical,
not-nullability fixes. Result: **67 → 62 unique warning sites**, clean rebuild + 147/147 tests.
Detail in the rewritten "Not a nullability problem" section below.

**Step 7 (2026-11-06, branch `fix/postasync-result-nullable-dereference`)**: resolved most of the
CS8602 cluster — `GetAsync()`/`PostAsync()` results dereferenced without a null-check inside
`ImportMultipleX` methods, plus one genuine latent bug (a correctly-written null-check positioned
*after* its dereference). Result: **62 → 47 unique warning sites**, clean rebuild + 147/147 tests.
Detail in the new section below.

**Pre-merge review (2026-11-06)**: ran a multi-angle review across the full stack (steps 2-7, 6 PRs)
before merging. Caught one real regression from step 5 and fixed it in the same branch (see step 5's
section) plus two unrelated trivial simplifications; several architectural/duplication observations
were documented instead of fixed, to avoid re-touching 9+ files again right before merge — see "Found
during pre-merge review, deferred" near the bottom. **Steps 2-7 merged to `master` 2026-11-06.**

**Step 8 (2026-11-06, branch `fix/odatatype-nullable-cluster`)**: resolved the `.OdataType`-nullable
cluster spotted during step 7's review. Result: **47 → 44 unique warning sites**, clean rebuild +
147/147 tests. Detail in the new section below.

## How this was measured

```
dotnet build InToolz.csproj -p:Platform=x64 -c Release
```

on a clean `obj`/`bin` (2026-11-03, commit `5933b39`).

**The raw build log is not the right count to use.** It contains **1,088** `warning CSxxxx` lines,
but the same `(file, line, column, code, message)` tuple repeats 2x, 4x, or 8x throughout the log —
WinUI 3's XAML markup compiler and the multi-platform (`x86;x64;ARM64`) project shape appear to
invoke the C# compiler more than once per file in a single `dotnet build`. This wasn't investigated
further since it doesn't change *what* needs fixing, only how many times the build happens to repeat
each one.

Deduplicating by that tuple gives **270 unique warning sites** — this is the number everything below
is based on. (This differs from the issue's originally-reported 546; not investigated which measurement
method produced that number, but 270 is what a clean build on today's `master` actually contains.)

```
grep -E "warning CS[0-9]+" build.log | sort -u | wc -l   # 270
```

## By warning code

| Code | Count | Meaning |
|---|---|---|
| CS8604 | 189 | Possible null reference argument |
| CS8602 | 47 | Dereference of a possibly null reference |
| CS8601 | 14 | Possible null reference assignment |
| CS8625 | 6 | Cannot convert null literal to non-nullable reference type |
| CS8600 | 4 | Converting null literal or possible null value to non-nullable type |
| CS0618 | 3 | Obsolete API |
| CS8603 | 2 | Possible null reference return |
| CS0168 | 2 | Variable declared but never used |
| CS8620 | 1 | Nullability mismatch in generic argument |
| CS8619 | 1 | Nullability mismatch in generic argument (reference conversion) |
| CS8618 | 1 | Non-nullable field not set in constructor |

**264 of 270 (98%) are nullable-reference-annotation warnings** (the `CS86xx` family). `CS0618` and
`CS0168` are a different kind of problem entirely (see "Not a nullability problem" below).

## By file (top 10 of 25 files with warnings)

| File | Count |
|---|---|
| `Pages/ImportPage.xaml.cs` | 43 |
| `Pages/AssignmentPage.xaml.cs` | 39 |
| `Pages/RenamingPage.xaml.cs` | 38 |
| `Pages/CleanupPage.xaml.cs` | 24 |
| `Graph/IntuneHelperClasses/WindowsAutoPilotHelper.cs` | 10 |
| `Graph/IntuneHelperClasses/ProactiveRemediationsHelper.cs` | 10 |
| `Graph/IntuneHelperClasses/DeviceCompliancePolicyHelper.cs` | 10 |
| `Graph/IntuneHelperClasses/SettingsCatalogHelper.cs` | 9 |
| `Graph/IntuneHelperClasses/DeviceConfigurationHelper.cs` | 9 |
| `Graph/IntuneHelperClasses/ApplicationHelper.cs` | 9 |

4 page files account for 144 of 270 (53%). The rest is spread thinly across 13 Graph helper files
(3-10 each), which matters for sequencing: fixing the 4 pages is a few large, high-value changes;
fixing the helpers is many small, repetitive ones.

## Root causes (the actual finding)

The 264 nullability warnings are not 264 separate problems. Nearly all of them trace back to **three**
underlying causes:

### 1. `graphServiceClient` and its `source`/`destination` variants are typed nullable, but never actually null in practice (100 of 189 CS8604, plus most of CS8602 where one of these is dereferenced directly) — **fixed 2026-11-04**

This is exactly what #114 originally called out. Every page calls Graph helpers with a client that's
only ever assigned post-sign-in, but the parameter/property type is nullable (`GraphServiceClient?`),
so the compiler can't rule out null at any of these hundred-plus call sites. Example
(`Pages/RenamingPage.xaml.cs:493`):

```csharp
async id => (await sourceGraphServiceClient.DeviceManagement.ConfigurationPolicies[id]
    .GetAsync(r => r.QueryParameters.Select = new[] { "name" }))?.Name),
```

`sourceGraphServiceClient.DeviceManagement` is the dereference the compiler flags (CS8602) — the
client itself is the thing that's nullable here, not anything Graph returns.

**The design question was: where should "the user is signed in, so this is never null" actually be
guaranteed?** Traced the invariant through the existing architecture instead of guessing: `SettingsPage`
sets `sourceGraphServiceClient`/`Variables.sourceTenantName` together on sign-in and clears both
together on sign-out (same is true for destination and for Demo Mode), and
`BaseMultiTenantPage.ValidateAuthenticationState()` disables every action-triggering control unless
`Variables.sourceTenantName`/`destinationTenantName` is non-empty. So the invariant already holds by
construction — by the time any button handler can fire, the relevant client is guaranteed non-null.
It just wasn't expressed in the type system.

**The fix**: added a throwing accessor next to each existing nullable static —
`SourceUserAuthentication.RequireSourceClient()` / `DestinationUserAuthentication.RequireDestinationClient()`
(implemented once in `UserAuthenticationBase.RequireClient(string tenantLabel)`) — and replaced each
outermost read of the nullable static (a button click handler, or the point where a per-content-type
registry of lambdas is built) with a single captured call to the new accessor, threading the resulting
non-null local through instead of re-reading the nullable static repeatedly. Where a registry-building
method (`GetImportTypeRegistry`, `GetRenameTypeRegistry`, `GetDeleteTypeRegistry`) needed the client
inside several closures, it got an added `GraphServiceClient client` parameter instead, so the caller
captures once. Touched 6 pages: `ImportPage`, `AssignmentPage`, `RenamingPage`, `CleanupPage`,
`ManageAssignmentsPage`, `JsonPage`.

**One page needed a different answer, not the same fix**: `JsonPage`'s export/import already has an
explicit `if (sourceGraphServiceClient == null)` check with a graceful fallback (export metadata-only
JSON without a signed-in tenant is a deliberate feature, not a bug) — that code was left untouched;
only its `ListAllButton_Click`/`SearchStagingBar_SearchRequested`/auto-run-search, which *are* gated by
`ValidateAuthenticationState`, got the `RequireSourceClient()` treatment.

**Found but out of scope for this pass**: a separate, smaller cluster of ~7 CS8604 sites
(`AssignGroupsToSingleX(string xId, ...)` across `AppleBYODEnrollmentProfileHelper`,
`DeviceCompliancePolicyHelper`, `DeviceConfigurationHelper`, `PowerShellScriptsHelper`,
`ProactiveRemediationsHelper`, `WindowsAutoPilotHelper`, `macOSShellScript`) where a freshly-created
Graph object's `.Id` (nullable per the SDK, but always present right after a successful `PostAsync`)
flows into a non-nullable `string` parameter during import. Same shape as pattern 2/3, different call
path (import, not list/export) — not discovered until this pass, not part of "pattern 1," left for a
future session.

**Result**: 197 → 91 unique warning sites, verified via clean rebuild + 147/147 tests.

### 2 & 3 (merged). `CustomContentInfo`/`DuplicateContentInfo` nullability and the `DisplayName`/`Id`/`Description` call sites that feed them — **fixed 2026-11-04**

These turned out to be the same root cause (Graph SDK `DisplayName`/`Id`/`Description` are genuinely
nullable) showing up at two call sites, not two separate problems. What was actually done:

**First, the wrong fix was tried and measured.** Stripping `CustomContentInfo`'s `?`s directly
(no fallback) was tested by editing, rebuilding, and diffing the warning set — not just reading call
sites, because reading alone can't tell you whether the *value* being assigned is provably non-null.
Result: unique warnings went 270 → 358, net **worse** — the warning doesn't disappear, it relocates
from the dereference site (CS8602/CS8604) to the construction-site assignment (CS8601), and a
property-only class with no constructor also picks up one new CS8618 per property. That approach was
reverted.

**The real fix, applied on `chore/customcontentinfo-non-nullable`:**
- `Utilities/HelperClass.cs`: `TranslatePolicyPlatformName`, `TranslateApplicationType`,
  `TranslateODataTypeFromApplicationType`, `ApplyPrefixRemoval`, `ApplySuffixRemoval`,
  `ApplyFindAndReplace`, `FindPreFixInPolicyName`, `FindSuffixInPolicyName`,
  `RemovePrefixFromPolicyName`, `RemoveExactPrefixFromPolicyName`, `RemoveExactSuffixFromPolicyName`
  all already null-checked their `string policyName`/`platformName`/etc. parameter internally
  (`string.IsNullOrEmpty`/`IsNullOrWhiteSpace`) and returned a sane value for null input — the
  non-nullable parameter type was simply lying about what the method already did at runtime. Changed
  each parameter to `string?` and changed the early-return branches that returned the (possibly-null)
  parameter directly to `?? string.Empty`, so the return type stays non-nullable `string`. Pure
  annotation correction, zero behavior change — this alone resolved pattern 3's ~48 "policyName"
  CS8604 sites across 14 helper files and `RenamingPage.xaml.cs`, with no call-site edits needed.
- `Utilities/CustomContentInfo.cs` / `DuplicateContentInfo.cs`: dropped `?` from `ContentName`/
  `ContentType`/`ContentId`/`ContentPlatform`/`ContentDescription` (all 5 on `CustomContentInfo`; the
  4 string ones on `DuplicateContentInfo` — its `CreatedDateTime`/`LastModifiedDateTime`/
  `HasAssignments`/`HasMembers` stay nullable, confirmed genuinely optional: they're populated only on
  a best-effort basis inside try/catch at `CleanupPage.xaml.cs:924-951`, with `?? "—"` fallbacks
  already in the display properties). Added `= string.Empty` defaults to avoid CS8618.
- All 35 construction sites (34 `CustomContentInfo` + 1 `DuplicateContentInfo`) checked individually
  for which assigned expression is actually nullable — this varied more than expected (most sites
  only needed it on `ContentName`/`ContentId`/`ContentDescription`, but `JsonPage.xaml.cs`'s imported-
  JSON DTO was nullable on all 5, and `CleanupPage.xaml.cs`'s stale-device exports were nullable on
  `ContentPlatform`/`ContentId`/`ContentName` but not `ContentType`/`ContentDescription`) — added
  `?? string.Empty` wherever the compiler actually flagged it, then verified by rebuilding.

**Result**: 270 → 197 unique warning sites, verified via a clean rebuild (not incremental — an
incremental rebuild after editing `ImportPage.xaml.cs` produced ~40 warnings that looked new but were
actually pre-existing warnings whose line numbers shifted by 2 after a 2-line simplification; always
diff clean-build output, not incremental, when checking for regressions this way). 147/147 tests
still pass. No remaining warnings in either model file.

### 4. `AssignGroupsToSingleX(string xId, ...)` fed a freshly-created Graph object's `.Id` during import (~8 CS8604) — **fixed 2026-11-06**

Found during step 3, not part of pattern 1 — same shape as patterns 2/3 (an SDK model's `.Id` is
nullable per the type, but provably non-null in this specific context) in a different call path: 8
import methods (`AppleBYODEnrollmentProfileHelper`, `DeviceCompliancePolicyHelper`,
`DeviceConfigurationHelper`, `PowerShellScriptsHelper`, `ProactiveRemediationsHelper`,
`SettingsCatalogHelper`, `WindowsAutoPilotHelper`, `macOSShellScript`) do:

```csharp
var import = await destinationGraphServiceClient.DeviceManagement.X.PostAsync(newItem);
AppLogger.Info($"Imported '{import.DisplayName}' successfully.", appFunction.Import);
if (assignments)
{
    await AssignGroupsToSingleX(import.Id, import.DisplayName ?? string.Empty, groups, destinationGraphServiceClient);
}
```

`import` is the direct response from a successful `PostAsync` — Microsoft Graph always returns the
created resource with its `Id` populated on success, even though the SDK model marks `Id` nullable
(the same model type serves request bodies, where `Id` is legitimately absent).

**Fix**: made the guarantee explicit with `import.Id ?? throw new InvalidOperationException(...)`
right before each assignment call, instead of a silent cast or a skip. Each of these 8 sites is
already inside a per-item `try`/`catch` that logs a failure and continues the bulk-import loop — so a
hypothetical violation surfaces as a clear, logged per-item failure (`"Imported profile 'X' has no
ID."`) exactly like any other import failure already does, rather than a `NullReferenceException` deep
in Graph SDK internals. Zero behavior change on the success path, since `Id` is always actually
present. (One wrinkle: the new local needed a distinct name — `newProfileId`/`newConfigId`/
`newScriptId` instead of `profileId`/`configId`/`scriptId` — in the 3 files where the enclosing
`foreach` loop already used that name for the *source*-side ID being imported.)

**Result**: 91 → 83 unique warning sites, verified via a clean rebuild. 147/147 tests pass.

### 5. `PageIterator.CreatePageIterator`'s `page` parameter (16 of 189 CS8604) — **fixed 2026-11-06**

The Graph SDK's own `PageIterator.CreatePageIterator` expects its `page` argument non-null, but the
collection response it's handed (the direct result of `graphServiceClient.X.GetAsync(...)`) can
legitimately be null. All 16 sites — one `GetAllX`/`SearchForX` pair each in 7 helper files, plus one
site in `AuditLogHelper` and one already-non-nullable-guarded sibling method in
`DeviceCompliancePolicyHelper` — follow the exact same shape:

```csharp
var result = await graphServiceClient.X.GetAsync(...);
List<T> items = new();
var pageIterator = PageIterator<T, TResponse>.CreatePageIterator(graphServiceClient, result, (item) =>
{
    items.Add(item);
    return true;
});
await pageIterator.IterateAsync();
return items;
```

**Fix**: wrapped the `CreatePageIterator`/`IterateAsync` pair in `if (result != null) { ... }` at all
16 sites — mechanical, uniform. Deliberately used the same silent-skip shape everywhere rather than
mixing in the `?? throw` style from pattern 4: these 8 files' `catch` blocks aren't uniform
(`GroupHelperClass`'s two methods catch only `ODataError`, not a general `Exception`, while the other
7 files do catch general `Exception`), so throwing would have been safe in some files and an uncaught
new exception path in others. The skip avoids depending on that catch-block shape at all.
(`DeviceCompliancePolicyHelper.GetAllDeviceCompliancePolicies` already had its own `if (result ==
null) throw` guard predating this session — left untouched, not flagged, not part of this fix.)

**Correction, caught in pre-merge review (2026-11-06)**: the original claim of "zero behavior change"
above was wrong. Before this fix, a null `result` meant `CreatePageIterator`/`IterateAsync` would
dereference it and throw — an exception caught by each method's own enclosing `catch (Exception ex) {
AppLogger.Error(...); return empty list; }`, so the failure was *logged*. After the fix, a null
`result` silently produces an empty list with **no log at all** — a real diagnostic regression
(a transient Graph hiccup now looks identical to "this tenant genuinely has zero items"). Fixed in the
same branch by adding an `else { AppLogger.Warning(...); }` to all 16 sites, so the "nothing to
iterate" case is visible again without reintroducing the exception-throwing behavior the skip was
chosen to avoid.

**Result**: 83 → 67 unique warning sites, verified via a clean rebuild. 147/147 tests pass.

## Not a nullability problem — separate, unrelated fixes — **fixed 2026-11-06**

- **`CS0618` (3, obsolete API)**: `AssignRequestBuilder.PostAsync(...)` is obsolete in the current
  Graph SDK; `PostAsAssignPostResponseAsync` replaces it. All 3 sites were assignment-related
  (`DeviceCompliancePolicyHelper.cs` ×2, `ManageAssignmentsPage.xaml.cs` ×1) — a mechanical rename,
  nothing to do with null-reference warnings. One of the three (`ManageAssignmentsPage.xaml.cs`'s
  `GetRemoveSingleAssignmentRegistry`) was a copy-paste miss sitting right next to its own fix: the
  `SettingsCatalog`/`DeviceConfigurationPolicy` branches of that same dictionary literal already used
  `PostAsAssignPostResponseAsync`, only the `DeviceCompliancePolicy` branch in between still called
  the obsolete `PostAsync`.
- **`CS0168` (2, unused variable)**: `WindowsAutoPilotHelper.cs` (`catch (ODataError error)`),
  `HelperClass.cs` (`catch (Exception ex)`) — neither body referenced the caught exception. Dropped
  the variable name in both (`catch (ODataError)` / `catch (Exception)`), same shape as the #116
  dead-static cleanup.

**Result**: 67 → 62 unique warning sites, verified via a clean rebuild. 147/147 tests pass.

### 6. `GetAsync()`/`PostAsync()` results dereferenced without a null-check inside `ImportMultipleX` methods (CS8602) — **fixed 2026-11-06**

The dominant shape in what was left of CS8602: every `ImportMultipleX` method does roughly

```csharp
var result = await sourceGraphServiceClient.X[id].GetAsync();        // source fetch, nullable
... reflection copy from result into a new request body ...
var import = await destinationGraphServiceClient.X.PostAsync(requestBody);  // create, nullable
AppLogger.Info($"Imported '{import.DisplayName}' successfully.", appFunction.Import);
```

Same root cause as patterns 2/3 and the `AssignGroupsToSingleX` cluster — the SDK types both the
single-item `GetAsync()` result and the `PostAsync()` result nullable, but in practice a *found*
single-item fetch and a *successful* create are never null. Fixed 16 of these across
`DeviceCompliancePolicyHelper`, `DeviceConfigurationHelper`, `WindowsAutoPilotHelper`,
`PowerShellScriptsHelper`, `ProactiveRemediationsHelper`, `SettingsCatalogHelper` (import *and*
rename methods), `AppleBYODEnrollmentProfileHelper`, `FilterHelperClass` with the same `?? throw new
InvalidOperationException(...)` idiom used in step 4 — each site is already inside a per-item
`try`/`catch` that logs a failure and continues, so this is a message-quality improvement, not a
behavior change.

**One site was a genuine latent bug, not an annotation gap**: `GroupHelperClass.ImportMultipleGroups`
already had the right null-check (`if (sourceGroup == null) { ...skip...; continue; }`) — just
positioned *after* the dereference (`groupName = sourceGroup.DisplayName`) instead of before it. If a
group ID ever actually failed to resolve, this would have thrown `NullReferenceException` before ever
reaching the graceful skip. Fixed by reordering, not by adding a throw — preserving the existing
graceful-skip design the original author clearly intended, rather than overriding it with the
`?? throw` idiom used everywhere else in this cluster (which is for cases with *no* existing
handling).

**Found but deferred, same session**: fixing `WindowsAutoPilotHelper`'s and
`DeviceConfigurationHelper`'s `result`/`originalConfig` nullability unmasked a *different*, adjacent
issue on the very next line in both — `result.OdataType.Contains(...)` /
`originalConfig.OdataType.Equals(...)` dereference `OdataType` itself (not the object it hangs off
of), which is independently nullable. Same shape as `ApplicationHelper.cs:288`'s
`assignment.Target.OdataType` (already out of scope for this pass) — a `.OdataType`-nullable cluster
spotted three times now, worth its own small, well-defined session.

**Also explicitly out of scope, left for later**: the `as T` cast results dereferenced without a
null-check (`DeviceCompliancePolicyHelper.cs:183`, `DeviceConfigurationHelper.cs:125` — the cast
should never actually fail since `Activator.CreateInstance` is given a type derived from the same
object, but that's a different kind of claim than "Graph guarantees this"); the `sender as DataGrid`
casts in three UI sort handlers (`AssignmentPage.xaml.cs:922,1125`, `ImportPage.xaml.cs:727`); and
`HelperClass.cs:235`'s `tenantInfo.Value[0]`, which needs its own decision about what an empty
`Value` array should mean.

**Result**: 62 → 47 unique warning sites, verified via a clean rebuild. 147/147 tests pass.

### 7. The `.OdataType`-nullable cluster (3 sites) — **fixed 2026-11-06**

Spotted three times while fixing other clusters (see above), it turned out to be two distinct shapes
rather than one:

- `WindowsAutoPilotHelper.cs`'s `result.OdataType.Contains(...)` and `DeviceConfigurationHelper.cs`'s
  `originalConfig.OdataType.Equals(...)` are genuinely about `OdataType` itself being nullable — the
  object it hangs off of (`result`/`originalConfig`) was already proven non-null by step 7's `?? throw`
  fix, but `OdataType` is its own separately-nullable string. Fixed with
  `result.OdataType?.Contains(...) ?? false` — if the discriminator is ever actually missing, that's
  the same as "doesn't match this particular known special case," which is already how the surrounding
  code treats any other unrecognized `OdataType` value. No behavior change for the actual (always
  non-null in practice) case.
- `ApplicationHelper.cs`'s `assignment.Target.OdataType` turned out to be a *different* object
  entirely — `Target`, not `OdataType`, is what's nullable here. Traced the three places this loop
  constructs a `MobileAppAssignment` (all-users, all-devices, regular-group branches) and confirmed
  every single one sets `Target` to a non-null object — so, same story, just one level up. Fixed with
  `assignment.Target?.OdataType == "..."`, which is actually simpler than a `?? throw` here: comparing
  a `string?` to a literal via `?.` is already null-safe and reads as "if there's no target, this
  can't be that specific target type" — exactly the fallback behavior already implicit in the code.

**Result**: 47 → 44 unique warning sites, verified via a clean rebuild. 147/147 tests pass.

## Found during pre-merge review, deferred (not a correctness bug — a maintainability observation)

A multi-angle review across the full steps-2-through-7 stack, before merging any of it, repeatedly
flagged the same shape from independent angles: this effort fixed every site by hand instead of
introducing a shared helper, even though the fix shape repeats identically dozens of times.

- **The `?? throw new InvalidOperationException("...")` idiom** (pattern 4 + step 7, ~17 sites across
  9 files) each writes its own ad-hoc message. `UserAuthenticationBase.RequireClient(string
  tenantLabel)` (pattern 1) is proof the codebase already reaches for a small shared "require non-null
  or throw" helper when it's worth it — this effort didn't generalize that same idea to Graph
  SDK call results (e.g. an `EnsureNotNull<T>(this T? value, string what)` extension), so a 10th
  `PostAsync` call site added later has nothing nudging its author toward the guard.
- **The `if (result != null) { ...CreatePageIterator...IterateAsync... }` guard** (step 5, 16 sites
  across 8 files) is identical aside from type parameters. A shared `IterateAllPagesAsync<T,
  TResponse>(client, result, onItem)` helper could absorb the null-check once, rather than depending
  on every future copy-paste to retain the wrapping `if`.
- **`RequireSourceClient()`/`RequireDestinationClient()` is a convention, not a compiler-enforced
  guarantee.** The raw nullable `sourceGraphServiceClient`/`destinationGraphServiceClient` properties
  are still public and still used directly in a few places — deliberately, for the `AttachDataGridContextMenu`
  lazy-getter lambdas (see pattern 1's writeup: changing those to throw was explicitly considered and
  rejected, since the lambda fires later, at a point this session didn't fully vet). Nothing in the
  type system stops a future contributor from reading the raw property elsewhere out of habit or IDE
  autocomplete, silently reintroducing the exact warning this pattern was built to prevent.
- **`GetImportTypeRegistry`/`GetRenameTypeRegistry`/`GetDeleteTypeRegistry` take the client as a
  parameter; every other orchestrator method in the same files calls `RequireSourceClient()` inline.**
  This is deliberate (a registry built once needs the client threaded into many closures; a one-shot
  orchestrator doesn't), but it's an asymmetry worth a one-line comment for the next person who copies
  the wrong neighboring pattern.

None of these are correctness bugs — the fixes that exist are all individually correct, verified by
clean rebuilds and the full test suite at every step. They're flagged here because fixing them *now*
would mean re-touching 9+ files a second time, right before merging six already-reviewed PRs; better
as a deliberate follow-up session that extracts the shared helpers once, in one pass, rather than
folded into warning-count work already in flight.

## Suggested sequencing

1. ~~Patterns 2 & 3 (`CustomContentInfo`/`DuplicateContentInfo` + `DisplayName`/`Id`/`Description`
   call sites)~~ — **done**, see above. 270 → 197 unique warnings.
2. ~~`graphServiceClient` non-null guarantee (pattern 1)~~ — **done**, see above. 197 → 91 unique
   warnings.
3. ~~`AssignGroupsToSingleX` / import-flow `.Id` cluster~~ — **done**, see above. 91 → 83 unique
   warnings.
4. ~~Guard the `PageIterator` call sites~~ — **done**, see above. 83 → 67 unique warnings.
5. ~~`CS0618` (3) and `CS0168` (2)~~ — **done**, see above. 67 → 62 unique warnings.
6. ~~`GetAsync()`/`PostAsync()` results dereferenced in `ImportMultipleX` methods~~ — **done**, see
   above. 62 → 47 unique warnings.
7. ~~The `.OdataType`-nullable cluster~~ — **done**, see above. 47 → 44 unique warnings.
8. **Remaining `CS8602`** (8, three shapes): two `as T` casts dereferenced unchecked
   (`DeviceCompliancePolicyHelper.cs:183`, `DeviceConfigurationHelper.cs:125`);
   three `sender as DataGrid` UI sort-handler casts (`AssignmentPage.xaml.cs:922,1125`,
   `ImportPage.xaml.cs:727`); two `script.Publisher.Equals(...)` calls on a possibly-null `Publisher`
   in `ProactiveRemediationsHelper.cs:21,53`; and `HelperClass.cs:235`'s `tenantInfo.Value[0]`, which
   needs its own decision about what an empty `Value` array should mean.
9. **Remaining `CS8604` (8)**: a scattered, not-yet-categorized grab-bag — `ComboBox.SelectedItem`
   style UI inputs flowing into `HelperClass.Get*` setters (`AssignmentPage.xaml.cs`), a couple of
   `Dictionary`/`Collection` operations in `ImportPage.xaml.cs`, `FilterHelperClass.cs:99`,
   `MainWindow.xaml.cs:75`. Unlike the patterns above these don't obviously share one root cause —
   worth auditing individually before deciding on a fix shape.
10. **Long tail** (`CS8625` 6, `CS8600` 4, `CS8601` 14, `CS8603` 2, `CS8619` 1, `CS8618` 1 — 28
    total, not yet investigated at all): look at these individually; several may turn out to be the
    same root causes in a slightly different shape.

Each step above should land as its own small, reviewable PR with a CI-enforced warning-count check
(baseline ratchet) added once step 8 starts, so progress can't silently regress while this is spread
across multiple sessions.
