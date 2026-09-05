# ISSUES-2026-09-05 联机聊天框 Esc 后无法再次呼出

## 状态

**已修复，联机房间人工回归通过。**

## 现象与根因

联机地图中执行 `Enter -> Esc -> Esc -> Enter` 后，聊天输入框无法再次呼出；修复前重新打开后还无法用 `Enter` 发送文字，只能用 `Esc` 退出。

实测原生执行顺序为：

```text
KeyboardInput.Update -> ChatTextBox.Update -> PauseController.Update
```

`KeyboardInput.Update` 处理聊天状态时会清除 `KeyboardInput.open`，随后 `PauseController.Update` 仍处理同一枚 `Esc` 并进入暂停。返回游戏后 `PauseMenu.MenuActive` 可能仍为 `true`，而原生 `KeyboardInput.Update` 在 `PauseMenu.instance` 存在时要求菜单激活且聊天已打开才执行 `Toggle()`，导致后续 `Enter` 被吞掉。聊天关闭超过 6 秒后，`ChatTextBox` 目标自然移到屏外，加重了表象。

## 修复

- 新增 `src/HarmonyDiagnostics.Chat.cs`。
- 联机且游戏处于 `UnPaused` 时，聊天关闭的 `Enter` 和聊天打开后的发送 `Enter` 都调用原生 `KeyboardInput.Toggle()`。
- `PauseController.TogglePause` 返回 `UnPaused` 后同步清除残留的 `PauseMenu.MenuActive`。
- 未修改 `ChatTextBox.target`，未全局设置 `ForceOnScreen`，未清除 `InputReader.IsBlocked`，未替换 `Assembly-CSharp.dll`。

## 验证

本次在真正的联机房间验证通过：

- `Enter` 可打开聊天输入框。
- 输入文字后按 `Enter` 可正常发送。
- 聊天中按 `Esc` 可退出，不再留下导致后续失效的状态。
- 再次执行 `Enter -> Esc -> Esc -> Enter` 后，聊天框仍可呼出。
- 未再复现“无法再次呼出”或“只能通过 Esc 退出输入框”。

主机与加入方的独立双端回归尚未分别记录，仍需在实际联机房间补测。
