<#
    Tests for BuildWarnings.ps1. Plain PowerShell (no Pester), so it runs anywhere pwsh does:

        pwsh -NoProfile -File .github/scripts/BuildWarnings.Tests.ps1

    Each scenario writes build logs and a baseline file to a temp folder.
    Exits non-zero if any scenario fails.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BuildWarnings.ps1')

$script:failures = 0

function Assert-Equal {
    param([string] $Name, $Actual, $Expected)
    if ($Actual -eq $Expected) { Write-Host "  ok    $Name"; return }
    $script:failures++
    Write-Host "  FAIL  $Name"
    Write-Host "        expected: $Expected"
    Write-Host "        got:      $Actual"
}

function Assert-Throws {
    param([string] $Name, [scriptblock] $Action, [string] $Like)
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -like "*$Like*") { Write-Host "  ok    $Name"; return }
        $script:failures++
        Write-Host "  FAIL  $Name"
        Write-Host "        expected an error containing '$Like', got: $($_.Exception.Message)"
        return
    }
    $script:failures++
    Write-Host "  FAIL  $Name"
    Write-Host "        expected an error containing '$Like', got none"
}

# Lines as MSBuild prints them. The first is a C# warning, the next two are not.
$csWarning  = 'D:\a\InToolz\InToolz\Pages\ImportPage.xaml.cs(352,39): warning CS8604: Possible null reference argument for parameter ''item'' in ''void Collection<string>.Add(string item)''. [D:\a\InToolz\InToolz\InToolz.csproj]'
$docWarning = 'CSC : warning EnableGenerateDocumentationFile: Set MSBuild property ''GenerateDocumentationFile'' to ''true'' in project file to enable IDE0005 (Remove unnecessary usings/imports) on build (https://github.com/dotnet/roslyn/issues/41640) [D:\a\InToolz\InToolz\InToolz.csproj]'
$priWarning = 'WINAPPSDKGENERATEPROJECTPRIFILE : warning : PRI263: 0xdef01051 - No default or neutral resource given for ''Files/Assets/Square44x44Logo.ico''. [D:\a\InToolz\InToolz\InToolz.csproj]'
$noise = @(
    'Build started 11/07/2026 08:00:00.',
    '',
    'C:\sdk\Microsoft.NET.RuntimeIdentifierInference.targets(385,5): message NETSDK1057: You are using a preview version of .NET.',
    '  InToolz -> D:\a\InToolz\InToolz\bin\x64\Release\InToolz.dll',
    '',
    '    2 Warning(s)',
    '    0 Error(s)')

$dir = Join-Path ([System.IO.Path]::GetTempPath()) ("build-warnings-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null

function New-Log([string] $Name, [string[]] $Lines) {
    $path = Join-Path $dir $Name
    Set-Content -Path $path -Value $Lines
    $path
}
function New-Baseline([string[]] $Lines) {
    $path = Join-Path $dir ("baseline-" + [guid]::NewGuid().ToString('N') + '.txt')
    Set-Content -Path $path -Value $Lines
    $path
}

try {
    Write-Host 'Counting warnings'

    $log = New-Log 'a.log' ($noise + $docWarning + $priWarning)
    Assert-Equal 'non-C# warnings count' (Get-UniqueBuildWarnings -Line (Get-Content $log)).Count 2

    $log = New-Log 'b.log' ($noise + $docWarning + $docWarning + $docWarning + $docWarning)
    Assert-Equal 'the same warning printed four times counts once' (Get-UniqueBuildWarnings -Line (Get-Content $log)).Count 1

    $otherProject = $docWarning -replace '\[[^\]]+\]$', '[D:\a\InToolz\InToolz\InToolz.Tests\InToolz.Tests.csproj]'
    $log = New-Log 'c.log' ($docWarning, $otherProject)
    Assert-Equal 'the same warning from two projects counts once' (Get-UniqueBuildWarnings -Line (Get-Content $log)).Count 1

    $log = New-Log 'd.log' ($noise + $csWarning + $docWarning)
    Assert-Equal 'a C# warning is a different warning' (Get-UniqueBuildWarnings -Line (Get-Content $log)).Count 2

    $log = New-Log 'blank.log' ($noise + $docWarning)
    Assert-Equal 'blank lines in a real log are fine' (Get-UniqueBuildWarnings -Line (Get-Content $log)).Count 1

    Assert-Equal 'a build with no warnings counts zero' (Get-UniqueBuildWarnings -Line $noise).Count 0
    Assert-Equal 'an empty log counts zero' (Get-UniqueBuildWarnings -Line @()).Count 0

    Write-Host 'The baseline check'

    $two = New-Log 'two.log' ($noise + $docWarning + $priWarning)

    $result = Test-BuildWarnings -LogPath $two -BaselinePath (New-Baseline @('2'))
    Assert-Equal 'at the baseline: no problems' $result.Problems.Count 0
    Assert-Equal 'at the baseline: no notices' $result.Notices.Count 0

    $three = New-Log 'three.log' ($noise + $docWarning + $priWarning + $csWarning)
    $result = Test-BuildWarnings -LogPath $three -BaselinePath (New-Baseline @('2'))
    Assert-Equal 'above the baseline: one problem' $result.Problems.Count 1
    Assert-Equal 'above the baseline: says how many' ($result.Problems[0] -like '*prints 3 unique warning(s); the baseline allows 2*') $true
    Assert-Equal 'above the baseline: the new warning is in the result' (($result.Warnings -join "`n") -like '*CS8604*') $true

    $result = Test-BuildWarnings -LogPath $two -BaselinePath (New-Baseline @('5'))
    Assert-Equal 'below the baseline: not a failure' $result.Problems.Count 0
    Assert-Equal 'below the baseline: a notice to lower it' ($result.Notices[0] -like '*Lower*to 2*') $true

    $zero = New-Log 'zero.log' $noise
    $result = Test-BuildWarnings -LogPath $zero -BaselinePath (New-Baseline @('0'))
    Assert-Equal 'zero warnings against a baseline of zero passes' ($result.Problems.Count + $result.Notices.Count) 0

    $result = Test-BuildWarnings -LogPath $zero, $two -BaselinePath (New-Baseline @('2'))
    Assert-Equal 'several logs are counted together' $result.Warnings.Count 2

    $result = Test-BuildWarnings -LogPath $two -BaselinePath (New-Baseline @('# the most unique warnings the build may print', '', '2'))
    Assert-Equal 'comments and blank lines in the baseline are ignored' $result.Baseline 2

    Write-Host 'Bad input fails loudly, not as a pass'

    Assert-Throws 'a missing build log' { Test-BuildWarnings -LogPath (Join-Path $dir 'nope.log') -BaselinePath (New-Baseline @('2')) } 'Build log not found'
    Assert-Throws 'a missing baseline file' { Test-BuildWarnings -LogPath $two -BaselinePath (Join-Path $dir 'nope.txt') } 'baseline file not found'
    Assert-Throws 'a baseline that is not a number' { Test-BuildWarnings -LogPath $two -BaselinePath (New-Baseline @('lots')) } 'exactly one non-negative integer'
    Assert-Throws 'an empty baseline' { Test-BuildWarnings -LogPath $two -BaselinePath (New-Baseline @('# nothing here')) } 'exactly one non-negative integer'
    Assert-Throws 'two numbers in the baseline' { Test-BuildWarnings -LogPath $two -BaselinePath (New-Baseline @('1', '2')) } 'exactly one non-negative integer'
}
finally {
    Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
}

if ($script:failures -gt 0) {
    Write-Host "`n$($script:failures) scenario(s) failed."
    exit 1
}
Write-Host "`nAll scenarios passed."
