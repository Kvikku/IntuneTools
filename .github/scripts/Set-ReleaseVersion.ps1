<#
.SYNOPSIS
    Stamps a release version into the project file and both package manifests.

.DESCRIPTION
    Used by every job in release.yml that builds, so they all agree. The files are edited
    byte-for-byte apart from the version: byte-order marks and line endings are kept, so a
    version-bump commit contains only the version change.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,

    # The repository root
    [string] $Root = '.'
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Version '$Version' is not in major.minor.patch.build form (for example 1.9.0.0)."
}

function Set-VersionInFile {
    param([string] $Path, [string] $Pattern)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.IO.File]::ReadAllText($Path)    # drops a BOM, leaves line endings alone

    if (-not [regex]::IsMatch($text, $Pattern)) {
        throw "Could not find the version to update in $Path."
    }
    $updated = [regex]::Replace($text, $Pattern, { param($m) $m.Groups[1].Value + $Version + $m.Groups[2].Value }, 'None')

    [System.IO.File]::WriteAllText($Path, $updated, (New-Object System.Text.UTF8Encoding($hasBom)))
    Write-Host "$Path -> $Version"
}

# <Version>1.8.0.0</Version>
Set-VersionInFile -Path (Join-Path $Root 'InToolz.csproj') -Pattern '(<Version>)[^<]+(</Version>)'

# <Identity Name="..." Publisher="..." Version="1.8.0.0" />, in the root manifest and the Store package's
foreach ($manifest in 'Package.appxmanifest', 'InToolzPackaged/Package.appxmanifest') {
    Set-VersionInFile -Path (Join-Path $Root $manifest) -Pattern '(<Identity\b[^>]*\bVersion=")[^"]+(")'
}