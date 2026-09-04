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
$rocketLibPath = [string]$propertyGroup.RocketLibPath
$testDeployModPath = [string]$propertyGroup.TestDeployModPath
$infoSourcePath = Join-Path $repoRoot 'modinfo.json'
if ([string]::IsNullOrWhiteSpace($broforceManagedPath) -or
    [string]::IsNullOrWhiteSpace($unityModManagerPath) -or
    [string]::IsNullOrWhiteSpace($rocketLibPath)) {
    throw 'LocalBroforcePath.props must define BroforceManagedPath, UnityModManagerPath and RocketLibPath.'
}
if (-not (Test-Path -LiteralPath $infoSourcePath)) {
    throw "Missing UMM metadata template: $infoSourcePath"
}
$infoMetadata = Get-Content -Encoding UTF8 -Raw -LiteralPath $infoSourcePath | ConvertFrom-Json
$modVersion = [string]$infoMetadata.Version
if ($modVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "modinfo.json Version must use major.minor.patch format: $modVersion"
}
$assemblyVersion = $modVersion + '.0'

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
    $rocketLibPath,
    (Join-Path $broforceManagedPath 'Assembly-CSharp.dll')
)
foreach ($requiredPath in $requiredPaths) {
    if ([string]::IsNullOrWhiteSpace([string]$requiredPath) -or
        -not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build path does not exist: $requiredPath"
    }
}

$releasePath = Join-Path $repoRoot 'Release'
$packageModPath = Join-Path $releasePath 'UMM\Mods\CustomMapMultiplayer'
$packageInfoPath = Join-Path $packageModPath 'Info.json'
New-Item -ItemType Directory -Force -Path $packageModPath | Out-Null
Copy-Item -LiteralPath $infoSourcePath -Destination $packageInfoPath -Force
Write-Host "Updated package metadata $packageInfoPath from modinfo.json"

$outputPath = Join-Path $packageModPath 'CustomMapMultiplayer.dll'
$packageZipPath = Join-Path $releasePath 'CustomMapMultiplayer.zip'
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
    $rocketLibPath,
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
[assembly: System.Reflection.AssemblyVersion("$assemblyVersion")]
[assembly: System.Reflection.AssemblyFileVersion("$assemblyVersion")]
[assembly: System.Reflection.AssemblyInformationalVersion("$modVersion")]

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

$packageFiles = @(
    (Join-Path $releasePath 'manifest.json'),
    (Join-Path $releasePath 'README.md'),
    (Join-Path $releasePath 'icon.png'),
    (Join-Path $packageModPath 'CustomMapMultiplayer.dll'),
    (Join-Path $packageModPath 'Info.json')
)
foreach ($packageFile in $packageFiles) {
    if (-not (Test-Path -LiteralPath $packageFile)) {
        throw "Missing package file: $packageFile"
    }
}
$archivePaths = @(
    (Join-Path $releasePath 'manifest.json'),
    (Join-Path $releasePath 'README.md'),
    (Join-Path $releasePath 'icon.png'),
    (Join-Path $releasePath 'UMM')
)
$packageZipTempPath = Join-Path $releasePath (
    'CustomMapMultiplayer.' + [Guid]::NewGuid().ToString('N') + '.tmp.zip')
Compress-Archive -Path $archivePaths -DestinationPath $packageZipTempPath
Move-Item -LiteralPath $packageZipTempPath -Destination $packageZipPath -Force
Write-Host "Created package $packageZipPath"

$localModPath = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods\GJKen-CustomMapMultiplayer\CustomMapMultiplayer'
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
    if ($packageZipTempPath -and (Test-Path -LiteralPath $packageZipTempPath)) {
        Remove-Item -LiteralPath $packageZipTempPath -Force -ErrorAction SilentlyContinue
    }
}
