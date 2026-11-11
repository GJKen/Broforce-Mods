# Aquabro 海王安装包

需要先安装 Unity Mod Manager、RocketLib 和 BroMaker 2.6.2 或更高版本。

安装前关闭 Broforce。将包内 `UMM/BroMaker_Storage/Aquabro` 文件夹复制到当前游戏配置的 `UMM/BroMaker_Storage/` 下，覆盖已有的 `Aquabro` 文件夹。普通安装也可将同一个角色文件夹放入 BroMaker 实际使用的 `BroMaker_Storage/`。

最终应为：

```text
BroMaker_Storage/
└─ Aquabro/
   ├─ Aquabro.dll
   ├─ Aquabro.json
   ├─ gz.aquabro.mod.json
   └─ *.png
```

DLL、两份 JSON 和全部 PNG 必须一起安装。启动游戏后通过 BroMaker 菜单选择 Aquabro。

`bin/`、`obj/` 是编译和测试工作目录，不用于安装；安装包不附带游戏程序集、依赖 DLL、PDB 或编译缓存。
