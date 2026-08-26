param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$propsPath = Join-Path $projectRoot 'LocalBroforcePath.props'

if (-not (Test-Path -LiteralPath $propsPath)) {
    throw 'Missing LocalBroforcePath.props. Copy LocalBroforcePath.props.example first.'
}

[xml]$propsXml = Get-Content -Encoding UTF8 -LiteralPath $propsPath
$propertyGroup = @($propsXml.Project.PropertyGroup) |
    Where-Object { $_.BroforceManagedPath -or $_.UnityModManagerPath } |
    Select-Object -First 1
$broforceManagedPath = [string]$propertyGroup.BroforceManagedPath
$unityModManagerPath = [string]$propertyGroup.UnityModManagerPath

if ([string]::IsNullOrWhiteSpace($broforceManagedPath) -or
    [string]::IsNullOrWhiteSpace($unityModManagerPath)) {
    throw 'LocalBroforcePath.props must define BroforceManagedPath and UnityModManagerPath.'
}

$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
$frameworkRoot = Join-Path $env:windir 'Microsoft.NET\Framework64\v2.0.50727'
$systemCoreCandidates = @(
    (Join-Path $env:windir 'assembly\GAC_MSIL\System.Core\3.5.0.0__b77a5c561934e089\System.Core.dll'),
    (Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\System.Core.dll')
)
$systemCore = $systemCoreCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
$references = @(
    (Join-Path $frameworkRoot 'mscorlib.dll'),
    (Join-Path $frameworkRoot 'System.dll'),
    $systemCore,
    (Join-Path $broforceManagedPath 'UnityEngine.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll'),
    (Join-Path $unityModManagerPath '0Harmony.dll'),
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll')
)
$requiredPaths = @($compiler) + $references
foreach ($requiredPath in $requiredPaths) {
    if ([string]::IsNullOrWhiteSpace([string]$requiredPath) -or
        -not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build path does not exist: $requiredPath"
    }
}

$packagePath = Join-Path $projectRoot 'BroforceBugFix'
$infoSourcePath = Join-Path $projectRoot 'modinfo.json'
$packageInfoPath = Join-Path $packagePath 'Info.json'
$outputPath = Join-Path $packagePath 'BroforceBugFix.dll'
New-Item -ItemType Directory -Force -Path $packagePath | Out-Null
Copy-Item -LiteralPath $infoSourcePath -Destination $packageInfoPath -Force

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File |
    Sort-Object Name |
    Select-Object -ExpandProperty FullName)
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

$localModPath = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods\GJKen-BroforceBugFix'
$networkModPath = '\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\GJKen-BroforceBugFix'
foreach ($deploymentPath in @($localModPath, $networkModPath)) {
    New-Item -ItemType Directory -Force -Path $deploymentPath | Out-Null
    Copy-Item -LiteralPath $outputPath -Destination (Join-Path $deploymentPath 'BroforceBugFix.dll') -Force
    Copy-Item -LiteralPath $infoSourcePath -Destination (Join-Path $deploymentPath 'Info.json') -Force
    Write-Host "Deployed $deploymentPath"
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outputPath).Hash
Write-Host "DLL SHA-256: $hash"
Write-Host 'Build and deployment completed.'
