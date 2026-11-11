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

`obj/`、`bin/` 是编译和测试工作目录，不用于安装；安装包不包含游戏程序集、依赖 DLL、PDB 或编译缓存。
