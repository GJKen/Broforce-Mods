# 关闭 Workshop 注入后恢复官方地图

## 状态

已修复、完成标准构建与双端部署，并由用户确认关闭注入后可以正常返回官方地图。

## 现象

双方关闭 UMM 中的 `Inject configured workshop map into online level switching` 后，后续联机选图仍可能进入 `Test Evan2`，无法恢复官方战役。保存文件中的 `EnableOnlineWorkshopInjection` 已经是 `false`，因此不是复选框没有保存。

## 根因

旧实现只在下一次注入前读取开关。已经发生注入时，运行态仍保留 `_injectedForSession=true`，并已写入 `GameState.loadCustomCampaign=true`、`customLevelID` 和 `sceneToLoad=Test Evan2`，同时清空了官方 `LevelSelectionController.CurrentCampaign`。关闭复选框没有反向清理这些值。

Steam `LeaveMatch` 也只清理房间身份，没有像 FRP 退出路径一样清除 Workshop 切关、暂停和地图状态。此外，新建或加入 Steam 房间时会无条件清空 `CurrentCampaign`，即使注入已经关闭，导致原本用于恢复官方流程的新房间仍可能缺少原生战役状态。

## 修复

- Workshop 注入复选框改为边沿触发；从开启切到关闭时立即保存设置并调用统一清理。
- 停用或卸载整个 UMM Mod 时执行同一清理。
- Steam `LeaveMatch` 与 FRP 退出使用等价的 Workshop 运行态清理范围。
- 清理范围包括会话标记、Lobby 地图身份、晚加入和对象同步缓存、重复加载抑制、关卡号覆盖、`GameModeController` 待切关状态、`GameState` 自定义战役字段、`LevelSelectionController` Workshop 字段、网络暂停和遗留输入暂停。
- `IsWorkshopOnlineSession` 同时要求总开关开启；开关关闭后不再因为默认场景名或遗留阶段误启用 Workshop 专用补丁。
- 新会话只在配置有效或检测到本 Mod 的注入残留时清空自定义战役状态；普通官方联机不再无条件清空 `CurrentCampaign`。
- 保留 Steam `JoinLobby` 内部清理用 `LeaveMatch` 的保护，避免把加入过程误当成真实离房。

热关闭不会强制中断或切换当前场景。关闭后退出当前房间，返回菜单并新建官方房间，即可由原生世界地图重新建立官方战役。已保存的 Workshop ID 可以保留，开关关闭时不会参与官方联机选图。

## 验证

- `buildHash=9520bd6f64881db71fa6fbc6f94d5547c9d216f3b18aaa4fceedfedc87bd1eb9`。
- DLL 大小为 `233472` 字节，SHA-256 为 `1B9407952B78C7A1BF97E3704A73C54297F69CD69775C8CFD955DD8CC12AF3B2`。
- .NET Framework 3.5 Release 编译通过。
- 项目包、本机 UMM 和内网端 UMM 三份 DLL 的大小和 SHA-256 一致。
- `git diff --check` 通过。
- 用户重启并实测后确认问题消失，可以正常进入官方地图。
