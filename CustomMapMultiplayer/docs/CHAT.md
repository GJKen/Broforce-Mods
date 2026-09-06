# Custom Map Multiplayer：联机聊天输入

[返回开发文档索引](DEVELOPMENT.md)

## 范围

联机聊天功能集中在 `src/HarmonyDiagnostics.Chat.cs`。补丁扩展原生 `KeyboardInput`、`MessageController`、`ChatTextBox` 和 `Caret` 的输入与显示行为，不修改 `Assembly-CSharp.dll`、聊天网络协议或已发送消息格式。

当前实现包含：

- Enter/Esc 聊天边界、Backspace/Delete、左右移动和真实键盘长按编辑。
- Unity IME 组合输入和提交文本，支持中文、中文符号及数字输入；同一帧重复数字会去重。
- `MessageController.message` 中最多 500 个 UTF-16 字符。完整消息保留在消息字段中，显示层不按固定 80 字符截断。
- 根据输入框实际 `RectTransform`、字体和 Unity `TextGenerator` 行表计算 viewport；Up/Down 按视觉行移动并保留 preferred X，单行 Up 保留原生上一条消息编辑行为。
- 挂在输入文本 `RectTransform` 下的右下角字数显示，分子使用完整消息的 `Length`，分母复用 `ChatMessageMaxLength`。
- 发送历史消息按聊天 Fade 的实际可视边界重新排列和隐藏旧消息；最新消息即使单条超过边界也保持可见。

## 运行时边界

- 聊天完整输入保留在 `MessageController.message`；viewport 只改变 `Caret` 的显示文本、显示光标位置和 Unity UI 文本设置。
- 字数显示是独立的 `UnityEngine.UI.Text`，不写入消息正文、`Caret` 字段、发送字段或远端消息。
- 输入对象或 Canvas 重建后，补丁重新绑定实际聊天输入对象；关闭聊天时隐藏计数和输入显示，不创建永久悬浮对象。
- 历史消息仍保留在 `ChatTextBox.chatMessages` 层级中，超出动态可视边界的旧消息通过 `SetActive(false)` 隐藏，不作为网络消息删除。

## 当前验收状态

已通过用户真实键盘确认的范围包括：中文 IME 候选提交、数字和符号输入、Enter/Esc、删除、左右移动、长按、首次超过可视高度时的 viewport 跟随、Up/Down 最小回归、右下角字数显示，以及发送后的输入清空。

完整聊天输入矩阵仍待覆盖：开头/中间/末尾位置、英文/中文/数字/空格/符号混合、自动和显式换行、短行、编辑后视觉行移动、IME 候选提交后移动、发送后重开和数字回归。聊天历史还需要专项确认发送瞬间顶部闪现、最新长消息可见性和旧消息动态边界。

“Enter -> Esc -> Esc -> Enter”后无法再次呼出聊天的问题已经修复并通过联机房间人工回归；修复同时恢复了重新打开聊天后的 Enter 发送能力。主机与加入方的独立双端回归尚未分别记录，见 [聊天框 Esc 问题](../issues/ISSUES-2026-09-05-联机聊天框Esc后无法再次呼出.md)。

## 相关源码与记录

- `src/HarmonyDiagnostics.Chat.cs`：聊天输入、IME、长度限制、viewport、视觉行导航、计数显示和历史消息显示补丁。
- [中文输入法支持与关键改动](../issues/ISSUES-2026-09-05-联机聊天中文输入法支持与关键改动.md)
- [长消息可视滚动效果](../issues/ISSUES-2026-09-05-联机聊天输入框长消息可视滚动效果不理想.md)
- [上下方向键跨视觉行移动](../issues/ISSUES-2026-09-06-联机聊天输入框上下方向键跨视觉行移动.md)
- [右下角字数显示](../issues/ISSUES-2026-09-06-联机聊天输入框右下角字数显示.md)
- [长消息发送后历史显示](../issues/ISSUES-2026-09-06-联机聊天长消息发送后历史显示被隐藏.md)
