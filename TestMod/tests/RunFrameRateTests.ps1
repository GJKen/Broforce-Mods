$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot 'obj\FrameRateTests'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$outputPath = Join-Path $outputDir 'FrameRateControllerTests.exe'
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v3.5\csc.exe'
try {
    & $compiler /nologo /target:exe "/out:$outputPath" `
        (Join-Path $PSScriptRoot 'FrameRateControllerTests.cs') `
        (Join-Path $projectRoot 'src\FrameRateController.cs')
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $LASTEXITCODE" }
    & $outputPath
    if ($LASTEXITCODE -ne 0) { throw "Frame rate controller tests failed: $LASTEXITCODE" }
}
finally {
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath }
    if (@(Get-ChildItem -LiteralPath $outputDir -Force).Count -eq 0) {
        Remove-Item -LiteralPath $outputDir
    }
}
