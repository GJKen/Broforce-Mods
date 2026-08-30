param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,
    [switch]$StageForNextLaunch,
    [switch]$SkipDeploy
)

$ErrorActionPreference = 'Stop'
$modRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $modRoot
$propsPath = Join-Path $projectRoot 'LocalBroforcePath.props'

if (-not (Test-Path -LiteralPath $propsPath)) {
    throw "Missing LocalBroforcePath.props: $propsPath"
}
if (-not (Test-Path -LiteralPath $CandidatePath)) {
    throw "Candidate DLL does not exist: $CandidatePath"
}
if ($StageForNextLaunch -and $SkipDeploy) {
    throw '-StageForNextLaunch requires UMM deployment and cannot be combined with -SkipDeploy.'
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
    (Join-Path $unityModManagerPath 'UnityModManager.dll')
)
foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build path does not exist: $requiredPath"
    }
}

$packagePath = $modRoot
$outputPath = Join-Path $packagePath 'AssemblyCSharpChineseInputSwitch.dll'
$helperDirectory = Join-Path $packagePath 'tools'
$helperOutputPath = Join-Path $helperDirectory 'AssemblyCSharpChineseInputSwitch.Helper.exe'
$payloadDirectory = Join-Path $packagePath 'payload'
$payloadPath = Join-Path $payloadDirectory 'Assembly-CSharp-ChineseInput.candidate.dll'
$infoSourcePath = Join-Path $packagePath 'modinfo.json'
$infoOutputPath = Join-Path $packagePath 'Info.json'

New-Item -ItemType Directory -Force -Path $helperDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $payloadDirectory | Out-Null
Copy-Item -LiteralPath $CandidatePath -Destination $payloadPath -Force
Copy-Item -LiteralPath $infoSourcePath -Destination $infoOutputPath -Force

$pluginSourcePath = Join-Path $packagePath 'src\Plugin.cs'
$pluginReferences = @(
    $mscorlib,
    $system,
    $systemCore,
    (Join-Path $broforceManagedPath 'UnityEngine.dll'),
    (Join-Path $broforceManagedPath 'UnityEngine.CoreModule.dll'),
    (Join-Path $unityModManagerPath 'UnityModManager.dll')
)
$pluginArguments = @(
    '/noconfig',
    '/nostdlib+',
    '/target:library',
    "/out:$outputPath",
    '/debug-',
    '/optimize+'
)
$pluginArguments += $pluginReferences | ForEach-Object { "/reference:$_" }
& $compiler $pluginArguments $pluginSourcePath
if ($LASTEXITCODE -ne 0) {
    throw "Plugin compilation failed with exit code $LASTEXITCODE."
}

$helperSourcePath = Join-Path $packagePath 'tools\Helper.cs'
$helperArguments = @(
    '/noconfig',
    '/nostdlib+',
    '/target:exe',
    "/out:$helperOutputPath",
    '/debug-',
    '/optimize+'
)
$helperArguments += @($mscorlib, $system) | ForEach-Object { "/reference:$_" }
& $compiler $helperArguments $helperSourcePath
if ($LASTEXITCODE -ne 0) {
    throw "Helper compilation failed with exit code $LASTEXITCODE."
}

$candidateHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $payloadPath).Hash.ToUpperInvariant()
Write-Host "Candidate DLL SHA-256: $candidateHash"
Write-Host "Built $outputPath"
Write-Host "Built $helperOutputPath"
Write-Host "Prepared payload $payloadPath"

$localModPath = Join-Path (Split-Path -Parent $unityModManagerPath) 'Mods\GJKen-AssemblyCSharpChineseInputSwitch'
if ($SkipDeploy) {
    Write-Host 'Skipping UMM deployment because -SkipDeploy was specified.'
}
else {
    New-Item -ItemType Directory -Force -Path $localModPath | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $localModPath 'tools') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $localModPath 'payload') | Out-Null
    Copy-Item -LiteralPath $outputPath -Destination (Join-Path $localModPath 'AssemblyCSharpChineseInputSwitch.dll') -Force
    Copy-Item -LiteralPath $infoOutputPath -Destination (Join-Path $localModPath 'Info.json') -Force
    Copy-Item -LiteralPath $helperOutputPath -Destination (Join-Path $localModPath 'tools\AssemblyCSharpChineseInputSwitch.Helper.exe') -Force
    Copy-Item -LiteralPath $payloadPath -Destination (Join-Path $localModPath 'payload\Assembly-CSharp-ChineseInput.candidate.dll') -Force
    Write-Host "Deployed UMM mod to $localModPath"
}

if ($StageForNextLaunch) {
    $gameProcesses = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -match '^Broforce' })
    if ($gameProcesses.Count -gt 0) {
        throw 'Broforce is running; stop the game before staging Assembly-CSharp.dll.'
    }

    $liveAssemblyPath = Join-Path $broforceManagedPath 'Assembly-CSharp.dll'
    $stateDirectory = Join-Path $localModPath 'state'
    $originalBackupPath = Join-Path $stateDirectory 'Assembly-CSharp.original.bak'
    $activeMarkerPath = Join-Path $stateDirectory 'active.marker'
    if (-not (Test-Path -LiteralPath $liveAssemblyPath)) {
        throw "Live Assembly-CSharp.dll does not exist: $liveAssemblyPath"
    }

    New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
    $liveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $liveAssemblyPath).Hash.ToUpperInvariant()
    if ($liveHash -eq $candidateHash) {
        if (-not (Test-Path -LiteralPath $originalBackupPath)) {
            throw 'Candidate Assembly-CSharp.dll is already active but the original backup is missing.'
        }
        Write-Host 'Chinese-input Assembly-CSharp.dll is already staged.'
    }
    else {
        if (Test-Path -LiteralPath $originalBackupPath) {
            $backupHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $originalBackupPath).Hash.ToUpperInvariant()
            if ($backupHash -ne $liveHash) {
                throw 'Live Assembly-CSharp.dll does not match the saved original backup; refusing to stage.'
            }
        }
        else {
            Copy-Item -LiteralPath $liveAssemblyPath -Destination $originalBackupPath -Force
        }

        $stagePath = $liveAssemblyPath + '.stage-' + $PID + '.tmp'
        try {
            Copy-Item -LiteralPath $payloadPath -Destination $stagePath -Force
            [IO.File]::Replace($stagePath, $liveAssemblyPath, $originalBackupPath, $true)
        }
        finally {
            if (Test-Path -LiteralPath $stagePath) {
                Remove-Item -LiteralPath $stagePath -Force -ErrorAction SilentlyContinue
            }
        }

        [IO.File]::WriteAllText(
            $activeMarkerPath,
            "candidateHash=$candidateHash`noriginalHash=$liveHash`n",
            (New-Object Text.UTF8Encoding($false)))
        Write-Host "Staged candidate Assembly-CSharp.dll for the next launch: $liveAssemblyPath"
        Write-Host "Original backup: $originalBackupPath"
    }
}

Write-Host 'Assembly-CSharp Chinese input switch build completed.'
