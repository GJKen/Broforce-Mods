$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$releasePath = Join-Path $projectRoot 'Release'
$infoPath = Join-Path $releasePath 'UMM\Mods\EnemyAIEnhancement\Info.json'
$manifestPath = Join-Path $releasePath 'manifest.json'
$info = Get-Content -Raw -Encoding UTF8 -LiteralPath $infoPath | ConvertFrom-Json
$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.version_number -ne $info.Version) {
    throw 'manifest.json and Info.json must have the same version.'
}
$packageFiles = @(
    'manifest.json',
    'README.md',
    'icon.png',
    'UMM/Mods/EnemyAIEnhancement/EnemyAIEnhancement.dll',
    'UMM/Mods/EnemyAIEnhancement/Info.json'
)
foreach ($relativePath in $packageFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $releasePath $relativePath) -PathType Leaf)) {
        throw "Missing package file: $relativePath"
    }
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archivePath = Join-Path $releasePath 'EnemyAIEnhancement.zip'
$stream = [System.IO.File]::Create($archivePath)
$archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
try {
    foreach ($relativePath in $packageFiles) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, (Join-Path $releasePath $relativePath), $relativePath,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
Write-Host "Packaged $archivePath (version $($info.Version))"
