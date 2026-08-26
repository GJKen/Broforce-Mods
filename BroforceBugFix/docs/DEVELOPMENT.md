# Broforce Bug Fix 开发与测试

## 维护原则

- 只修复已有运行日志、稳定复现或原版源码调用链支持的缺陷。
- 每个修复使用独立补丁类和独立证据文档，避免把无关行为合并到一个 Harmony 补丁。
- 补丁默认保留原版行为，只拦截能够明确判定为异常的状态。
- Harmony Prefix 或 Finalizer 发生内部异常时，不应吞掉原版异常或让保护状态永久残留。
- 新修复必须在离线和联机环境分别评估；本机状态修复不自动等于网络同步修复。

## 架构与代码职责

- `src/Plugin.cs`：UMM 生命周期、启用和卸载。
- `src/BugFixSettings.cs`：全部修复主开关和各修复的持久化独立开关。
- `src/DoodadCrateReentryFix.cs`：`DoodadCrate.ActuallyCollapse` 单实例同步重入保护。
- `BuildAndDeploy.ps1`：使用 .NET Framework 3.5 编译器构建，并更新本机、内网测试端和项目安装包。
- `issues/`：保存原始现象、日志结论、源码证据、修复边界和复测结果。

插件使用独立 Harmony ID：

```text
GJKen.BroforceBugFix.DoodadCrateReentry
```

卸载本插件时只移除该 ID 的补丁，不影响 `Broforce_Online` 或其它 Mod。

## 开关模型

每个修复的生效条件统一为：

```text
UMM Mod 已启用 && EnableAllFixes && 对应修复开关
```

`EnableAllFixes` 关闭时卸载所有修复，但不修改独立开关值。重新打开主开关后，只恢复仍被独立开关选中的修复。设置面板中的变化立即调用补丁协调逻辑，UMM 保存设置时写入持久化配置。新增修复必须接入 `ApplyConfiguredFixes` 和 `RemoveAllFixes`，并拥有自己的设置字段。

## 爆炸箱重入保护

### 原版缺陷

`DoodadCrate.CreateExplosion` 调用 `MapController.DamageGround`。相邻爆炸箱收到伤害后会进入 `DoodadCrate.ActuallyCollapse -> Block.ActuallyCollapse -> EffectsDestroyed -> CreateExplosion`。

`Block.ActuallyCollapse` 在调用 `EffectsDestroyed` 后才进入 `DestroyBlockInternal`，而 `destroyed=true` 又在后者靠后的位置写入。因此箱子 A 和 B 可以在双方都尚未设置 `destroyed` 时形成：

```text
A.CreateExplosion
  -> B.ActuallyCollapse
    -> B.CreateExplosion
      -> A.ActuallyCollapse
        -> A.CreateExplosion
          -> ...
```

### 补丁行为

Prefix 使用引用相等的集合记录当前正在执行 `ActuallyCollapse` 的箱子实例：

- 实例第一次进入时加入集合并执行原版方法。
- 同一实例在原调用返回前再次进入时返回 `false`，只跳过这一次递归调用。
- 不同实例仍正常进入，所以正常的 A 引爆 B 连锁反应不受影响。
- Finalizer 无论原版方法正常返回还是抛出异常都会移除实例，并将原异常原样返回。

保护是同步调用栈级别的，不会永久标记箱子，也不改变 `destroyed`、伤害、爆炸半径或 RPC。

## 构建与部署

路径配置与 `Broforce_Online` 相同，使用 `LocalBroforcePath.props`：

```xml
<Project>
  <PropertyGroup>
    <BroforceManagedPath>...\Broforce_beta_Data\Managed</BroforceManagedPath>
    <UnityModManagerPath>...\UMM\Core</UnityModManagerPath>
  </PropertyGroup>
</Project>
```

标准构建命令：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

成功构建必须同时得到：

- `BroforceBugFix/BroforceBugFix.dll`；
- 本机 `UMM\Mods\GJKen-BroforceBugFix` 部署副本；
- 内网测试端同名 UMM 目录部署副本；
- 构建脚本输出的 DLL SHA-256。

## 复测要求

1. 先在离线模式触发两个相邻爆炸弹药箱，确认只发生有限的正常连锁爆炸。
2. 在 Workshop `3660163376` 的第 2 关复现原故障区域，确认不再出现爆炸特效无限增长或严重掉帧。
3. 检查 UMM `Log.txt` 是否出现 `Suppressed recursive DoodadCrate.ActuallyCollapse call`，且没有 `StackOverflowException`。
4. 联机时房主和加入方都安装相同版本，确认双方仍能看到正常的首次爆炸和后续关卡重开。
5. 分别测试普通木箱、普通弹药箱、单个爆炸弹药箱和多个相邻爆炸弹药箱。

目前完成的是静态构建与补丁目标验证；游戏内回归结果应补充到对应 issue。
