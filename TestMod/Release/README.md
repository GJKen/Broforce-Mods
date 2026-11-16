# Test Mod 安装包

需要先安装 Unity Mod Manager。

安装前关闭 Broforce。将包内 `UMM/Mods/TestMod` 文件夹复制到当前游戏配置的 `UMM/Mods/` 下，覆盖已有的 `TestMod` 文件夹。

最终应为：

```text
UMM/
└─ Mods/
   └─ TestMod/
      ├─ TestMod.dll
      └─ Info.json
```

启动游戏后，在 UMM 面板启用 Test Mod。自动清敌默认开启，帧率数字默认显示，可在设置中关闭。

“作弊”分类可选择 P1～P4，分别开启无敌、无限技能与自动原地复活，死亡后也可点击“立即原地复活”。联机各端都需启用新版 TestMod，任意一端可为任意玩家操作。作弊默认关闭，不保存到下次启动；关卡结束后无法复活，危险位置可能导致再次死亡。

`obj/`、`bin/` 是编译和测试工作目录，不用于安装；安装包不包含游戏程序集、依赖 DLL、PDB 或编译缓存。
