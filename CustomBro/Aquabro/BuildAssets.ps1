param(
    [switch]$SkipDeploy,
    [string]$AsepritePath,
    [string]$ReferenceDirectory,
    [string[]]$DeployDirectories
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $projectRoot 'tools\BuildEnvironment.ps1')
$settings = Get-AquabroBuildSettings -ProjectRoot $projectRoot -AsepritePath $AsepritePath -ReferenceDirectory $ReferenceDirectory -DeployDirectories $DeployDirectories
$aseprite = $settings.AsepritePath
$sourceRoot = Join-Path $projectRoot 'aseprite-source'
$outputRoot = Join-Path $projectRoot '_Mod'
$deployRoots = $settings.DeployDirectories
if (-not $SkipDeploy -and $deployRoots.Count -eq 0) {
    throw '未配置部署目录。仅构建请使用 -SkipDeploy，部署请设置 -DeployDirectories 或 LocalBuild.json。'
}
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Aquabro-assets-' + [Guid]::NewGuid().ToString('N'))
$sourceDirs = Get-ChildItem -LiteralPath $sourceRoot -Directory
$actionRoot = ($sourceDirs | Where-Object {
    $_.Name -like '01_*' -and
    (Test-Path -LiteralPath (Join-Path $_.FullName 'haiwang_trident_charge-release-blue-flow-v8.aseprite'))
} | Select-Object -First 1).FullName
$skillRoot = Join-Path $sourceRoot '03_特殊技能'
if (-not (Test-Path -LiteralPath $skillRoot -PathType Container)) { throw "Skill source directory not found: $skillRoot" }
$terrainRoot = ($sourceDirs | Where-Object { $_.Name -like '02_*' } | Select-Object -First 1).FullName
$gibRoot = ($sourceDirs | Where-Object { $_.Name -like '04_*' } | Select-Object -First 1).FullName

if (-not (Test-Path -LiteralPath $aseprite)) {
    throw "Aseprite not found: $aseprite"
}

New-Item -ItemType Directory -Force -Path $outputRoot,$tempRoot | Out-Null

function Export-Sheet([string]$source, [string]$name) {
    $sheet = Join-Path $tempRoot ($name + '.png')
    & $aseprite -b $source --sheet $sheet 2>&1 | Out-Null
    if (-not (Test-Path -LiteralPath $sheet)) { throw "Aseprite export failed: $source" }
    return [System.Drawing.Bitmap]::new($sheet)
}

try {
    $atlasScript = Join-Path $projectRoot 'tools\build_runtime_atlases.lua'
    $atlasArgs = @(
        '-b'
        '--script-param'
        "actions=$actionRoot"
        '--script-param'
        "skills=$skillRoot"
        '--script-param'
        "terrain=$terrainRoot"
        '--script-param'
        "gibs=$gibRoot"
        '--script-param'
        "output=$tempRoot"
        '--script-param'
        "layout=$(Join-Path $tempRoot 'TridentAtlas.cs')"
        '--script'
        $atlasScript
    )
    & $aseprite @atlasArgs | Out-Host
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw ('Aseprite atlas build failed with exit code ' + $LASTEXITCODE)
    }

    # Aseprite can return just before the PNG encoder has finished flushing the
    # larger atlas. Wait for both runtime atlases to become stable before copy.
    foreach ($name in @('sprite.png', 'gunSprite.png')) {
        $atlasPath = Join-Path $tempRoot $name
        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            if (Test-Path -LiteralPath $atlasPath) {
                $firstLength = (Get-Item -LiteralPath $atlasPath).Length
                if ($firstLength -gt 0) {
                    Start-Sleep -Milliseconds 100
                    $secondLength = (Get-Item -LiteralPath $atlasPath).Length
                    if ($firstLength -eq $secondLength) {
                        $ready = $true
                        break
                    }
                }
            }
            Start-Sleep -Milliseconds 100
        }
        if (-not $ready) {
            throw "Atlas output did not become stable: $atlasPath"
        }
    }

    $projectileSheet = Export-Sheet (Join-Path $actionRoot 'haiwang_trident_projectile.aseprite') 'projectile'
    try {
        $projectileSheet.Save((Join-Path $tempRoot 'trident.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $projectileSheet.Dispose()
    }

    foreach ($artExporter in @('export_avatar.lua','export_skill_icons.lua')) {
        $artProcess = Start-Process -FilePath $aseprite -ArgumentList @('-b','--script-param',('"root=' + $projectRoot + '"'),'--script-param',('"output=' + $tempRoot + '"'),'--script',('"' + (Join-Path $projectRoot ('tools\' + $artExporter)) + '"')) -WindowStyle Hidden -Wait -PassThru
        if ($artProcess.ExitCode -ne 0) { throw "HUD asset export failed: $artExporter" }
    }

    & $aseprite -b --script-param "root=$projectRoot" --script-param "output=$tempRoot" --script (Join-Path $projectRoot 'tools\export_water_effects.lua') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Water effect export failed' }

    & $aseprite -b --script-param "root=$projectRoot" --script-param "output=$tempRoot" --script (Join-Path $projectRoot 'tools\export_skill_effects.lua') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Skill effect export failed' }

    & $aseprite -b --script-param "root=$projectRoot" --script-param "output=$tempRoot" --script (Join-Path $projectRoot 'tools\export_acid_skills.lua') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Acid skill export failed' }

    # 图集和选帧表必须与 DLL 配套；全部生成和编译成功后再替换正式产物。
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    $response = New-AquabroCompilerResponse -ProjectRoot $projectRoot -ReferenceDirectory $settings.ReferenceDirectory -OutputDirectory $tempRoot -GeneratedDirectory $tempRoot
    $responsePath = Join-Path $tempRoot 'compile.rsp'
    [System.IO.File]::WriteAllText($responsePath, $response, [System.Text.UTF8Encoding]::new($true))
    & $compiler /nologo "@$responsePath"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $tempRoot 'Aquabro.dll'))) {
        throw 'Aquabro DLL compilation failed'
    }
    Copy-Item -LiteralPath (Join-Path $tempRoot 'TridentAtlas.cs') -Destination (Join-Path $projectRoot 'src\TridentAtlas.cs') -Force
    Copy-Item -LiteralPath (Join-Path $tempRoot 'SpringPixelMask.cs') -Destination (Join-Path $projectRoot 'src\SpringPixelMask.cs') -Force
    Copy-Item -LiteralPath (Join-Path $tempRoot 'SharkPixelMask.cs') -Destination (Join-Path $projectRoot 'src\SharkPixelMask.cs') -Force
    Copy-Item -LiteralPath (Join-Path $tempRoot 'MaelstromPixelMask.cs') -Destination (Join-Path $projectRoot 'src\MaelstromPixelMask.cs') -Force
    Copy-Item -LiteralPath (Join-Path $tempRoot 'TidalWavePixelMask.cs') -Destination (Join-Path $projectRoot 'src\TidalWavePixelMask.cs') -Force
    Copy-Item -LiteralPath (Join-Path $tempRoot 'ChargedWaterPixelMask.cs') -Destination (Join-Path $projectRoot 'src\ChargedWaterPixelMask.cs') -Force
    foreach ($name in @('Aquabro.dll','sprite.png','gunSprite.png','trident.png','avatar.png','special.png','special-tidal-wave.png','special-shark-hunt.png','special-maelstrom.png','special-abyssal-spring.png','water-hit-small.png','water-rocket-explosion.png','skill-tidal-wave.png','skill-shark-hunt.png','skill-shark-hunt-body.png','skill-shark-hunt-water-back.png','skill-shark-hunt-water-front.png','skill-maelstrom.png','skill-abyssal-spring.png','skill-tidal-wave-acid-1.png','skill-tidal-wave-acid-2.png','skill-maelstrom-acid-1.png','skill-maelstrom-acid-2.png','skill-abyssal-spring-acid-1.png','skill-abyssal-spring-acid-2.png')) {
        Copy-Item -LiteralPath (Join-Path $tempRoot $name) -Destination (Join-Path $outputRoot $name) -Force
    }

    foreach ($deployRoot in $(if ($SkipDeploy) { @() } else { $deployRoots })) {
        if (Test-Path -LiteralPath $deployRoot) {
            foreach ($name in @('Aquabro.json','Aquabro.dll','sprite.png','gunSprite.png','trident.png','avatar.png','special.png','special-tidal-wave.png','special-shark-hunt.png','special-maelstrom.png','special-abyssal-spring.png','water-hit-small.png','water-rocket-explosion.png','skill-tidal-wave.png','skill-shark-hunt.png','skill-shark-hunt-body.png','skill-shark-hunt-water-back.png','skill-shark-hunt-water-front.png','skill-maelstrom.png','skill-abyssal-spring.png','skill-tidal-wave-acid-1.png','skill-tidal-wave-acid-2.png','skill-maelstrom-acid-1.png','skill-maelstrom-acid-2.png','skill-abyssal-spring-acid-1.png','skill-abyssal-spring-acid-2.png')) {
                $source = Join-Path $outputRoot $name
                if (Test-Path -LiteralPath $source) {
                    $destination = Join-Path $deployRoot $name
                    Copy-Item -LiteralPath $source -Destination $destination -Force
                    $sourceLength = (Get-Item -LiteralPath $source).Length
                    $destinationLength = (Get-Item -LiteralPath $destination).Length
                    if ($destinationLength -ne $sourceLength) {
                        [System.IO.File]::Copy($source, $destination, $true)
                        $destinationLength = (Get-Item -LiteralPath $destination).Length
                    }
                    if ($destinationLength -ne $sourceLength -or
                        (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
                        throw "Deployment copy failed: $destination"
                    }
                }
            }
            $cache = Join-Path $deployRoot 'Aquabro.dll.cache'
            if (Test-Path -LiteralPath $cache) {
                Remove-Item -LiteralPath $cache -Force
            }
        }
    }
    Write-Output ('Aquabro build completed; deployment=' + (-not $SkipDeploy))
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)
        $expectedTempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $resolvedTempRoot.StartsWith($expectedTempParent, [System.StringComparison]::OrdinalIgnoreCase) -or
            [System.IO.Path]::GetFileName($resolvedTempRoot) -notlike 'Aquabro-assets-*') {
            throw "Unexpected temporary build directory: $resolvedTempRoot"
        }
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

