param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $project 'tools/BuildEnvironment.ps1')
$settings = Get-AquabroBuildSettings -ProjectRoot $project
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Exactly the production compiler, response and generated source; no asset generation or deployment.
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v3.5/csc.exe'
$response = New-AquabroCompilerResponse -ProjectRoot $project -ReferenceDirectory $settings.ReferenceDirectory -OutputDirectory $output -GeneratedDirectory (Join-Path $project 'src')
$rsp = Join-Path $output 'compile.rsp'
[System.IO.File]::WriteAllText($rsp, $response, [System.Text.UTF8Encoding]::new($true))
& $compiler /nologo ('@' + $rsp)
if ($LASTEXITCODE -ne 0) { throw '正式参数复编译失败' }
$dll = Join-Path $output 'Aquabro.dll'
[pscustomobject]@{Bytes=(Get-Item -LiteralPath $dll).Length; Sha256=(Get-FileHash -LiteralPath $dll).Hash; CompilerSha256=(Get-FileHash -LiteralPath $compiler).Hash; ResponseSha256=(Get-FileHash -LiteralPath (Join-Path $project 'aquabro-build.rsp')).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build.json') -Encoding utf8
Write-Output ('DLL_BYTES=' + (Get-Item -LiteralPath $dll).Length)
