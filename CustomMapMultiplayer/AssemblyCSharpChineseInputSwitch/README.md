# Assembly-CSharp Chinese Input Switch

这是一个独立的 UMM Mod，用于临时切换到外部提供的中文输入版 `Assembly-CSharp.dll`。

## 使用方式

在项目根目录执行构建脚本，并把备份目录中的候选 DLL 作为参数传入：

```powershell
powershell -ExecutionPolicy Bypass -File .\AssemblyCSharpChineseInputSwitch\BuildAndDeploy.ps1 -Configuration Release -CandidatePath '<CANDIDATE_DLL_PATH>'
```

如果希望下一次启动就直接使用候选 DLL，并且当前没有运行 Broforce，可以加上 `-StageForNextLaunch`。该选项会先备份游戏原版 DLL，再把候选文件原子替换到 `Managed\Assembly-CSharp.dll`：

```powershell
powershell -ExecutionPolicy Bypass -File .\AssemblyCSharpChineseInputSwitch\BuildAndDeploy.ps1 -Configuration Release -CandidatePath '<CANDIDATE_DLL_PATH>' -StageForNextLaunch
```

然后启用 UMM 中的 `Assembly-CSharp Chinese Input Switch`：

1. 不使用 `-StageForNextLaunch` 时，当前进程已经加载原版 `Assembly-CSharp.dll`，Mod 只会备份原版并安排下一次启动替换。
2. 使用 `-StageForNextLaunch` 时，下一次启动直接使用候选 DLL。
3. 退出游戏后，辅助程序校验当前文件哈希并恢复原版 DLL。
4. 再次启动游戏时应回到原版 DLL。

UMM Mod 无法在当前游戏进程中卸载并重新加载已经使用的 `Assembly-CSharp` 程序集，因此首次启动不会立即改变当前进程的代码。不要在辅助程序尚未完成时同时启动第二个 Broforce 进程。

## 文件安全

- 原版备份保存在 UMM Mod 的 `state\Assembly-CSharp.original.bak`。
- 候选 DLL 只作为 Mod payload 使用，备份目录不会被修改。
- 替换前后都校验 SHA-256；如果 live DLL 与备份或候选哈希不符，辅助程序会拒绝覆盖。
- `state\switch-helper.log` 记录替换和恢复结果。
- 如果候选 DLL 导致游戏在 UMM 完全加载前退出，请停止游戏后用 `state\Assembly-CSharp.original.bak` 手动恢复到游戏 `Managed\Assembly-CSharp.dll`，再删除 `state\active.marker`。

本 Mod 只适合隔离测试，不应与修改同一原生方法的 Harmony 补丁同时启用，也不应作为正式分发方案。
