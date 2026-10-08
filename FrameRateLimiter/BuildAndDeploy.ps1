param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipDeploy
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
$testDeployModPath = [string]$propertyGroup.TestDeployModPath
$releasePath = Join-Path $projectRoot 'Release'
$packageModPath = Join-Path $releasePath 'UMM\Mods\FrameRateLimiter'
$packageInfoPath = Join-Path $packageModPath 'Info.json'

if ([string]::IsNullOrWhiteSpace($broforceManagedPath) -or
    [string]::IsNullOrWhiteSpace($unityModManagerPath)) {
    throw 'LocalBroforcePath.props must define BroforceManagedPath and UnityModManagerPath.'
}
if (-not (Test-Path -LiteralPath $packageInfoPath)) {
    throw "Missing UMM metadata: $packageInfoPath"
}

$infoMetadata = Get-Content -Encoding UTF8 -Raw -LiteralPath $packageInfoPath | ConvertFrom-Json
$modVersion = [string]$infoMetadata.Version
if ($modVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Info.json Version must use major.minor.patch format: $modVersion"
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
    (Join-Path $broforceManagedPath 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll')
)

$requiredPaths = @($compiler) + $references
foreach ($requiredPath in $requiredPaths) {
    if ([string]::IsNullOrWhiteSpace([string]$requiredPath) -or
        -not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build path does not exist: $requiredPath"
    }
}

New-Item -ItemType Directory -Force -Path $packageModPath | Out-Null
$outputPath = Join-Path $packageModPath 'FrameRateLimiter.dll'
$packageZipPath = Join-Path $releasePath 'FrameRateLimiter.zip'
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

$packageFiles = @(
    (Join-Path $releasePath 'manifest.json'),
    (Join-Path $releasePath 'README.md'),
    (Join-Path $packageModPath 'FrameRateLimiter.dll'),
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
    (Join-Path $releasePath 'UMM')
)
$packageZipTempPath = Join-Path $releasePath (
    'FrameRateLimiter.' + [Guid]::NewGuid().ToString('N') + '.tmp.zip')
Compress-Archive -Path $archivePaths -DestinationPath $packageZipTempPath
Move-Item -LiteralPath $packageZipTempPath -Destination $packageZipPath -Force
Write-Host "Created package $packageZipPath"

$localModPath = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods\GJKen-FrameRateLimiter\FrameRateLimiter'
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
        $destinationPath = Join-Path $deploymentPath 'FrameRateLimiter.dll'
        Copy-Item -LiteralPath $outputPath -Destination $destinationPath -Force
        Copy-Item -LiteralPath $packageInfoPath -Destination (Join-Path $deploymentPath 'Info.json') -Force
        Write-Host "Deployed $destinationPath"
    }
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outputPath).Hash.ToUpperInvariant()
Write-Host "DLL SHA-256: $hash"
Write-Host 'Build and deployment completed.'
