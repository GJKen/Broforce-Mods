param(
    [string]$ManagedDirectory = 'E:/SteamLibrary/steamapps/common/Broforce/Broforce_beta_Data/Managed',
    [string]$UmmDirectory = 'E:/SteamLibrary/steamapps/common/Broforce/r2mod/Broforce/profiles/Broforce/UMM',
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$waveProject = Split-Path -Parent $PSScriptRoot
$waveRepo = (Resolve-Path (Join-Path $waveProject '../..')).Path
$waveOutput = Join-Path $waveProject 'bin/WaveNetwork'
$waveManaged = $ManagedDirectory
$waveUmm = $UmmDirectory
if (!$CompilerPath) { $CompilerPath = Join-Path $waveRepo 'UnityInspectorMod/.tools/roslyn/tasks/net472/csc.exe' }
New-Item -ItemType Directory -Force -Path $waveOutput | Out-Null
$waveRefs = @{}
foreach ($waveName in @('Assembly-CSharp','UnityEngine','UnityEngine.CoreModule','UnityEngine.ImageConversionModule','UnityEngine.PhysicsModule','UnityEngine.IMGUIModule','UnityEngine.TextRenderingModule')) {
    $waveRefs[$waveName] = Join-Path $waveManaged ($waveName + '.dll')
}
$waveRefs['0Harmony'] = Join-Path $waveUmm 'Core/0Harmony.dll'
$waveRefs['BroMakerLib'] = Join-Path $waveUmm 'Mods/BroMaker-BroMaker/BroMakerLib/BroMakerLib.dll'
$waveRefs['RocketLib'] = Join-Path $waveUmm 'Mods/RocketLib-RocketLib/RocketLib/RocketLib.dll'
$waveRsp = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $waveProject 'aquabro-build.rsp')
# Roslyn's default csc.rsp targets .NET 4.x. Broforce runs the Mono 2.0 API.
$waveFramework = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v2.0.50727'
$waveFramework35 = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/v3.5'
$waveSystemRefs = @(
    (Join-Path $waveFramework 'mscorlib.dll'),
    (Join-Path $waveFramework 'System.dll'),
    (Join-Path $waveFramework35 'System.Core.dll')
)
foreach ($waveReference in $waveSystemRefs) {
    if (!(Test-Path -LiteralPath $waveReference)) { throw "Missing framework reference: $waveReference" }
    $waveRsp = '/reference:"' + $waveReference + '"' + [Environment]::NewLine + $waveRsp
}
$waveRsp = '/nostdlib+' + [Environment]::NewLine + $waveRsp
foreach ($waveName in $waveRefs.Keys) {
    if (!(Test-Path -LiteralPath $waveRefs[$waveName])) { throw "Missing reference: $waveName" }
    $waveRsp = $waveRsp.Replace('@REFERENCES_ROOT@\' + $waveName + '.dll', $waveRefs[$waveName])
}
$waveRsp = $waveRsp.Replace('@OUTPUT_ROOT@',$waveOutput).Replace('@PROJECT_ROOT@',$waveProject).Replace('@GENERATED_ROOT@',(Join-Path $waveProject 'src'))
$waveRspPath = Join-Path $waveOutput 'compile.rsp'
[IO.File]::WriteAllText($waveRspPath,$waveRsp,(New-Object Text.UTF8Encoding($false)))
& $CompilerPath /noconfig /nologo ('@' + $waveRspPath) 2>&1 | Tee-Object -FilePath (Join-Path $waveOutput 'mono35-build.log')
if ($LASTEXITCODE -ne 0) { throw 'Aquabro compilation failed' }
Get-Item -LiteralPath (Join-Path $waveOutput 'Aquabro.dll') | Select-Object FullName,Length,LastWriteTime
& (Join-Path $PSScriptRoot 'Package.ps1') -AssemblyPath (Join-Path $waveOutput 'Aquabro.dll')
