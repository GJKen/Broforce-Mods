Add-Type -AssemblyName System.Drawing

$work = Join-Path $PSScriptRoot ''
$scale = 8
$tile = 30 * $scale
$pad = 28
$labelHeight = 78
$canvas = New-Object System.Drawing.Bitmap(($tile * 2 + $pad * 3), ($tile + $labelHeight + $pad * 2))
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$graphics.Clear([System.Drawing.Color]::FromArgb(52, 57, 66))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$font = New-Object System.Drawing.Font('Microsoft YaHei UI', 10)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 180, 190), 1)

$reference = [System.Drawing.Image]::FromFile((Join-Path $work 'rambro-reference.png'))
$aquabro = [System.Drawing.Image]::FromFile((Join-Path $work 'aquabro-gibs.png'))
$leftX = $pad
$rightX = $pad * 2 + $tile
$imageY = $pad
$graphics.DrawImage($reference, $leftX, $imageY, $tile, $tile)
$graphics.DrawImage($aquabro, $rightX, $imageY, $tile, $tile)
$graphics.DrawRectangle($border, $leftX, $imageY, $tile, $tile)
$graphics.DrawRectangle($border, $rightX, $imageY, $tile, $tile)
$graphics.DrawString('原版兰博  源稿帧 1', $font, $brush, $leftX, $imageY + $tile + 6)
$graphics.DrawString('海王换装  源稿帧 1', $font, $brush, $rightX, $imageY + $tile + 6)
$graphics.DrawString('图集采样：头(320,17)16×16；躯干(320,30)16×16', $font, $brush, $pad, $imageY + $tile + 28)
$graphics.DrawString('双臂(341,13)8×8；双腿(341,27)8×8', $font, $brush, $pad, $imageY + $tile + 46)

$output = Join-Path $work 'rambro-aquabro-compare-annotated.png'
$canvas.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$font.Dispose()
$brush.Dispose()
$border.Dispose()
$reference.Dispose()
$aquabro.Dispose()
$canvas.Dispose()
