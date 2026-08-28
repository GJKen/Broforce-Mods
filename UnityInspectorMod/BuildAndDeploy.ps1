param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
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
    Where-Object { $_.BroforceManagedPath -or $_.UnityModManagerPath } |
    Select-Object -First 1
$broforceManagedPath = [string]$propertyGroup.BroforceManagedPath
$unityModManagerPath = [string]$propertyGroup.UnityModManagerPath
$inspectorModDependenciesPath = [string]$propertyGroup.InspectorModDependenciesPath

if ([string]::IsNullOrWhiteSpace($broforceManagedPath) -or
    [string]::IsNullOrWhiteSpace($unityModManagerPath)) {
    throw 'LocalBroforcePath.props must define BroforceManagedPath and UnityModManagerPath.'
}

$sourcePath = Join-Path $repoRoot 'src'
$buildPath = Join-Path $repoRoot ("bin\" + $Configuration)
$packagePath = Join-Path $repoRoot 'UnityInspectorMod'
$infoSourcePath = Join-Path $sourcePath '_ModContent\Info.json'

if ([string]::IsNullOrWhiteSpace($inspectorModDependenciesPath)) {
    $modsRoot = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods'
    $dependencyCandidates = @(
        $packagePath,
        (Join-Path $modsRoot 'Unity Inspector Mod'),
        (Join-Path $modsRoot 'alexneargarder-UnityInspectorMod'),
        (Join-Path $modsRoot 'Unknown-alexneargarder-UnityInspectorMod')
    )
    $inspectorModDependenciesPath = $dependencyCandidates |
        Where-Object {
            (Test-Path -LiteralPath (Join-Path $_ 'mcs.dll')) -and
            (Test-Path -LiteralPath (Join-Path $_ 'Newtonsoft.Json.dll'))
        } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($inspectorModDependenciesPath)) {
    throw 'Unable to locate mcs.dll and Newtonsoft.Json.dll. Set InspectorModDependenciesPath in LocalBroforcePath.props to an installed Unity Inspector Mod package.'
}

$mcsPath = Join-Path $inspectorModDependenciesPath 'mcs.dll'
$newtonsoftJsonPath = Join-Path $inspectorModDependenciesPath 'Newtonsoft.Json.dll'
Write-Host "Using Unity Inspector Mod runtime dependencies from $inspectorModDependenciesPath"
$compilerCandidates = @(
    (Join-Path $repoRoot '.tools\roslyn\tasks\net472\csc.exe'),
    (Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe')
)
$compiler = $compilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
$mscorlib = Join-Path $env:windir 'Microsoft.NET\Framework64\v2.0.50727\mscorlib.dll'
$system = Join-Path $env:windir 'Microsoft.NET\Framework64\v2.0.50727\System.dll'
$systemCoreCandidates = @(
    (Join-Path $env:windir 'assembly\GAC_MSIL\System.Core\3.5.0.0__b77a5c561934e089\System.Core.dll'),
    (Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\System.Core.dll')
)
$systemCore = $systemCoreCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

$requiredFiles = @(
    $compiler,
    $mscorlib,
    $system,
    $systemCore,
    $infoSourcePath,
    (Join-Path $unityModManagerPath '0Harmony.dll'),
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll'),
    $mcsPath,
    $newtonsoftJsonPath,
    (Join-Path $unityModManagerPath 'UnityModManager.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.ScreenCaptureModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.UI.dll')
)
foreach ($requiredFile in $requiredFiles) {
    if ([string]::IsNullOrWhiteSpace([string]$requiredFile) -or
        -not (Test-Path -LiteralPath $requiredFile)) {
        throw "Required build file does not exist: $requiredFile"
    }
}

$sourceFiles = @(Get-ChildItem -LiteralPath $sourcePath -Filter '*.cs' -File -Recurse | Sort-Object FullName |
    Select-Object -ExpandProperty FullName)
if ($sourceFiles.Count -eq 0) {
    throw "No C# source files found in $sourcePath"
}

New-Item -ItemType Directory -Force -Path $buildPath | Out-Null
New-Item -ItemType Directory -Force -Path $packagePath | Out-Null
$outputPath = Join-Path $buildPath 'Unity Inspector Mod.dll'

$references = @(
    $mscorlib,
    $system,
    $systemCore,
    (Join-Path $unityModManagerPath '0Harmony.dll'),
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.ScreenCaptureModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.UI.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll'),
    $mcsPath,
    $newtonsoftJsonPath
)
$compilerArguments = @(
    '/noconfig',
    '/nostdlib+',
    '/target:library',
    '/langversion:latest',
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

$packageFiles = @(
    @{ Source = $outputPath; Name = 'Unity Inspector Mod.dll' },
    @{ Source = $mcsPath; Name = 'mcs.dll' },
    @{ Source = $newtonsoftJsonPath; Name = 'Newtonsoft.Json.dll' },
    @{ Source = $infoSourcePath; Name = 'Info.json' }
)
foreach ($packageFile in $packageFiles) {
    $destinationPath = Join-Path $packagePath $packageFile.Name
    if ([IO.Path]::GetFullPath($packageFile.Source) -ne [IO.Path]::GetFullPath($destinationPath)) {
        Copy-Item -LiteralPath $packageFile.Source -Destination $destinationPath -Force
    }
}

Write-Host "Package ready: $packagePath"
if ($SkipDeploy) {
    Write-Host 'Deployment skipped.'
    exit 0
}

$modsRoot = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods'
$localModPath = Join-Path $modsRoot 'Unity Inspector Mod'
New-Item -ItemType Directory -Force -Path $localModPath | Out-Null
foreach ($packageFile in $packageFiles) {
    Copy-Item -LiteralPath $packageFile.Source -Destination (Join-Path $localModPath $packageFile.Name) -Force
}

Write-Host "Deployed to $localModPath"
Write-Host 'Build and deployment completed.'
