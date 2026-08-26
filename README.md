# Broforce-Mods

这是 Broforce 相关 Mod 的 Git 仓库。

## 项目

- `Broforce_Online`：第三方 Workshop 地图联机、FRP Direct 和在线诊断。
- `BroforceBugFix`：独立的原版游戏缺陷修复插件；首个修复处理 `DoodadCrate` 爆炸递归栈溢出。

## 更新约定

`QuickUpdate.bat` 位于仓库根目录 `D:\Study\C#\Broforce-Mods\QuickUpdate.bat`，用于重大改动完成后的快速同步。脚本会先从 `origin/main` 拉取更新，再全量暂存、自动生成提交并推送到 `origin main`。

运行脚本前应确认工作区中的改动都属于本次发布范围；日常小改动和需要精细拆分的提交应先手动检查，不要直接执行该脚本。
