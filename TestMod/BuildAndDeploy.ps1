param(
    [switch]$SkipDeploy,
    [string]$RemoteModPath = '\\5700G\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\TestMod'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$propsPath = Join-Path $projectRoot 'LocalBroforcePath.props'
[xml]$propsXml = Get-Content -Encoding UTF8 -LiteralPath $propsPath
if ($propsXml.Project.Import) {
    $propsPath = Join-Path $projectRoot ([string]$propsXml.Project.Import.Project)
    [xml]$propsXml = Get-Content -Encoding UTF8 -LiteralPath $propsPath
}
$managedPath = [string]$propsXml.Project.PropertyGroup.BroforceManagedPath
$ummPath = [string]$propsXml.Project.PropertyGroup.UnityModManagerPath
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
$frameworkPath = Join-Path $env:windir 'Microsoft.NET\Framework64\v2.0.50727'
$systemCore = Join-Path $env:windir 'assembly\GAC_MSIL\System.Core\3.5.0.0__b77a5c561934e089\System.Core.dll'
$packagePath = Join-Path $projectRoot 'Release\UMM\Mods\TestMod'
$outputPath = Join-Path $packagePath 'TestMod.dll'
$references = @(
    (Join-Path $frameworkPath 'mscorlib.dll'),
    (Join-Path $frameworkPath 'System.dll'),
    $systemCore,
    (Join-Path $managedPath 'UnityEngine.dll'),
    (Join-Path $managedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $managedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managedPath 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managedPath 'Assembly-CSharp.dll'),
    (Join-Path $ummPath 'UnityModManager.dll'),
    (Join-Path $ummPath '0Harmony.dll')
)
foreach ($requiredPath in @($compiler) + $references) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Missing build dependency: $requiredPath" }
}
New-Item -ItemType Directory -Force -Path $packagePath | Out-Null
$arguments = @('/noconfig', '/nostdlib+', '/target:library', '/debug-', '/optimize+', "/out:$outputPath")
$arguments += $references | ForEach-Object { "/reference:$_" }
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File |
    Select-Object -ExpandProperty FullName
& $compiler $arguments $sourceFiles
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $projectRoot 'Info.json') -Destination (Join-Path $packagePath 'Info.json') -Force

if (-not $SkipDeploy) {
    $localModPath = Join-Path (Split-Path -Parent $ummPath) 'Mods\TestMod'
    foreach ($destination in @($localModPath, $RemoteModPath) | Select-Object -Unique) {
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        Copy-Item -LiteralPath $outputPath -Destination (Join-Path $destination 'TestMod.dll') -Force
        Copy-Item -LiteralPath (Join-Path $packagePath 'Info.json') -Destination (Join-Path $destination 'Info.json') -Force
        Get-Item -LiteralPath (Join-Path $destination 'TestMod.dll') | Select-Object FullName,Length,LastWriteTime
    }
}
