# Custom bro build script, modeled on CustomMapMultiplayer\BuildAndDeploy.ps1.
# Compiles src/*.cs with the .NET Framework 3.5 csc.exe (no Visual Studio / MSBuild needed)
# and deploys the dll into the bro's BroMaker_Storage folder.
#
# Expected repo layout:
#   <repoRoot>\
#     BuildBro.ps1
#     LocalBroforcePath.props          (copy from LocalBroforcePath.props.example, fill in paths)
#     src\*.cs                         (your bro's source files)
#     _Mod\                            (content folder: MyBro.mod.json, MyBro.json, sprites, sounds)
#
# Usage:  powershell -ExecutionPolicy Bypass -File BuildBro.ps1 [-SkipDeploy]

param(
    [switch]$SkipDeploy
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$propsPath = Join-Path $repoRoot 'LocalBroforcePath.props'

if (-not (Test-Path -LiteralPath $propsPath)) {
    throw "Missing LocalBroforcePath.props. Copy LocalBroforcePath.props.example first."
}

[xml]$propsXml = Get-Content -Encoding UTF8 -LiteralPath $propsPath
$propertyGroup = @($propsXml.Project.PropertyGroup) |
    Where-Object { $_.BroforceManagedPath -or $_.BroMakerLibPath } |
    Select-Object -First 1

$broforceManagedPath = [string]$propertyGroup.BroforceManagedPath
$unityModManagerPath = [string]$propertyGroup.UnityModManagerPath
$rocketLibPath = [string]$propertyGroup.RocketLibPath
$broMakerLibPath = [string]$propertyGroup.BroMakerLibPath
$newtonsoftJsonPath = [string]$propertyGroup.NewtonsoftJsonPath
$additionalReferencePaths = @(([string]$propertyGroup.AdditionalReferencePaths -split ';') |
    ForEach-Object { $_.Trim() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$broMakerStoragePath = [string]$propertyGroup.BroMakerStoragePath
$broStorageFolderName = [string]$propertyGroup.BroStorageFolderName

if ([string]::IsNullOrWhiteSpace($broforceManagedPath) -or
    [string]::IsNullOrWhiteSpace($unityModManagerPath) -or
    [string]::IsNullOrWhiteSpace($broMakerLibPath) -or
    [string]::IsNullOrWhiteSpace($broMakerStoragePath) -or
    [string]::IsNullOrWhiteSpace($broStorageFolderName)) {
    throw 'LocalBroforcePath.props must define BroforceManagedPath, UnityModManagerPath, BroMakerLibPath, BroMakerStoragePath and BroStorageFolderName.'
}

$modContentPath = Join-Path $repoRoot '_Mod'
$modFileCandidates = @(Get-ChildItem -LiteralPath $modContentPath -Filter '*.mod.json' -File)
if ($modFileCandidates.Count -ne 1) {
    throw "Expected exactly one .mod.json in $modContentPath, found $($modFileCandidates.Count)."
}
$modFilePath = $modFileCandidates[0].FullName
$modManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $modFilePath | ConvertFrom-Json
$assemblies = @($modManifest.Assemblies)
if ($assemblies.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$assemblies[0])) {
    throw 'Expected exactly one assembly path in the .mod.json Assemblies array.'
}
$assemblyRelativePath = [string]$assemblies[0]
if ([IO.Path]::GetExtension($assemblyRelativePath) -ne '.dll' -or
    [IO.Path]::IsPathRooted($assemblyRelativePath) -or
    $assemblyRelativePath -match '(^|[\\/])\.\.([\\/]|$)') {
    throw "Assemblies entry must be a relative DLL path inside _Mod: $assemblyRelativePath"
}
# BroMaker reads the assembly file names from the .mod.json's Assemblies array.
# The compiled dll is written to the path declared in Assemblies under _Mod.
$outputPath = Join-Path $modContentPath $assemblyRelativePath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null

$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
$mscorlib = Join-Path $env:windir 'Microsoft.NET\Framework64\v2.0.50727\mscorlib.dll'
$system = Join-Path $env:windir 'Microsoft.NET\Framework64\v2.0.50727\System.dll'
$systemCoreCandidates = @(
    (Join-Path $env:windir 'assembly\GAC_MSIL\System.Core\3.5.0.0__b77a5c561934e089\System.Core.dll'),
    (Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\System.Core.dll')
)
$systemCore = $systemCoreCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

$references = @(
    $mscorlib,
    $system,
    $systemCore,
    (Join-Path $broforceManagedPath 'UnityEngine.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.UI.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll'),
    (Join-Path $unityModManagerPath '0Harmony.dll'),
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll'),
    $broMakerLibPath
)
if (-not [string]::IsNullOrWhiteSpace($rocketLibPath)) {
    $references += $rocketLibPath
}
if (-not [string]::IsNullOrWhiteSpace($newtonsoftJsonPath)) {
    $references += $newtonsoftJsonPath
}
$references += $additionalReferencePaths

$requiredPaths = $references | Where-Object { $_ -like '*.dll' }
foreach ($requiredPath in $requiredPaths) {
    if ([string]::IsNullOrWhiteSpace([string]$requiredPath) -or
        -not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build path does not exist: $requiredPath"
    }
}

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' -File -Recurse |
    Sort-Object Name |
    Select-Object -ExpandProperty FullName)
if ($sourceFiles.Count -eq 0) {
    throw "No .cs files found under $repoRoot\src."
}

$compilerArguments = @(
    '/noconfig',
    '/nostdlib+',
    '/target:library',
    "/out:$outputPath",
    '/debug-',
    '/optimize+'
)
$compilerArguments += $references | ForEach-Object { "/reference:$_" }

Write-Host "Building $outputPath"
& $compiler $compilerArguments $sourceFiles
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}
Write-Host "Built $outputPath"

if ($SkipDeploy) {
    Write-Host 'Skipping BroMaker_Storage deployment because -SkipDeploy was specified.'
    return
}

$gameProcesses = @(Get-Process -Name 'Broforce' -ErrorAction SilentlyContinue)
if ($gameProcesses.Count -gt 0) {
    throw 'Broforce is running. Close the game before deploying the mod.'
}

# Deploy the whole _Mod content folder (dll + .mod.json + bro json + sprites/sounds).
$deployPath = Join-Path $broMakerStoragePath $broStorageFolderName
New-Item -ItemType Directory -Force -Path $deployPath | Out-Null
Get-ChildItem -LiteralPath $modContentPath -File -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($modContentPath.Length).TrimStart([char[]](92, 47))
    $destination = Join-Path $deployPath $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}
Write-Host "Deployed content to $deployPath"
Write-Host 'Build and deployment completed.'
