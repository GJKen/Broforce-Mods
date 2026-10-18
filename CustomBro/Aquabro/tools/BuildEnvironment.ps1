# 本机配置不进入 Git；工具脚本共享同一组路径设置。
function Get-AquabroBuildSettings {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [string]$AsepritePath,
        [string]$ReferenceDirectory,
        [string[]]$DeployDirectories
    )
    $configPath = Join-Path $ProjectRoot 'LocalBuild.json'
    $config = if (Test-Path -LiteralPath $configPath) {
        Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    } else { $null }
    if (-not $AsepritePath) { $AsepritePath = $env:AQUABRO_ASEPRITE_PATH }
    if (-not $AsepritePath -and $config) { $AsepritePath = $config.AsepritePath }
    if (-not $AsepritePath) { $AsepritePath = 'aseprite' }
    if (-not (Test-Path -LiteralPath $AsepritePath -PathType Leaf)) {
        $command = Get-Command $AsepritePath -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $command) { throw '找不到 Aseprite，请设置 -AsepritePath、AQUABRO_ASEPRITE_PATH 或 LocalBuild.json。' }
        $AsepritePath = $command.Source
    }
    $AsepritePath = (Resolve-Path -LiteralPath $AsepritePath).ProviderPath
    if (-not $ReferenceDirectory -and $config) { $ReferenceDirectory = $config.ReferenceDirectory }
    if (-not $ReferenceDirectory) { $ReferenceDirectory = '.aquabro-buildrefs' }
    if (-not [System.IO.Path]::IsPathRooted($ReferenceDirectory)) {
        $ReferenceDirectory = Join-Path $ProjectRoot $ReferenceDirectory
    }
    if (-not $DeployDirectories -and $config) { $DeployDirectories = @($config.DeployDirectories) }
    [pscustomobject]@{
        AsepritePath = $AsepritePath
        ReferenceDirectory = $ReferenceDirectory
        DeployDirectories = @($DeployDirectories | Where-Object { $_ })
    }
}

function New-AquabroCompilerResponse {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$ReferenceDirectory,
        [Parameter(Mandatory = $true)][string]$OutputDirectory,
        [Parameter(Mandatory = $true)][string]$GeneratedDirectory
    )
    $response = [System.IO.File]::ReadAllText((Join-Path $ProjectRoot 'aquabro-build.rsp'))
    $response = $response.Replace('@PROJECT_ROOT@', $ProjectRoot)
    $response = $response.Replace('@REFERENCES_ROOT@', $ReferenceDirectory)
    $response = $response.Replace('@OUTPUT_ROOT@', $OutputDirectory)
    $response = $response.Replace('@GENERATED_ROOT@', $GeneratedDirectory)
    if ($response -match '@[A-Z_]+@') { throw '编译响应文件存在未展开的路径占位符。' }
    return $response
}
