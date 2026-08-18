# Broforce 第三方地图联机 Mod

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。它复用游戏原有的 Steam 多人大厅，让已经订阅同一张 Workshop 地图的玩家尝试共同进入第三方地图。

所有玩家必须安装相同版本的 Mod，并提前订阅、下载相同的 Workshop 地图。

## 当前状态

当前版本为实验性 `0.3.0`：

- 已验证主机和朋友可以通过官方大厅流程进入同一张 Workshop 地图。
- 已加入实验性的晚加入处理：主机已经进入配置中的 Workshop 场景后，客户端加入大厅时会尝试自动加载同一张地图。
- `test009` 双端测试已验证：过场加载期间加入可以进入地图，P2 角色可以正常创建，双方角色控制保持独立。
- 已支持在 UMM 设置中填写 Workshop ID、可选的战役名和场景名，并使用会话 ID 和可选日志标签关联双端日志。
- 已保留官方英雄类型请求；朋友端收不到回复时，会在等待 18 秒后使用本地备用生成。
- 仍存在英雄状态不同步和 Broforce 原生崩溃风险，尚未达到稳定发布状态。

## 使用方式

> 目前所有测试环境只包含 `UMM` 以及 `BroforceOnlineDiagnostics.dll`, 待逐渐完善后再考虑装载其它mod的情况下测试.

1. 所有玩家必须安装 `r2modman`
2. `r2modman` 管理器安装好了之后, 找到 `UMM` 并安装, 之后启动一次游戏, 确认 `UMM` 加载成功.
3. 找到对应 `r2modman` 的配置(profiles) `xxxxxx\Broforce\profiles\Broforce\mods.yml`, 增加如下内容:
```
- manifestVersion: 1
  name: GJKen-BroforceOnlineDiagnostics
  authorName: GJKen
  websiteUrl: ''
  displayName: BroforceOnlineDiagnostics
  description: 测试
  gameVersion: ''
  networkMode: ''
  packageType: ''
  installMode: ''
  installedAtTime: 1786929010047
  loaders: []
  dependencies: []
  incompatibilities: []
  optionalDependencies: []
  versionNumber:
    major: 1
    minor: 0
    patch: 0
  enabled: true
  onlineSource: false
```
> `r2modman` 有个好处就是可以创建不同的 profiles 来创建不同的mod环境.
4. 从项目根目录复制 `BroforceOnlineDiagnostics` 文件夹到 `xxxxxx\Broforce\profiles\Broforce\UMM\Mods`。该文件夹就是给其它玩家复制的安装包，必须包含 `BroforceOnlineDiagnostics.dll` 和 `Info.json`。构建者每次运行 `BuildAndDeploy.ps1` 后，项目内的安装包文件夹会自动更新，同时覆盖本机和内网测试端的 DLL。之后在 UMM 设置中开启 `Inject configured workshop map into online level switching`。
5. 第 3 步和第 4 步完成后需要重启一次 `r2modman`，让它重新读取新增的 `BroforceOnlineDiagnostics`。
6. 所有玩家订阅并下载相同的 Workshop 地图，并在 UMM 设置中填写相同的 Workshop ID。战役名不预填；场景名默认是 `Test Evan2`，如果地图使用其它场景名再按实际情况修改。

首次双端测试可以使用以下配置：

```text
Workshop ID: <实际的 Workshop 数字 ID>
Workshop campaign name: 留空
Custom level scene: Test Evan2
Diagnostic session ID: test001
两端 Diagnostic label: 任意标识或留空
```

两端的 Workshop ID 和 Diagnostic session ID 必须完全一致；保存双方 UMM 设置后，再开启线上地图注入。

填写或修改设置后，请点击 UMM 设置面板的保存按钮；正常切换 Mod 或退出游戏时也会尝试自动保存。

如果旧配置中的 `Custom level scene` 为空，插件加载时会自动补回默认值 `Test Evan2`；已经填写其它场景名的配置不会被覆盖。

升级旧版本配置时，插件会清理旧版本遗留的测试默认值；已经填写的其它自定义值不会被覆盖。`Diagnostic label` 只用于日志文件名和关联信息，不参与联机行为；任意一端都可以创建大厅，实际网络角色由游戏大厅流程自动决定。

7. 任意一端按官方流程创建线上大厅，另一端先加入 `p1-p4` 选择界面。加入方必须按一次攻击键占用自己的位置，创建方确认双方出现在不同位置后，再按攻击键开始进入地图游玩；不要等进入地图后才选择角色。

当前版本也支持“创建方正在进入或已经进入 Workshop 地图时，另一端再加入”。加入方检测到创建方处于 Workshop 加载阶段后，会主动刷新 Lobby 数据、申请新的本地玩家槽位并行加载地图；host 端只在晚加入 Workshop 会话中放宽原生角色请求保护。`test009` 已验证过场期间加入和 P2 角色创建成功。正常测试仍建议先加入大厅、确认占用独立位置后，再由创建方进入地图。

双端排查时可以在 UMM 设置中填写相同的 `Diagnostic session ID`，并为两端填写不同的可选 `Diagnostic label`。进入或加入 Steam 大厅时会自动创建新的会话日志；Harmony 详细追踪会写入同名的 `.trace.log` 文件，普通事件日志不会与上一次联机测试混在一起。日志还会记录实际网络角色，但标签本身不参与联机行为。

Mod 默认关闭注入；关闭注入时只记录诊断信息，不改变游戏行为。

## 构建

项目面向 .NET Framework 3.5，使用 Broforce 和 Unity Mod Manager 的程序集引用。先根据 `LocalBroforcePath.props.example` 创建本机的 `LocalBroforcePath.props`，然后运行 `BuildAndDeploy.ps1`。

构建脚本会直接将标准文件名 `BroforceOnlineDiagnostics.dll` 生成到项目内的可复制安装包，然后自动覆盖部署到本机 UMM Mod 目录和内网测试端：

```text
<项目根目录>\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
<本机 UMM>\Mods\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
\\192.168.1.181\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\BroforceOnlineDiagnostics\BroforceOnlineDiagnostics.dll
```

日常构建和部署以 `BuildAndDeploy.ps1` 为准。工程的构建后目标也指向项目安装包和两处部署目录，但只有在本机 MSBuild 正确读取 `LocalBroforcePath.props` 时才可使用；不要用未验证的 IDE/MSBuild 构建代替脚本。内网路径不可访问或 DLL 被锁定时，构建部署应视为失败，不要继续进行双端测试。

当前标准程序集文件名始终为 `BroforceOnlineDiagnostics.dll`。自动部署始终覆盖 DLL；项目内安装包的 `Info.json` 是固定清单，目标目录缺少 `Info.json` 时才会从 `modinfo.json` 初始化，不覆盖已有的 `Info.json` 或其它文件。

## 项目结构

```text
src/                         Mod 源码
BroforceOnlineDiagnostics.csproj  C# 工程文件
BuildAndDeploy.ps1             .NET 3.5 构建和双端自动部署脚本
BroforceOnlineDiagnostics/   给其它玩家复制的 UMM Mod 安装包
  BroforceOnlineDiagnostics.dll
  Info.json
modinfo.json                 UMM Mod 清单模板
LocalBroforcePath.props.example   本机路径配置示例
docs/DEVELOPMENT.md          开发、逆向、测试和故障排查记录
```

## 文档

- [开发与测试文档](docs/DEVELOPMENT.md)

开发文档包含已确认的官方联机流程、Workshop 注入调用链、英雄回复问题、构建约束、日志分析和后续测试步骤。

## 参考资料

- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
