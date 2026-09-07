# Workshop 缺图运行时提示未按语言切换

## 当前现象

加入方在 UMM 中将 Mod 语言设置为 English 后，未订阅房主 Workshop 地图时，运行时仍显示中文提示。

## 根因

`src/HarmonyDiagnostics.WorkshopIdentity.cs` 在 `TrySynchronizeClientWorkshopIdentity` 和 `ShouldBlockMissingWorkshopLoad` 两处直接写死中文提示。

`src/SettingsUiText.cs` 的中英文资源只用于 UMM 设置页，没有被运行时 Workshop 通知使用。因此 UMM 语言设置不会影响缺图提示。

## 修复方案

1. 在 `SettingsUiText` 增加“房主地图未订阅”运行时提示字段，分别提供中文和英文资源。
2. 中文使用指定文案：

   `房主使用的 Steam 创意工坊地图 ID 为 {id}，但本机尚未订阅。请先在 Steam 创意工坊订阅地图，重启游戏后，再重新加入房间。`

3. 英文使用对应文案：

   `The host is using Steam Workshop map ID {id}, but the map is not subscribed on this local machine. Please subscribe to the map in the Steam Workshop, restart the game, and then rejoin the room.`

4. 在 `HarmonyDiagnostics.WorkshopIdentity.cs` 提供统一的运行时提示生成入口，根据 `Plugin.Settings.SettingsLanguage` 使用现有 `SettingsUiLocalization`，两处显示提示都调用同一入口。

## 验收重点

- UMM 设置为 English 后，缺图运行时提示为英文。
- UMM 设置为中文后，显示上述指定中文文案。
- 提示中的 Workshop ID 与房主发布的地图 ID 一致。

## 实现记录（2026-09-04）

- `src/SettingsUiText.cs` 增加房主地图未订阅提示的中英文资源。
- `src/HarmonyDiagnostics.WorkshopIdentity.cs` 增加统一提示生成入口，按 `Plugin.Settings.SettingsLanguage` 调用 `SettingsUiLocalization`，身份同步和缺图阻止共用该入口。
- 中文和英文文案均使用房主发布的 Workshop ID 替换 `{id}` 占位符。
- 已使用 .NET Framework 3.5 Release 构建脚本编译通过。

## 验收记录（2026-09-04）

- 双端实测确认：UMM 设置为 English 和中文时，缺图运行时提示均按对应语言正确显示。
