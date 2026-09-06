# ISSUES-2026-09-05 联机聊天框 Esc 后无法再次呼出

## 状态

**已修复，联机房间人工回归通过。**

## 根因

执行 `Enter -> Esc -> Esc -> Enter` 时，聊天关闭和暂停处理发生在同一输入周期：聊天状态已关闭，但同一枚 `Esc` 又被暂停逻辑处理。返回游戏后 `PauseMenu.MenuActive` 可能仍为 `true`，原生 `KeyboardInput.Update()` 因此吞掉后续聊天 `Enter`。重新打开后也可能无法用 `Enter` 发送。

## 修复

- 在联机且处于 `UnPaused` 时，聊天边界显式调用原生 `KeyboardInput.Toggle()`，保证打开和发送的 `Enter` 都能进入原生路径。
- `PauseController.TogglePause` 返回 `UnPaused` 后清理残留的 `PauseMenu.MenuActive`。
- 暂停、关闭聊天或发送完成后清理聊天输入重复状态。
- 未修改 `ChatTextBox.target`、网络协议或 `Assembly-CSharp.dll`。

## 关键验证

- `Enter` 打开聊天，输入后 `Enter` 正常发送。
- 聊天中按 `Esc` 后再次按 `Enter` 可以打开聊天。
- 完整执行 `Enter -> Esc -> Esc -> Enter` 后，聊天可以再次呼出并继续发送。
