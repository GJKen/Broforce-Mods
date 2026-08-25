# Broforce 第三方地图联机 Mod

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。默认复用官方 Steam Lobby/Steam P2P；可选的 `FRP Direct` 使用独立房间、PID 和游戏 RPC，但 Workshop 内容仍由 Steam 下载。

所有参与联机的玩家必须安装相同构建的 Mod，并订阅、下载相同的 Workshop 地图。排查版本时以双方日志中的 `BUILD_INFO buildHash` 为准。

## 当前状态

当前版本为实验性 `0.5.0`，尚未达到稳定发布状态。

| 项目 | 状态 |
| --- | --- |
| 当前分发构建 | `buildHash=f4be08d8d30129049f3acd003c93116e077edaf5f3be3cda8a9ce1faac8701a5` |
| DLL SHA-256 | `93E8848A4BBCA07DD39B35442CC5F43B68C443C6828D6BFC6023104F8B27B7F2` |
| Steam 联机 | 默认路径；已验证官方大厅进入同一张 Workshop 地图 |
| FRP Direct | 默认关闭的实验路径；已验证公共 UDP 双端游玩和在线玩家名显示 |

当前构建已经验证：

- 双端准备后进入、创建方先进入后加入、退出房间后重新加入，双方角色和控制保持独立。
- Workshop 线上会话屏蔽“按开枪键加入游戏”横幅，但攻击键加入仍可用。
- `Esc` 可跳过 Workshop 通关时间/评分界面并返回在线房间列表；大厅返回主菜单的 Logo、文字和高亮动画时序已修复。
- Workshop 道具的普通弹药箱、远程拾取和弹药已满时的重复 RPC 已做确定性和幂等处理。
- UMM 设置支持 Workshop、FRP Direct、诊断日志三个可持久化折叠面板，以及九类日志开关和五种预设。

仍需继续覆盖：AFK 诊断在真实双端会话中的内容、官方 Steam 大厅下的道具修复、多地图兼容、异常断网、高延迟和长期多轮退出/重入。FRP 第一版只支持房主加一台远端机器，不支持主机迁移。部分 Workshop 地图可能有自身的 `GeneratePole.Awake`、`BroBase` 或特效销毁异常。详细实现和证据见 [开发与测试文档](docs/DEVELOPMENT.md)、[问题记录索引](issues/README.md)。

## 安装与首次测试

1. 所有玩家安装 `r2modman`，为 Broforce 创建或选择同一个 profile，并在其中安装 UMM。
2. 启动一次游戏确认 UMM 加载成功。若本地 Mod 尚未登记，在 profile 的 `mods.yml` 增加：

```yaml
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
    major: 0
    minor: 5
    patch: 0
  enabled: true
  onlineSource: false
```

3. 将项目内安装包 `BroforceOnlineDiagnostics` 下的 `BroforceOnlineDiagnostics.dll` 和 `Info.json` 复制到 profile 的 `UMM\Mods\GJKen-BroforceOnlineDiagnostics`。目录名必须是 `GJKen-BroforceOnlineDiagnostics`。运行 `BuildAndDeploy.ps1` 后，构建者的安装包和配置的测试部署目标会自动更新。
4. 重启 r2modman，在 UMM 中确认 `Broforce Online Diagnostics 0.5.0` 已加载。填写设置后点击 UMM 的保存按钮；切换 Mod 或退出游戏时也会尝试自动保存。
5. 双方订阅并下载相同 Workshop 地图，在 UMM 填写相同的 Workshop ID。战役名可留空，场景名默认 `Test Evan2`，地图使用其它场景时再修改；确认配置后开启线上地图注入。

首次测试建议：

```text
Workshop ID: <Workshop 页面 URL 中 id= 后的数字>
Workshop campaign name: 留空
Custom level scene: Test Evan2
Diagnostic session ID: test001（可留空）
Diagnostic label: 任意标识或留空
```

6. 任意一端按官方流程创建线上大厅，另一端加入 `p1-p4` 选择界面。加入方按一次攻击键占用自己的位置；创建方确认双方位于不同位置后，再按攻击键进入地图。稳定测试优先采用“先加入大厅、创建方后进入地图”。

创建方已经进入 Workshop 地图时也支持晚加入。加入方会等待场景和原生玩家生成就绪后自动申请一次本地槽位；当前构建还会在最终场景向重入客户端重放房主的 buffered 网络实例。成功判据和诊断关键字见 [晚加入与重入](docs/DEVELOPMENT.md#晚加入与重入)。

### 常用设置

- `Diagnostic session ID` 用于关联双方同一轮日志；两端填写相同值。`Diagnostic label` 只影响日志文件名，不参与联机行为。
- `Disable automatic AFK spectator mode in online games` 默认关闭，由每台客户端独立控制；要保护双方角色，双方必须分别开启。它不拦截手动退出、断线或正常死亡。
- 诊断日志预设只影响输出，不改变联机行为。双方排查同一问题时应尽量选择相同类别。

每轮测试结束后，收集所有参与端的诊断 `.log`、`.trace.log`，并尽量同时保存 UMM `Core\Log.txt` 和游戏 `error.log`。日志目录为 `Application.persistentDataPath\BroforceOnlineDiagnostics\`，不是 DLL 部署目录。只有单端日志时，结论必须明确证据缺口，不能单独断定网络根因。

## FRP Direct 实验联机

FRP Direct 默认关闭。只有同时开启以下两个设置，FRP 才会接管 Broforce 房间、PID 和游戏 RPC；只开启传输原型时仍保持 Steam 游戏联机：

```text
Enable FRP Direct transport prototype: 开启
Route Broforce rooms and RPC through FRP Direct (experimental): 开启
```

房主：

```text
FRP Direct role: Host
Local UDP listen port: 27045
FRP room password: 双方约定的临时密码，或留空
```

点击 `Apply / restart FRP Direct` 后应显示 `Listening on UDP 27045`；`frpc` 将公网 UDP 端口转发到 `127.0.0.1:27045`。

加入方：

```text
FRP Direct role: Client
FRP server endpoint: 服务商提供的完整 host:port（IPv6 使用 [地址]:端口）
FRP room password: 与房主一致
```

双方使用同一标准构建且密码一致时，状态应为 `Handshake complete; heartbeat active`。协议版本、`buildHash` 或密码不一致会拒绝握手且不会自动降级。密码会保存在本机 UMM 设置文件中，但不会写入日志或通过网络明文发送；请使用临时密码，不要复用其它账号密码。FRP token 只属于 `frpc`，不要填入 Mod。

握手完成后，房主照常创建线上大厅，加入方在在线大厅列表中选择唯一的 FRP 房间；双方进入 `p1-p4` 后分别占位，再由房主进入 Workshop 地图。FRP 当前不支持主机迁移；异常断网重连、多地图、高延迟和长期稳定性仍需验收。完整协议和历史失败记录见 [FRP Direct 验收记录](issues/archive/ISSUES-2026-08-24-FRP内网穿透联机方案.md)。

## 构建

项目面向 .NET Framework 3.5。复制 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props`，填写 Broforce `Managed` 目录和 UMM 核心目录，然后从项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

标准脚本会生成项目内安装包并部署到配置的本机/内网 UMM 目录，同时计算并嵌入 SHA-256 `buildHash`。部署路径不可访问、目录创建失败或 DLL 复制失败时，构建视为失败，不要继续双端测试。不要用未经标准脚本验证的 IDE/手工构建代替；这类构建会记录 `UNBUILT`。

## 项目结构与文档

```text
src/                              Mod 源码
BroforceOnlineDiagnostics.csproj C# 工程文件
BuildAndDeploy.ps1                .NET 3.5 构建和部署脚本
BroforceOnlineDiagnostics/        可复制的 UMM 安装包（DLL + Info.json）
modinfo.json                      UMM 清单模板
LocalBroforcePath.props.example   本机路径配置示例
docs/DEVELOPMENT.md               开发、逆向、测试和故障排查
issues/                           历史问题、测试证据和验收记录
umm-settings-preview.html         UMM 设置界面预览
```

- [开发与测试文档](docs/DEVELOPMENT.md)
- [问题记录索引](issues/README.md)
- [Utility Mod 借鉴与 AFK 诊断记录](issues/ISSUES-2026-08-25-Utility-Mod代码借鉴方案与AFK诊断改进.md)
- [BroforceMods Wiki](https://github.com/alexneargarder/BroforceMods/wiki)
- [Viewing Broforce's Code](https://github.com/alexneargarder/BroforceMods/wiki/Viewing-Broforce's-Code)
