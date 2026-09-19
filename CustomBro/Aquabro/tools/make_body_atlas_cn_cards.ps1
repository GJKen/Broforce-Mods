Add-Type -AssemblyName System.Drawing

$toolRoot = $PSScriptRoot
# Keep the script ASCII-safe. The output still contains full Chinese labels.
function U([int[]]$codes) {
    return -join ($codes | ForEach-Object { [char]$_ })
}

$keyDir = (U @(0x6D77,0x738B)) + '_Aseprite' + (U @(0x5173,0x952E,0x6587,0x4EF6))
$atlasDir = '03_' + (U @(0x6E38,0x620F,0x56FE,0x96C6))
$atlasPath = Join-Path $toolRoot ($keyDir + '\' + $atlasDir + '\haiwang_trident_body_atlas.png')
$outPath = Join-Path $toolRoot ($keyDir + '\' + $atlasDir + '\haiwang_trident_body_atlas_' + (U @(0x4E2D,0x6587,0x6807,0x6CE8)) + '.png')

$cell = 32
$scale = 4
$tile = $cell * $scale
$columns = 8
$cardWidth = 172
$cardHeight = 174
$marginX = 24
$marginY = 18
$headerHeight = 58
$sectionHeight = 40
$sectionGap = 22
$cardGap = 8

$title = U @(0x6D77,0x738B,0x8EAB,0x4F53,0x56FE,0x96C6,0xFF1A,0x52A8,0x4F5C,0x5E27,0x6807,0x6CE8)
$subtitle = U @(0x6BCF,0x5F20,0x5361,0x7247,0x53EA,0x663E,0x793A,0x4E00,0x4E2A,0x0033,0x0032,0x00D7,0x0033,0x0032,0x683C,0xFF1B,0x8FB9,0x6846,0x5728,0x7A7A,0x767D,0x5916,0x56F4,0xFF0C,0x4E0D,0x8986,0x76D6,0x89D2,0x8272,0x50CF,0x7D20,0xFF1B,0x683C,0x53F7,0x4ECE,0x0030,0x5F00,0x59CB)
$legend = U @(0x7EFF,0xFF1A,0x5DF2,0x66FF,0x6362,0x4E14,0x4F7F,0x7528,0x3000,0x84DD,0xFF1A,0x7D20,0x6750,0x5B58,0x5728,0x4F46,0x505C,0x7528,0x3000,0x7EA2,0xFF1A,0x660E,0x786E,0x672A,0x66FF,0x6362,0x3000,0x9EC4,0xFF1A,0x5F85,0x786E,0x8BA4,0xFF0F,0x539F,0x7248,0x4FDD,0x7559)
$frameText = U @(0x683C)
$localFrameText = U @(0x7B2C)
$emptyText = U @(0x7A7A,0x767D,0x683C)
$sharedText = U @(0x5171,0x7528,0x683C)

$statusUsed = U @(0x5DF2,0x66FF,0x6362,0x4E14,0x4F7F,0x7528)
$statusDisabled = U @(0x7D20,0x6750,0x5B58,0x5728,0x4F46,0x5F53,0x524D,0x505C,0x7528)
$statusMissing = U @(0x660E,0x786E,0x672A,0x66FF,0x6362)
$statusReview = U @(0x5F85,0x786E,0x8BA4)
$statusRetained = U @(0x539F,0x7248,0x4FDD,0x7559,0xFF0F,0x5F53,0x524D,0x672A,0x5355,0x72EC,0x63A5,0x7BA1)

$colors = @{
    Used = [System.Drawing.Color]::FromArgb(48, 158, 93)
    Disabled = [System.Drawing.Color]::FromArgb(48, 116, 207)
    Missing = [System.Drawing.Color]::FromArgb(211, 68, 68)
    Review = [System.Drawing.Color]::FromArgb(226, 165, 28)
    Retained = [System.Drawing.Color]::FromArgb(112, 119, 130)
}

$groups = New-Object System.Collections.Generic.List[object]
function Add-Group([string]$name, [int[]]$frames, [string]$statusKey, [bool]$showEmpty) {
    [void]$groups.Add([pscustomobject]@{
        Name = $name
        Frames = @($frames)
        StatusKey = $statusKey
        ShowEmpty = $showEmpty
    })
}

$standing = U @(0x7AD9,0x7ACB)
$oldJump = U @(0x65E7,0x7248,0x8DF3,0x8DC3,0x56DE,0x9000)
$death = U @(0x666E,0x901A,0x6B7B,0x4EA1)
$crouch = U @(0x8E72,0x6301)
$oldCrouchWalk = U @(0x65E7,0x7248,0x8E72,0x8D70,0x56DE,0x9000)
$oldWall = U @(0x65E7,0x7248,0x8D34,0x5899,0x4E0E,0x653E,0x624B,0x56DE,0x9000)
$highFive = U @(0x51FB,0x638C)
$retained = U @(0x539F,0x7248,0x4FDD,0x7559,0x52A8,0x4F5C)
$run = U @(0x8DD1,0x6B65)
$crouchRun = U @(0x8E72,0x8D70)
$forwardLand = U @(0x524D,0x8FDB,0x843D,0x5730)
$roll = U @(0x9AD8,0x5904,0x843D,0x5730,0x7FFB,0x6EDA)
$forwardJump = U @(0x524D,0x8FDB,0x8DF3,0x8DC3)
$idleJump = U @(0x539F,0x5730,0x8DF3,0x8DC3)
$wallDrag = U @(0x8D34,0x5899,0x6293,0x9644)
$climb = U @(0x653E,0x624B,0x6500,0x722C)
$dash = U @(0x51B2,0x523A)
$idleLand = U @(0x539F,0x5730,0x843D,0x5730)
$hangingMove = U @(0x60AC,0x6302,0x6A2A,0x79FB)
$hangingStop = U @(0x60AC,0x6302,0x505C,0x9A7B,0x6536,0x52BF)
$ziplineMove = U @(0x6ED1,0x7D22,0x9006,0x884C,0x6362,0x624B)
$ziplineStop = U @(0x6ED1,0x7D22,0x6ED1,0x884C,0x6536,0x52BF)
$pushing = U @(0x63A8,0x52A8,0x7269,0x4F53)
$waterWall = U @(0x6C34,0x5899,0x7279,0x6280)
$ladderUp = U @(0x722C,0x68AF,0x4E0A,0x884C)
$ladderDown = U @(0x722C,0x68AF,0x4E0B,0x6ED1)
$ladderIdle = U @(0x722C,0x68AF,0x505C,0x9A7B)
$ladderTransition = U @(0x722C,0x68AF,0x8FDB,0x51FA,0x8FC7,0x6E21)
$chimneyFlip = U @(0x8E6C,0x5899,0x7FFB,0x8F6C)
$salute = U @(0x656C,0x793C)
$wave = U @(0x6325,0x624B)
$point = U @(0x6307,0x5411)
$airFlexFlip = U @(0x7A7A,0x4E2D,0x79C0,0x808C,0x540E,0x7FFB,0x8F6C)
$airDash = U @(0x7A7A,0x4E2D,0x7FFB,0x8F6C,0xFF0F,0x51B2,0x523A,0x59FF,0x6001)
$groundFlex = U @(0x5730,0x9762,0x79C0,0x808C)
$hipThrust = U @(0x9876,0x80EF)
$airFlex = U @(0x7A7A,0x4E2D,0x79C0,0x808C)
$kneel = U @(0x8DEA,0x5730)
$dance = U @(0x9ED8,0x8BA4,0x8DF3,0x821E)
$elbow = U @(0x80D8,0x51FB)
$getUp = U @(0x843D,0x5730,0x8D77,0x8EAB)
$shush = U @(0x5618,0x58F0)
$otherSpecial = U @(0x539F,0x7248,0x7279,0x6B8A,0x52A8,0x4F5C)
$unmapped = U @(0x5F85,0x786E,0x8BA4,0xFF1A,0x5176,0x4ED6,0x539F,0x7248,0x52A8,0x4F5C)

Add-Group $standing @(0) 'Used' $false
Add-Group $oldJump (1..3) 'Retained' $false
Add-Group $death (4..5) 'Used' $false
Add-Group $crouch @(6) 'Used' $false
Add-Group $oldCrouchWalk (7..9) 'Retained' $false
Add-Group $oldWall (10..16) 'Retained' $false
Add-Group $highFive (17..22) 'Used' $false
Add-Group $retained (25..31) 'Retained' $false

Add-Group $run (32..39) 'Used' $false
Add-Group $crouchRun (40..47) 'Used' $false
Add-Group $forwardLand (48..50) 'Used' $false
Add-Group $roll (51..63) 'Used' $false
Add-Group $forwardJump (64..69) 'Used' $false
Add-Group $idleJump (70..75) 'Used' $false
Add-Group $wallDrag (76..80) 'Used' $false
Add-Group $climb (81..85) 'Used' $false
Add-Group $wallDrag (86..90) 'Used' $false
Add-Group $climb (91..95) 'Used' $false
Add-Group $dash (96..103) 'Used' $false
Add-Group $idleLand (104..106) 'Used' $false
Add-Group $hangingMove (107..118) 'Used' $false
Add-Group $hangingStop (119..124) 'Used' $false

Add-Group $pushing (128..135) 'Retained' $false
Add-Group $waterWall (145..152) 'Used' $false
Add-Group $ladderUp (160..167) 'Disabled' $false
Add-Group $ladderDown (168..170) 'Disabled' $false
Add-Group $ladderIdle (171..173) 'Disabled' $false
Add-Group $ladderTransition (192..197) 'Disabled' $false
Add-Group $chimneyFlip (203..214) 'Used' $false
Add-Group $airDash (235..242) 'Review' $false

Add-Group $salute (256..260) 'Missing' $false
Add-Group $wave (288..298) 'Missing' $false
Add-Group $point (320..325) 'Missing' $false
Add-Group $airFlexFlip (330..342) 'Review' $false
Add-Group $groundFlex (352..375) 'Used' $false
Add-Group $otherSpecial (376..379) 'Review' $false
Add-Group $hipThrust (384..389) 'Missing' $false
Add-Group $airFlex (402..406) 'Missing' $false
Add-Group $kneel (416..427) 'Missing' $false
Add-Group $dance (431..442) 'Review' $true
Add-Group $elbow (434..437) 'Review' $false
Add-Group $getUp (448..453) 'Review' $false
Add-Group $shush (480..492) 'Missing' $false
Add-Group $ziplineMove (512..523) 'Used' $false
Add-Group $ziplineStop (524..529) 'Used' $false

function Get-FrameRect([int]$frame) {
    $x = ($frame % 32) * $cell
    $y = [math]::Floor($frame / 32) * $cell
    return [System.Drawing.Rectangle]::new($x, $y, $cell, $cell)
}

function Format-FrameRanges([int[]]$frames) {
    $ordered = @($frames | Sort-Object -Unique)
    if ($ordered.Count -eq 0) { return '' }
    $ranges = New-Object System.Collections.Generic.List[string]
    $start = $ordered[0]
    $previous = $start
    for ($i = 1; $i -lt $ordered.Count; $i++) {
        $current = $ordered[$i]
        if ($current -ne ($previous + 1)) {
            if ($start -eq $previous) { [void]$ranges.Add([string]$start) }
            else { [void]$ranges.Add(([string]$start + '-' + [string]$previous)) }
            $start = $current
        }
        $previous = $current
    }
    if ($start -eq $previous) { [void]$ranges.Add([string]$start) }
    else { [void]$ranges.Add(([string]$start + '-' + [string]$previous)) }
    return ($ranges -join [string][char]0x3001)
}

$atlas = [System.Drawing.Bitmap]::new([string](Resolve-Path $atlasPath))
$filled = New-Object System.Collections.Generic.List[int]
for ($frame = 0; $frame -lt 1024; $frame++) {
    $source = Get-FrameRect $frame
    $hasPixel = $false
    for ($y = 0; $y -lt $cell -and -not $hasPixel; $y++) {
        for ($x = 0; $x -lt $cell; $x++) {
            if ($atlas.GetPixel($source.X + $x, $source.Y + $y).A -gt 0) {
                $hasPixel = $true
                break
            }
        }
    }
    if ($hasPixel) { [void]$filled.Add($frame) }
}

# Add every visible cell that has no known action mapping, so no sprite is left unexplained.
$known = New-Object System.Collections.Generic.HashSet[int]
foreach ($group in $groups) {
    foreach ($frame in $group.Frames) { [void]$known.Add($frame) }
}
$unmappedFrames = @($filled | Where-Object { -not $known.Contains($_) })
if ($unmappedFrames.Count -gt 0) {
    Add-Group $unmapped $unmappedFrames 'Review' $false
}

$overviewScale = 2
$tile = $cell * $overviewScale
$gapX = 10
$rowGap = 26
$betweenRows = 14
$rowPitch = $tile + $rowGap + $betweenRows
$atlasWidth = 32 * ($tile + $gapX) - $gapX
$atlasHeight = 17 * $rowPitch - $betweenRows
$rightWidth = 940
$width = $marginX + $atlasWidth + 36 + $rightWidth + $marginX
$height = 110 + $atlasHeight
$sheet = [System.Drawing.Bitmap]::new([int]$width, [int]$height)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(248, 249, 251))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

$fontTitle = [System.Drawing.Font]::new('Microsoft YaHei UI', 22, [System.Drawing.FontStyle]::Bold)
$fontSubtitle = [System.Drawing.Font]::new('Microsoft YaHei UI', 11, [System.Drawing.FontStyle]::Regular)
$fontSection = [System.Drawing.Font]::new('Microsoft YaHei UI', 12, [System.Drawing.FontStyle]::Bold)
$fontLabel = [System.Drawing.Font]::new('Microsoft YaHei UI', 8, [System.Drawing.FontStyle]::Bold)
$fontSmall = [System.Drawing.Font]::new('Microsoft YaHei UI', 9, [System.Drawing.FontStyle]::Regular)
$black = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(35, 39, 45))
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(95, 103, 114))
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$g.DrawString($title, $fontTitle, $black, $marginX, 8)
$g.DrawString($subtitle, $fontSubtitle, $muted, $marginX, 42)
$g.DrawString($legend, $fontSmall, $muted, $marginX, 64)

$atlasX = $marginX
$atlasY = 96
$frameStatus = @{}
$frameNames = @{}
$priority = @{ Used = 5; Disabled = 4; Missing = 3; Review = 2; Retained = 1 }
foreach ($group in $groups) {
    foreach ($frame in $group.Frames) {
        if (-not $frameStatus.ContainsKey($frame) -or $priority[$group.StatusKey] -gt $priority[$frameStatus[$frame]]) {
            $frameStatus[$frame] = $group.StatusKey
            $frameNames[$frame] = $group.Name
        }
    }
}

# Render each original 32x32 cell separately, with a real gutter for labels and lines.
$annotated = New-Object System.Collections.Generic.HashSet[int]
foreach ($group in $groups) { foreach ($frame in $group.Frames) { [void]$annotated.Add($frame) } }
foreach ($frame in $filled) { [void]$annotated.Add($frame) }
foreach ($frame in ($annotated | Sort-Object)) {
    $source = Get-FrameRect $frame
    $column = $frame % 32
    $row = [math]::Floor($frame / 32)
    $x = $atlasX + $column * ($tile + $gapX)
    $rowY = $atlasY + $row * $rowPitch
    $imageY = $rowY + $rowGap
    $destination = [System.Drawing.Rectangle]::new($x, $imageY, $tile, $tile)
    $g.DrawImage($atlas, $destination, $source, [System.Drawing.GraphicsUnit]::Pixel)
    $label = [string]$frame
    $size = $g.MeasureString($label, $fontLabel)
    $g.DrawString($label, $fontLabel, $black, $x + ($tile - $size.Width) / 2, $rowY + 2)
}

# Draw one rectangle for each consecutive segment of an action on each atlas row.
# The image tiles have gutters, so these lines never touch sprite pixels.
foreach ($group in $groups) {
    $framesByRow = @{}
    foreach ($frame in ($group.Frames | Sort-Object -Unique)) {
        $row = [math]::Floor($frame / 32)
        if (-not $framesByRow.ContainsKey($row)) { $framesByRow[$row] = New-Object System.Collections.Generic.List[int] }
        [void]$framesByRow[$row].Add($frame % 32)
    }
    foreach ($rowKey in $framesByRow.Keys) {
        $columnsInRow = @($framesByRow[$rowKey] | Sort-Object)
        if ($columnsInRow.Count -eq 0) { continue }
        $start = $columnsInRow[0]; $last = $start
        $segments = New-Object System.Collections.Generic.List[object]
        for ($i = 1; $i -lt $columnsInRow.Count; $i++) {
            if ($columnsInRow[$i] -ne ($last + 1)) {
                [void]$segments.Add(@($start, $last)); $start = $columnsInRow[$i]
            }
            $last = $columnsInRow[$i]
        }
        [void]$segments.Add(@($start, $last))
        foreach ($segment in $segments) {
            $x1 = $atlasX + $segment[0] * ($tile + $gapX) - 4
            $x2 = $atlasX + $segment[1] * ($tile + $gapX) + $tile + 4
            $rowY = $atlasY + ([int]$rowKey) * $rowPitch
            $y1 = $rowY + $rowGap - 5
            $y2 = $rowY + $rowGap + $tile + 4
            $pen = [System.Drawing.Pen]::new($colors[$group.StatusKey], 2)
            $g.DrawRectangle($pen, $x1, $y1, $x2 - $x1, $y2 - $y1)
            $pen.Dispose()
        }
    }
}

# Right-side legend and action map, preserving the original overview layout.
$tableX = $atlasX + $atlasWidth + 36
$g.DrawString((U @(0x52A8,0x4F5C,0x5BF9,0x5E94,0x8868)), $fontTitle, $black, $tableX, 12)
$g.DrawString($legend, $fontSmall, $muted, $tableX, 48)
$cursorY = 82
$tableColumnWidth = [math]::Floor($rightWidth / 2)
for ($groupIndex = 0; $groupIndex -lt $groups.Count; $groupIndex++) {
    $group = $groups[$groupIndex]
    $tableColumn = $groupIndex % 2
    $tableRow = [math]::Floor($groupIndex / 2)
    $lineX = $tableX + $tableColumn * $tableColumnWidth
    $lineY = $cursorY + $tableRow * 44
    $range = Format-FrameRanges $group.Frames
    $status = switch ($group.StatusKey) {
        'Used' { $statusUsed }
        'Disabled' { $statusDisabled }
        'Missing' { $statusMissing }
        'Review' { $statusReview }
        default { $statusRetained }
    }
    $brush = [System.Drawing.SolidBrush]::new($colors[$group.StatusKey])
    $g.FillRectangle($brush, $lineX, $lineY + 5, 12, 12)
    $brush.Dispose()
    $line = $group.Name + ' [' + $range + '] - ' + $status
    $g.DrawString($group.Name, $fontSmall, $black, $lineX + 20, $lineY)
    $detail = '[' + $range + '] - ' + $status
    $g.DrawString($detail, $fontLabel, $muted, $lineX + 20, $lineY + 17)
}

$sheet.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output ("Created: {0} ({1}x{2})" -f $outPath, $width, $height)
Write-Output ("Visible atlas cells: {0}; mapped groups: {1}; unmapped visible cells added: {2}" -f $filled.Count, $groups.Count, $unmappedFrames.Count)

foreach ($obj in @($g, $sheet, $atlas, $fontTitle, $fontSubtitle, $fontSection, $fontLabel, $fontSmall, $black, $muted, $white)) {
    if ($null -ne $obj) { $obj.Dispose() }
}
