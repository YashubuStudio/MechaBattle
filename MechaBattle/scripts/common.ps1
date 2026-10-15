$ErrorActionPreference = 'Stop'
$script:ProjectRoot = Split-Path -Parent $PSScriptRoot

function Resolve-MechaEditor {
    param([string]$UnityPath)
    $versionFile = Join-Path $script:ProjectRoot 'ProjectSettings/ProjectVersion.txt'
    $versionText = Get-Content -LiteralPath $versionFile -Raw
    if ($versionText -notmatch '(?m)^m_EditorVersion:\s*(\S+)') { throw 'Cannot read the project Editor version.' }
    $version = $Matches[1]
    if (-not $UnityPath) { $UnityPath = $env:UNITY_EDITOR_PATH }
    if (-not $UnityPath) { $UnityPath = 'C:/Unity/' + $version + '/Editor/Unity.exe' }
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        throw ('Editor not found. Supply -UnityPath for Unity ' + $version + '. No module is installed automatically.')
    }
    return (Resolve-Path -LiteralPath $UnityPath).Path
}

function Invoke-MechaEditor {
    param([string]$UnityPath, [string[]]$Arguments)
    if (Test-Path -LiteralPath (Join-Path $script:ProjectRoot 'Temp/UnityLockfile')) {
        throw 'This project is open. Run tests in the open Editor, or close it before batch testing/building.'
    }
    $editor = Resolve-MechaEditor $UnityPath
    $quotedArguments = $Arguments | ForEach-Object {
        if ($_ -match '["\r\n]') { throw 'Invalid quote/newline in Unity argument.' }
        '"' + $_ + '"'
    }
    $process = Start-Process -FilePath $editor -ArgumentList $quotedArguments -WorkingDirectory $script:ProjectRoot -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw ('Unity exited with code ' + $process.ExitCode + '. Check Build/Reports logs.') }
}
