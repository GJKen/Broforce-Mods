# ISSUES-2026-08-24 联机加入方重复角色与 AFK 开关编译测试记录

## 记录范围

本文按本次对话的实际时间顺序记录每轮代码修改、编译结果、部署校验和联机测试结论。

需要区分两个概念：

- `buildHash`：由 `BuildAndDeploy.ps1` 根据源码、引用程序集、编译器和配置在编译时生成并嵌入 DLL 的构建标识，运行日志会记录它。
- DLL SHA-256：对最终二进制文件计算的文件校验值，用于确认项目安装包、本机部署目录和共享部署目录是否是同一个文件。

“编译成功”不等于“联机测试通过”；没有实际运行测试的构建必须单独标注。

## 初始问题：加入方角色不是自己的本地角色

### 现象

旧测试日志显示，加入方创建出的角色为 `Player.Start(... playerNum=1 ... IsMine=False ...)`，所以加入方看得见角色或收到角色对象，但不能操作自己的角色。房主日志里的 `RequestJoinGame(... requesteeID=PID{IsMine=False})` 属于房主看到的远程 PID，单独不能证明房主端错误。

初步判断为加入方的 `HeroController.PIDS[index]` 没有被识别为 `PID.MyID`，导致 `Player.IsMine` 和 `NetworkObject.IsMine` 为 false。

## 编译轮次

### 轮次 1：加入方本地所有权修复，第一次编译失败

修改目标：

- 在 `Player.Start` 原方法执行前，针对“正在等待的本地加入请求”校正 `HeroController.PIDS[index]`。
- 同步本地控制器编号。
- 调用 `NetworkObject.SetOwner(PID.MyID)`。
- 在日志中记录前后所有权状态。
- 注册 `SpawnJoinedPlayersPostfix` 作为后置兜底。

编译结果：失败。

- 临时构建标识：`b0b78874da6a0e73aff3258de49fc69126b4317c5b4097c66c601d9619113e36`。
- 错误 1：Harmony 后置补丁注册的嵌套括号导致 `CS1026`。
- 错误 2：`Player.controllerNum` 是只读属性，不能直接赋值。

该轮没有可部署 DLL。

### 轮次 2：修正补丁注册和只读属性写入，第二次编译失败

修改：

- 将复杂的嵌套三元表达式拆为 `GetPostfixForTarget`。
- 使用现有反射辅助方法写入 `controllerNum` 内部字段。

编译结果：仍失败。

- 临时构建标识：`3adcf96567b713500b83ca60bddf05e26956c3943ee2cdccccbc8e01b78b3d28`。
- 错误：补丁注册表达式仍有一个括号不匹配，`CS1026`。

该轮没有可部署 DLL。

### 轮次 3：加入方所有权修复首次编译成功

修改完成：

- 新增本地加入请求到槽位的跟踪。
- `Player.Start` 前按控制器和待处理请求匹配槽位。
- 将匹配槽位的 PID 改为 `PID.MyID`，同步控制器并重写网络对象 Owner。
- `SpawnJoinedPlayers` 后置阶段增加所有权核对。
- 加入明确日志：`Repaired pending local Workshop player ownership`。

编译和部署结果：成功。

- `buildHash`：`88d00f55dbecddfc1710e8122f64dcd609127618411965b739411c6876dd893b`。
- 项目 DLL、本机部署 DLL、共享部署 DLL 的文件 SHA-256：`01BA9ABBB66E561B393A8613C4D511F6C4267474F2FFDE57FFD208FFC28E7BD0`。
- 文件大小：`131072` 字节。

当时没有新的双端联机测试；旧日志没有出现新修复日志，不能用旧日志判断该修复是否有效。

### 轮次 4：兼容实际分配槽位与预计槽位不同

旧逻辑只匹配“预计槽位”。根据之前日志，加入方预计槽位可能是 0，但房主实际将加入方放入槽位 1，因此增加了按本次请求控制器识别实际槽位的分支，并在成功修复后清除待处理请求。

编译和部署结果：成功。

- `buildHash`：`b8ecffd7607742e29990bb85bffa5077f9074d852f5d1eb274b4184c20ef43df`。
- 三处 DLL 文件 SHA-256：`E6F7A644511021B3ECDC688EF753BEE5FD22A4334F659FBD14E11F7940F5E7B9`。
- 文件大小：`131072` 字节。

当时没有新的双端联机测试。

### 轮次 5：兼容 Player.Start 时控制器尚未写入

根据旧日志，`Player.Start` 前的早期阶段可能仍显示 `controllerNum=-1`。为避免错过真正的本地待加入角色，增加了从当前唯一待处理加入请求中取得控制器的回退逻辑。该回退仍受“加入方、存在待处理请求、PID 已设置”等条件限制。

编译和部署结果：成功。

- `buildHash`：`dee7cf00ea45792e591e3f5cd0fa8b44749503cdc5bad858a570622f31c0891b`。
- 三处 DLL 文件 SHA-256：`D6C85A116FD18894A045B03CA1F24B04BE3CA7FE018071547517E87CB4586E67`。
- 文件大小：`131072` 字节。

该构建随后用于下文的实际双端测试，并暴露了 P2-P4 重复角色问题。

## 实际测试轮次：重复出现 P2-P4 角色

### 测试构建

本次测试使用的双方日志都记录相同构建：

- `buildHash`：`dee7cf00ea45792e591e3f5cd0fa8b44749503cdc5bad858a570622f31c0891b`。
- 本机日志：`diagnostics-client-test003-20260824-132212-133.log`。
- 加入方日志：`另外的加入方日志/diagnostics-client-666-20260824-132225-135.log`。
- 双方日志中的 `BUILD_INFO` 和 `SESSION_BEGIN` 均存在，说明这次不是两端 DLL 版本不一致。

### 关键证据

加入方：

- `13:22:44` 第一次 `HeroController.AddLocalPlayer(playernum=-1, controllerID=0)`。
- 随后请求超时，代码每约 5 秒再次调用 `AddLocalPlayer`。
- 最终加入方收到 `HeroController.AddPlayer` 的 `playerNum=1/2/3`，三个槽位均显示 `playerPID=PID{IsMine=True}`。
- 随后的 `Player.Start` 也出现 `playerNum=1/2/3`，三个角色都被当作本地角色。

房主：

- `13:22:52` 第一次收到 `RequestJoinGame`，分配 `playerNum=1`。
- 同一时间又收到重试请求，分配 `playerNum=2`。
- 随后继续收到重试请求，分配 `playerNum=3`。
- 房主日志反复出现同一个远程玩家名的 `RequestJoinGame(controllerNum=0)` 和 `AddPlayer(playerNum=1/2/3)`。

### 根因结论

根因不是 DLL 不一致，而是两个逻辑叠加：

1. 加入方等待窗口只有 5 秒；网络和场景同步延迟超过该窗口后，加入方误以为请求失败并重复发送。
2. 房主端为 Workshop 晚加入绕过了原生控制器注册防重判断；同一个 PID 的重试请求因此不断占用新槽位。

此前加入方本地所有权修复还会将多个被重复创建的本地槽位标记为 `IsMine=True`，所以最终表现为 P2-P4 多个角色。

## 编译轮次 6：修复重复 RequestJoinGame 和 P2-P4

修改：

- 房主按请求 PID 检查是否已经占用玩家槽位；已存在时拦截重复请求。
- 重复请求不再清理已有槽位或重新分配。
- 加入方请求超时从 5 秒延长到 45 秒，避免正常延迟触发重试。
- 删除会把多个槽位误判成本地角色的过宽 `SpawnJoinedPlayers` 兜底。

编译和部署结果：成功。

- `buildHash`：`7a9cd1b1f0e6ad869381cfcc9ca61572b08b0389285d231590ab88b730a9128a`。
- 三处 DLL 文件 SHA-256：`0F4F0A1519BBB10DFEEBA93F46B7AA9B2C54CE0BE85380F9F462A2275C4EDDDE`。
- 文件大小：`131072` 字节。

该轮编译后尚未立即完成新的实际联机测试；当时记录的状态是“代码和日志链路上的预期，尚未完成验收”。后续最新 DLL 测试结果见本文末尾。

## 编译轮次 7：增加联机 AFK 开关

用户提出需要一个开关，禁用角色长时间不操作后被自动删除并进入观战。

修改：

- `DiagnosticSettings` 新增 `DisableOnlineAfkSpectatorMode`，默认关闭。
- UMM 设置界面新增 `Disable automatic AFK spectator mode in online games`。
- 对 `Player.Update` 安装前置补丁。开启该选项时，仅对“联机且由本机拥有”的角色把原生 `Player.idleTimer` 清零。
- 不拦截手动退出、断线、正常死亡，也不影响离线游戏。
- README 和开发文档已补充使用说明。

编译和部署结果：成功。

- `buildHash`：`373c58897c8981e349a32b3c3f7ddb9c464ac17308c888c732f5064881905c6e`。
- 三处 DLL 文件 SHA-256：`D06CD172BA124EF6DF995724531F4F1159CAE987FBF857DB5EE3D61821224A00`。
- 文件大小：`131072` 字节。

该轮编译时只有编译、部署和静态核对，尚未完成运行验收；后续最新 DLL 测试结果见本文末尾。

## 后续实测：异地高延迟加入与 AFK 开关

用户反馈：使用最后一轮 DLL（`buildHash=373c58897c8981e349a32b3c3f7ddb9c464ac17308c888c732f5064881905c6e`）进行新的异地联机测试后：

- 对方处于高延迟网络环境时仍能正常加入房间。
- 没有再次出现同一加入方生成 P2、P3、P4 多个角色的问题。
- AFK 开关开启后，角色长时间不操作没有被自动删除，也没有被移入观战。

### 验收结论

截至本次反馈，以下两项视为通过用户实测：

1. 异地高延迟加入和重复 `RequestJoinGame` 防护通过。
2. 联机 AFK 自动观战禁用开关通过。

本次反馈未附新的双方日志文件，因此不能补充具体日志文件名、事件时间线或新的 `BUILD_INFO` 证据。后续若再次出现异常，仍应保留双方日志并比较 `buildHash`。

## 当前状态和下一轮测试

当前项目最后一次构建包含下列功能；其中重复角色防护和 AFK 开关已经完成用户实测：

- P2-P4 重复角色修复。
- 加入方请求超时保护。
- 联机 AFK 观战禁用开关。
- 编译期 `buildHash` 日志。

如果继续回归测试，双方仍应彻底退出并重启 Broforce，确保没有旧进程继续使用旧 DLL。建议步骤：

1. 双方都确认 UMM 设置中的 `Disable automatic AFK spectator mode in online games` 已按需要开启。
2. 房主创建房间，加入方只发起一次加入，等待最多 45 秒。
3. 进入地图后确认房主只有一个本地槽位，加入方只有一个本地槽位，不出现 P3/P4。
4. 加入方保持不操作超过原生 AFK 时间，确认角色没有被删除或转为观战。
5. 测试结束后保存双方 `.log` 和 `.trace.log`，优先比较 `BUILD_INFO buildHash`。

验收标准：加入方只有一个角色；双方角色可操作；重复请求不会创建新槽位；开启 AFK 开关后长时间不操作不会自动删除本机角色。本次用户反馈已确认后两项关键场景通过。
