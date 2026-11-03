<#
    Tests for StorePackage.ps1. Plain PowerShell (no Pester):

        pwsh -NoProfile -File .github/scripts/StorePackage.Tests.ps1

    Builds small fake .msixupload files (an upload containing a bundle containing a package, all
    in memory) so each rule can be made to fail on its own. Exits non-zero if any scenario fails.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'StorePackage.ps1')

$script:failures = 0
$publisher = 'CN=TEST-PUBLISHER'
$name = 'Test.Product'

function Assert-Package {
    param([string] $Scenario, [object[]] $Problems, [string[]] $Expect = @())
    $text = ($Problems -join "`n")
    $ok = ($Problems.Count -eq $Expect.Count)
    foreach ($e in $Expect) { if ($text -notlike "*$e*") { $ok = $false } }
    if ($ok) { Write-Host "  ok    $Scenario"; return }
    $script:failures++
    Write-Host "  FAIL  $Scenario"
    Write-Host "        expected $($Expect.Count) problem(s) containing: $($Expect -join ' | ')"
    Write-Host "        got      $($Problems.Count): $text"
}

# A zip, in memory, from name -> content (string or byte[])
function New-ZipBytes([hashtable] $Files) {
    $memory = New-Object System.IO.MemoryStream
    $zip = New-Object System.IO.Compression.ZipArchive($memory, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    foreach ($entryName in $Files.Keys) {
        $bytes = if ($Files[$entryName] -is [byte[]]) { $Files[$entryName] } else { [System.Text.Encoding]::UTF8.GetBytes([string]$Files[$entryName]) }
        $entry = $zip.CreateEntry($entryName)
        $stream = $entry.Open()
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Dispose()
    }
    $zip.Dispose()
    return ,$memory.ToArray()
}

# A fake upload. Defaults describe a good package; each scenario overrides one thing.
function New-FakeUpload {
    param(
        [string] $Path,
        [string] $IdentityName = $name,
        [string] $IdentityPublisher = $publisher,
        [string] $BundleName = $IdentityName,
        [string] $BundlePublisher = $IdentityPublisher,
        [string] $BundleVersion = '1.9.0.0',
        [string] $PackageVersion = '1.9.0.0',
        [string] $Architecture = 'x64',
        [string] $ApplicationId = 'App',
        [string] $Executable = 'InToolz\InToolz.exe',
        [bool] $IncludeExecutable = $true,
        [bool] $RunFullTrust = $true,
        [bool] $IncludeBundle = $true
    )

    $capabilities = '<Capability Name="internetClient" />'
    if ($RunFullTrust) { $capabilities += '<rescap:Capability Name="runFullTrust" />' }
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Identity Name="$IdentityName" Publisher="$IdentityPublisher" Version="$PackageVersion" ProcessorArchitecture="$Architecture" />
  <Applications><Application Id="$ApplicationId" Executable="$Executable" EntryPoint="Windows.FullTrustApplication" /></Applications>
  <Capabilities>$capabilities</Capabilities>
</Package>
"@
    $packageFiles = @{ 'AppxManifest.xml' = $manifest }
    if ($IncludeExecutable) { $packageFiles[($Executable -replace '\\', '/')] = [byte[]](1, 2, 3) }
    $package = New-ZipBytes $packageFiles

    $bundleManifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Bundle xmlns="http://schemas.microsoft.com/appx/2013/bundle" SchemaVersion="6.0.0.0">
  <Identity Name="$BundleName" Publisher="$BundlePublisher" Version="$BundleVersion" />
  <Packages>
    <Package Type="application" Version="$PackageVersion" Architecture="$Architecture" FileName="app.msix" Offset="0" Size="0" />
    <Package Type="resource" Version="$PackageVersion" FileName="scale-100.msix" Offset="0" Size="0" />
  </Packages>
</Bundle>
"@
    $bundle = New-ZipBytes @{ 'AppxMetadata/AppxBundleManifest.xml' = $bundleManifest; 'app.msix' = $package }

    $uploadFiles = @{ 'symbols.appxsym' = [byte[]](0) }
    if ($IncludeBundle) { $uploadFiles['App_x64.msixbundle'] = $bundle }
    [System.IO.File]::WriteAllBytes($Path, (New-ZipBytes $uploadFiles))
    return $Path
}

$dir = Join-Path ([System.IO.Path]::GetTempPath()) ("store-package-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
try {
    $association = Join-Path $dir 'Package.StoreAssociation.xml'
    Set-Content -Path $association -Value @"
<?xml version="1.0" encoding="utf-8"?>
<StoreAssociation xmlns="http://schemas.microsoft.com/appx/2010/storeassociation">
  <Publisher>$publisher</Publisher>
  <ProductReservedInfo><MainPackageIdentityName>$name</MainPackageIdentityName></ProductReservedInfo>
</StoreAssociation>
"@

    function Check([string] $File) { @(Test-StorePackage -Path $File -Version '1.9.0.0' -AssociationFile $association) }
    function Build([hashtable] $Override = @{}) { New-FakeUpload -Path (Join-Path $dir ([guid]::NewGuid().ToString('N') + '.msixupload')) @Override }

    Write-Host 'Store package check'
    Assert-Package 'a correct package passes' (Check (Build))
    Assert-Package 'a different identity name is rejected (it would be a different app)' (Check (Build @{ IdentityName = 'Other.App'; BundleName = 'Other.App' })) @('identity name is ''Other.App''', 'identity name is ''Other.App''')
    Assert-Package 'a different publisher is rejected' (Check (Build @{ IdentityPublisher = 'CN=SOMEONE-ELSE'; BundlePublisher = 'CN=SOMEONE-ELSE' })) @('publisher is ''CN=SOMEONE-ELSE''', 'publisher is ''CN=SOMEONE-ELSE''')
    Assert-Package 'a wrong version is rejected' (Check (Build @{ BundleVersion = '1.8.0.0'; PackageVersion = '1.8.0.0' })) @('Bundle version is 1.8.0.0', 'Package version is 1.8.0.0')
    Assert-Package 'a bundle that disagrees with its package is rejected' (Check (Build @{ BundleVersion = '1.8.0.0' })) @('Bundle version is 1.8.0.0')
    Assert-Package 'the wrong architecture is rejected' (Check (Build @{ Architecture = 'x86' })) @('Expected exactly one application package, for x64')
    Assert-Package 'a changed Application Id is rejected' (Check (Build @{ ApplicationId = 'InToolz' })) @('Application Id is ''InToolz''')
    Assert-Package 'a missing executable is rejected' (Check (Build @{ IncludeExecutable = $false })) @('that file is not in the package')
    Assert-Package 'a missing runFullTrust capability is rejected' (Check (Build @{ RunFullTrust = $false })) @('runFullTrust')
    Assert-Package 'an upload without a bundle is rejected' (Check (Build @{ IncludeBundle = $false })) @('exactly one .msixbundle')

    $notZip = Join-Path $dir 'not-a-package.msixupload'
    Set-Content -Path $notZip -Value 'this is not a zip file'
    Assert-Package 'a file that is not a package is rejected' (Check $notZip) @('not a readable .msixupload')
    Assert-Package 'a missing file is rejected' (Check (Join-Path $dir 'nope.msixupload')) @('does not exist')
}
finally {
    if (Test-Path -LiteralPath $dir) { [System.IO.Directory]::Delete($dir, $true) }
}

if ($script:failures -gt 0) {
    Write-Host "`n$($script:failures) scenario(s) failed."
    exit 1
}
Write-Host "`nAll scenarios passed."