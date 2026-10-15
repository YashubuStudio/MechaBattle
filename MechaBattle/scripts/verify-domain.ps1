[CmdletBinding()]
param([string]$DotnetPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $DotnetPath) {
    $DotnetPath = 'C:/Program Files/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $DotnetPath)) { $DotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
}
$dotnetRoot = Split-Path -Parent $DotnetPath
$nunit = @(Get-ChildItem -Path (Join-Path $projectRoot 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll') -File)
if ($nunit.Count -eq 0) { throw 'Open the project in Unity once to resolve the NUnit package.' }
$compiler = Get-ChildItem -Path (Join-Path $dotnetRoot 'sdk/*/Roslyn/bincore/csc.dll') -File | Sort-Object FullName -Descending | Select-Object -First 1
$refPack = Get-ChildItem -Path (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref/9.0.*') -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $compiler -or -not $refPack) { throw '.NET 9 SDK/targeting pack is required for the independent C# smoke harness.' }
$output = Join-Path $projectRoot 'Build/DomainSmoke'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @(Get-ChildItem -Path (Join-Path $refPack.FullName 'ref/net9.0/*.dll') -File)
$sources = @(Get-ChildItem -Path (Join-Path $projectRoot 'Assets/Game/Runtime/Contracts/*.cs'), (Join-Path $projectRoot 'Assets/Game/Runtime/Battlefield/*.cs'), (Join-Path $projectRoot 'Assets/Game/Runtime/Simulation/*.cs'), (Join-Path $projectRoot 'Assets/Game/Runtime/Observation/*.cs'), (Join-Path $projectRoot 'Assets/Game/Runtime/AI/Bots/*.cs') -File)
$sources += Get-ChildItem -Path (Join-Path $projectRoot 'Assets/Game/Tests/EditMode/*.cs') -File
$sources += Get-ChildItem -Path (Join-Path $PSScriptRoot 'DomainSmoke/*.cs') -File
$assemblyPath = Join-Path $output 'DomainSmoke.dll'
$lines = @('/nostdlib+', '/target:exe', '/langversion:latest', ('/out:"' + $assemblyPath + '"'))
$lines += $references | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$lines += '/reference:"' + $nunit[0].FullName + '"'
$lines += $sources | ForEach-Object { '"' + $_.FullName + '"' }
$response = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($response, $lines, [Text.UTF8Encoding]::new($false))
& $DotnetPath $compiler.FullName /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Pure C# compilation failed.' }
Copy-Item -LiteralPath $nunit[0].FullName -Destination (Join-Path $output 'nunit.framework.dll') -Force
$runtime = '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"},"rollForward":"LatestMinor"}}'
[IO.File]::WriteAllText((Join-Path $output 'DomainSmoke.runtimeconfig.json'), $runtime)
& $DotnetPath $assemblyPath $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Pure C# smoke checks failed. See Build/Reports/domain-smoke.json.' }
