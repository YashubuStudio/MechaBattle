[CmdletBinding()]
param([string]$UnityPath = 'C:/Unity/6000.3.26f1/Editor/Unity.exe', [string]$DotnetPath = 'C:/Program Files/dotnet/dotnet.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetRoot = Split-Path -Parent $DotnetPath
$compiler = Get-ChildItem (Join-Path $dotnetRoot 'sdk/*/Roslyn/bincore/csc.dll') | Sort-Object FullName -Descending | Select-Object -First 1
$refPack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref/9.0.*') -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $compiler -or -not $refPack) { throw '.NET 9 targeting pack / C# compiler not found.' }
$editorData = Join-Path (Split-Path -Parent $UnityPath) 'Data'
$engineReferences = @(Get-ChildItem (Join-Path $editorData 'Managed/UnityEngine/*.dll') -File)
if ($engineReferences.Count -eq 0) { throw 'Unity API modules not found.' }
$frameworkReferences = @(Get-ChildItem (Join-Path $refPack.FullName 'ref/net9.0/*.dll') -File)
$packageReferences = @(Get-ChildItem (Join-Path $projectRoot 'Library/ScriptAssemblies/*.dll') -File | Where-Object { $_.Name -notlike 'Game.*' -and $_.Name -notlike 'Assembly-CSharp*' })
$nunit = Get-ChildItem (Join-Path $projectRoot 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll') -File | Select-Object -First 1
$output = Join-Path $projectRoot 'Build/CompileCheck'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$assemblyFiles = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Game') -Filter '*.asmdef' -Recurse -File)
$pending = @{}
foreach ($file in $assemblyFiles) { $definition = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json; $pending[$definition.name] = @{ Definition = $definition; Directory = $file.DirectoryName } }
$internalNames = @($pending.Keys)
$compiled = @{}
$records = @()
while ($pending.Count -gt 0) {
    $progress = $false
    foreach ($name in @($pending.Keys | Sort-Object)) {
        $item = $pending[$name]
        $dependencies = @($item.Definition.references | Where-Object { $internalNames -contains $_ })
        if (@($dependencies | Where-Object { -not $compiled.ContainsKey($_) }).Count -gt 0) { continue }
        $nested = @($assemblyFiles | Where-Object { $_.DirectoryName -ne $item.Directory -and $_.DirectoryName.StartsWith($item.Directory + [IO.Path]::DirectorySeparatorChar) } | ForEach-Object { $_.DirectoryName + [IO.Path]::DirectorySeparatorChar })
        $sources = @(Get-ChildItem -LiteralPath $item.Directory -Filter '*.cs' -Recurse -File | Where-Object {
            $source = $_.FullName
            @($nested | Where-Object { $source.StartsWith($_) }).Count -eq 0
        })
        if ($sources.Count -eq 0) { throw ('Assembly has no source files yet: ' + $name) }
        $references = @($frameworkReferences.FullName)
        $references += @($dependencies | ForEach-Object { $compiled[$_] })
        if (-not $item.Definition.noEngineReferences) {
            $references += @($engineReferences.FullName) + @($packageReferences.FullName)
        }
        if ($item.Definition.optionalUnityReferences -contains 'TestAssemblies') {
            if (-not $nunit) { throw 'NUnit reference not found.' }
            $references += $nunit.FullName
        }
        $assemblyPath = Join-Path $output ($name + '.dll')
        $lines = @('/nostdlib+', '/target:library', '/langversion:latest', ('/out:"' + $assemblyPath + '"'))
        $lines += $references | Sort-Object -Unique | ForEach-Object { '/reference:"' + $_ + '"' }
        $lines += $sources | ForEach-Object { '"' + $_.FullName + '"' }
        $rsp = Join-Path $output ($name + '.rsp')
        [IO.File]::WriteAllLines($rsp, $lines, [Text.UTF8Encoding]::new($false))
        & $DotnetPath $compiler.FullName /noconfig /nologo ('@' + $rsp)
        if ($LASTEXITCODE -ne 0) { throw ('Static API compilation failed: ' + $name) }
        $compiled[$name] = $assemblyPath
        $records += @{ assembly = $name; sourceCount = $sources.Count; status = 'compiled' }
        $pending.Remove($name)
        $progress = $true
    }
    if (-not $progress) { throw ('Assembly cycle or missing dependency: ' + ($pending.Keys -join ', ')) }
}
$report = @{ scope = 'static-api-compile-not-unity-import-or-player-build'; executedUtc = [DateTime]::UtcNow.ToString('o'); assemblies = $records }
$reports = Join-Path $projectRoot 'Build/Reports'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reports 'static-api-compile.json') -Encoding UTF8
Write-Host ('Static API compile: ' + $compiled.Count + ' assemblies. Unity import/PlayMode/build remain separate.')
