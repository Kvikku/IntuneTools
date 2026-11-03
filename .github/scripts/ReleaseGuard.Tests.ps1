<#
    Tests for ReleaseGuard.ps1. Plain PowerShell (no Pester), so it runs anywhere pwsh does:

        pwsh -NoProfile -File .github/scripts/ReleaseGuard.Tests.ps1

    Each scenario runs against a throwaway git repository and a stand-in for `gh`.
    Exits non-zero if any scenario fails.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseGuard.ps1')

$script:failures = 0
$script:ciPassed = 1      # what the stand-in `gh` reports as successful ci.yml runs
$script:ghFails = $false  # make the stand-in `gh` fail like an API error
$script:publishedCount = 0 # how many published (non-draft) releases the stand-in `gh` reports for the version

# Stand-in for the GitHub CLI; a function shadows the real executable.
function gh {
    $global:LASTEXITCODE = if ($script:ghFails) { 1 } else { 0 }
    if ($script:ghFails) { return }
    if ($args[0] -eq 'release') { return $script:publishedCount }
    return $script:ciPassed
}

function Assert-Guard {
    param([string] $Name, [object[]] $Problems, [string[]] $Expect = @())
    $text = ($Problems -join "`n")
    $ok = ($Problems.Count -eq $Expect.Count)
    foreach ($e in $Expect) { if ($text -notlike "*$e*") { $ok = $false } }
    if ($ok) { Write-Host "  ok    $Name"; return }
    $script:failures++
    Write-Host "  FAIL  $Name"
    Write-Host "        expected $($Expect.Count) problem(s) containing: $($Expect -join ' | ')"
    Write-Host "        got      $($Problems.Count): $text"
}

function Add-ProjectCommit([string] $ProjectVersion, [string] $Message) {
    # The message is embedded so every commit differs, even when the version does not change.
    Set-Content -Path 'InToolz.csproj' -Value "<Project><!-- $Message --><PropertyGroup><Version>$ProjectVersion</Version></PropertyGroup></Project>"
    git add -A | Out-Null
    git commit -q -m $Message | Out-Null
    $sha = git rev-parse HEAD
    if ($sha -notmatch '^[0-9a-f]{40}$') { throw "Test setup failed: '$Message' did not produce a commit ($sha)" }
    return $sha
}

function Use-Commit([string] $Sha) { git -c advice.detachedHead=false checkout -q $Sha }

function Invoke-Guard([string] $Version, [string] $Sha, [string] $CurrentTag = '', [bool] $Explicit = $true) {
    @(Test-ReleaseIntent -Version $Version -Sha $Sha -Repository 'owner/repo' -CurrentTag $CurrentTag -VersionWasExplicit $Explicit)
}

# --- a small history: 1.7.0.0 released, 1.8.0.0 bumped on master, plus an unmerged feature commit ---
$repo = Join-Path ([System.IO.Path]::GetTempPath()) ("release-guard-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $repo | Out-Null
Push-Location $repo
try {
    git init -q
    git config user.name 'test'
    git config user.email 'test@example.com'
    git config commit.gpgsign false

    $c1 = Add-ProjectCommit '1.7.0.0' 'release 1.7'
    git tag 1.7.0.0
    $c2 = Add-ProjectCommit '1.8.0.0' 'bump to 1.8'
    git update-ref refs/remotes/origin/master $c2        # master is at c2
    git -c advice.detachedHead=false checkout -q -b feature
    $c3 = Add-ProjectCommit '1.8.0.0' 'unmerged feature work'   # not on master

    Write-Host 'Release guard'

    Use-Commit $c2
    Assert-Guard 'an intended release passes' (Invoke-Guard '1.8.0.0' $c2)
    Assert-Guard 'a malformed version is rejected' (Invoke-Guard '1.8' $c2) @('major.minor.patch.build')

    Use-Commit $c3
    Assert-Guard 'a commit that is not on master is rejected' (Invoke-Guard '1.8.0.0' $c3) @('not on master')

    Use-Commit $c2
    Assert-Guard 'the same version as the latest release is rejected' (Invoke-Guard '1.7.0.0' $c2) @('the same as the latest release', 'says 1.8.0.0')
    Assert-Guard 'an older version is rejected' (Invoke-Guard '1.6.0.0' $c2) @('older than the latest release', 'says 1.8.0.0')

    Use-Commit $c1
    Assert-Guard 'a premature tag (right version, commit lacks the bump) is rejected' (Invoke-Guard '1.8.0.0' $c1) @('says 1.7.0.0')
    Assert-Guard 'auto-increment does not need the project file to match' (Invoke-Guard '1.7.1.0' $c1 '' $false)

    Use-Commit $c2
    git tag 1.8.0.0
    Assert-Guard 'the tag that triggered the run is not "already released"' (Invoke-Guard '1.8.0.0' $c2 '1.8.0.0')
    Assert-Guard 'the same tag is "already released" when it did not trigger the run' (Invoke-Guard '1.8.0.0' $c2) @('the same as the latest release')

    $script:ciPassed = 0
    Assert-Guard 'a commit with no successful CI run is rejected' (Invoke-Guard '1.9.0.0' $c2) @('no successful run', 'says 1.8.0.0')
    $script:ciPassed = 1

    $script:ghFails = $true
    Assert-Guard 'failed GitHub lookups are reported, not ignored' (Invoke-Guard '1.8.0.0' $c2 '1.8.0.0') @('Could not look up CI runs', 'Could not look up existing releases')
    $script:ghFails = $false

    # Publishing a draft creates its tag, which starts the workflow again for a version that is already out.
    $script:publishedCount = 1
    Assert-Guard 'a version that is already published is rejected (the run started by publishing)' (Invoke-Guard '1.8.0.0' $c2 '1.8.0.0') @('already published')
    $script:publishedCount = 0

    Use-Commit $c3
    $script:ciPassed = 0
    Assert-Guard 'every problem is reported at once' (Invoke-Guard '1.7.0.0' $c3) @('not on master', 'older than the latest release', 'says 1.8.0.0', 'no successful run')
}
finally {
    Pop-Location
    Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue
}

if ($script:failures -gt 0) {
    Write-Host "`n$($script:failures) scenario(s) failed."
    exit 1
}
Write-Host "`nAll scenarios passed."