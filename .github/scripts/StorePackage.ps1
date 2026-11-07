<#
.SYNOPSIS
    Checks a built Store upload package before it can be submitted.

.DESCRIPTION
    Used by release.yml and tested by StorePackage.Tests.ps1. Opens the .msixupload without
    extracting it and compares what is inside with what the Store expects. The expected package
    identity is read from Package.StoreAssociation.xml, so there is one source of truth: an
    accidental rename or a wrong publisher is caught here instead of at certification, days later.

    The bundle must hold exactly one application package per expected architecture (x64 and
    ARM64), and each package is checked on its own, including that its executable is really built
    for that architecture: a manifest saying "arm64" around an x64 executable would still install
    and run (emulated), so nothing else would notice.

    Returns the list of problems found; an empty result means the package looks right.
#>

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# An entry of a zip that is itself a zip (.msixupload > .msixbundle > .msix), opened in memory.
function Open-NestedArchive {
    param([System.IO.Compression.ZipArchiveEntry] $Entry)
    $memory = New-Object System.IO.MemoryStream
    $stream = $Entry.Open()
    try { $stream.CopyTo($memory) } finally { $stream.Dispose() }
    $memory.Position = 0
    return New-Object System.IO.Compression.ZipArchive($memory, [System.IO.Compression.ZipArchiveMode]::Read)
}

function Read-ArchiveXml {
    param([System.IO.Compression.ZipArchive] $Archive, [string] $Name)
    $entry = $Archive.Entries | Where-Object { $_.FullName -eq $Name } | Select-Object -First 1
    if (-not $entry) { return $null }
    $reader = New-Object System.IO.StreamReader($entry.Open())
    try { return [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
}

# The architecture a Windows executable was built for, from the machine field of its PE header,
# or $null if the entry is not a PE file. Only the first 4 KB are read.
function Get-PeArchitecture {
    param([System.IO.Compression.ZipArchiveEntry] $Entry)

    $buffer = New-Object byte[] 4096
    $read = 0
    $stream = $Entry.Open()
    try {
        while ($read -lt $buffer.Length) {
            $count = $stream.Read($buffer, $read, $buffer.Length - $read)
            if ($count -le 0) { break }
            $read += $count
        }
    }
    finally { $stream.Dispose() }

    if ($read -lt 0x40 -or $buffer[0] -ne 0x4D -or $buffer[1] -ne 0x5A) { return $null }       # 'MZ'
    $peOffset = [BitConverter]::ToInt32($buffer, 0x3C)
    if ($peOffset -lt 0 -or ($peOffset + 6) -gt $read) { return $null }
    if ($buffer[$peOffset] -ne 0x50 -or $buffer[$peOffset + 1] -ne 0x45 -or $buffer[$peOffset + 2] -ne 0 -or $buffer[$peOffset + 3] -ne 0) { return $null }   # 'PE'

    switch ([BitConverter]::ToUInt16($buffer, $peOffset + 4)) {
        0x8664  { return 'x64' }
        0xAA64  { return 'arm64' }
        0x014C  { return 'x86' }
        0x01C4  { return 'arm' }
        default { return 'unknown' }
    }
}

# Checks one application package of the bundle and adds what is wrong to $Problems.
function Test-ApplicationPackage {
    param(
        [System.IO.Compression.ZipArchive] $Bundle,
        $PackageNode,
        [string] $Architecture,
        [string] $ExpectedName,
        [string] $ExpectedPublisher,
        [string] $Version,
        [string] $ApplicationId,
        [System.Collections.Generic.List[string]] $Problems
    )

    $fileName = $PackageNode.GetAttribute('FileName')
    $packageEntry = $Bundle.Entries | Where-Object { $_.FullName -eq $fileName } | Select-Object -First 1
    if (-not $packageEntry) {
        $Problems.Add("($Architecture) The bundle lists '$fileName' but does not contain it.")
        return
    }

    $package = Open-NestedArchive $packageEntry
    try {
        $manifest = Read-ArchiveXml $package 'AppxManifest.xml'
        if (-not $manifest) {
            $Problems.Add("($Architecture) The application package has no AppxManifest.xml.")
            return
        }

        $identity = $manifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
        if ($identity.GetAttribute('Name') -ne $ExpectedName) {
            $Problems.Add("($Architecture) Package identity name is '$($identity.GetAttribute('Name'))' but the Store product is '$ExpectedName'. A different name would be a different app.")
        }
        if ($identity.GetAttribute('Publisher') -ne $ExpectedPublisher) {
            $Problems.Add("($Architecture) Package publisher is '$($identity.GetAttribute('Publisher'))' but the Store account's is '$ExpectedPublisher'.")
        }
        if ($identity.GetAttribute('Version') -ne $Version) {
            $Problems.Add("($Architecture) Package version is $($identity.GetAttribute('Version')) but the release is $Version.")
        }
        if ($identity.GetAttribute('ProcessorArchitecture') -ne $Architecture) {
            $Problems.Add("($Architecture) Package architecture is '$($identity.GetAttribute('ProcessorArchitecture'))', expected $Architecture.")
        }

        $application = $manifest.SelectSingleNode("//*[local-name()='Application']")
        if ($application.GetAttribute('Id') -ne $ApplicationId) {
            $Problems.Add("($Architecture) Application Id is '$($application.GetAttribute('Id'))', expected '$ApplicationId'. Changing it breaks pins and shortcuts after an update.")
        }

        $executable = $application.GetAttribute('Executable')
        $executableEntry = $package.Entries | Where-Object { $_.FullName -ieq ($executable -replace '\\', '/') } | Select-Object -First 1
        if (-not $executableEntry) {
            $Problems.Add("($Architecture) The manifest starts '$executable' but that file is not in the package.")
        }
        else {
            $built = Get-PeArchitecture $executableEntry
            if (-not $built) {
                $Problems.Add("($Architecture) '$executable' is not a Windows executable.")
            }
            elseif ($built -ne $Architecture) {
                $Problems.Add("($Architecture) '$executable' is built for $built, not $Architecture. The package would install and run, but not natively.")
            }
        }

        if (-not $manifest.SelectSingleNode("//*[local-name()='Capability'][@Name='runFullTrust']")) {
            $Problems.Add("($Architecture) The runFullTrust capability is missing, so the app would not start.")
        }
    }
    finally { $package.Dispose() }
}

function Test-StorePackage {
    [CmdletBinding()]
    param(
        # The .msixupload to check
        [Parameter(Mandatory)] [string] $Path,

        # The version being released
        [Parameter(Mandatory)] [string] $Version,

        # Package.StoreAssociation.xml: where the expected identity comes from
        [Parameter(Mandatory)] [string] $AssociationFile,

        # The bundle must contain exactly these application packages, one each
        [string[]] $Architecture = @('x64', 'arm64'),

        # Part of the app's AppUserModelID: changing it breaks pins and shortcuts after an update
        [string] $ApplicationId = 'App'
    )

    $problems = [System.Collections.Generic.List[string]]::new()

    # What the Store expects
    $association = [xml](Get-Content -LiteralPath $AssociationFile -Raw)
    $expectedName = $association.SelectSingleNode("//*[local-name()='MainPackageIdentityName']").InnerText
    $expectedPublisher = $association.SelectSingleNode("/*/*[local-name()='Publisher']").InnerText
    if (-not $expectedName -or -not $expectedPublisher) {
        $problems.Add("Could not read the expected identity from $AssociationFile.")
        return $problems
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        $problems.Add("The package '$Path' does not exist.")
        return $problems
    }

    $upload = $null
    $bundle = $null
    try {
        try { $upload = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path).Path) }
        catch {
            $problems.Add("'$Path' is not a readable .msixupload (zip) file: $($_.Exception.Message)")
            return $problems
        }

        $bundleEntries = @($upload.Entries | Where-Object { $_.FullName -match '\.(msixbundle|appxbundle)$' })
        if ($bundleEntries.Count -ne 1) {
            $problems.Add("Expected exactly one .msixbundle inside the upload, found $($bundleEntries.Count).")
            return $problems
        }
        $bundle = Open-NestedArchive $bundleEntries[0]

        # --- the bundle ---
        $bundleXml = Read-ArchiveXml $bundle 'AppxMetadata/AppxBundleManifest.xml'
        if (-not $bundleXml) {
            $problems.Add('The bundle has no AppxMetadata/AppxBundleManifest.xml.')
            return $problems
        }
        $bundleIdentity = $bundleXml.SelectSingleNode("/*[local-name()='Bundle']/*[local-name()='Identity']")
        if ($bundleIdentity.GetAttribute('Name') -ne $expectedName) {
            $problems.Add("Bundle identity name is '$($bundleIdentity.GetAttribute('Name'))' but the Store product is '$expectedName'. A different name would be a different app.")
        }
        if ($bundleIdentity.GetAttribute('Publisher') -ne $expectedPublisher) {
            $problems.Add("Bundle publisher is '$($bundleIdentity.GetAttribute('Publisher'))' but the Store account's is '$expectedPublisher'.")
        }
        if ($bundleIdentity.GetAttribute('Version') -ne $Version) {
            $problems.Add("Bundle version is $($bundleIdentity.GetAttribute('Version')) but the release is $Version.")
        }

        # One application package per expected architecture, and nothing else. -contains and
        # -notcontains ignore case, which matters: the bundle says "arm64", MSBuild says "ARM64".
        $applicationPackages = @($bundleXml.SelectNodes("//*[local-name()='Package'][@Type='application']"))
        $found = @($applicationPackages | ForEach-Object { $_.GetAttribute('Architecture') })
        $missing = @($Architecture | Where-Object { $found -notcontains $_ })
        $unexpected = @($found | Where-Object { $Architecture -notcontains $_ })
        if ($applicationPackages.Count -ne $Architecture.Count -or $missing.Count -gt 0 -or $unexpected.Count -gt 0) {
            $problems.Add("Expected exactly one application package for each of $($Architecture -join ', '); the bundle has: $($found -join ', ').")
            return $problems
        }

        # --- each application package inside the bundle ---
        foreach ($wanted in $Architecture) {
            $node = $applicationPackages | Where-Object { $_.GetAttribute('Architecture') -eq $wanted } | Select-Object -First 1
            Test-ApplicationPackage -Bundle $bundle -PackageNode $node -Architecture $wanted `
                -ExpectedName $expectedName -ExpectedPublisher $expectedPublisher -Version $Version `
                -ApplicationId $ApplicationId -Problems $problems
        }
    }
    finally {
        foreach ($archive in $bundle, $upload) { if ($archive) { $archive.Dispose() } }
    }

    return $problems
}
