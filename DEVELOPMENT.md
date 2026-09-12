# 开发说明

本文档面向参与 STS2 Philosophers 开发与验证的同学。玩家玩法、安装方法和当前限制请先阅读 [README.md](./README.md)。

## 自动接续

当前任务使用应用内定期唤醒和文件检查点，默认每两小时检查一次。完整办法及当前动作在 Obsidian `08问题闭环/02自动接续.md`，仓库恢复镜像为 [RESUME.md](./docs/issues/RESUME.md)。检查点在阶段中持续保存，自动运行按现有授权接续，不创建新任务、不更换账号或模型、不消耗重置券。

用量不足时不进入新阶段，恢复后在后续唤醒尝试执行；这不是额度重置事件触发器，也不能保证额度耗尽时调度仍会运行。本地任务依赖电脑开启、应用运行以及项目和G盘文件可用。真实额度中断后的自动恢复尚待验证。纯笔记或接续配置变化只做文档一致性检查，不重复构建游戏产物。

## 赐福流程代码结构

- `src/Patches/NeowProceedPatch.cs` 与 `src/Patches/ActTwoPhilosophersGazePatch.cs` 分别负责第一层和第二层事件插入。
- 第一层插入会拦截已经结束的涅奥事件；必须先对底层涅奥 `EventRoom` 调用 `MarkPreFinished` 并保存，再用嵌套房间进入“诸子观照”。否则退出读档会恢复一个仍可结算的涅奥房间，造成奖励重复领取。
- `src/Events/PhilosophersGaze.cs` 负责事件页面、遗物授予与替换、拒绝处理和保存。
- `src/Events/PhilosophersGazeFlowPolicy.cs` 定义页面、选项和结果转换。
- `src/Events/PhilosophersGazeContinuationPolicy.cs` 负责第二层候选的通用门控；六条“根遗物 → 固定后继”已隔离到 `LegacyRelicContinuationCandidateSource`，事件暂时继续使用该兼容候选源。
- `src/Philosophy/` 保存新赐福流程的局内哲学状态、第一层候选策略与序列化逻辑。状态以不可见的 `STS2PhilosophersRunState.V1_*` 保存标记写入本局存档，载入时先取出标记再交给游戏恢复原始事件历史；该标记没有本地化或资源，也不产生可见遗物。
- Phase 2A 在 `ActBehaviorState` 中分开保存通用游戏事实、表达机会与行为印象。事实可在一场战斗内累计；同一表达机会和同一行为印象每场最多结算一次。`ActiveCombat` 支持战斗中途随局内状态往返，旧 Phase 1 存档缺少的新字段会恢复为空集合。
- `src/Patches/BehaviorObservationPatch.cs` 监听游戏全局战斗开始、出牌完成与战斗结束钩子，仅在单人局调用 `BehaviorObservationRecorder`。当前事实包括战斗开始/完成、出牌总数及攻击、技能、能力、状态、诅咒、任务等牌类型；事实不直接生成行为印象，也不参与候选。
- 每场被记录的单人战斗结束后，日志会输出 `[STS2Philosophers] Behavior observation:` 摘要；字段按固定顺序排列，事实键按代码序排序，可用于核对跨战斗累计与读档恢复。
- `config/thinker_proposals.json` 是“人物 + 具体思想提案”的配置雏形，以嵌入资源进入 DLL。Phase 1 只登记六件现有根遗物；`qualification_rule_ids` 与 `resonance_tags` 保持为空，等待后续行为候选设计。
- 六件根遗物分别保存第二层是否已经处理；12 件遗物的战斗状态保存在各自实例上。当前唯一显式跨遗物数据迁移是青玉佩的 `Virtue` 在替换时写入熊掌的 `InheritedVirtue`。
- `tests/ModLogicChecks/Program.cs` 覆盖页面流、根遗物互斥、第二层过滤与替换、德继承、重复回调和本地化键。

以上是 Phase 1 开始时的兼容基线，不代表未来动态候选架构；详细设计状态与未决定问题以 Obsidian 的 `02想法与机制/02赐福系统` 和活动问题队列为准。

## 本机配置

项目从根目录的 `local.props` 读取本机路径。该文件不会提交；新机器请复制 `local.props.example`，并设置：

- `GameDirectory`：《杀戮尖塔 2》安装目录。
- `GodotExecutable`：Godot 4.5.1 Mono 控制台程序。
- `DotNetRoot`：包含 `dotnet.exe` 的 .NET 根目录。
- `DeployOnBuild`：普通构建是否同时生成 PCK 并部署。

## 纯逻辑测试

推荐从仓库根目录执行完整阶段检查：

```powershell
./tools/VerifyMod.ps1
```

该入口使用 `local.props` 中的 SDK，依次运行纯逻辑测试、禁止部署的 Release 构建、无界面的 PCK 打包及独立加载校验。全部通过且游戏未运行时才复制三个 MOD 文件并逐一核对 SHA256；游戏运行时保留校验结果并报告部署受阻。传入 `-SkipDeploy` 可明确只检查。任何测试、构建或内容包错误都会停止后续部署。

每次执行先重置 `bin/Release/net9.0/verification.md` 为进行中；失败记录具体阶段和错误，避免误读上次成功。成功报告保存开始/结束时的工作区状态、源代码基准提交与产物哈希，已跟踪差异保存在同目录 `verification_changes.patch`；未跟踪文件仅列名，不归档内容。构建使用整个工作区，不能把报告中的基准提交当成部署版本的完整描述。脚本不启动游戏、Steam 或 Godot 界面，自动检查不能代替游戏内验收。

阶段收尾按 [AGENTS.md](./AGENTS.md) 的固定顺序执行。审查、纳入本阶段的 Godot 生成配置与清单修改也应随阶段提交；仅记录其他窗口的改动，不擅自提交。构建后再次检查这些文件，避免打包生成的新差异被遗漏。

以下检查不启动游戏：

```powershell
dotnet run --project .\tests\ModLogicChecks\ModLogicChecks.csproj -c Release
```

当前覆盖木铎回合规则、青玉佩仁行与奖励策略、熊掌回合触发、绳墨序列、墨色竹简的相利状态、历物筹的共同格挡与首次破甲奖励、守城图与守城械的守御窗口、大瓠的保留与两次奖励、全生璧的能量保全与减伤，以及“诸子观照”三阶段页面流、第一层候选等概率与状态往返、行为事实累计、表达机会与印象单场去重、重复战斗开始、未完成战斗丢弃、旧存档兼容、导航零副作用、根遗物互斥、二层过滤与替换、德继承、重复回调门控、插入防重和中英本地化键。

## 构建 DLL

只构建程序集，不生成 PCK，也不部署：

```powershell
dotnet build -c Release -p:DeployOnBuild=false
```

输出位于 `bin\Release\net9.0\STS2Philosophers.dll`。

## 构建与校验 PCK

先按 `local.props` 设置本机路径，再调用构建脚本：

```powershell
$godotExecutable = 'C:\path\to\Godot_console.exe'
$dotNetRoot = 'C:\path\to\dotnet'

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\BuildContentPck.ps1 `
  -GodotExecutable $godotExecutable `
  -DotNetRoot $dotNetRoot `
  -ProjectDirectory .\content `
  -OutputPath .\bin\Release\net9.0\STS2Philosophers.pck
```

独立加载并检查内容包：

```powershell
$env:DOTNET_ROOT = $dotNetRoot
$env:DOTNET_ROLL_FORWARD = 'Major'
$verifierProject = (Resolve-Path .\tools\PckVerifier).Path
$verifierScript = (Resolve-Path .\tools\PckVerifier\VerifyContentPck.gd).Path
$pckPath = (Resolve-Path .\bin\Release\net9.0\STS2Philosophers.pck).Path

& $godotExecutable --headless `
  --path $verifierProject `
  --script $verifierScript `
  -- $pckPath
```

校验器会确认本地化文件和事件、遗物纹理已经打包且可以加载。

## 部署

`DeployOnBuild` 未设为 `false` 时，普通 Release 构建会生成 PCK，并把以下文件复制到 `$(GameDirectory)\mods\STS2Philosophers`：

- `STS2Philosophers.dll`
- `STS2Philosophers.pck`
- `STS2Philosophers.json`

按仓库规则，自动部署前必须先完成相关测试、DLL 构建与 PCK 校验，并确认游戏进程未运行。部署后应对源文件和目标文件进行哈希或等价校验。不要自动启动 Godot GUI、Steam 或游戏本体。

## 游戏内调试

必须从 Steam 启动游戏。启用 Mod 后，初始化日志应包含：

```text
[STS2Philosophers] Initialized successfully.
```

Windows 日志位置：

```text
%APPDATA%\SlayTheSpire2\logs\godot.log
```

进入一局游戏后，按反引号键或单引号键打开开发者控制台：

- `kongzimuduo`：授予木铎。
- `kongziqingyupei`：授予青玉佩。
- `mengzixiongzhang`：授予熊掌。
- `xunzishengmo`：授予绳墨。
- `mozimosezhujian`：授予墨色竹简。
- `huishiliwuchou`：授予历物筹。
- `mozishouchengtu`：授予守城图。
- `qingulishouchengxie`：授予守城械。
- `laoziwuweishujian`：授予无为书简。
- `laozishuiyu`：授予水玉。
- `zhuangzidahu`：授予大瓠。
- `yangzhuquanshengbi`：授予全生璧。
- `mengzixiongzhang virtue [amount]`：查看或设置当前玩家的德；持有熊掌时读写熊掌自身继承的德，否则读写青玉佩。

## 项目结构

- `src\Events`：事件模型和纯逻辑策略。
- `src\Patches`：Harmony 插入点。
- `src\Relics`：按思想家划分的遗物、状态与控制台命令。
- `content\STS2Philosophers`：中英文本地化和纹理资源。
- `tests\ModLogicChecks`：不依赖游戏进程的逻辑检查。
- `tools`：内容包构建、校验和占位资源工具。

新增或重命名内容前必须遵守 [NAMING.md](./NAMING.md)，仓库协作和验证规则见 [AGENTS.md](./AGENTS.md)。

## 西哲W1基础

config/western_routes.json与WesternRouteCatalog登记七问题六入口；WesternActOneCandidatePolicy使用独立WESTERN_ACT_ONE_THINKERS键生成并保存六选三候选，复用PhilosophyRunState编码，无新增公开ModelId。测试覆盖20种组合、既定映射、读档稳定、与东哲候选隔离和问题选择授权。目前尚未接入游戏事件或战斗机制，后续阶段见Obsidian的10西哲路线实现。

## 西哲研究基线导入

tools/ImportWesternResearch.ps1 -ResearchDirectory <西方哲学史笔记目录> 从14人物索引和18游戏流程投影导入config/western_research.json。固定核对89人和12样例，重复运行输出一致；原文哈希与出处保留。该文件不嵌入游戏，研究用途不直接决定可玩资格。WesternResearchChecks随纯逻辑入口检查覆盖与边界。

## 西哲三幕图

W2D将配置升级为版本2，每条边新增WesternEdgeContext：起终问题、受控关系类型、中介、明确无额外行为门槛、伦理与解释边界、LocalPlanning证据状态及来源。现有12样例和节点/边ID保持；已有存档无需改节点ID。校验拒绝伪造直接影响、丢失必要中介、问题错配、未知行为规则及证据状态升格。上下文是本地规划转录，不是新史学考证；后继游戏UI尚未开放。

western_graph.json嵌入47节点42边和12条已可遍历的样例。WesternRouteGraph按当前节点、幕位、问号前置和一局人物去重过滤，最多展示三人并保存候选窗口；完整路径前瞻排除无法抵达结局的选项。WesternJourneyState现已接入PhilosophyRunState的可空字段，旧存档不自动生成旅程；游戏UI尚未接入。拒绝以幕号防重，未接受挑战不生成条件，允许后幕反思、延迟连续边和无新主说的保留结局。关系说明来自研究样例，不能将条件性同题关系改称历史师承。

## 西哲入口实践纯逻辑

WesternBeingPracticePolicy另行实现8个存在与变化后继问题面的纯规则，覆盖不变重复、潜能现实顺序、原子组合、三类组合、成对表达、交替反思、少量异名与差异序列。WesternBeingChecks为每项检验正反例；这些规则尚未分配给游戏遗物或后继事件，不能当成已可玩内容。

W4A由PhilosophersGazeWestern分部提供西哲候选、人物问题与结果页，NeowProceedPatch同时预保存西哲候选。WesternEntryPolicy检验幕位、事件结束、邀请、当前主说及遗物所有权，成功获得指定遗物后才写入共用主说/印记和WesternJourney。没有新事件ModelId；原东哲授予检查增加西哲占位，机制数值不变。全量检查与部署通过，正常入口已开放，三幕后继未接，尚待游戏验收。

W3B新增七件入口遗物和WesternPracticeRelic共享战斗适配；保存字符串由WesternPracticeStateCodec校验，错误路线/空或损坏载荷不补发收益。BeforeSideTurnStart建立回合，AfterCardPlayed记录所属玩家事实，AfterSideTurnEnd结算，AfterPlayerTurnStartLate领取，AfterCombatEnd清空。模型已注册事件池、配套中英本地化及SVG/轮廓；PCK验证包含14张新增纹理。没有正常路线入口，未来W4接入，尚未进行游戏验收。

WesternPracticeState将七问题映射为有限的回合实践，接收所属玩家的牌类型、模型ID、打出身份与自动标记；CloseTurn只结算一次，TakeReward只在次回合领取一次，EndCombat清空待发收益并保留统计。调用者负责战斗开始/结束与当前主说绑定，尚无游戏适配器。WesternPracticeChecks验证七组正反例、保存和重复回调及长连打收益上限。
