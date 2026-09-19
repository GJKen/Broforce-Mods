Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$atlasPath = Join-Path $root '_Mod\sprite.png'
$outOverview = Join-Path $root 'tools\missing_frames_overview.png'
$outDetail = Join-Path $root 'tools\missing_frames_detail.png'

$scale = 4
$cell = 32
$atlas = [System.Drawing.Bitmap]::new($atlasPath)

# Keep the script ASCII-safe for Windows PowerShell while rendering Chinese labels.
function U([int[]]$codes) {
    return -join ($codes | ForEach-Object { [char]$_ })
}

$cn = @{
    title = (U @(0x6D77,0x738B,0x8EAB,0x4F53,0x56FE,0x96C6,0xFF1A,0x5E27,0x8986,0x76D6,0x6807,0x6CE8,0xFF08,0x6BCF,0x683C,0x0033,0x0032,0x00D7,0x0033,0x0032,0xFF0C,0x5E27,0x53F7,0x4ECE,0x0030,0x5F00,0x59CB,0xFF09))
    redLegend = (U @(0x7EA2,0x6846,0xFF1A,0x660E,0x786E,0x672A,0x66FF,0x6362))
    yellowLegend = (U @(0x9EC4,0x6846,0xFF1A,0x672A,0x66FF,0x6362,0xFF0C,0x4F46,0x9700,0x786E,0x8BA4,0x662F,0x5426,0x4F1A,0x64AD,0x653E))
    blueLegend = (U @(0x84DD,0x6846,0xFF1A,0x5DF2,0x6709,0x7D20,0x6750,0xFF0C,0x4F46,0x5F53,0x524D,0x505C,0x7528))
    detailTitle = (U @(0x6D77,0x738B,0x672A,0x66FF,0x6362,0x5E27,0x9010,0x5E27,0x653E,0x5927,0x9884,0x89C8))
    detailLegend = (U @(0x7EA2,0x8272,0xFF1A,0x660E,0x786E,0x672A,0x66FF,0x6362,0x3000,0x9EC4,0x8272,0xFF1A,0x9700,0x786E,0x8BA4,0x3000,0x84DD,0x8272,0xFF1A,0x5DF2,0x6709,0x7D20,0x6750,0x4F46,0x505C,0x7528))
    salute = (U @(0x656C,0x793C))
    wave = (U @(0x6325,0x624B))
    point = (U @(0x6307,0x5411))
    groundFlex = (U @(0x5730,0x9762,0x79C0,0x808C,0x8089))
    hipThrust = (U @(0x9876,0x80EF))
    airFlex = (U @(0x7A7A,0x4E2D,0x79C0,0x808C,0x8089))
    kneel = (U @(0x8DEA,0x5730))
    shush = (U @(0x5618,0x58F0))
    airFlexFlip = (U @(0x7A7A,0x4E2D,0x79C0,0x808C,0x8089,0x540E,0x7684,0x7FFB,0x8F6C))
    defaultDance = (U @(0x9ED8,0x8BA4,0x8DF3,0x821E))
    elbowStrike = (U @(0x80D8,0x51FB))
    getUp = (U @(0x843D,0x5730,0x8D77,0x8EAB))
    ladder = (U @(0x722C,0x68AF))
    ladderTransition = (U @(0x722C,0x68AF,0x8FC7,0x6E21))
    frame = (U @(0x5E27))
}

$groups = @(
    [pscustomobject]@{
        Name = $cn.redLegend
        ShortName = $cn.redLegend
        Color = [System.Drawing.Color]::FromArgb(230, 210, 45, 45)
        TextColor = [System.Drawing.Color]::White
        Actions = @(
            [pscustomobject]@{ Name = $cn.salute; Frames = (256..260) }
            [pscustomobject]@{ Name = $cn.wave; Frames = (288..298) }
            [pscustomobject]@{ Name = $cn.point; Frames = (320..325) }
            [pscustomobject]@{ Name = $cn.groundFlex; Frames = (352..375) }
            [pscustomobject]@{ Name = $cn.hipThrust; Frames = (384..389) }
            [pscustomobject]@{ Name = $cn.airFlex; Frames = (402..406) }
            [pscustomobject]@{ Name = $cn.kneel; Frames = (416..427) }
            [pscustomobject]@{ Name = $cn.shush; Frames = (480..492) }
        )
    }
    [pscustomobject]@{
        Name = $cn.yellowLegend
        ShortName = $cn.yellowLegend
        Color = [System.Drawing.Color]::FromArgb(230, 235, 170, 25)
        TextColor = [System.Drawing.Color]::Black
        Actions = @(
            [pscustomobject]@{ Name = $cn.airFlexFlip; Frames = (330..341) }
            [pscustomobject]@{ Name = $cn.defaultDance; Frames = (431..442) }
            [pscustomobject]@{ Name = $cn.elbowStrike; Frames = (434..437) }
            [pscustomobject]@{ Name = $cn.getUp; Frames = (450..453) }
        )
    }
    [pscustomobject]@{
        Name = $cn.blueLegend
        ShortName = $cn.blueLegend
        Color = [System.Drawing.Color]::FromArgb(230, 35, 120, 210)
        TextColor = [System.Drawing.Color]::White
        Actions = @(
            [pscustomobject]@{ Name = $cn.ladder; Frames = (160..173) }
            [pscustomobject]@{ Name = $cn.ladderTransition; Frames = (192..197) }
        )
    }
)

function Get-FrameRect([int]$frame) {
    $x = ($frame % 32) * $cell
    $y = [math]::Floor($frame / 32) * $cell
    return [System.Drawing.Rectangle]::new($x, $y, $cell, $cell)
}

function Add-Label([System.Drawing.Graphics]$g, [string]$text, [int]$x, [int]$y, [System.Drawing.Font]$font, [System.Drawing.Brush]$brush) {
    $g.DrawString($text, $font, $brush, $x, $y)
}

$font = [System.Drawing.Font]::new('Microsoft YaHei UI', 12, [System.Drawing.FontStyle]::Bold)
$smallFont = [System.Drawing.Font]::new('Microsoft YaHei UI', 9, [System.Drawing.FontStyle]::Bold)
$whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$blackBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::Black)
$bgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(245, 245, 245))

# Overview: enlarge the atlas and draw thick colored frame borders.
$overview = [System.Drawing.Bitmap]::new($atlas.Width * 2, $atlas.Height * 2 + 64)
$og = [System.Drawing.Graphics]::FromImage($overview)
$og.Clear([System.Drawing.Color]::FromArgb(245, 245, 245))
$og.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$og.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$og.DrawImage($atlas, [System.Drawing.Rectangle]::new(0, 64, $atlas.Width * 2, $atlas.Height * 2), 0, 0, $atlas.Width, $atlas.Height, [System.Drawing.GraphicsUnit]::Pixel)

$title = $cn.title
Add-Label $og $title 12 10 $font $blackBrush
$legendX = 12
foreach ($group in $groups) {
    $brush = [System.Drawing.SolidBrush]::new($group.Color)
    $og.FillRectangle($brush, $legendX, 40, 18, 18)
    Add-Label $og $group.ShortName ($legendX + 24) 39 $smallFont $blackBrush
    $legendX += 300
}

foreach ($group in $groups) {
    $pen = [System.Drawing.Pen]::new($group.Color, 3)
    foreach ($action in $group.Actions) {
        foreach ($frame in $action.Frames) {
            $r = Get-FrameRect $frame
            $rr = [System.Drawing.Rectangle]::new($r.X * 2, $r.Y * 2 + 64, $r.Width * 2, $r.Height * 2)
            $og.DrawRectangle($pen, $rr)
        }
    }
}

$overview.Save($outOverview, [System.Drawing.Imaging.ImageFormat]::Png)

# Detail sheet: each group gets a row of enlarged, labeled frame cells.
$detailScale = 6
$tileW = $cell * $detailScale
$tileSize = $cell * $detailScale
$tileH = $tileSize + 28
$detailW = 8 * $tileW + 32
$detailH = 72
foreach ($group in $groups) {
    $detailH += 44
    foreach ($action in $group.Actions) {
        $detailH += 24 + ([math]::Ceiling($action.Frames.Count / 8) * ($tileH + 8))
    }
    $detailH += 16
}
$detail = [System.Drawing.Bitmap]::new($detailW, $detailH)
$dg = [System.Drawing.Graphics]::FromImage($detail)
$dg.Clear([System.Drawing.Color]::FromArgb(245, 245, 245))
$dg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$dg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
Add-Label $dg $cn.detailTitle 16 12 $font $blackBrush
Add-Label $dg $cn.detailLegend 16 38 $smallFont $blackBrush

$cursorY = 72
foreach ($group in $groups) {
    $headerBrush = [System.Drawing.SolidBrush]::new($group.Color)
    $dg.FillRectangle($headerBrush, 16, $cursorY, $detailW - 32, 28)
    $labelBrush = if ($group.TextColor -eq [System.Drawing.Color]::Black) { $blackBrush } else { $whiteBrush }
    $totalFrames = 0
    foreach ($action in $group.Actions) { $totalFrames += $action.Frames.Count }
    Add-Label $dg ($group.Name + ' (' + $totalFrames + ' frame cells)') 24 ($cursorY + 4) $smallFont $labelBrush
    $cursorY += 36
    foreach ($action in $group.Actions) {
        Add-Label $dg ($action.Name + '  [' + (($action.Frames | Select-Object -First 1)) + '-' + (($action.Frames | Select-Object -Last 1)) + ']') 20 $cursorY $smallFont $blackBrush
        $cursorY += 24
        $i = 0
        foreach ($frame in $action.Frames) {
            $row = [math]::Floor($i / 8)
            $col = $i % 8
            $x = 16 + $col * $tileW
            $y = $cursorY + $row * ($tileH + 8)
            $src = Get-FrameRect $frame
            $dst = [System.Drawing.Rectangle]::new($x, $y, $tileW, $tileSize)
            $dg.FillRectangle([System.Drawing.Brushes]::Black, $dst)
            $dg.DrawImage($atlas, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
            $pen = [System.Drawing.Pen]::new($group.Color, 3)
            $dg.DrawRectangle($pen, $dst)
            $label = $cn.frame + ' ' + $frame
            $size = $dg.MeasureString($label, $smallFont)
            $dg.FillRectangle([System.Drawing.Brushes]::White, $x, $y + $tileSize, $tileW, 28)
            Add-Label $dg $label ($x + [math]::Max(2, ($tileW - $size.Width) / 2)) ($y + $tileSize + 5) $smallFont $blackBrush
            $i++
        }
        $cursorY += ([math]::Ceiling($action.Frames.Count / 8) * ($tileH + 8))
    }
    $cursorY += 16
}

$detail.Save($outDetail, [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($obj in @($og, $dg, $overview, $detail, $atlas, $font, $smallFont, $whiteBrush, $blackBrush, $bgBrush)) { if ($null -ne $obj) { $obj.Dispose() } }
Write-Output "Created: $outOverview"
Write-Output "Created: $outDetail"
