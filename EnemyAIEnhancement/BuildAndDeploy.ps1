param([switch]$SkipDeploy)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$propsPath = Join-Path $projectRoot 'LocalBroforcePath.props'
if (-not (Test-Path -LiteralPath $propsPath)) {
    throw 'Copy LocalBroforcePath.props.example to LocalBroforcePath.props and set the game paths.'
}
[xml]$props = Get-Content -Raw -Encoding UTF8 -LiteralPath $propsPath
$managedPath = [string]$props.Project.PropertyGroup.BroforceManagedPath
$ummPath = [string]$props.Project.PropertyGroup.UnityModManagerPath
$testModsPath = [string]$props.Project.PropertyGroup.TestDeployModsPath
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
$frameworkPath = Join-Path $env:windir 'Microsoft.NET\Framework64\v2.0.50727'
$references = @(
    (Join-Path $frameworkPath 'mscorlib.dll'),
    (Join-Path $frameworkPath 'System.dll'),
    (Join-Path $env:windir 'assembly\GAC_MSIL\System.Core\3.5.0.0__b77a5c561934e089\System.Core.dll'),
    (Join-Path $managedPath 'UnityEngine.dll'),
    (Join-Path $managedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $managedPath 'UnityEngine.PhysicsModule.dll'),
    (Join-Path $managedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managedPath 'Assembly-CSharp.dll'),
    (Join-Path $ummPath 'UnityModManager.dll'),
    (Join-Path $ummPath '0Harmony.dll')
)
foreach ($requiredPath in (@($compiler) + $references)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Missing build dependency: $requiredPath" }
}
$packagePath = Join-Path $projectRoot 'Release\UMM\Mods\EnemyAIEnhancement'
$outputPath = Join-Path $packagePath 'EnemyAIEnhancement.dll'
$arguments = @('/nologo', '/utf8output', '/noconfig', '/nostdlib+', '/target:library', '/optimize+', '/debug-', "/out:$outputPath")
$arguments += $references | ForEach-Object { "/reference:$_" }
$sourcePaths = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File |
    Sort-Object Name | Select-Object -ExpandProperty FullName)
& $compiler $arguments $sourcePaths
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Write-Host "Built $outputPath"
& (Join-Path $projectRoot 'Package.ps1')
if ($SkipDeploy) { return }

# A loaded Unity assembly cannot be replaced safely. Stop the local game and verify exit.
$gameProcesses = @(Get-Process -Name 'Broforce', 'Broforce_beta' -ErrorAction SilentlyContinue)
foreach ($gameProcess in $gameProcesses) {
    Stop-Process -Id $gameProcess.Id -Force
    Wait-Process -Id $gameProcess.Id -ErrorAction SilentlyContinue
}
if (Get-Process -Name 'Broforce', 'Broforce_beta' -ErrorAction SilentlyContinue) {
    throw 'Broforce is still running; deployment cancelled.'
}
$modsPaths = @((Join-Path (Split-Path -Parent $ummPath) 'Mods'))
if (-not [string]::IsNullOrWhiteSpace($testModsPath)) { $modsPaths += $testModsPath }
# Stop the remote game separately before using TestDeployModsPath.
foreach ($modsPath in ($modsPaths | Select-Object -Unique)) {
    $destination = Join-Path $modsPath 'GJKen-EnemyAIEnhancement\EnemyAIEnhancement'
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -LiteralPath $outputPath -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $packagePath 'Info.json') -Destination $destination -Force
    Write-Host "Deployed $destination"
}
