# Broforce 第三方地图联机 Mod

这是一个面向 Steam 版 Broforce 的 Unity Mod Manager + Harmony Mod。它复用游戏原有的 Steam 多人大厅，让已经订阅同一张 Workshop 地图的玩家尝试共同进入第三方地图。

所有玩家必须安装相同版本的 Mod，并提前订阅、下载相同的 Workshop 地图。

## 当前状态

当前版本为实验性 `0.3.0`：

- 已验证主机和朋友可以通过官方大厅流程进入同一张 Workshop 地图。
- 已支持在 UMM 设置中填写 Workshop ID、可选的战役名和场景名。
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
4. 之后复制项目里面的 `BroforceOnlineDiagnostics` 文件夹到 `xxxxxx\Broforce\profiles\Broforce\UMM\Mods`, 启动游戏后就能在 UMM 里面看见此mod了, 并在 UMM 设置中开启 `Inject configured workshop map into online level switching`
5. 3和4的步骤完成后需要重启一次 `r2modman` 才能看见新增的 `BroforceOnlineDiagnostics`
6. 所有玩家订阅并下载相同的 Workshop 地图, 填写相同的 Workshop ID。战役名可以留空; 标准 Workshop 战役的场景名通常使用 `Test Evan2`。
7. 主机按官方流程创建线上大厅，先让朋友加入p1-p4选择界面, 等待进入后可直接按攻击键开始进入地图游玩.

Mod 默认关闭注入；关闭注入时只记录诊断信息，不改变游戏行为。

## 构建

项目面向 .NET Framework 3.5，使用 Broforce 和 Unity Mod Manager 的程序集引用。先根据 `LocalBroforcePath.props.example` 创建本机的 `LocalBroforcePath.props`，再构建 `BroforceOnlineDiagnostics.csproj`。

当前标准程序集文件名始终为 `BroforceOnlineDiagnostics.dll`。构建产物需要部署到 Unity Mod Manager 的 `Mods\BroforceOnlineDiagnostics` 目录，并配套部署 `modinfo.json`（文件名改为 `Info.json`）。

## 项目结构

```text
src/                         Mod 源码
BroforceOnlineDiagnostics.csproj  C# 工程文件
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
