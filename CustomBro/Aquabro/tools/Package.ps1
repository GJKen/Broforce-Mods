param([string]$AssemblyPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$contentRoot = Join-Path $projectRoot '_Mod'
$releaseRoot = Join-Path $projectRoot 'Release'
$packageRoot = Join-Path $releaseRoot 'UMM\BroMaker_Storage\Aquabro'
if (-not $AssemblyPath) { $AssemblyPath = Join-Path $contentRoot 'Aquabro.dll' }
$packageSources = @($AssemblyPath) + @(Get-ChildItem -LiteralPath $contentRoot -File |
    Where-Object { $_.Extension -in @('.json', '.png') } |
    Select-Object -ExpandProperty FullName)
# Resolve every source before replacing the generated package files.
$packageSources = @(Get-Item -LiteralPath $packageSources -ErrorAction Stop)
$readmePath = Join-Path $releaseRoot 'README.md'
Get-Item -LiteralPath $readmePath -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
Get-ChildItem -LiteralPath $packageRoot -File | Remove-Item -Force
Copy-Item -LiteralPath $packageSources.FullName -Destination $packageRoot -Force
$zipPath = Join-Path $releaseRoot 'Aquabro.zip'
Compress-Archive -LiteralPath (Join-Path $releaseRoot 'UMM'), $readmePath -DestinationPath $zipPath -Force
Write-Output ('Aquabro package directory: ' + $packageRoot)
Write-Output ('Aquabro package archive: ' + $zipPath)
