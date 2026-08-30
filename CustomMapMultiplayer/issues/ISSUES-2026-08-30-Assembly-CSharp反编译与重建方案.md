# Assembly-CSharp.dll 反编译、修改与重新构建方案

## 状态

**方案已整理，尚未执行正式的 Assembly-CSharp 重建。**

本文用于记录如何在 Windows 下对 Broforce 的 `Assembly-CSharp.dll` 进行只读反编译、受控修改、重新构建和运行时验证。本文的目标是建立可重复的研究流程，不代表当前项目已经替换官方游戏程序集。

当前已另行加入 `AssemblyCSharpChineseInputSwitch` 实验性 UMM Mod：它把外部提供的中文输入版 DLL 作为 payload，在游戏进程退出后执行受哈希保护的临时替换，并在下一次退出时恢复原版。该 Mod 只用于本机隔离测试，不代表官方程序集已经完成反编译重建，也不属于正式分发构建。

当前 `CustomMapMultiplayer` 仍采用 UMM/Harmony 架构：`Assembly-CSharp.dll` 只作为编译引用，正式分发物是 `CustomMapMultiplayer.dll`。除非 Harmony 无法覆盖目标原生路径，否则不应直接替换官方程序集。

本机已经存在一份较完整的反编译源码树：`..\Broforce_src\Broforce-Source`。该目录约有 `1,951` 个 C# 文件，并包含 `Assembly-CSharp.sln` 和 `Assembly-CSharp.csproj`，目标框架为 .NET 3.5。它不是完整的 Unity 原始工程，缺少场景、Prefab、AssetBundle 和 ProjectSettings；项目还依赖约 28 个外部 DLL，引用路径硬编码到 Steam 安装目录，当前源码目录中的这些引用并不齐全。因此完整重建应优先复用这份源码树，但不能假设它开箱即编译。

## 目标与边界

### 目标

1. 取得指定类、方法、字段和调用链的可读源码或 IL 证据。
2. 对单个方法或有限范围进行可回退修改。
3. 在不污染正式游戏目录的前提下生成新的 DLL，并确认程序集身份、依赖和运行时行为。
4. 保留原始文件哈希、修改前后差异、构建日志和双端测试证据。

### 边界

- 反编译器输出不是官方源码，不能假设命名、注释、泛型约束和编译器生成状态机完全还原。
- 重新构建后的 DLL 不保证与原文件具有相同的元数据顺序、MVID、调试信息或二进制哈希。
- 不在未备份的生产目录中直接覆盖 `Assembly-CSharp.dll`。
- 不把官方程序集或包含其代码的导出工程提交到本仓库或公开分发。
- 不把一次 IL 热改或运行时注入记录成正式 Mod 修复；正式修复仍需迁移到可维护的 UMM/Harmony 源码并重新验收。

## 成功性评估

以下是基于当前源码资产和 Unity Mono 程序集特征的规划估计，不是已经完成的测试结果：

| 目标 | 预计成功性 | 主要条件 |
| --- | --- | --- |
| 只读反编译、搜索调用链 | 很高，约 95% 以上 | DLL 未损坏，使用同一版本依赖即可读取大多数托管类型 |
| 单方法 IL 修改并保存新 DLL | 高，约 75% 至 95% | 修改范围小，方法签名、异常块和资源不变；强名称签名需另行处理 |
| 使用现有 `Broforce-Source` 编译出 `Assembly-CSharp.dll` | 中高，约 60% 至 85% | 补齐 28 个外部 DLL、修正引用路径并解决反编译源码的编译错误 |
| 从 DLL 导出 C# 后完整重建 | 中等，约 30% 至 60% | 需要人工修复大量生成代码、资源属性、序列化字段和初始化顺序 |
| 重建 DLL 直接替换后完整联机运行 | 中等偏低，约 25% 至 50% | 除能编译外，还必须保持 Unity 类型、网络 RPC、资源和程序集身份兼容 |
| 当前项目继续使用 UMM/Harmony 实现同一行为 | 高，约 80% 至 95% | 目标入口可被 Harmony 稳定拦截，不需要改原生初始化流程 |

提高成功率的关键是先尝试现有源码树和小范围 IL 修改，避免一开始就进行全量 C# 导出和官方 DLL 替换。所有百分比都应在第一轮静态编译和隔离运行后重新评估。

## 工具与输入

### 推荐工具

- `ILSpy`：只读浏览、搜索调用关系、导出 C# 项目。
- `dnSpyEx`：浏览 IL、编辑单个方法并保存模块，适合小范围实验。
- `dnlib` 或 `Mono.Cecil`：编写可重复的 IL 读取和写回工具，适合需要多次重建的补丁。
- `ildasm`/`ilasm`：需要确认 IL 级差异或进行 IL 往返时使用，属于可选工具。
- PowerShell `Get-FileHash`：记录原始和输出 DLL 的 SHA-256。

工具版本应记录在实验记录中。不同版本的反编译器可能生成不同的 C# 结构，不能把导出结果当作稳定源码输入而不记录版本。

### 工具能力边界

- `ILSpy` 适合浏览、搜索和导出 C#；导出结果需要人工修复，不能直接保存为已修改的官方程序集。
- `dnSpyEx` 可以对单个方法执行 C# 编辑、编译并保存模块，适合小范围验证；它不等于完整工程重建工具。
- `dnlib`/`Mono.Cecil` 可以把 IL 修改流程写成可重复脚本，适合固定方法签名和多次实验。
- `ildasm`/`ilasm` 适合 IL 往返，要求手工维护 IL、引用和资源，不能用 C# 反编译文本直接替代。
- 完整重建必须把导出的 C# 作为独立工程，用与游戏兼容的 .NET Framework 3.5 编译器和原始依赖重新生成 DLL。

### 输入文件

```text
<BROFORCE_DIR>\Broforce_beta_Data\Managed\Assembly-CSharp.dll
<BROFORCE_DIR>\Broforce_beta_Data\Managed\UnityEngine.dll
<BROFORCE_DIR>\Broforce_beta_Data\Managed\UnityEngine.*.dll
```

依赖程序集必须从同一份 Broforce 安装中取得。不要混用其它版本、其它机器或网上下载的 `Assembly-CSharp.dll`。

## 工作目录与备份

在项目外建立隔离工作目录，例如：

```text
<WORK_DIR>\original\Assembly-CSharp.dll
<WORK_DIR>\decompiled\
<WORK_DIR>\source\
<WORK_DIR>\patched\
<WORK_DIR>\build\
<WORK_DIR>\logs\
```

开始前记录：

- 原始 DLL 的完整路径、文件大小、修改时间和 SHA-256。
- `AssemblyName`、版本、Culture、PublicKeyToken、目标框架和引用列表。
- Broforce 版本、Steam 分支、游戏安装目录和工具版本。

可用 PowerShell 记录哈希和程序集名称：

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath '<WORK_DIR>\original\Assembly-CSharp.dll'
[Reflection.AssemblyName]::GetAssemblyName('<WORK_DIR>\original\Assembly-CSharp.dll')
```

原始文件只读保存。所有导出、修改和构建输出都写入工作目录，不写入游戏的 `Managed` 目录。

## 路线选择

### 路线 A：只修改 IL 或单个方法

适用于修复一个分支、替换一个调用、增加一个判定或验证明确的调用顺序。

1. 在 `dnSpyEx` 或 ILSpy 中按完整类型名和方法签名定位目标。
2. 同时查看 C# 反编译结果和 IL，记录原始方法 token、参数、返回类型、异常处理块和所有调用点。
3. 优先使用 `dnlib`/`Mono.Cecil` 编写确定性的补丁；实验阶段也可以用 dnSpyEx 的方法编辑功能。
4. 将结果保存为 `Assembly-CSharp.patched.dll`，禁止覆盖 `original` 文件。
5. 重新读取输出 DLL，检查程序集身份、引用和目标方法的 IL，确认只有预期方法发生变化。

这条路线保留原有程序集的大部分元数据，通常比重新导出整个工程更容易保持 Unity 的类型和资源兼容性。但它仍可能影响强名称签名、MVID、异常处理和编译器生成代码，必须做静态和运行时验证。

### 路线 B：导出 C# 后完整重建

适用于需要修改多个类、增加大量逻辑或必须在源码层维护的实验。该路线工作量大，反编译结果通常需要人工修复：

- 编译器生成的迭代器、协程、匿名类和闭包类型。
- 重载方法、显式接口实现、访问修饰符和泛型约束。
- Unity/Mono 旧版本 API、序列化字段和特性。
- 原程序集中的资源、嵌入文件、程序集属性和初始化顺序。

执行步骤：

1. 用 ILSpy 导出目标命名空间或完整项目到 `decompiled`，并保留工具生成的类型名。
2. 将导出代码复制到 `source`，逐项修复编译错误；不要直接在原始游戏目录编辑。
3. 按原程序集的目标框架和引用列表编译，输出到 `build\Assembly-CSharp.rebuilt.dll`。
4. 用反编译器重新打开输出，核对公共类型、方法签名、资源和关键调用链。
5. 只有静态检查和隔离运行都通过后，才考虑在单独的测试安装中替换程序集。

完整重建不能以“能编译”作为成功标准。只要公共 API、序列化字段、网络 RPC 签名或 Unity 类型布局变化，游戏就可能在加载或联机时失败。

### 路线 B-1：优先使用现有 Broforce-Source

当目标类型已经存在于 `..\Broforce_src\Broforce-Source` 时，不要再次从 DLL 导出同一批源码。推荐步骤如下：

1. 将 `Broforce-Source` 复制到独立工作目录，保留其 `Assembly-CSharp.sln`、`Assembly-CSharp.csproj` 和 `!SOURCE_ANALYSIS.md`。
2. 打开项目文件，逐一检查 `HintPath`；将 28 个外部 DLL 映射到同一份 Broforce `Managed` 目录，不把机器专用绝对路径提交回源码树。
3. 确认 `AssemblyName=Assembly-CSharp`、`TargetFrameworkVersion=v3.5`、Release 优化和 `AllowUnsafeBlocks` 与原项目一致。
4. 先不修改功能，只做一次干净 Release 编译，记录全部缺失引用和编译错误。
5. 每次只修改一个目标类或方法；编译成功后立即做程序集身份、引用、IL 和最小单机启动检查。
6. 只有在现有源码树可以稳定重建后，才把性能或网络实验加入正式分支。

该路线比从 DLL 全量导出更接近可维护源码，但它仍缺少 Unity 资源和原始构建流水线，不能保证生成的 DLL 是官方二进制的等价物。

## 编译约定

当前项目使用 .NET Framework 3.5 和 Windows 下的 v3.5 C# 编译器。若完整重建确实需要使用该目标，应使用与游戏兼容的编译器和原始依赖，而不是直接套用现代 .NET 项目设置：

```text
target: library
framework: .NET Framework 3.5
references: 原始 Assembly-CSharp.dll 的全部托管依赖
output: <WORK_DIR>\build\Assembly-CSharp.rebuilt.dll
```

本项目的 `BuildAndDeploy.ps1` 只负责构建 `CustomMapMultiplayer.dll`，并将 `Assembly-CSharp.dll` 作为引用；它不是官方程序集的重建脚本。不要把 `BuildAndDeploy.ps1` 的 Mod 输出误当作 `Assembly-CSharp` 的替换产物。

如果使用导出的 C# 源码构建，必须逐一确认：

- `mscorlib`、`System`、UnityEngine 模块和游戏其它托管 DLL 来自同一安装。
- 编译器生成的程序集名称为 `Assembly-CSharp`，且版本、Culture 和 PublicKeyToken 符合原文件要求。
- 原 DLL 若有强名称签名，必须确认是否拥有合法密钥；没有原密钥时不要宣称生成了等价程序集。
- 资源文件、嵌入程序集属性和必要的初始化类型没有被导出过程丢失。
- 输出文件与原 DLL 分开保存，构建失败不能影响原文件。

## 静态验证

对原始、IL 修改版和完整重建版分别执行：

1. 比较 SHA-256、文件大小和 PE/CLI 头。
2. 比较 `AssemblyName`、版本、Culture、PublicKeyToken 和目标框架。
3. 比较引用程序集名称和版本；缺少 Unity 模块或游戏依赖时不得进入运行测试。
4. 检查目标类型、方法签名、字段类型、网络 RPC 参数和序列化字段。
5. 用 ILSpy/dnSpyEx 对关键方法重新查看，确认分支、调用顺序和异常处理符合预期。
6. 如安装了 `ildasm`，将原始和输出模块导出为 IL 文本，只比较目标方法和必要的元数据变化。

至少保留以下证据：

```text
original_sha256.txt
patched_sha256.txt 或 rebuilt_sha256.txt
assembly_identity.txt
references.txt
关键方法的原始/修改后 IL 或截图
编译器完整命令和构建日志
```

## 隔离运行与回滚

### 测试顺序

停止游戏后，在独立测试安装或已完整备份的测试机上进行：

1. 原始 `Assembly-CSharp.dll`、UMM Mod 关闭：确认原生菜单和单机地图正常。
2. 修改版 `Assembly-CSharp.dll`、UMM Mod 关闭：确认程序集能加载、菜单和单机地图正常。
3. 修改版 DLL、`CustomMapMultiplayer` 开启：确认 Mod 加载、Workshop 地图和目标功能正常。
4. 双端使用完全相同的修改版 DLL，分别记录 `error.log`、UMM `Core\Log.txt` 和 Mod 诊断日志。
5. 性能问题必须使用同一地图、同一设置、新会话和交换 Host 的 A/B 窗口；记录 Host/Client 的 p50、p95、p99 帧时间。

如果第二步失败，不得进入联机测试；如果只有第三步失败，应先区分官方程序集变化和 Mod Harmony 兼容性问题。

### 回滚

- 测试目录始终保留原始 DLL 和哈希。
- 回滚前停止游戏和 Steam 相关进程，确认目标路径确实是测试安装。
- 用原始备份恢复后重新计算 SHA-256，并启动一次菜单验证。
- 不使用 Steam 验证文件代替自己的备份；Steam 校验可能覆盖其它测试改动。

## 当前项目的推荐落地方式

对于 `CustomMapMultiplayer` 当前的酸液、实体终态和诊断性能问题，推荐顺序如下：

1. 用 ILSpy/dnSpyEx 只读确认原生调用链、字段状态和网络入口。
2. 优先在 `CustomMapMultiplayer/src` 中使用 Harmony Prefix、Postfix 或 Transpiler 实现补丁。
3. 使用项目标准脚本构建 Mod，并通过 `BUILD_INFO buildHash` 确认双端版本一致。
4. 只有在目标方法无法由 Harmony 稳定拦截，或必须改变原生程序集初始化逻辑时，才建立独立的 `Assembly-CSharp` IL/源码重建分支。
5. 官方程序集重建通过后，仍应评估能否把行为迁移回 UMM/Harmony；不能因为一次重建成功就改变正式分发架构。

## 验收标准

方案只有在以下条件全部满足时，才能标记为“可用于测试”：

- 原始 DLL 已备份并记录哈希。
- 输出 DLL 可以被反编译器重新打开，程序集身份和关键公共 API 符合预期。
- 测试安装能进入主菜单和目标地图，无新增程序集加载错误。
- 目标方法行为与预期一致，网络 RPC、序列化字段和 Unity 生命周期没有回归。
- 双端使用同一输出，完成至少一轮功能测试和一轮回滚测试。
- 性能结论有统一设置下的 A/B 数据；“体感改善”不能代替 p50/p95/p99 验收。
- 所有构建、测试和回滚证据均保存在不含官方源码的内部记录中。

在这些条件满足前，输出只能标记为“反编译实验”或“待验证重建”，不能作为正式发布构建。
