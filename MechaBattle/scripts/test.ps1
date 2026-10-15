[CmdletBinding()]
param([ValidateSet('EditMode', 'PlayMode', 'Both')][string]$Mode = 'EditMode', [string]$UnityPath)
. (Join-Path $PSScriptRoot 'common.ps1')
$reports = Join-Path $script:ProjectRoot 'Build/Reports'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$modes = @($Mode)
if ($Mode -eq 'Both') { $modes = @('EditMode', 'PlayMode') }
foreach ($platform in $modes) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $resultPath = Join-Path $reports ($platform + '-' + $stamp + '.xml')
    $logPath = Join-Path $reports ($platform + '-' + $stamp + '.log')
    $unityArgs = @('-batchmode', '-nographics', '-projectPath', $script:ProjectRoot, '-runTests', '-testPlatform', $platform, '-testResults', $resultPath, '-logFile', $logPath)
    # Do not add -quit: the Unity Test Runner owns process shutdown.
    Invoke-MechaEditor -UnityPath $UnityPath -Arguments $unityArgs
    if (-not (Test-Path -LiteralPath $resultPath)) { throw ('No test result produced: ' + $resultPath) }
    [xml]$result = Get-Content -LiteralPath $resultPath -Raw
    $summary = $result.SelectSingleNode('/test-run')
    if (-not $summary -or [int]$summary.total -le 0 -or [int]$summary.failed -gt 0 -or $summary.result -ne 'Passed') {
        throw ('Tests failed, skipped, or no tests discovered. See ' + $resultPath)
    }
    Write-Host ($platform + ': ' + $summary.passed + ' passed. ' + $resultPath)
}
