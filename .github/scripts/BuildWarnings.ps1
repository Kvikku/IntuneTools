<#
.SYNOPSIS
    Fails CI when the build gets noisier than the recorded baseline.

.DESCRIPTION
    Used by .github/workflows/ci.yml after the builds, and tested by BuildWarnings.Tests.ps1.
    A build that prints dozens of warnings can't tell you anything: a new one is invisible
    (see docs/WARNINGS_AUDIT.md, #114). This is a ratchet. The baseline is the number of unique
    warnings the build is allowed to print; it can only go down.

    Warnings are counted as unique lines, not MSBuild's own "N Warning(s)" total. WinUI's XAML
    compiler and the multi-platform project shape run the C# compiler more than once per file,
    so the same warning is printed 2x, 4x or 8x and that total moves in steps nobody chose.
    Every warning code counts, not just CSxxxx: the two the baseline starts with are not C# warnings.

    A drop below the baseline is reported as a notice rather than a failure, so a dependency
    update that happens to remove a warning does not fail its own pull request. Lower the
    baseline when you see the notice.
#>

function Get-UniqueBuildWarnings {
    [CmdletBinding()]
    param(
        # Raw MSBuild output; real logs are full of blank lines
        [Parameter(Mandatory)] [AllowEmptyCollection()] [AllowEmptyString()] [string[]] $Line
    )

    # "<origin>: warning <code>: <message> [<project>]". The trailing [project] is dropped so the
    # same warning from two projects (or two passes over one) is one warning.
    @($Line |
        Where-Object { $_ -match ': warning ' } |
        ForEach-Object { ($_ -replace '\s+\[[^\]]+\]\s*$', '').Trim() } |
        Sort-Object -Unique)
}

function Read-WarningBaseline {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string] $Path)

    if (-not (Test-Path $Path)) { throw "Warning baseline file not found: $Path" }

    # One integer; '#' lines are comments.
    $value = @(Get-Content $Path | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
    if ($value.Count -ne 1 -or $value[0] -notmatch '^\d+$') {
        throw "Warning baseline file $Path must contain exactly one non-negative integer (comment lines start with #)."
    }
    [int]$value[0]
}

function Test-BuildWarnings {
    [CmdletBinding()]
    param(
        # Captured MSBuild output of every build that counts toward the baseline
        [Parameter(Mandatory)] [string[]] $LogPath,

        [string] $BaselinePath = '.github/warning-baseline.txt'
    )

    # A log that can't be read or parsed must not look like a build with no warnings.
    $ErrorActionPreference = 'Stop'

    $lines = foreach ($path in $LogPath) {
        if (-not (Test-Path $path)) { throw "Build log not found: $path" }
        Get-Content $path
    }
    $warnings = @(Get-UniqueBuildWarnings -Line @($lines))
    $baseline = Read-WarningBaseline -Path $BaselinePath

    $problems = [System.Collections.Generic.List[string]]::new()
    $notices = [System.Collections.Generic.List[string]]::new()

    if ($warnings.Count -gt $baseline) {
        $problems.Add("The build prints $($warnings.Count) unique warning(s); the baseline allows $baseline. Fix the new warning(s). Raising the baseline is not the answer to a new warning.")
    }
    elseif ($warnings.Count -lt $baseline) {
        $notices.Add("The build prints $($warnings.Count) unique warning(s), below the baseline of $baseline. Lower $BaselinePath to $($warnings.Count) so the improvement can't regress.")
    }

    [pscustomobject]@{
        Warnings = $warnings
        Baseline = $baseline
        Problems = @($problems)
        Notices  = @($notices)
    }
}
