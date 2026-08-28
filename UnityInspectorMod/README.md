# Unity Inspector Mod 构建

这个目录已经配置为使用当前本机 Broforce、UMM 和已安装 Unity Inspector Mod 包中的程序集构建。

在 PowerShell 中运行：

```powershell
Set-Location 'D:\Study\C#\Broforce-Mods\UnityInspectorMod'
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

本地路径写在 `LocalBroforcePath.props` 中。该文件包含机器相关路径，不应提交到公共仓库；模板是 `LocalBroforcePath.props.example`。

## 运行和连接

1. 启动 Broforce。
2. 在 UMM 中启用 `Unity Inspector Mod`。
3. 确认 Mod 面板显示 `TCP Server Status: Running`，端口为 `9999`。
4. 启动 `Broforce_src\unity-inspector-mcp`，或让 Codex/其它 MCP 客户端按配置自动启动它。

Windows 下可以先检查端口：

```powershell
Test-NetConnection 127.0.0.1 -Port 9999
```

MCP 连接的是当前正在运行的 Broforce 客户端，只能读取这一台机器的游戏状态；房主退出后，无法继续读取已退出的远程客户端。

## 日志

Unity Inspector Mod 的连接、脚本和运行时错误仍会写入 UMM 日志：

```text
<r2modman profile>\UMM\Core\Log.txt
```

当前本机路径为：

```text
E:\SteamLibrary\steamapps\common\Broforce\r2mod\Broforce\profiles\Broforce\UMM\Core\Log.txt
```

MCP 自身的连接信息通过标准错误输出返回给 MCP 客户端。MCP 的 `read_log` 和 `watch_log` 目前默认查找 `Default` profile；如果使用 `profiles\Broforce` 等自定义 profile，应直接收集实际的 `Core\Log.txt`。

该 Mod 的 TCP 服务没有认证，并默认监听所有网卡。只应在可信的本机或局域网环境中使用，不要将 `9999` 端口暴露到公网。
