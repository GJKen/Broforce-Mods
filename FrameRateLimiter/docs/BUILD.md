# FrameRateLimiter 构建说明

## 本机配置

复制项目根目录的 `LocalBroforcePath.props.example` 为 `LocalBroforcePath.props`，填写本机 Broforce `Managed` 目录和 UMM `Core` 目录。该文件只用于本机构建部署，已被 Git 忽略。

## 构建

从 `FrameRateLimiter` 项目目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1
```

脚本使用 Broforce 实际 Unity 程序集、UMM 程序集和 .NET Framework 3.5 `csc.exe` 编译源码。构建产物统一写入：

```text
Release\UMM\Mods\FrameRateLimiter\FrameRateLimiter.dll
Release\UMM\Mods\FrameRateLimiter\Info.json
Release\FrameRateLimiter.zip
```

脚本还会部署到本机 UMM：

```text
<UMM>\Mods\GJKen-FrameRateLimiter\FrameRateLimiter\
```

可使用 `-SkipDeploy` 只生成 Release 包而不复制到本机 UMM：

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildAndDeploy.ps1 -SkipDeploy
```

## 包结构

```text
Release\
├─ FrameRateLimiter.zip
├─ README.md
├─ manifest.json
└─ UMM\
   └─ Mods\
      └─ FrameRateLimiter\
         ├─ FrameRateLimiter.dll
         └─ Info.json
```
