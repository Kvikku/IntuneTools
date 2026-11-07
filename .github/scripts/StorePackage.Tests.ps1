<#
    Tests for StorePackage.ps1. Plain PowerShell (no Pester):

        pwsh -NoProfile -File .github/scripts/StorePackage.Tests.ps1

    Builds small fake .msixupload files (an upload containing a bundle containing one package per
    architecture, all in memory) so each rule can be made to fail on its own, in one package or in
    all of them. Exits non-zero if any scenario fails.
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

# The first bytes of a Windows executable built for a machine type: 'MZ', a pointer to a 'PE' header at
# 0x80, and the machine field. 'none' is not an executable at all.
function New-FakeExecutable([string] $Machine) {
    if ($Machine -eq 'none') { return [byte[]](1, 2, 3) }
    $code = switch ($Machine) { 'x64' { 0x8664 } 'arm64' { 0xAA64 } 'x86' { 0x014C } default { throw "Unknown machine '$Machine'" } }
    $bytes = New-Object byte[] 0x200
    $bytes[0] = 0x4D; $bytes[1] = 0x5A
    $bytes[0x3C] = 0x80
    $bytes[0x80] = 0x50; $bytes[0x81] = 0x45
    $bytes[0x84] = $code -band 0xFF
    $bytes[0x85] = ($code -shr 8) -band 0xFF
    return ,$bytes
}

# One application package. Defaults describe a good one for the architecture; $O overrides one thing.
function New-FakePackage([string] $Architecture, [hashtable] $O) {
    $identityName = if ($O.ContainsKey('IdentityName')) { $O.IdentityName } else { $name }
    $identityPublisher = if ($O.ContainsKey('IdentityPublisher')) { $O.IdentityPublisher } else { $publisher }
    $packageVersion = if ($O.ContainsKey('PackageVersion')) { $O.PackageVersion } else { '1.9.0.0' }
    $manifestArchitecture = if ($O.ContainsKey('ManifestArchitecture')) { $O.ManifestArchitecture } else { $Architecture }
    $applicationId = if ($O.ContainsKey('ApplicationId')) { $O.ApplicationId } else { 'App' }
    $executable = if ($O.ContainsKey('Executable')) { $O.Executable } else { 'InToolz\InToolz.exe' }
    $includeExecutable = if ($O.ContainsKey('IncludeExecutable')) { $O.IncludeExecutable } else { $true }
    $runFullTrust = if ($O.ContainsKey('RunFullTrust')) { $O.RunFullTrust } else { $true }
    $machine = if ($O.ContainsKey('ExecutableMachine')) { $O.ExecutableMachine } else { $Architecture.ToLowerInvariant() }

    $capabilities = '<Capability Name="internetClient" />'
    if ($runFullTrust) { $capabilities += '<rescap:Capability Name="runFullTrust" />' }
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Identity Name="$identityName" Publisher="$identityPublisher" Version="$packageVersion" ProcessorArchitecture="$manifestArchitecture" />
  <Applications><Application Id="$applicationId" Executable="$executable" EntryPoint="Windows.FullTrustApplication" /></Applications>
  <Capabilities>$capabilities</Capabilities>
</Package>
"@
    $files = @{ 'AppxManifest.xml' = $manifest }
    if ($includeExecutable) { $files[($executable -replace '\\', '/')] = New-FakeExecutable $machine }
    return ,(New-ZipBytes $files)
}

# A fake upload. Defaults describe a good x64 + arm64 package; each scenario overrides one thing.
#   $All    overrides applied to every package        (e.g. a wrong publisher everywhere)
#   $Only   architecture -> overrides for that package (e.g. a wrong version in arm64 alone)
function New-FakeUpload {
    param(
        [string] $Path,
        [string[]] $Architectures = @('x64', 'arm64'),
        [hashtable] $All = @{},
        [hashtable] $Only = @{},
        [string] $BundleName = $name,
        [string] $BundlePublisher = $publisher,
        [string] $BundleVersion = '1.9.0.0',
        [bool] $IncludeBundle = $true
    )

    $bundleFiles = @{}
    $packageElements = ''
    for ($i = 0; $i -lt $Architectures.Count; $i++) {
        $arch = $Architectures[$i]
        $overrides = @{} + $All
        if ($Only.ContainsKey($arch)) { foreach ($key in $Only[$arch].Keys) { $overrides[$key] = $Only[$arch][$key] } }
        $fileName = "app_$i.msix"
        $bundleFiles[$fileName] = New-FakePackage $arch $overrides
        $packageElements += "    <Package Type=`"application`" Version=`"1.9.0.0`" Architecture=`"$arch`" FileName=`"$fileName`" Offset=`"0`" Size=`"0`" />`n"
    }
    $packageElements += '    <Package Type="resource" Version="1.9.0.0" FileName="scale-100.msix" Offset="0" Size="0" />'

    $bundleFiles['AppxMetadata/AppxBundleManifest.xml'] = @"
<?xml version="1.0" encoding="utf-8"?>
<Bundle xmlns="http://schemas.microsoft.com/appx/2013/bundle" SchemaVersion="6.0.0.0">
  <Identity Name="$BundleName" Publisher="$BundlePublisher" Version="$BundleVersion" />
  <Packages>
$packageElements
  </Packages>
</Bundle>
"@
    $bundle = New-ZipBytes $bundleFiles

    $uploadFiles = @{ 'symbols.appxsym' = [byte[]](0) }
    if ($IncludeBundle) { $uploadFiles['App_x64_arm64.msixbundle'] = $bundle }
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

    Write-Host 'Store package check: the whole bundle'
    Assert-Package 'a correct x64 + arm64 package passes' (Check (Build))
    Assert-Package 'a different identity name is rejected everywhere (it would be a different app)' (Check (Build @{ BundleName = 'Other.App'; All = @{ IdentityName = 'Other.App' } })) @('identity name is ''Other.App''', 'identity name is ''Other.App''', 'identity name is ''Other.App''')
    Assert-Package 'a different publisher is rejected everywhere' (Check (Build @{ BundlePublisher = 'CN=SOMEONE-ELSE'; All = @{ IdentityPublisher = 'CN=SOMEONE-ELSE' } })) @('publisher is ''CN=SOMEONE-ELSE''', 'publisher is ''CN=SOMEONE-ELSE''', 'publisher is ''CN=SOMEONE-ELSE''')
    Assert-Package 'a wrong version is rejected everywhere' (Check (Build @{ BundleVersion = '1.8.0.0'; All = @{ PackageVersion = '1.8.0.0' } })) @('Bundle version is 1.8.0.0', 'Package version is 1.8.0.0', 'Package version is 1.8.0.0')
    Assert-Package 'a bundle that disagrees with its packages is rejected' (Check (Build @{ BundleVersion = '1.8.0.0' })) @('Bundle version is 1.8.0.0')
    Assert-Package 'a changed Application Id is rejected' (Check (Build @{ All = @{ ApplicationId = 'InToolz' } })) @('Application Id is ''InToolz''', 'Application Id is ''InToolz''')
    Assert-Package 'an upload without a bundle is rejected' (Check (Build @{ IncludeBundle = $false })) @('exactly one .msixbundle')

    Write-Host 'Store package check: which architectures the bundle holds'
    Assert-Package 'an x64-only bundle is rejected as incomplete' (Check (Build @{ Architectures = @('x64') })) @('each of x64, arm64; the bundle has: x64')
    Assert-Package 'an arm64-only bundle is rejected as incomplete' (Check (Build @{ Architectures = @('arm64') })) @('each of x64, arm64; the bundle has: arm64')
    Assert-Package 'an unexpected extra architecture is rejected' (Check (Build @{ Architectures = @('x64', 'arm64', 'x86') })) @('the bundle has: x64, arm64, x86')
    Assert-Package 'the wrong second architecture is rejected' (Check (Build @{ Architectures = @('x64', 'x86') })) @('the bundle has: x64, x86')
    Assert-Package 'two packages for one architecture are rejected' (Check (Build @{ Architectures = @('x64', 'x64') })) @('the bundle has: x64, x64')
    Assert-Package 'architecture names are matched ignoring case (MSBuild says ARM64, the bundle says arm64)' (Check (Build @{ Architectures = @('x64', 'ARM64') }))
    Assert-Package 'a caller can still ask for an x64-only bundle (to re-check an older package)' @(Test-StorePackage -Path (Build @{ Architectures = @('x64') }) -Version '1.9.0.0' -AssociationFile $association -Architecture 'x64')

    Write-Host 'Store package check: one package at a time, named by architecture'
    Assert-Package 'a wrong version in the arm64 package alone is rejected, and says which' (Check (Build @{ Only = @{ arm64 = @{ PackageVersion = '1.8.0.0' } } })) @('(arm64) Package version is 1.8.0.0')
    Assert-Package 'a wrong version in the x64 package alone is rejected, and says which' (Check (Build @{ Only = @{ x64 = @{ PackageVersion = '1.8.0.0' } } })) @('(x64) Package version is 1.8.0.0')
    Assert-Package 'a manifest claiming the wrong architecture is rejected' (Check (Build @{ Only = @{ arm64 = @{ ManifestArchitecture = 'x64' } } })) @('(arm64) Package architecture is ''x64'', expected arm64')
    Assert-Package 'a missing executable is rejected' (Check (Build @{ Only = @{ arm64 = @{ IncludeExecutable = $false } } })) @('(arm64) The manifest starts*that file is not in the package')
    Assert-Package 'a missing runFullTrust capability is rejected' (Check (Build @{ Only = @{ x64 = @{ RunFullTrust = $false } } })) @('(x64) The runFullTrust capability is missing')

    Write-Host 'Store package check: the executable is really built for its architecture'
    Assert-Package 'an x64 executable inside the arm64 package is rejected' (Check (Build @{ Only = @{ arm64 = @{ ExecutableMachine = 'x64' } } })) @('(arm64) ''InToolz\InToolz.exe'' is built for x64, not arm64')
    Assert-Package 'an arm64 executable inside the x64 package is rejected' (Check (Build @{ Only = @{ x64 = @{ ExecutableMachine = 'arm64' } } })) @('(x64) ''InToolz\InToolz.exe'' is built for arm64, not x64')
    Assert-Package 'an x86 executable is rejected' (Check (Build @{ Only = @{ x64 = @{ ExecutableMachine = 'x86' } } })) @('is built for x86, not x64')
    Assert-Package 'an executable that is not a Windows executable is rejected' (Check (Build @{ Only = @{ x64 = @{ ExecutableMachine = 'none' } } })) @('(x64) ''InToolz\InToolz.exe'' is not a Windows executable')
    Assert-Package 'every problem in every package is reported at once' (Check (Build @{ Only = @{ x64 = @{ PackageVersion = '1.8.0.0' }; arm64 = @{ ExecutableMachine = 'x64'; RunFullTrust = $false } } })) @('(x64) Package version is 1.8.0.0', '(arm64) ''InToolz\InToolz.exe'' is built for x64', '(arm64) The runFullTrust capability is missing')

    Write-Host 'Store package check: bad input'
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
