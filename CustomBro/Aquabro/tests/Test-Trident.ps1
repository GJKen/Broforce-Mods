$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$testPath = Join-Path $env:TEMP 'haiwang-trident-tests'
New-Item -ItemType Directory -Force -Path $testPath | Out-Null
$testExe = Join-Path $testPath 'TridentAttackStateTests.exe'
$compilerPath = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
& $compilerPath /nologo /target:exe "/out:$testExe" `
    (Join-Path $projectPath 'src\TridentAttackState.cs') `
    (Join-Path $projectPath 'src\TridentStrikeGeometry.cs') `
    (Join-Path $PSScriptRoot 'TridentAttackStateTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Trident state test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Trident state tests failed.' }

$waveTestExe = Join-Path $testPath 'RisingWaveDamageTests.exe'
& $compilerPath /nologo /target:exe "/out:$waveTestExe" `
    (Join-Path $projectPath 'src\RisingWaveDamage.cs') `
    (Join-Path $PSScriptRoot 'RisingWaveDamageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Rising wave damage test compilation failed.' }
& $waveTestExe
if ($LASTEXITCODE -ne 0) { throw 'Rising wave damage tests failed.' }

$movementTestExe = Join-Path $testPath 'TridentMovementAnimationTests.exe'
& $compilerPath /nologo /target:exe "/out:$movementTestExe" `
    (Join-Path $projectPath 'src\TridentMovementAnimation.cs') `
    (Join-Path $projectPath 'src\TridentAttackState.cs') `
    (Join-Path $PSScriptRoot 'TridentMovementAnimationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Movement animation test compilation failed.' }
& $movementTestExe
if ($LASTEXITCODE -ne 0) { throw 'Movement animation tests failed.' }

& (Join-Path $PSScriptRoot 'Test-LegacyLadderAtlas.ps1')

$groundTestExe = Join-Path $testPath 'GroundVisualAlignmentTests.exe'
& $compilerPath /nologo /target:exe /reference:System.Drawing.dll "/out:$groundTestExe" `
    (Join-Path $projectPath 'src\TridentMovementAnimation.cs') `
    (Join-Path $PSScriptRoot 'GroundVisualAlignmentTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Ground visual alignment test compilation failed.' }
& $groundTestExe (Join-Path $projectPath '_Mod\sprite.png')
if ($LASTEXITCODE -ne 0) { throw 'Ground visual alignment tests failed.' }
