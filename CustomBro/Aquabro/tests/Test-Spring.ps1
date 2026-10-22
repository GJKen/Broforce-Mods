param([string]$AsepritePath, [string]$ReferenceDirectory)
$ErrorActionPreference = 'Stop'
$springProject = Split-Path -Parent $PSScriptRoot
. (Join-Path $springProject 'tools\BuildEnvironment.ps1')
$springSettings = Get-AquabroBuildSettings -ProjectRoot $springProject -AsepritePath $AsepritePath -ReferenceDirectory $ReferenceDirectory
$springCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
$springExecutable = Join-Path ([System.IO.Path]::GetTempPath()) ('Aquabro-SpringTests-' + [Guid]::NewGuid().ToString('N') + '.exe')
$springGenerated = Join-Path $springProject 'archive\skill-regression\previews'
New-Item -ItemType Directory -Force -Path $springGenerated | Out-Null
try {
    & $springSettings.AsepritePath -b --script-param "root=$springProject" --script-param "output=$springGenerated" --script (Join-Path $springProject 'tools\export_skill_effects.lua') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Skill mask export failed' }
    & $springCompiler /nologo /target:exe /reference:System.Drawing.dll "/reference:$(Join-Path $springSettings.ReferenceDirectory '0Harmony.dll')" "/out:$springExecutable" (Join-Path $PSScriptRoot 'SkillSpringTests.cs') (Join-Path $PSScriptRoot 'PixelMaskTests.cs') (Join-Path $PSScriptRoot 'SkillSharkTests.cs') (Join-Path $PSScriptRoot 'SkillMaelstromTests.cs') (Join-Path $PSScriptRoot 'SkillDamageTargetTests.cs') (Join-Path $PSScriptRoot 'SkillOcclusionTests.cs') (Join-Path $PSScriptRoot 'BossDamageTestStubs.cs') (Join-Path $PSScriptRoot 'BossSkillDamageTests.cs') (Join-Path $springProject 'src\CollisionDamageTarget.cs') (Join-Path $springProject 'src\AquabroSkills.cs') (Join-Path $springProject 'src\PixelMask.cs') (Join-Path $springProject 'src\SkillContactMask.cs') (Join-Path $springProject 'src\WaterProjectileBarrier.cs') (Join-Path $springGenerated 'SpringPixelMask.cs') (Join-Path $springGenerated 'SharkPixelMask.cs') (Join-Path $springGenerated 'MaelstromPixelMask.cs') (Join-Path $springGenerated 'TidalWavePixelMask.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Skill test compilation failed' }
    & $springExecutable (Join-Path $springGenerated 'skill-abyssal-spring.png') (Join-Path $springGenerated 'skill-tidal-wave.png') (Join-Path $springGenerated 'skill-shark-hunt.png') (Join-Path $springGenerated 'skill-maelstrom.png')
    if ($LASTEXITCODE -ne 0) { throw 'Skill spring regression failed' }
}
finally {
    if (Test-Path -LiteralPath $springExecutable) { Remove-Item -LiteralPath $springExecutable -Force }
}
