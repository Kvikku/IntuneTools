<#
.SYNOPSIS
    Decides whether a release run is likely to be intended.

.DESCRIPTION
    Used by .github/workflows/release.yml before anything is stamped, built or pushed, and
    tested by ReleaseGuard.Tests.ps1. A release can succeed and still be wrong: it publishes
    to the Store under a version number that can never be reused, so these checks fail fast
    when the commit or version looks unintended.

    Returns the list of problems found; an empty result means the release looks intended.
    Every problem is reported, not just the first, so one run shows everything to fix.
#>

function Test-ReleaseIntent {
    [CmdletBinding()]
    param(
        # The version being released, e.g. 1.9.0.0
        [Parameter(Mandatory)] [string] $Version,

        # The commit being released
        [Parameter(Mandatory)] [string] $Sha,

        # owner/name, used to look up CI runs
        [Parameter(Mandatory)] [string] $Repository,

        # The tag that triggered this run, if any. It already exists, so it is not "already released".
        [string] $CurrentTag = '',

        # False when the version was derived from the project file (auto-increment), where the
        # project file is expected to differ because the workflow stamps it.
        [bool] $VersionWasExplicit = $true,

        [string] $ProjectFile = 'InToolz.csproj',

        # The branch releases must come from
        [string] $MainRef = 'origin/master'
    )

    $problems = [System.Collections.Generic.List[string]]::new()
    $short = $Sha.Substring(0, [Math]::Min(7, $Sha.Length))

    # 1. A well-formed version. Nothing below can compare versions without it.
    if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        $problems.Add("Version '$Version' is not in major.minor.patch.build form (for example 1.9.0.0).")
        return $problems
    }
    $release = [version]$Version

    # 2. The commit must be on master. Tagging a feature-branch commit would publish unreviewed work.
    git rev-parse --verify --quiet $MainRef *> $null
    if ($LASTEXITCODE -ne 0) {
        $problems.Add("Cannot check whether commit $short is on master: '$MainRef' was not found in this checkout.")
    }
    else {
        git merge-base --is-ancestor $Sha $MainRef *> $null
        if ($LASTEXITCODE -ne 0) {
            $problems.Add("Commit $short is not on master. Merge the work to master first, then release from there.")
        }
    }

    # 3. The version must be newer than everything already released. Compared against the existing
    #    release tags, not the project file: the documented flow bumps the project file first, so
    #    the project file legitimately equals the version being released.
    $released = @(git tag --list |
        Where-Object { $_ -match '^\d+\.\d+\.\d+\.\d+$' -and $_ -ne $CurrentTag } |
        ForEach-Object { [version]$_ })
    if ($released.Count -gt 0) {
        $latest = ($released | Sort-Object -Descending)[0]
        if ($release -le $latest) {
            $relation = if ($release -eq $latest) { 'the same as' } else { 'older than' }
            $problems.Add("Version $Version is $relation the latest release, $latest. Use a version greater than $latest.")
        }
    }

    # 4. The project file at this commit must already say the version being released. The Store
    #    package is built from the project file's version, so "tag first, bump later" ships the
    #    wrong thing. This is what a premature tag looks like: the right version number on a
    #    commit that does not contain the work.
    if ($VersionWasExplicit) {
        if (-not (Test-Path $ProjectFile)) {
            $problems.Add("Cannot find $ProjectFile at commit $short to check its version.")
        }
        else {
            $projectXml = Get-Content $ProjectFile -Raw
            if ($projectXml -match '<Version>([^<]+)</Version>') {
                if ($Matches[1] -ne $Version) {
                    $problems.Add("$ProjectFile says $($Matches[1]) at commit $short but you are releasing $Version. Bump <Version> in $ProjectFile, merge it to master, then release.")
                }
            }
            else {
                $problems.Add("Cannot find a <Version> element in $ProjectFile at commit $short.")
            }
        }
    }

    # 5. CI must have passed on this exact commit. Filtered to ci.yml so a release run's own
    #    successful jobs can't count as CI.
    $passed = gh api "repos/$Repository/actions/workflows/ci.yml/runs?head_sha=$Sha&per_page=50" --jq '[.workflow_runs[] | select(.conclusion == "success")] | length'
    if ($LASTEXITCODE -ne 0) {
        $problems.Add("Could not look up CI runs for commit $short (the GitHub API call failed). Re-run the release.")
    }
    elseif ([int]$passed -lt 1) {
        $problems.Add("CI (ci.yml) has no successful run for commit $short. Wait for CI to finish or fix it, then release.")
    }

    return $problems
}