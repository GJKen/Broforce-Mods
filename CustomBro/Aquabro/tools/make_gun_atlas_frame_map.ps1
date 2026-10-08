param(
    [string]$InputPath = (Join-Path $PSScriptRoot '..\_Mod\gunSprite.png'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '海王_Aseprite关键文件\03_游戏图集\haiwang_trident_gun_atlas_帧组标注.png')
)

Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Bitmap]::new([string](Resolve-Path $InputPath))
if ($source.Width -ne 1024 -or $source.Height -ne 1024) {
    throw "Expected a 1024x1024 gun atlas, got $($source.Width)x$($source.Height)."
}

$left = 20
$atlasTop = 130
$panelLeft = 1070
$canvas = [System.Drawing.Bitmap]::new(1440, 1180, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$graphics.Clear([System.Drawing.Color]::White)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$titleFont = [System.Drawing.Font]::new('Microsoft YaHei UI', 18, [System.Drawing.FontStyle]::Bold)
$subtitleFont = [System.Drawing.Font]::new('Microsoft YaHei UI', 10)
$labelFont = [System.Drawing.Font]::new('Microsoft YaHei UI', 9, [System.Drawing.FontStyle]::Bold)
$legendFont = [System.Drawing.Font]::new('Microsoft YaHei UI', 10)
$smallFont = [System.Drawing.Font]::new('Microsoft YaHei UI', 9)
$black = [System.Drawing.Brushes]::Black
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(90, 90, 90))
$panelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(246, 247, 249))
$borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(210, 214, 220), 1)
$emptyPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(145, 145, 145), 2)
$emptyPen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash

$colors = @(
    [System.Drawing.Color]::FromArgb(25, 118, 210),
    [System.Drawing.Color]::FromArgb(0, 137, 123),
    [System.Drawing.Color]::FromArgb(239, 108, 0),
    [System.Drawing.Color]::FromArgb(123, 31, 162),
    [System.Drawing.Color]::FromArgb(194, 24, 91),
    [System.Drawing.Color]::FromArgb(46, 125, 50),
    [System.Drawing.Color]::FromArgb(117, 117, 117)
)

$groups = @(
    @{ Name = '站立 / 普通攻击姿态'; Ranges = @([pscustomobject]@{ Start = 0; End = 8 }); Color = $colors[0] },
    @{ Name = '站立持戟'; Ranges = @([pscustomobject]@{ Start = 9; End = 9 }); Color = $colors[1] },
    @{ Name = '跑步 / 冲刺持戟'; Ranges = @([pscustomobject]@{ Start = 10; End = 15 }, [pscustomobject]@{ Start = 25; End = 26 }); Color = $colors[1] },
    @{ Name = '跑步攻击姿态'; Ranges = @([pscustomobject]@{ Start = 16; End = 24 }); Color = $colors[2] },
    @{ Name = '前进跳跃持戟'; Ranges = @([pscustomobject]@{ Start = 27; End = 31 }, [pscustomobject]@{ Start = 41; End = 41 }); Color = $colors[3] },
    @{ Name = '蹲姿攻击姿态'; Ranges = @([pscustomobject]@{ Start = 32; End = 40 }); Color = $colors[4] },
    @{ Name = '原地跳跃持戟'; Ranges = @([pscustomobject]@{ Start = 42; End = 47 }); Color = $colors[3] },
    @{ Name = '单手回退姿态'; Ranges = @([pscustomobject]@{ Start = 48; End = 51 }, [pscustomobject]@{ Start = 53; End = 56 }); Color = $colors[5] },
    @{ Name = '落地接跑步'; Ranges = @([pscustomobject]@{ Start = 57; End = 59 }); Color = $colors[6] },
    @{ Name = '落地接站立'; Ranges = @([pscustomobject]@{ Start = 60; End = 62 }); Color = $colors[6] },
    @{ Name = '蹲持 / 蹲走'; Ranges = @([pscustomobject]@{ Start = 64; End = 72 }); Color = $colors[1] }
)

function CellRect([int]$start, [int]$end) {
    $row = [math]::Floor($start / 32)
    $column = $start % 32
    if ([math]::Floor($end / 32) -ne $row) {
        throw "Annotation range $start-$end crosses a row. Split it first."
    }
    return [System.Drawing.Rectangle]::new(
        $left + $column * 32,
        $atlasTop + $row * 32,
        ($end - $start + 1) * 32,
        32)
}

function DrawLabel([System.Drawing.Rectangle]$rect, [string]$text, [System.Drawing.Color]$color, [bool]$above) {
    $textSize = $graphics.MeasureString($text, $labelFont)
    $x = $rect.X + [math]::Max(1, ($rect.Width - $textSize.Width) / 2)
    $y = if ($above) { $rect.Y - $textSize.Height - 2 } else { $rect.Y + 2 }
    if ($y -lt 2) { $y = $rect.Y + 2 }
    $fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(220, $color.R, $color.G, $color.B))
    $textBrush = [System.Drawing.Brushes]::White
    $background = [System.Drawing.RectangleF]::new($x - 2, $y - 1, $textSize.Width + 4, $textSize.Height + 2)
    $graphics.FillRectangle($fill, $background)
    $graphics.DrawString($text, $labelFont, $textBrush, $x, $y)
    $fill.Dispose()
}

function DrawRange([int]$start, [int]$end, [System.Drawing.Color]$color, [bool]$above) {
    $rect = CellRect $start $end
    $pen = [System.Drawing.Pen]::new($color, 3)
    $graphics.DrawRectangle($pen, $rect.X + 1, $rect.Y + 1, $rect.Width - 2, $rect.Height - 2)
    $pen.Dispose()
    $rangeLabel = if ($start -eq $end) { "$start" } else { "$start-$end" }
    DrawLabel $rect $rangeLabel $color $above
}

$graphics.DrawString('海王三叉戟武器图集：帧组标注', $titleFont, $black, 20, 14)
$graphics.DrawString('原图内容未改动；数字是武器图集格号。滑索区另标出对应身体帧。', $subtitleFont, $muted, 22, 48)
$graphics.DrawImage($source, $left, $atlasTop, $source.Width, $source.Height)

# Keep the first row labels above the atlas; labels on later rows sit inside their own boxes.
foreach ($group in $groups) {
    $first = $true
    foreach ($range in $group.Ranges) {
        $above = [math]::Floor($range.Start / 32) -eq 0
        DrawRange ([int]$range.Start) ([int]$range.End) $group.Color $above
        $first = $false
    }
}

# The two reserved blank cells are called out explicitly.
$graphics.DrawRectangle($emptyPen, (CellRect 52 52))
$graphics.DrawRectangle($emptyPen, (CellRect 63 63))

# Annotate the 18 zipline groups: one body frame owns nine consecutive weapon poses.
for ($body = 512; $body -le 529; $body++) {
    $offset = $body - 512
    $start = 595 + $offset * 9
    $end = $start + 8
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(0, 105, 92), 3)
    $segmentStart = $start
    $firstRect = $null
    while ($segmentStart -le $end) {
        $rowEnd = ([math]::Floor($segmentStart / 32) + 1) * 32 - 1
        $segmentEnd = [math]::Min($end, $rowEnd)
        $rect = CellRect $segmentStart $segmentEnd
        if ($null -eq $firstRect) { $firstRect = $rect }
        $graphics.DrawRectangle($pen, $rect.X + 1, $rect.Y + 1, $rect.Width - 2, $rect.Height - 2)
        $segmentStart = $segmentEnd + 1
    }
    $pen.Dispose()
    DrawLabel $firstRect ("身体$body / $start-$end") ([System.Drawing.Color]::FromArgb(0, 105, 92)) $false
}

# Mark the cleared middle area without covering the atlas pixels.
$graphics.DrawString('73-594：透明空位（空手地形动作不显示武器）', $smallFont, $muted, $panelLeft, 100)

$graphics.FillRectangle($panelBrush, $panelLeft, 130, 345, 1024)
$graphics.DrawRectangle($borderPen, $panelLeft, 130, 345, 1024)
$graphics.DrawString('动作分组 / 图集格号', $subtitleFont, $black, $panelLeft + 14, 146)
$legendY = 172
foreach ($group in $groups) {
    $rangeText = (($group.Ranges | ForEach-Object { if ($_.Start -eq $_.End) { "$($_.Start)" } else { "$($_.Start)-$($_.End)" } }) -join ', ')
    $brush = [System.Drawing.SolidBrush]::new($group.Color)
    $graphics.FillRectangle($brush, $panelLeft + 14, $legendY + 3, 12, 12)
    $brush.Dispose()
    $graphics.DrawString("$($group.Name)：$rangeText", $legendFont, $black, $panelLeft + 34, $legendY)
    $legendY += 27
}

$legendY += 5
$zipBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(0, 105, 92))
$graphics.FillRectangle($zipBrush, $panelLeft + 14, $legendY + 3, 12, 12)
$zipBrush.Dispose()
$graphics.DrawString('滑索：每个身体帧对应 9 个武器姿态', $legendFont, $black, $panelLeft + 34, $legendY)
$legendY += 28
for ($body = 512; $body -le 529; $body++) {
    $start = 595 + ($body - 512) * 9
    $end = $start + 8
    $graphics.DrawString("身体帧 $body  ->  武器格 $start-$end", $smallFont, $muted, $panelLeft + 34, $legendY)
    $legendY += 21
}

$graphics.DrawString('空格：52、63（保留/未绘制）', $smallFont, $muted, $panelLeft + 14, 1115)

$outputFull = [System.IO.Path]::GetFullPath($OutputPath)
$outputDir = [System.IO.Path]::GetDirectoryName($outputFull)
[System.IO.Directory]::CreateDirectory($outputDir) | Out-Null
$canvas.Save($outputFull, [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($object in @($source, $canvas, $graphics, $titleFont, $subtitleFont, $labelFont, $legendFont, $smallFont, $muted, $panelBrush, $borderPen, $emptyPen)) {
    if ($null -ne $object) { $object.Dispose() }
}

Write-Output $outputFull
