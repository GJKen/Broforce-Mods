# Workshop 地图选择与联机加载流程优化

## 当前实现

### UMM 设置页

- UMM 左侧导航中的 `Workshop 地图配置` 是独立页面，`多人游戏选项` 只保留玩家重叠近战和自动 AFK 设置。
- Workshop 配置页左侧显示注入开关、房主/加入方行为、本地目录状态和高级地图设置；右侧提供手工 `Workshop ID` 输入和地图选择入口。
- 手工 Workshop ID 始终可用。选择目录项只写入 `Settings.WorkshopId`，不会覆盖 `WorkshopCampaignName` 或 `WorkshopSceneName`。
- 加入方仍采用房主发布的 Workshop 地图身份，本地填写的 ID 不会覆盖房主配置。

### Workshop 地图目录

- `WorkshopMapDirectory` 读取 Steam 已订阅项目，保留未安装、未完整下载和战役不可读项目，并显示对应本地状态。
- 地图标题通过 Steam Workshop 详情查询读取；原生订阅菜单的名称结果也会合并到标题缓存。
- 标题读取失败时仍显示完整 Workshop ID。刷新只在首次打开页面或选择页手动刷新时触发。
- Mod 不自动订阅、搜索社区或主动下载地图；实际下载继续由 Broforce/Steam 原生流程处理。

### 地图选择页

- 点击“选择已订阅地图”后，整个 Workshop 地图配置内容区切换为地图选择页，不使用 `GUI.Window` 或屏幕坐标 `GUILayout.BeginArea` 浮层。
- 选择页提供搜索、`全部`、`最近联机地图`、`已标星`筛选、刷新和收藏功能。`全部`和`已标星`按钮显示当前结果数量，`最近联机地图`不显示数量；当前处于`已标星`且存在星标地图时，标签旁显示 `×`，点击会清空全部本地星标，不会取消 Steam 订阅，其他标签不显示该按钮。
- 地图采用左右两列网格排列；标题保持单行，Workshop ID 使用独立颜色显示，已选地图使用橙色边框和选择状态提示。
- 右上角 `X` 返回 Workshop 地图配置页；按 `Esc` 也可以返回。点击地图后只保存 Workshop ID，仍停留在选择页，不自动返回配置页。
- 地图卡片会显示本地安装、未完整下载或战役文件不可读等状态；手工输入的不在订阅目录中的 ID 仍可使用。

### 最近联机地图

- “最近联机地图”只在 Workshop 地图实际完成加载后记录，不再按点击选择记录，也不限制保存数量。
- 设置版本迁移会清空旧版本按“点击选择”记录的历史，之后重新根据完成加载的地图建立列表。

## 维护契约与关键实现索引

### 为什么不能使用屏幕坐标浮层

UMM 的 `UI.DrawTab` 会在主窗口的 `GUILayout.BeginScrollView` 内调用 Mod 的 `OnGUI`。因此 Mod 内部的 `GUI.Window` 会形成嵌套窗口，`GUILayout.BeginArea` 也不能脱离当前 GUIClip；这类实现会把内容错误绘制到 UMM 左上角并覆盖其它 Mod。后续维护不得把地图选择器改回屏幕坐标浮层，应继续使用 UMM 当前内容区的页面状态切换。

### 页面状态和写入边界

- `Plugin.DrawWorkshopSettings` 根据 `_workshopMapSelectionOpen` 在配置页和选择页之间二选一；选择页不是配置页下方的额外区域。
- `Plugin.OpenWorkshopMapSelection` 打开页面时重置搜索、筛选和滚动位置；`Plugin.DrawWorkshopMapSelectionPage` 处理右上角 `X` 和 `Esc` 返回。
- `Plugin.SelectWorkshopMap` 只能执行 `Settings.WorkshopId = item.WorkshopId.ToString()` 和保存设置。不得在这里关闭选择页，也不得写入 `WorkshopCampaignName` 或 `WorkshopSceneName`。
- 配置页只提供手工 ID 和“选择已订阅地图”入口；刷新按钮属于选择页，不能重新放回配置页。

### 目录、标题和状态来源

- `WorkshopMapDirectory.Refresh` 先通过 `HarmonyDiagnostics.TryGetSubscribedWorkshopItems` 枚举 Steam 已订阅项目，再通过 `HarmonyDiagnostics.GetWorkshopMapLocalState` 判断 `InstalledReadable`、`NotInstalled` 和 `CampaignUnreadable`。
- `WorkshopMapDirectory.PatchNativeWorkshopCampaignEntries` 用于接收原生订阅菜单返回的名称并合并标题缓存；分页 Workshop 详情查询是补充路径，不是社区搜索入口。
- 标题为空、查询失败或查询仍在进行时，必须保留完整数字 Workshop ID，不能因此从目录中删除项目。

### 选择页布局不变量

- `Plugin.GetWorkshopMapSelectionItems` 的筛选语义为：`0` 全部、`1` 最近联机地图、`2` 已标星；搜索同时匹配标题和数字 ID。
- `Plugin.DrawWorkshopMapSelectionPage` 使用固定的两列网格。计算卡片宽度时必须扣除内容面板内边距、垂直滚动条和列间距，避免再次出现横向滚动条。
- `Plugin.DrawWorkshopMapGridItem` 使用固定卡片高度；地图标题必须保持单行并裁剪，不能让长标题改变网格行高。
- `Plugin.IsWorkshopMapSelected` 以当前 `Settings.WorkshopId` 与卡片 ID 比较决定橙色选中样式；选中状态和选择页顶部文字都必须同步显示。
- `全部`和`已标星`按钮可以显示当前结果数，`最近联机地图`不显示数量。不要重新加入已取消的数量文本。
- 仅在当前筛选为`已标星`且星标列表非空时显示批量清除按钮；批量清除只清空 `Settings.WorkshopFavoriteIds`，不触碰 Steam 订阅和地图目录。

### 最近联机地图和设置迁移

- `HarmonyDiagnostics.WorkshopCache.WorkshopLevelLoadCompletePostfix` 在待处理 Workshop 加载完成后调用 `Plugin.RecordWorkshopMapUsage`；不要在点击卡片的 `SelectWorkshopMap` 中记录最近使用。
- `Plugin.AddRecentWorkshopMap` 只去重并把最新完成加载的 ID 放到首位，不得恢复旧的固定数量上限。
- `DiagnosticSettings` 持久化 `WorkshopFavoriteIds` 和 `WorkshopRecentIds`。当前 `Plugin.CurrentDiagnosticSettingsVersion` 为 `13`；版本小于 `13` 时清空旧的点击选择历史，然后从后续完成加载事件重新建立最近联机地图列表。

### 联机流程保护

地图目录和选择页只能改变本地设置展示与 `WorkshopId`。不得用本地缓存战役替换原生 `SteamController.LoadLevel`，也不得提前调用 `OnLevelLoadComplete` 绕过原生加载阶段；Workshop 身份同步、Steam 下载、房主/加入方身份、房间创建/加入、主机迁移和其它联机 RPC 流程仍由原生或既有联机逻辑处理。加入方始终采用房主发布的 Workshop ID，本地保存的 ID 不能覆盖房主配置。

### 本轮联机加载精简（2026-09-09）

本 issue 的 Workshop 地图目录和选择页实验曾引出联机加载时序回归。精简前，Mod 在本地缓存命中时会阻止原生 `SteamController.LoadLevel`，再直接反射调用 `OnLevelLoadComplete`，可能跳过房间内 P1-P4 确认和原生地图加载过场。

精简前的路径：

```text
SteamController.LoadLevel
    -> Mod 检查本地缓存
    -> 缓存命中后阻止原生加载
    -> Mod 直接调用 OnLevelLoadComplete
    -> 可能跳过 P1-P4 和加载动画
```

本轮精简后的路径：

```text
SteamController.LoadLevel
    -> Mod 只记录 Workshop ID
    -> 继续执行 Broforce 原生流程
    -> 显示并确认 P1-P4
    -> 播放原生地图加载动画
    -> 进入地图
```

- 联机 Workshop 的 `SteamController.LoadLevel` 始终交给原生流程处理，不再用缓存战役直接完成加载。
- Workshop UGC 详情回调不再读取缓存战役并替换原生结果。
- 保留原生 `OnLevelLoadComplete` 后置记录，用于记录实际完成加载的最近 Workshop 地图。
- 保留房主退出后拦截过期 `LoadLevel` 请求的保护。
- 这不是地图加载加速，而是减少 Mod 对原生联机时序的干预；地图实际加载继续由 Broforce/Steam 原生流程负责。

## 联机行为边界

- 房主创建房间时使用当前选中的 Workshop 地图；加入方进入房间后使用房主发布的 Workshop ID。
- 不改变 Workshop 加载、Steam 下载、房主身份、加入方身份和现有联机流程。
- 所有玩家仍需使用相同的 Mod 构建，并订阅同一张地图；Mod 不代替玩家完成订阅或下载。

## 验收边界

- 地图选择页的最新运行画面仍未进行新进程 MCP 画面验收；本轮已单独完成联机 Workshop 加载时序实机验收。
- 用户实机测试确认：创建房间后显示 P1-P4，确认后才进入第三方地图，原生加载过场动画正常保留，没有再次直接进入地图。
- 本轮未新增 MCP 截图或双端日志，以上结论以用户实际运行结果为依据。

## 构建状态

- 当前 Release `buildHash=71c81fb162edbadc47809db14fa12676b93b56565c314b5dea34cd404c4ac337`。
- 当前 Release DLL SHA-256 为 `8A4BFE125D6EBFFDEAB9F55DA965EDDA919EEC87770880CA2F2297F4FF105B87`。

## 相关源码

- `src/Plugin.cs`
- `src/WorkshopMapDirectory.cs`
- `src/HarmonyDiagnostics.WorkshopCache.cs`
- `src/HarmonyDiagnostics.WorkshopIdentity.cs`
- `src/HarmonyDiagnostics.cs`
- `src/DiagnosticSettings.cs`
- `src/SettingsUiText.cs`
