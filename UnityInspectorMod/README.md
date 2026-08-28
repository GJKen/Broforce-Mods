# Unity Inspector Mod

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

1. 启动 Broforce。
2. 在 UMM 中启用 `Unity Inspector Mod`。
3. 确认 Mod 面板显示 `TCP Server Status: Running`，端口为 `9999`。状态为 `Stopped` 时，在面板中点击 `Start Server`；默认会在 Mod 加载时自动启动。
4. 在 `Broforce_src\unity-inspector-mcp` 中安装 Node.js 18+ 依赖并配置 MCP 客户端。具体步骤见 [MCP README](../Broforce_src/unity-inspector-mcp/README.md)；客户端已配置该服务时，不要再手动启动第二个实例。

Windows 下可以先检查端口：

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

该 Mod 的 TCP 服务没有认证，并默认监听所有网卡。只应在可信的本机或局域网环境中使用，通过防火墙限制 `9999` 的访问来源，且不要将该端口暴露到公网。
