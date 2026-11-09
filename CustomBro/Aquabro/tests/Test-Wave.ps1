param(
    [string]$HarmonyPath = 'E:/SteamLibrary/steamapps/common/Broforce/r2mod/Broforce/profiles/Broforce/UMM/Core/0Harmony.dll',
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$waveProject = Split-Path -Parent $PSScriptRoot
$waveRepo = (Resolve-Path (Join-Path $waveProject '../..')).Path
if (!$CompilerPath) { $CompilerPath = Join-Path $waveRepo 'UnityInspectorMod/.tools/roslyn/tasks/net472/csc.exe' }
$waveTestOutput = Join-Path $waveProject 'bin/WaveNetwork/tests'
New-Item -ItemType Directory -Force -Path $waveTestOutput | Out-Null
$waveSources = @('tests/SkillSpringTests.cs','tests/PixelMaskTests.cs','tests/SkillSharkTests.cs','tests/SkillMaelstromTests.cs','tests/SkillDamageTargetTests.cs','tests/SkillOcclusionTests.cs','tests/BossDamageTestStubs.cs','tests/BossSkillDamageTests.cs','src/CollisionDamageTarget.cs','src/AquabroSkills.cs','src/PixelMask.cs','src/SkillContactMask.cs','src/WaterProjectileBarrier.cs','src/SpringPixelMask.cs','src/SharkPixelMask.cs','src/MaelstromPixelMask.cs','src/TidalWavePixelMask.cs') | ForEach-Object { Join-Path $waveProject $_ }
$waveTestExe = Join-Path $waveTestOutput 'WaveTests.exe'
$waveHarmony = $HarmonyPath
Copy-Item -LiteralPath $waveHarmony -Destination (Join-Path $waveTestOutput '0Harmony.dll')
& $CompilerPath /nologo /target:exe /reference:System.Drawing.dll ("/reference:$waveHarmony") ("/out:$waveTestExe") $waveSources 2>&1 | Tee-Object -FilePath (Join-Path $waveTestOutput 'test-build.log')
if ($LASTEXITCODE -ne 0) { throw 'Wave test compilation failed' }
& $waveTestExe --wave-network (Join-Path $waveProject '_Mod/skill-tidal-wave.png') 2>&1 | Tee-Object -FilePath (Join-Path $waveTestOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Wave/network regression failed' }
