# Aquabro 自动任务：供新对话 AI 接手

用户不会操作程序，只会向 AI 说“检查自动任务”“继续”“停止”等。维护 AI 应直接检查并完成对应操作，再用中文简要报告结果；不要要求用户自己运行命令，也不要仅给操作建议。本文件由 Aquabro/AGENTS.md 指向，位于每轮快照排除的控制器目录中，单轮回退不会删除。仓库根只提供通用的子项目约束发现规则。

如果你是外层控制器启动的单轮代理，且提示中已有“本轮交接参数”，只执行 round.md 指定的一轮；不要接管控制器、再次启动任务、取消自身、修改规则或本文件。以下是用户主动打开的新维护对话的接手流程。

## 固定目标与授权边界

- 最终目标：减小正式部署的 Aquabro.dll 实际字节数，同时保持全部原机制。性能微优化、代码行数缩短、源码变小本身不能算成功。
- 每轮至少减少 512 字节；使用相同正式编译器和参数独立复编译核对，五类蒙版完整字节相同、受影响机制实机前后对照、五组回归通过才保留并新增 docs 验收报告。
- 每轮和重试显式使用 --effort max；模型沿用 Codeg 当前设置。不要改回默认 low，也不要擅自换模型。
- 验证失败：撤销当前方案，换可行实现；候选可用方案耗尽后恢复并跳过，继续找其他候选。不能第一次失败就结束任务，也不能通过放宽测试或改变原机制验收。
- 保留全部既有未提交工作、既有已验收轮次和其他插件修改；不得 stash、checkout、reset、clean、提交或推送来恢复。用户没有要求 Git 发布。
- 修改前后必须实机记录；部署前关闭 Broforce 并确认退出，清理后关闭游戏。不能凭编译通过宣称机制不变。

## 文件与本机入口

以下路径除注明外均相对仓库根目录：

| 内容 | 路径 |
| --- | --- |
| 统一启停入口 | CustomBro/Aquabro/tools/auto-optimize/Start.ps1 |
| 控制器、单轮规则、结果格式 | 同目录 controller.py、round.md、round.schema.json |
| 正式参数独立体积编译 | 同目录 Measure-Build.ps1 |
| 保护的实机完整蒙版探针 | 同目录 MaskSnapshotLive.cs |
| 当前状态 | CustomBro/Aquabro/archive/auto-optimize/status.json |
| 全部验收历史 | 同目录 history.json |
| 控制器锁、停止请求 | 同目录 controller.lock、STOP |
| 控制器日志 | 同目录 controller-批次.log 和对应 .stderr.log |
| 每轮备份和证据 | 同目录 批次/round-轮次/snapshot、attempt-尝试/evidence |
| 每轮失败原因 | 同目录 批次/round-轮次/rejections.json |
| 每次 Codeg 运行 | attempt-尝试/launch.json、launch.log、codeg-result.json、result.json、prompt.md |
| 控制器独立验收记录 | attempt-尝试/size-verification.json、independent-build、Test-*.log |
| 构建与部署设置 | CustomBro/Aquabro/LocalBuild.json（本机配置，不输出凭据） |
| 正式 DLL | CustomBro/Aquabro/_Mod/Aquabro.dll |
| 简要用户说明 | CustomBro/Aquabro/docs/自动精简使用说明.md |
| 验收结果摘要 | CustomBro/Aquabro/docs/自动精简历史.md |

Codeg CLI 通过 `AQUABRO_CODEG_CLI` 环境变量指定，未指定时从 PATH 查找。Codeg 服务需运行；不要盲目重复启动服务或读取凭据。PowerShell 7 和 Python 可分别用 `Get-Command pwsh`、`Get-Command python` 查询。桌面已有启动、状态、停止三个快捷方式，但用户无需自己操作。

在仓库根通过 PowerShell 执行（由 AI 调用工具）：

    pwsh -NoProfile -ExecutionPolicy Bypass -File './CustomBro/Aquabro/tools/auto-optimize/Start.ps1' -Action status
    # action 改为 check / start / stop 分别检查环境、后台启动、请求安全停止

## 新对话先做什么

1. 读取当前 status.json、最新控制器日志及 .stderr.log，文件为零字节也实际读取；读取 history.json 的最近几轮和对应验收报告。不要把本文中的历史数字当作当前状态。
2. 核对 status.pid、controller.lock 指向的进程及其命令行是否真是本控制器。状态时间不更新不等于卡死：实机一轮可能持续较久。PID 也可能被系统复用，不要仅凭同号进程认定仍运行。
3. 如果有 run_id，用 codeg-cli result <run_id> --output-format json 查询是否仍运行；若 status 没有 run_id，查当前轮最新 attempt 的 launch.json 与 codeg-result.json。不要打印令牌或环境中的秘密。
4. 对照以下状态决策。运行中只做只读检查，不要并发操作本项目；需要修控制器或源码时先安全停止。

| 状态 | 处理 |
| --- | --- |
| running / starting / verifying / accepted / retrying，且控制器确实存活 | 已在执行，报告当前轮次、会话和已验收收益；不再启动 |
| stopping | 等待 Codeg 终止、控制器回退；分段等待并给进度，不与回退并发改文件 |
| stopped | 用户要求继续时，确认代理已结束、本轮已恢复后 start；用户只问状态时不要擅自重启 |
| complete | 连续两轮没有达标且可保持机制的候选是正常完成，报告结果；不要为了继续制造改动 |
| blocked / failed 或进程消失 | 读取具体错误和本轮 rejections/会话/日志，修根因；用户要求处理或继续时，完成恢复和核对后 start |

启动成功必须再读新 status.json，确认控制器进程、实际新 Codeg 会话已存在、effort=max；只看到启动脚本打印 PID 不能声称正常执行。运行期间的 DLL 可能是尚未验收候选，收益以 history 的已验收条目及对应文件为准。status 的 total_saved_bytes 是当前批次收益；跨批次要合计有实际起止依据的已验收记录，避免重启后把以前收益说成丢失。

## 用户让你停止或故障恢复

- 正常停止用 Start.ps1 -Action stop。它只发请求：控制器取消当前 Codeg run，确认终止后关闭游戏，恢复当前轮开始时的源码、构建相关文件及两处部署。已验收轮次保留。等待 phase=stopped、控制器退出、锁移除，并核对 DLL/部署再报告完成。
- 控制器异常退出或状态 blocked 但代理可能仍运行时，先查询实际 run；必要时用 codeg-cli cancel <run_id> --json，再查询终态。不能在代理可能仍写文件时回退。不要仅依据“Game process died”误报判断代理退出。
- 如果必须手动恢复，先确认执行代理和控制器均已终止、关闭游戏，定位该轮 snapshot 下的 project-manifest.json、deploy-N-manifest.json。用 controller.py 的 restore(PROJECT, snapshot/project, project_hashes, True) 和 restore(各实际部署目录, snapshot/deploy-N, 对应hashes) 恢复并核对；其 SHA256/路径检查不能跳过。部署顺序先核对 LocalBuild.json 与快照，不猜目录。
- 恢复会覆盖整轮快照范围。若故障后用户又有新修改，先保存那些修改并区分范围，不能用旧快照覆盖用户后来的工作。archive 中证据保留。
- 遗留 controller.lock 仅在确认原控制器和执行代理均已结束、回退/现场核对完成后才可清理；不能删锁来绕过并发保护。
- 修复控制器后运行 test_controller.py；只做与问题相关的验证，不额外改游戏逻辑。恢复运行前核对当前源码正式参数复编译体积与基线相符。出现真正无法解决的环境阻碍时明确报告原因，不能伪造成功。

## 已处理问题与可信历史

- 旧自动化默认继承 low，现已在 Controller.round 中显式传入 --effort max，已通过 Codeg launch.effective.effort 验证。重试也必须如此。
- 交接历史增长曾让 Windows 命令行超过长度限制（WinError 206），现使用 --prompt -，通过 subprocess.run(input=完整提示词, encoding='utf-8') 传递；不要改回把全文放命令参数。完整提示词仍保存为 prompt.md，超过5万字符的真实 CLI 传输已验证。
- Codeg 曾以进度文字结束而未提交JSON，返回 output_schema_unmet。不要把这种基础设施失败当作机制失败或已完成；控制器确认终止后回退，再开新对话重试。round.md 已要求完成/回退后提交最终结构化结果。
- 20261023-100531 第1轮已验收：内嵌蒙版无损DEFLATE，DLL从761856降至159744字节，减少602112字节（约79.03%）。完整蒙版、原机制、五回归及部署已验收，报告在 docs/自动精简-20261023-100531-0001.md。此处仅为历史锚点；当前值以状态、历史、实际文件为准，后续不要重复实施这一轮。
- 后续 DLL 体积节点及已验收源码微优化摘要见 `docs/自动精简历史.md`；中间细节以该表和保留的体积报告为准。
- 20261023-151126 批次曾从159744字节继续启动；批次/PID/会话随重启变化，绝不能硬编码本文件旧值用于停止或恢复。

## 用户可在下次直接说

“接手 Aquabro 自动任务，先读 CustomBro/Aquabro/AGENTS.md 和自动任务交接说明，检查当前状态；如果异常就修复并继续，保留原机制，每轮用 max。”

只要新对话仍打开这个仓库，AI 会按根目录通用规则读取 Aquabro/AGENTS.md，再找到本文件；直接打开 Aquabro 目录也可读取其 AGENTS.md。新增角色只需在自己的目录维护 AGENTS.md，无需逐项修改仓库根文件。换到其他目录/机器不会自动继承该工作区说明。跨机器需保留控制器及说明，运行时 archive/本机配置本来不在 Git，不能假设只克隆源码就带有会话、备份和部署现场。
