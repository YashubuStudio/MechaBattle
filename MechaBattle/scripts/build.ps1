[CmdletBinding()]
param([string]$UnityPath)
. (Join-Path $PSScriptRoot 'common.ps1')
$reports = Join-Path $script:ProjectRoot 'Build/Reports'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$logPath = Join-Path $reports ('WindowsBuild-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.log')
$unityArgs = @('-batchmode', '-quit', '-projectPath', $script:ProjectRoot, '-buildTarget', 'Win64', '-executeMethod', 'MechaBattle.EditorTools.PreparationCommands.BuildWindows', '-logFile', $logPath)
Invoke-MechaEditor -UnityPath $UnityPath -Arguments $unityArgs
$executable = Join-Path $script:ProjectRoot 'Build/Windows/MechaBattle.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Unity returned without a Windows executable.' }
Write-Host ('Foundation toolchain build: ' + $executable + '. This is not P0 acceptance.')
