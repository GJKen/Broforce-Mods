$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot 'obj\SpeedTests'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$outputPath = Join-Path $outputDir 'GameSpeedControllerTests.exe'
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
& $compiler /nologo /target:exe "/out:$outputPath" `
    (Join-Path $PSScriptRoot 'GameSpeedControllerTests.cs') `
    (Join-Path $projectRoot 'src\GameSpeedController.cs') `
    (Join-Path $projectRoot 'src\TestModSettings.cs')
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $LASTEXITCODE" }
& $outputPath
if ($LASTEXITCODE -ne 0) { throw "Game-speed tests failed: $LASTEXITCODE" }
