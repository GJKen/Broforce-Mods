param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$invariant = [Globalization.CultureInfo]::InvariantCulture

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Performance log path does not exist: $Path"
}

$inputItem = Get-Item -LiteralPath $Path
if ($inputItem.PSIsContainer) {
    $logFiles = @(Get-ChildItem -LiteralPath $inputItem.FullName -Filter '*.log' -File)
}
else {
    $logFiles = @($inputItem)
}

if ($logFiles.Count -eq 0) {
    throw "No .log files found under: $Path"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    if ($inputItem.PSIsContainer) {
        $OutputDirectory = $inputItem.FullName
    }
    else {
        $OutputDirectory = $inputItem.DirectoryName
    }
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Read-Fields([string]$line) {
    $marker = $line.IndexOf('PERF_SUMMARY ', [StringComparison]::Ordinal)
    if ($marker -lt 0) {
        return $null
    }

    $payload = $line.Substring($marker + 'PERF_SUMMARY '.Length)
    $fields = @{}
    foreach ($segment in $payload.Split(';')) {
        $trimmed = $segment.Trim()
        $separator = $trimmed.IndexOf('=')
        if ($separator -le 0) {
            continue
        }

        $key = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        $fields[$key] = $value
    }
    return $fields
}

function Get-Long($fields, [string]$name) {
    if (-not $fields.ContainsKey($name)) { return 0L }
    $value = 0L
    [Int64]::TryParse($fields[$name], [Globalization.NumberStyles]::Integer, $invariant, [ref]$value) | Out-Null
    return $value
}

function Get-Double($fields, [string]$name) {
    if (-not $fields.ContainsKey($name)) { return 0d }
    $value = 0d
    [Double]::TryParse($fields[$name], [Globalization.NumberStyles]::Float, $invariant, [ref]$value) | Out-Null
    return $value
}

$metricNames = @(
    'acidAuthority', 'acidPoolRefresh', 'acidHeroScan', 'acidHeroCache',
    'acidHook', 'acidObservation', 'entitySubmit', 'entityPending',
    'entityPrune', 'traceBuild', 'traceDedup', 'diagnosticWrite'
)
$rows = New-Object System.Collections.Generic.List[object]

foreach ($logFile in $logFiles) {
    foreach ($line in (Get-Content -Encoding UTF8 -LiteralPath $logFile.FullName)) {
        $fields = Read-Fields $line
        if ($null -eq $fields) { continue }

        $row = [ordered]@{
            source = $logFile.Name
            reason = if ($fields.ContainsKey('reason')) { $fields.reason } else { '' }
            buildHash = if ($fields.ContainsKey('buildHash')) { $fields.buildHash } else { '' }
            sessionId = if ($fields.ContainsKey('sessionId')) { $fields.sessionId } else { '' }
            role = if ($fields.ContainsKey('role')) { $fields.role } else { '' }
            scene = if ($fields.ContainsKey('scene')) { $fields.scene } else { '' }
            frameCount = Get-Long $fields 'frameCount'
            frameAvgMs = Get-Double $fields 'frameAvgMs'
            frameP50Ms = Get-Double $fields 'frameP50Ms'
            frameP95Ms = Get-Double $fields 'frameP95Ms'
            frameP99Ms = Get-Double $fields 'frameP99Ms'
            frameHist = if ($fields.ContainsKey('frameHist')) { $fields.frameHist } else { '' }
        }
        foreach ($metric in $metricNames) {
            $row["${metric}Calls"] = Get-Long $fields "$metric.calls"
            $row["${metric}Items"] = Get-Long $fields "$metric.items"
            $row["${metric}Hits"] = Get-Long $fields "$metric.hits"
            $row["${metric}Misses"] = Get-Long $fields "$metric.misses"
            $row["${metric}TotalMs"] = Get-Double $fields "$metric.totalMs"
            $row["${metric}MaxMs"] = Get-Double $fields "$metric.maxMs"
        }
        $rows.Add([pscustomobject]$row)
    }
}

if ($rows.Count -eq 0) {
    throw 'No PERF_SUMMARY records found.'
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$detailPath = Join-Path $OutputDirectory ("perf-summary-$stamp.csv")
$rows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath $detailPath

function Get-Percentile([hashtable]$histogram, [long]$total, [double]$percentile) {
    if ($total -le 0) { return 0 }
    $target = [Math]::Ceiling($total * $percentile)
    if ($target -lt 1) { $target = 1 }
    $seen = 0L
    foreach ($key in ($histogram.Keys | Sort-Object {[int]$_})) {
        $seen += [Int64]$histogram[$key]
        if ($seen -ge $target) { return [int]$key }
    }
    return 250
}

$aggregateRows = New-Object System.Collections.Generic.List[object]
foreach ($group in ($rows | Group-Object { "$($_.role)|$($_.sessionId)|$($_.buildHash)" })) {
    $first = $group.Group[0]
    $histogram = @{}
    $frameCount = 0L
    $frameAverageTotal = 0d
    foreach ($row in $group.Group) {
        $frameCount += $row.frameCount
        $frameAverageTotal += $row.frameAvgMs * $row.frameCount
        if ([string]::IsNullOrWhiteSpace($row.frameHist)) { continue }
        foreach ($pair in $row.frameHist.Split(',')) {
            $separator = $pair.IndexOf(':')
            if ($separator -le 0) { continue }
            $bin = $pair.Substring(0, $separator)
            $count = 0L
            [Int64]::TryParse($pair.Substring($separator + 1), [ref]$count) | Out-Null
            if (-not $histogram.ContainsKey($bin)) { $histogram[$bin] = 0L }
            $histogram[$bin] += $count
        }
    }

    $aggregate = [ordered]@{
        buildHash = $first.buildHash
        sessionId = $first.sessionId
        role = $first.role
        scene = $first.scene
        intervalCount = $group.Count
        frameCount = $frameCount
        frameAvgMs = if ($frameCount -gt 0) { [Math]::Round($frameAverageTotal / $frameCount, 3) } else { 0 }
        frameP50Ms = Get-Percentile $histogram $frameCount 0.50
        frameP95Ms = Get-Percentile $histogram $frameCount 0.95
        frameP99Ms = Get-Percentile $histogram $frameCount 0.99
    }
    foreach ($metric in $metricNames) {
        $aggregate["${metric}Calls"] = ($group.Group | Measure-Object -Property "${metric}Calls" -Sum).Sum
        $aggregate["${metric}Items"] = ($group.Group | Measure-Object -Property "${metric}Items" -Sum).Sum
        $aggregate["${metric}Hits"] = ($group.Group | Measure-Object -Property "${metric}Hits" -Sum).Sum
        $aggregate["${metric}Misses"] = ($group.Group | Measure-Object -Property "${metric}Misses" -Sum).Sum
        $aggregate["${metric}TotalMs"] = [Math]::Round(($group.Group | Measure-Object -Property "${metric}TotalMs" -Sum).Sum, 3)
        $aggregate["${metric}MaxMs"] = [Math]::Round(($group.Group | Measure-Object -Property "${metric}MaxMs" -Maximum).Maximum, 3)
    }
    $aggregateRows.Add([pscustomobject]$aggregate)
}

$aggregatePath = Join-Path $OutputDirectory ("perf-aggregate-$stamp.csv")
$aggregateRows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath $aggregatePath
Write-Host "Detailed summaries: $detailPath"
Write-Host "Aggregated results: $aggregatePath"
