# Custom Map Multiplayer：构建、部署与逆向

[返回开发文档索引](DEVELOPMENT.md)

## 构建与部署

构建或部署前必须读取项目根目录的 `LocalBroforcePath.props`：

1. `BroforceManagedPath` 是本机 Broforce `Broforce_beta_Data/Managed` 目录，其中必须含 `UnityEngine.TextRenderingModule.dll`。
2. `UnityModManagerPath` 是含 `UnityModManager.dll` 和 `0Harmony.dll` 的本机 UMM 核心目录。
3. `TestDeployModPath` 是本机测试机部署目录；值为空表示明确关闭额外测试部署。
4. 该文件包含本机专用路径，只允许用于执行构建或部署，不得写入公开文件、提交信息、日志摘录或对外回复。
5. 使用兼容 .NET Framework 3.5 的编译器。当前验证路径：`C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe`；不要直接使用 v4 编译器。

唯一标准入口：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

仅生成项目安装包而不复制到本机或内网 UMM 目录时，使用：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1 -Configuration Release -SkipDeploy
```

有效输出位置：

```text
<项目根目录>\Release\UMM\Mods\CustomMapMultiplayer\CustomMapMultiplayer.dll
<本机 UMM_PROFILE_DIR>\Mods\GJKen-CustomMapMultiplayer\CustomMapMultiplayer\CustomMapMultiplayer.dll
```

脚本输出 `Release\CustomMapMultiplayer.zip` 并嵌入 `Build hash`，覆盖 `Release\UMM\Mods\CustomMapMultiplayer` 下的 DLL；项目安装包固定包含顶层 `manifest.json`、`README.md`、`icon.png`，以及 UMM 子目录中的 `Info.json`。部署目标的 `Info.json` 每次均从 `modinfo.json` 同步，DLL 程序集版本也从该文件的版本生成。若配置了可选测试部署目标，目录创建或复制失败时整个部署失败，不得继续双端测试。

`CustomMapMultiplayer.csproj` 的 `OutputPath` 也指向 `Release\UMM\Mods\CustomMapMultiplayer`；`bin\Debug` 旧文件不得用于测试。IDE/MSBuild 只有正确读取本机 props 并执行构建后目标时才可替代脚本。

## 安装包结构

```text
Release\
  manifest.json
  icon.png
  README.md
  UMM\Mods\CustomMapMultiplayer\
    CustomMapMultiplayer.dll
    Info.json
```

ZIP 导入到 r2modman 后，插件 DLL 位于 `UMM\Mods\CustomMapMultiplayer`，安装后的外层目录名由包标识决定。程序集名保持 `CustomMapMultiplayer.dll`，程序集版本与 `modinfo.json` 的版本同步（例如 `0.5.0.0`）。

## 逆向参考

使用 dnSpy、ILSpy 等工具读取：

```text
<BROFORCE_DIR>\Broforce_beta_Data\Managed\Assembly-CSharp.dll
```

- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce%27s-Code)
- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [BroMaker Abilities Wiki](https://github.com/alexneargarder/Bro-Maker-Abilities-Wiki/wiki)

## 相关约定

- `LocalBroforcePath.props` 包含机器专用路径，不应提交。
- 标准构建完成后只做基本结果确认；未通过脚本构建的 DLL 不用于正式双端验收。
