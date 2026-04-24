# Contributing to IntuneTools

Thanks for contributing! A few project conventions to be aware of.

## `using` directives

To keep file headers focused on what's actually file-specific, this project
centralizes its common imports. **Do not re-add these to individual files.**

1. **BCL namespaces** are pulled in by `<ImplicitUsings>enable</ImplicitUsings>`
   in both `IntuneTools.csproj` and `IntuneTools.Tests/IntuneTools.Tests.csproj`.
   The SDK-default set includes `System`, `System.Collections.Generic`,
   `System.IO`, `System.Linq`, `System.Net.Http`, `System.Threading`, and
   `System.Threading.Tasks`.

2. **Project-wide globals** live in the `<ItemGroup>` of `<Using Include="…" />`
   entries inside `IntuneTools.csproj` (and mirrored in
   `IntuneTools.Tests.csproj`). This is the single source of truth — there is
   no `GlobalUsing.cs` file. Currently globalized:

   - `IntuneTools.Utilities`
   - `Microsoft.Graph.Beta`, `Microsoft.Graph.Beta.Models`
   - `System.Collections.ObjectModel`, `System.Text`, `System.Text.Json`
   - `Microsoft.Kiota.Serialization.Json`
   - Several `using static …` helpers
     (`HelperClass`, `TimeSaved`, `Variables`, `CustomContentInfo`,
     `DestinationUserAuthentication`, `SourceUserAuthentication`)

3. **Per-file imports** are still appropriate for namespaces that are only
   used in a few places — for example `Microsoft.UI.Xaml`,
   `Microsoft.UI.Xaml.Controls`, `Windows.Storage.Pickers`, or any
   `using static …Helper;` that should stay file-local for IntelliSense
   discoverability.

## Enforcement

Both projects enable `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`
and `.editorconfig` raises `IDE0005` ("Remove unnecessary using directives")
to a warning, plus enables consistent ordering via
`dotnet_sort_system_directives_first` and
`dotnet_separate_import_directive_groups`. If you see an IDE0005 warning when
building, just delete the flagged `using` line.
