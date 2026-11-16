$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot 'obj\CheatTests'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$outputPath = Join-Path $outputDir 'PlayerCheatTests.exe'
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
& $compiler /nologo /target:exe "/out:$outputPath" `
    (Join-Path $PSScriptRoot 'PlayerCheatTests.cs') `
    (Join-Path $projectRoot 'src\PlayerCheats.cs') `
    (Join-Path $projectRoot 'src\PlayerCheatNetwork.cs') `
    (Join-Path $projectRoot 'src\PlayerRevival.cs')
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $LASTEXITCODE" }
& $outputPath
if ($LASTEXITCODE -ne 0) { throw "Player-cheat tests failed: $LASTEXITCODE" }
