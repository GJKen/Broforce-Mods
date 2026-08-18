param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
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
$infoSourcePath = Join-Path $repoRoot 'modinfo.json'
if ([string]::IsNullOrWhiteSpace($broforceManagedPath) -or
    [string]::IsNullOrWhiteSpace($unityModManagerPath)) {
    throw 'LocalBroforcePath.props must define BroforceManagedPath and UnityModManagerPath.'
}
if (-not (Test-Path -LiteralPath $infoSourcePath)) {
    throw "Missing UMM metadata template: $infoSourcePath"
}

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

$requiredPaths = @(
    $compiler,
    $mscorlib,
    $system,
    $systemCore,
    (Join-Path $broforceManagedPath 'UnityEngine.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll'),
    (Join-Path $unityModManagerPath '0Harmony.dll'),
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll')
)
foreach ($requiredPath in $requiredPaths) {
    if ([string]::IsNullOrWhiteSpace([string]$requiredPath) -or
        -not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build path does not exist: $requiredPath"
    }
}

$packageModPath = Join-Path $repoRoot 'BroforceOnlineDiagnostics'
$packageInfoPath = Join-Path $packageModPath 'Info.json'
if (-not (Test-Path -LiteralPath $packageInfoPath)) {
    throw "Missing copyable package metadata: $packageInfoPath"
}

New-Item -ItemType Directory -Force -Path $packageModPath | Out-Null
$outputPath = Join-Path $packageModPath 'BroforceOnlineDiagnostics.dll'
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' -File |
    Sort-Object Name |
    Select-Object -ExpandProperty FullName)

$references = @(
    $mscorlib,
    $system,
    $systemCore,
    (Join-Path $broforceManagedPath 'UnityEngine.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll'),
    (Join-Path $unityModManagerPath '0Harmony.dll'),
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll')
)
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

$localModPath = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods\BroforceOnlineDiagnostics'
$networkModPath = '\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\BroforceOnlineDiagnostics'
Write-Host "Updated copyable package $outputPath"

$deploymentPaths = @($localModPath, $networkModPath)
foreach ($deploymentPath in $deploymentPaths) {
    New-Item -ItemType Directory -Force -Path $deploymentPath | Out-Null
    $destinationPath = Join-Path $deploymentPath 'BroforceOnlineDiagnostics.dll'
    Copy-Item -LiteralPath $outputPath -Destination $destinationPath -Force
    Write-Host "Deployed $destinationPath"

    $infoDestinationPath = Join-Path $deploymentPath 'Info.json'
    if (-not (Test-Path -LiteralPath $infoDestinationPath)) {
        Copy-Item -LiteralPath $infoSourcePath -Destination $infoDestinationPath
        Write-Host "Initialized $infoDestinationPath from modinfo.json"
    }
}

Write-Host 'Build and deployment completed.'
