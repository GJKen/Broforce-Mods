# TestMod 分辨率一键切换

日期：2026-11-10

状态：已实现并部署；实机验收待完成。

## 需求

在 TestMod 设置中列出游戏当前可设置的分辨率，玩家点击对应按钮即可切换。

## 实现

- 新增独立“分辨率”设置页，读取 `Screen.resolutions`。
- 按宽高合并重复项；分辨率切换沿用游戏三参数 `Screen.SetResolution`，不单独选择刷新率。
- 按钮保留当前窗口或全屏模式，并把宽高写入游戏 `PlayerOptions` 后保存，供游戏下次启动恢复。
- 使用滚动区域容纳不同显示器提供的分辨率列表，并标记当前分辨率。

## 修改范围

- `src/SettingsUI.cs`
- `src/Plugin.cs`
- `README.md`
- `issues/README.md`
- `issues/ISSUES-2026-11-10-TestMod分辨率一键切换.md`
- `Release/UMM/Mods/TestMod/TestMod.dll`

## 构建与部署

- `BuildAndDeploy.ps1 -SkipDeploy` 构建成功；编译仍有一条既有警告：`TestModBehaviour.cs` 使用的 `Application.loadedLevelName` 已过时。
- 部署前本机没有运行 Broforce；5700G 的停止接口返回 `Game stopped`。随后 `BuildAndDeploy.ps1` 成功部署到本机和 5700G。
- 发布目录及两端安装 DLL 的 SHA-256 均为 `2F2074E159000890103D3C413C376BB1B2FF3ECBD587B4145A152A0B38C4D381`。

## 待验收

| 场景 | 验收标准 | 状态 |
| --- | --- | --- |
| 列表显示 | 显示当前显示器支持的分辨率，同宽高不重复，当前分辨率可识别 | 待测 |
| 点击切换 | 选择后游戏立即切换，窗口/全屏模式保持不变 | 待测 |
| 重启恢复 | 选择的宽高保存到游戏选项，重启后仍生效 | 待测 |

本轮未启动游戏。实机验收通过前，本功能状态维持“已实现并部署，待验收”。
