param(
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    $CompilerPath = Join-Path $projectRoot '.tools/roslyn/tasks/net472/csc.exe'
}
if (-not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Set -CompilerPath to a Roslyn csc.exe, or use the local .tools/roslyn/tasks/net472/csc.exe compiler.'
}
$framework = Join-Path $env:windir 'Microsoft.NET/Framework64/v2.0.50727'
$outputDirectory = Join-Path $projectRoot 'bin/PortTests'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$executable = Join-Path $outputDirectory 'PortSwitchTests.exe'
$arguments = @(
    '/noconfig', '/nostdlib+', '/target:exe', '/debug-', '/optimize+',
    '/main:PortSwitchTests', "/out:$executable",
    "/reference:$(Join-Path $framework 'mscorlib.dll')",
    "/reference:$(Join-Path $framework 'System.dll')",
    "/reference:$(Join-Path $framework 'System.Xml.dll')",
    "/reference:$(Join-Path $env:windir 'assembly/GAC_MSIL/System.Core/3.5.0.0__b77a5c561934e089/System.Core.dll')",
    (Join-Path $projectRoot 'src/Main.cs'),
    (Join-Path $projectRoot 'src/TcpServer.cs'),
    (Join-Path $projectRoot 'src/MainThreadDispatcher.cs'),
    (Join-Path $projectRoot 'src/InputSimulator.cs'),
    (Join-Path $PSScriptRoot 'PortSwitchTests.cs')
)
& $CompilerPath $arguments
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $LASTEXITCODE" }
& $executable (Join-Path $outputDirectory 'Settings.xml')
if ($LASTEXITCODE -ne 0) { throw "Port switch tests failed: $LASTEXITCODE" }
