param(
    [string]$AtlasPath = (Join-Path (Split-Path -Parent $PSScriptRoot) '_Mod\sprite.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$atlas = [System.Drawing.Bitmap]::new($AtlasPath)
try {
    if ($atlas.Width -ne 1024 -or $atlas.Height -ne 1024) { throw 'Unexpected body atlas dimensions.' }
    $sourceCells = @(0, 34, 36, 38)
    $poses = [Collections.Generic.HashSet[string]]::new()
    for ($phase = 0; $phase -lt 4; $phase++) {
        $sourceCell = $sourceCells[$phase]
        $targetCell = 184 + $phase
        $pixels = [Collections.Generic.List[int]]::new()
        for ($y = 0; $y -lt 32; $y++) {
            for ($x = 0; $x -lt 32; $x++) {
                $source = $atlas.GetPixel(($sourceCell % 32) * 32 + $x, [int][Math]::Floor($sourceCell / 32) * 32 + $y)
                $target = $atlas.GetPixel(($targetCell % 32) * 32 + $x, 160 + $y)
                if (($source.A -ne 0 -or $target.A -ne 0) -and $source.ToArgb() -ne $target.ToArgb()) {
                    throw "Legacy ladder cell $targetCell is not Aquabro pose $sourceCell at ($x,$y)."
                }
                $pixels.Add($(if ($target.A -eq 0) { 0 } else { $target.ToArgb() }))
            }
        }
        if (-not $poses.Add(($pixels -join ','))) { throw 'Legacy ladder lost a distinct animation phase.' }
    }
    Write-Host 'Legacy ladder atlas: 4096 pixels match Aquabro poses; four distinct phases.'
}
finally {
    $atlas.Dispose()
}
