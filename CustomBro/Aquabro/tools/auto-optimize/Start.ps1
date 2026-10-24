param([ValidateSet('start','stop','status','check')][string]$Action = 'start')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$stateRoot = Join-Path $projectRoot 'archive/auto-optimize'
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
$python = (Get-Command python -CommandType Application -ErrorAction Stop).Source
$controller = Join-Path $PSScriptRoot 'controller.py'
$env:PYTHONIOENCODING = 'utf-8'
$env:PYTHONUTF8 = '1'
if ($Action -ne 'start') {
    & $python $controller $Action
    if ($LASTEXITCODE -ne 0) { throw '自动精简操作失败，请查看输出。' }
    return
}
& $python $controller check
if ($LASTEXITCODE -ne 0) { throw '请先启动 Codeg，再运行自动精简。' }
$log = Join-Path $stateRoot ('controller-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$errors = $log + '.stderr.log'
$quoted = '"' + $controller + '" start'
$process = Start-Process -FilePath $python -ArgumentList $quoted -WorkingDirectory $projectRoot -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $errors -PassThru
Write-Output ('已在后台启动自动精简，PID=' + $process.Id)
Write-Output ('日志：' + $log)
Write-Output '目标：减小最终 DLL；每轮至少减少512字节，机制及完整蒙版保持一致。'
Write-Output '每轮新对话和重试均显式使用思考等级 max。'
Write-Output '每轮会出现在 Codeg 对话列表。停止入口会取消并恢复当前未验收轮次。'
