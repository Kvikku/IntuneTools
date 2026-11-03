<#
.SYNOPSIS
    Checks a built Store upload package before it can be submitted.

.DESCRIPTION
    Used by release.yml and tested by StorePackage.Tests.ps1. Opens the .msixupload without
    extracting it and compares what is inside with what the Store expects. The expected package
    identity is read from Package.StoreAssociation.xml, so there is one source of truth: an
    accidental rename or a wrong publisher is caught here instead of at certification, days later.

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

function Test-StorePackage {
    [CmdletBinding()]
    param(
        # The .msixupload to check
        [Parameter(Mandatory)] [string] $Path,

        # The version being released
        [Parameter(Mandatory)] [string] $Version,

        # Package.StoreAssociation.xml: where the expected identity comes from
        [Parameter(Mandatory)] [string] $AssociationFile,

        [string] $Architecture = 'x64',

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
    $package = $null
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

        $applicationPackages = @($bundleXml.SelectNodes("//*[local-name()='Package'][@Type='application']"))
        $wanted = @($applicationPackages | Where-Object { $_.GetAttribute('Architecture') -eq $Architecture })
        if ($applicationPackages.Count -ne 1 -or $wanted.Count -ne 1) {
            $found = ($applicationPackages | ForEach-Object { $_.GetAttribute('Architecture') }) -join ', '
            $problems.Add("Expected exactly one application package, for $Architecture; the bundle has: $found.")
            return $problems
        }

        # --- the application package inside the bundle ---
        $packageEntry = $bundle.Entries | Where-Object { $_.FullName -eq $wanted[0].GetAttribute('FileName') } | Select-Object -First 1
        if (-not $packageEntry) {
            $problems.Add("The bundle lists '$($wanted[0].GetAttribute('FileName'))' but does not contain it.")
            return $problems
        }
        $package = Open-NestedArchive $packageEntry
        $manifest = Read-ArchiveXml $package 'AppxManifest.xml'
        if (-not $manifest) {
            $problems.Add('The application package has no AppxManifest.xml.')
            return $problems
        }

        $identity = $manifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
        if ($identity.GetAttribute('Name') -ne $expectedName) {
            $problems.Add("Package identity name is '$($identity.GetAttribute('Name'))' but the Store product is '$expectedName'. A different name would be a different app.")
        }
        if ($identity.GetAttribute('Publisher') -ne $expectedPublisher) {
            $problems.Add("Package publisher is '$($identity.GetAttribute('Publisher'))' but the Store account's is '$expectedPublisher'.")
        }
        if ($identity.GetAttribute('Version') -ne $Version) {
            $problems.Add("Package version is $($identity.GetAttribute('Version')) but the release is $Version.")
        }
        if ($identity.GetAttribute('ProcessorArchitecture') -ne $Architecture) {
            $problems.Add("Package architecture is '$($identity.GetAttribute('ProcessorArchitecture'))', expected $Architecture.")
        }

        $application = $manifest.SelectSingleNode("//*[local-name()='Application']")
        if ($application.GetAttribute('Id') -ne $ApplicationId) {
            $problems.Add("Application Id is '$($application.GetAttribute('Id'))', expected '$ApplicationId'. Changing it breaks pins and shortcuts after an update.")
        }

        $executable = $application.GetAttribute('Executable')
        $executableEntry = $executable -replace '\\', '/'
        if (-not ($package.Entries | Where-Object { $_.FullName -ieq $executableEntry })) {
            $problems.Add("The manifest starts '$executable' but that file is not in the package.")
        }

        if (-not $manifest.SelectSingleNode("//*[local-name()='Capability'][@Name='runFullTrust']")) {
            $problems.Add('The runFullTrust capability is missing, so the app would not start.')
        }
    }
    finally {
        foreach ($archive in $package, $bundle, $upload) { if ($archive) { $archive.Dispose() } }
    }

    return $problems
}