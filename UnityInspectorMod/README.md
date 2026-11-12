# Unity Inspector Mod

> [English](README.en.md)

这个目录包含 Unity Inspector Mod 的源码、构建脚本和本地配置模板。Mod 为 Broforce 提供 TCP 检查和运行时调试接口，供 `unity-inspector-mcp` 连接。

## 前置条件

- Windows、已安装的 Broforce 和 Unity Mod Manager（UMM）；
- 已安装的 Unity Inspector Mod 包，其中包含 `mcs.dll` 和 `Newtonsoft.Json.dll`。这些运行时 DLL 不提交到本仓库；
- 如果依赖包不在当前 UMM profile 的默认 Mods 目录中，需要在本地配置中设置 `InspectorModDependenciesPath`。

## 配置与构建

在仓库根目录的 PowerShell 中运行以下命令，先创建本机配置，再填写实际路径：

```powershell
Set-Location '.\UnityInspectorMod'
Copy-Item .\LocalBroforcePath.props.example .\LocalBroforcePath.props
notepad .\LocalBroforcePath.props
```

`LocalBroforcePath.props` 必须包含 `BroforceManagedPath` 和 `UnityModManagerPath`。如果脚本无法自动找到 `mcs.dll` 和 `Newtonsoft.Json.dll`，取消注释并填写 `InspectorModDependenciesPath`。该文件包含机器相关路径，不应提交到公共仓库。

配置完成后构建并部署：

```powershell
& .\BuildAndDeploy.ps1
```

脚本会完成以下工作：

- 使用 `LocalBroforcePath.props` 指向的当前游戏和 UMM DLL 编译 Mod；
- 从 `InspectorModDependenciesPath` 或已安装的 Unity Inspector Mod 包读取 `mcs.dll` 和 `Newtonsoft.Json.dll`；
- 在 `UnityInspectorMod` 目录生成可复制的 Mod 包；
- 部署到当前配置的 `UMM\Mods\Unity Inspector Mod`。

源码位于 `src\`。`libs\` 仅作为旧版本本地依赖缓存保留，不再参与标准构建，也不提交到 Git。

只编译、不部署：

```powershell
& .\BuildAndDeploy.ps1 -SkipDeploy
```

## 运行与 MCP

Mod 面板默认使用中文；顶部点击 `English` / `中文` 按钮可即时切换语言并保存选择，下次启动仍使用所选语言。状态、连接设置和错误提示随语言切换，正在运行的服务和连接不受影响。下文使用英文界面名称，中文界面对应“启动服务”“停止服务”“应用连接设置”。

1. 启动 Broforce。
2. 在 UMM 中启用 `Unity Inspector Mod`。
3. 确认 Mod 面板显示 `TCP Server Status: Running`，默认端口为 `9999`。状态为 `Stopped` 时，在面板中点击 `Start Server`；默认会在 Mod 加载时自动启动。
4. 在 `Broforce_src\unity-inspector-mcp` 中安装 Node.js 18+ 依赖并配置 MCP 客户端。具体步骤见 [MCP README](../Broforce_src/unity-inspector-mcp/README.md)；客户端已配置该服务时，不要再手动启动第二个实例。

在 Mod 面板的 `Port (1-65535)` 输入框填写端口，点击 `Apply Settings` 即可在游戏运行期间切换，无需重启游戏。输入框只保留 ASCII 数字，应用时检查 1–65535 范围。输入过程不影响当前服务；切换到无效或已被占用的新端口时会显示错误，并保留原监听和连接。成功切换端口会断开旧连接并立即保存设置；服务停止时应用只保存，点击 `Start Server` 才开始监听。

MCP 提供 `configure_connection` 工具，可直接切换目标，无需重启客户端。例如游戏改为 `19999` 后，调用该工具并传入：

```json
{"host":"127.0.0.1","port":19999,"token":"","testConnection":true}
```

`connection_status` 可查看当前目标和连接状态，不返回口令。工具修改在当前 MCP 进程内有效；需要重启后保留时，配置环境变量 `UNITY_INSPECTOR_HOST`、`UNITY_INSPECTOR_PORT`、`UNITY_INSPECTOR_TOKEN`。游戏监听设置和 MCP 目标需要分别应用；面板显示的是游戏实际监听地址和端口。

默认只监听 `127.0.0.1`。需要局域网或 TCP 内网穿透时，勾选“允许局域网/穿透连接”，填写连接口令后应用；此时监听 `0.0.0.0`，MCP 的 `token` 必须一致。本机连接也可选配口令。修改口令会断开旧连接；同端口切换监听地址需要重启监听，失败时尝试恢复原监听，已有连接需要重连。口令按 UMM 设置保存在本机 XML 中，不应提交到 Git。

Windows 下可以先检查端口（修改后替换示例中的 `9999`）：

```powershell
Test-NetConnection 127.0.0.1 -Port 9999
```

MCP 只会连接当前指定机器上正在运行的 Broforce；局域网中的另一台客户端需要单独配置 MCP 目标。它支持检查和运行时调试操作，并非只读接口；房主退出后，无法继续检查该已退出的客户端。

## 日志

Unity Inspector Mod 的连接、脚本和运行时错误仍会写入 UMM 日志：

```text
<r2modman profile>\UMM\Core\Log.txt
```

MCP 自身的连接信息通过标准错误输出返回给 MCP 客户端。MCP 的 `read_log` 和 `watch_log` 目前默认查找 `Default` profile；如果使用 `profiles\Broforce` 等自定义 profile，应直接收集实际的 `Core\Log.txt`。

口令认证不提供传输加密；跨公网调试建议通过 Tailscale、ZeroTier 或其他加密隧道连接。

## 调度、清理与验证

- TCP 请求按 ID 匹配响应，支持并发、分包、合包和 UTF-8 中文；超时后的迟到响应会丢弃，不会交给下一条请求。
- 对象查询、修改和脚本状态操作在 Unity 主线程执行；截图只把捕获操作排到主线程，文件等待在后台完成。脚本编译也与主线程上的表达式编译串行，避免共用 Mono 编译器状态发生竞争。
- 队列最多 256 项，每帧最多开始 32 项任务，调度预算 4ms；单个任务一旦开始不会被强行中断。排队超时、连接断开或禁用 Mod 会取消未开始任务；已经开始的任务超时会明确说明状态，重试前应核对游戏状态。
- 每个监听最多 8 个连接，请求上限 1 MiB；空闲读超时 120 秒。连接关闭后，MCP 下一次调用会重新连接。
- 禁用或卸载 Mod 会关闭连接、释放模拟按键和计时器、卸载动态脚本的补丁与跟踪对象，并清理本 Mod 的补丁和调度对象。动态编译程序集本身随游戏进程退出释放。

在本目录运行 `& .\tests\Test-PortSwitch.ps1` 验证端口、界面、调度和清理行为；在 MCP 目录运行 `npm test` 验证通信和 stdio 工具接口。
