param([string]$AssemblyPath)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$releaseRoot = Join-Path $projectRoot 'Release'
$packageRoot = Join-Path $releaseRoot 'UMM\Mods\TestMod'
$outputPath = Join-Path $packageRoot 'TestMod.dll'
if (-not $AssemblyPath) { $AssemblyPath = $outputPath }
$assembly = Get-Item -LiteralPath $AssemblyPath -ErrorAction Stop
$infoPath = Join-Path $projectRoot 'Info.json'
$readmePath = Join-Path $releaseRoot 'README.md'
Get-Item -LiteralPath $infoPath, $readmePath -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
if ($assembly.FullName -ne $outputPath) {
    Copy-Item -LiteralPath $assembly.FullName -Destination $outputPath -Force
}
Copy-Item -LiteralPath $infoPath -Destination (Join-Path $packageRoot 'Info.json') -Force
$zipPath = Join-Path $releaseRoot 'TestMod.zip'
Compress-Archive -LiteralPath (Join-Path $releaseRoot 'UMM'), $readmePath -DestinationPath $zipPath -Force
Write-Output ('TestMod package directory: ' + $packageRoot)
Write-Output ('TestMod package archive: ' + $zipPath)
