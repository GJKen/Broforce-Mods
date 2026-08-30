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
$testDeployModPath = [string]$propertyGroup.TestDeployModPath
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
    (Join-Path $broforceManagedPath 'UnityEngine.TextRenderingModule.dll'),
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

$packageModPath = Join-Path $repoRoot 'CustomMapMultiplayer'
$packageInfoPath = Join-Path $packageModPath 'Info.json'
if (-not (Test-Path -LiteralPath $packageInfoPath)) {
    throw "Missing copyable package metadata: $packageInfoPath"
}

New-Item -ItemType Directory -Force -Path $packageModPath | Out-Null
$outputPath = Join-Path $packageModPath 'CustomMapMultiplayer.dll'
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
    (Join-Path $broforceManagedPath 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll'),
    (Join-Path $unityModManagerPath '0Harmony.dll'),
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll')
)

function Get-Sha256Hex([string]$path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToUpperInvariant()
}

$manifestLines = New-Object System.Collections.Generic.List[string]
foreach ($sourceFile in $sourceFiles) {
    $sourceItem = Get-Item -LiteralPath $sourceFile
    $manifestLines.Add(
        ('source|{0}|{1}|{2}' -f $sourceItem.Name, $sourceItem.Length, (Get-Sha256Hex $sourceFile)))
}

$buildMetadataPath = Join-Path ([IO.Path]::GetTempPath()) (
    'CustomMapMultiplayer.BuildMetadata.' + [Guid]::NewGuid().ToString('N') + '.cs')

try {
    $referencePaths = @($references)
    foreach ($referencePath in $referencePaths) {
        $referenceItem = Get-Item -LiteralPath $referencePath
        $manifestLines.Add(
            ('reference|{0}|{1}|{2}' -f $referenceItem.Name, $referenceItem.Length, (Get-Sha256Hex $referencePath)))
    }

    $manifestLines.Add('compiler|.NET Framework 3.5 csc|')
    $manifestLines.Add('configuration|' + $Configuration + '|')
    $manifest = $manifestLines -join "`n"
    $buildHashBytes = [Text.Encoding]::UTF8.GetBytes($manifest)
    $buildHash = -join ([Security.Cryptography.SHA256]::Create().ComputeHash($buildHashBytes) |
        ForEach-Object { $_.ToString('x2') })
    Write-Host "Build hash: $buildHash"

    $metadataSource = @"
namespace CustomMapMultiplayer
{
    internal static partial class BuildMetadata
    {
        static partial void SetBuildHash(ref string value)
        {
            value = "$buildHash";
        }
    }
}
"@
    [IO.File]::WriteAllText(
        $buildMetadataPath,
        $metadataSource,
        (New-Object System.Text.UTF8Encoding($false)))
    $sourceFiles += $buildMetadataPath

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

$localModPath = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods\GJKen-CustomMapMultiplayer'
Write-Host "Updated copyable package $outputPath"

if ($SkipDeploy) {
    Write-Host 'Skipping UMM deployment because -SkipDeploy was specified.'
}
else {
    $deploymentPaths = @($localModPath)
    if (-not [string]::IsNullOrWhiteSpace($testDeployModPath)) {
        $deploymentPaths += $testDeployModPath.Trim()
    }
    $deploymentPaths = @($deploymentPaths | Select-Object -Unique)
    foreach ($deploymentPath in $deploymentPaths) {
        New-Item -ItemType Directory -Force -Path $deploymentPath | Out-Null
        $destinationPath = Join-Path $deploymentPath 'CustomMapMultiplayer.dll'
        Copy-Item -LiteralPath $outputPath -Destination $destinationPath -Force
        Write-Host "Deployed $destinationPath"

        $infoDestinationPath = Join-Path $deploymentPath 'Info.json'
        Copy-Item -LiteralPath $infoSourcePath -Destination $infoDestinationPath -Force
        Write-Host "Updated $infoDestinationPath from modinfo.json"
    }
}

Write-Host 'Build and deployment completed.'
}
finally {
    if (Test-Path -LiteralPath $buildMetadataPath) {
        Remove-Item -LiteralPath $buildMetadataPath -Force -ErrorAction SilentlyContinue
    }
}
