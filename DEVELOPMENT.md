# 开发说明

本文档面向参与 STS2 Philosophers 开发与验证的同学。玩家玩法、安装方法和当前限制请先阅读 [README.md](./README.md)。

## 自动接续

设计优先于代码。西哲接续时，除活动问题与检查点外，先读G盘真源 `02想法与机制/11西哲玩法设计.md`。通过自动检查的节点仅说明实现符合当前规则，不能证明哲学转译或可玩性成立。先完成具体选择、代价、表达机会和反例设计，再实施最小可验证内容。未完成设计审查的载体草稿不得混入部署；草稿归属与继续条件记录在RESUME。

当前任务使用应用内定期唤醒和文件检查点，默认每两小时检查一次。完整办法及当前动作在 Obsidian `08问题闭环/02自动接续.md`，仓库恢复镜像为 [RESUME.md](./docs/issues/RESUME.md)。检查点在阶段中持续保存，自动运行按现有授权接续，不创建新任务、不更换账号或模型、不消耗重置券。

只在实际额度耗尽、平台拒绝请求或真实阻塞时停止；额度仍可用且有明确下一步时继续，不按预估余量提前停止。恢复后由后续唤醒尝试执行；这不是额度重置事件触发器，也不能保证额度耗尽时调度仍会运行。本地任务依赖电脑开启、应用运行以及项目和G盘文件可用。纯笔记或接续配置变化只做文档一致性检查，不重复构建游戏产物。

## 开局事实检查

运行 node tools/design/StartingHandOpportunityChecks.cjs，读取 docs/design/native_starting_facts.json，枚举五个原始牌组共1800个首手组合的直接格挡能力。事实绑定本机原生DLL哈希，仅摘录数据；原生反编译源码留在忽略目录bin/design/native。范围排除进阶额外牌、升级、额外遗物与状态变化；不模拟完整战斗、星辰或Osty，不把组合比例当游戏触发率。此开发数据不嵌入PCK，单独更新时无需MOD构建。设计解释以G盘15开局表达机会为准。

## 有限承诺C3协议

SocratesBoundedCommitmentState与独立SocratesBoundedCommitmentStateCodec提供C3内存及版本1保存协议，两项专用检查随纯逻辑入口验证。13字段均必需，未知/重复字段与非法状态关系拒绝，恢复失败不返回可领奖的新状态。适配器应依次打开正常回合、领取旧奖励、冻结公开事实、声明/保留准则、锁定行动、结束观察；战斗结束或角色死亡必须清空待发奖励。C3两项均抽1，允许下一回合再次修订，当轮修订不产生新收益。尚无实际游戏保存挂钩或诘问杯机制替换；G盘16有限承诺试玩规格是当前设计依据。D13及D15在排除6个W3D2草稿的独立验证目录通过VerifyMod并部署，无游戏验收。D16定向核对发现普通存档不保存战斗牌堆：首版应在原生新战斗建立时初始化临时C3状态，不单独恢复某轮协议并额外强制保存。Codec只证明独立协议恢复，未提供全游戏快照。抽牌遵循原生满手/禁抽限制，不重试或积存；异步适配还需检查整个战斗令牌及live状态。详细证据在G盘03研究与资料/技术研究/01有限承诺接入。

## 西哲纸面试玩

D9保留C1全额防护/单体解围并加入C2半额防护/冻结攻击者集合对照；运行同一专用检查包含19项规则检查与两版各123条相同行动前缀，浏览器检查覆盖规则切换、目标控件和收益边界。增加可达机会不代表设计定稿；延续/修订价值、原生结束阶段和真实战斗仍未验证。

当前西哲图配置为schema3：保留12条来源样例，8条为Executable、4条为SourceOnly；后者保留旧序列与撤回理由，不参与可执行路径校验。5条已撤边继续保留ID和来源，enabled=false禁止新生成及旧候选接受。局内存档格式未改；“可执行样例”只指图状态检查，不代表完整可玩路线。

未完成载体草稿仍在主目录时，本次图修订在独立工作树`S:/01_Workspace/Projects/SlayTheSpire2Verification`验证，输入为186fe2c加本阶段图配置、解析及测试文件，排除6个草稿。该目录`bin/Release/net9.0/verification.md`保存本次部署证据；不要把主目录旧报告当作最新，也不要直接在含草稿的工作区构建部署。

`docs/design/socrates_commitment.html`是苏格拉底候选设计的交互片段，当前设计真源为G盘`02想法与机制/16有限承诺试玩规格.md`，14页保留设计沿革。默认C3推演固定手牌的六回合片段与三个边界情境，C1/C2单轮对照折叠保留。抽牌仅计请求，不生成真实牌堆的手牌；不是游戏引擎、完整战斗、存档或平衡验收。它不进入PCK，不替换诘问杯。

C3模型检查：`node tools/design/SocratesC3PaperChecks.cjs`，15项覆盖持续保留、反复修订、领取时点、隐藏信息、身份和终局；对应浏览器检查为`SocratesC3BrowserChecks.cjs`，参数与下面一致。320/360/760像素及明暗主题已检查。

历史模型检查：`node tools/design/SocratesCommitmentChecks.cjs`。覆盖12固定样本的123条合法行动前缀，以及冻结、修订、死亡、防重复等边界。

浏览器检查：先用visualize技能的`scripts/render.py`将片段包装到被忽略的`bin/design/socrates_commitment_preview.html`，再运行`node tools/design/SocratesCommitmentBrowserChecks.cjs <预览路径> <Playwright包路径> msedge`。包路径由本机运行时确定；脚本只打开并关闭独立无界面浏览器，不操作已有窗口。截图在预览同目录，未提交。仅修改纸面试玩时运行其专用检查，不调用游戏构建。

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

## 有限承诺交互接入边界

D17定向核对本机公开意图、原生选择与动作队列。隐藏/未知信息不补算；DeathBlow为SingleAttackIntent子类；完整AutoPrePlay结束后才满足设计快照时点。原生卡片弹窗不能直接替代自愿修订，ActionEnqueued先于取消检查，不能作为有效执行证明。技术证据和设计裁决见G盘技术研究01有限承诺接入及16有限承诺试玩规格。下一步先更新C3纸面跨回合原型，未接游戏、未重建部署。

## 苏格拉底知识记录协议

D59新增独立的`SocratesKnowledgeRouteRecord`与Codec，仅实现两天向导材料、明确说法、已见/未处理/离开及合法修订。载荷版本1按选择日志重放并核对说法、问题和晚入来源，拒绝缺字段、重复键、越序与伪造历史；缺载荷和损坏载荷分别返回Absent/Invalid。`SocratesKnowledgeRouteChecks`覆盖前两幕全部合法组合及保存断点。

记录尚未接入`PhilosophyRunState`、事件或地图，也没有切换诘问杯。生产玩法仍按README中的入口原型运行。旧节点策略在节点手稿草稿取消后无生产绑定调用，暂时保留隔离；新记录不引用牌序状态或节点策略。第三幕状态与真正运行存档集成留待后续设计阶段。VerifyMod已通过全量逻辑、Release零警告零错误、PCK及部署三文件哈希；游戏验收未执行。

## 苏格拉底设计恢复边界

D73最新判决以G盘真源57苏格拉底试授设计稿为准：地图意向方案已取消作为知识第三幕，有限询问仅保留未来问号，试授与反问仍是待体验候选。没有批准新的主要能力、第三幕、牌组修改或旧杯迁移执行；不要从历史页的旧下一步恢复实现。

技术研究02试授副本身份记录本机限定只读证据：`SerializableCard.Id`是模型身份，不能指认同名副本；`CardModel.ToSerializable`/`FromSerializable`和显式`CardTransformation`提供自有逐记录引用接续候选。保存牌名、模板回填或猜最终索引不能用于自动删除。自有方案尚无代码、真存档验证或游戏验收，不能视为已完成的能力。纯设计与记录更新不重复运行构建或部署。

D79德性最新设计见G盘58苏格拉底德性撤离候选。技术研究03主动撤离结束边界核对到`EndCombatInternal`包含`AfterCombatVictory`、胜利进度和事件，不能当中立撤离并仅屏蔽奖励；`CombatRoom.Exit`只提供部分清理，`MarkPreFinished`仅设标记。主动撤离仍是待体验候选，未批准自有退出事务；护送中段停止下一自愿战斗只是另一个问号候选，不能冒充已进入战斗后的救险能力。候选等待主要乐趣方向反馈，不新增生产状态、修改砝码或执行游戏验收。

## 苏格拉底双分支决定（D80）

2026年9月17日用户要求保留知识/德性两候选做分支；具体设计见G盘59苏格拉底双分支。既有入口已支持单次邀请后选择一个问题，复用WesternEntryPolicy防双领；本次没有新增生产能力。知识试授和德性非胜利撤离按各自循环顺序发展，取消必须淘汰一候选的等待，ISSUE007恢复处理中。代码、资源、游戏文案不变，README已核对，纯设计不重复构建/部署。源笔记已同步S盘阅读副本，G盘仍为真源；ISSUE010推送阻塞独立保留。

## 知识试授纸面循环（D83）

打开docs/design/socrates_trial.html可操作三选一卡、试授、固定战斗及收入/放弃；全部卡与敌人为虚构，实际游戏没有变化。使用Node运行tools/design/SocratesTrialPaperChecks.cjs（14项限定检查，含20种原话/回应/处置组合）；SocratesTrialBrowserChecks.cjs接受Playwright模块路径与可选浏览器通道，默认无界面msedge，检查流程、文字安全及860/360/320明暗响应。截图输出到忽略目录bin/design。本阶段未运行游戏构建、PCK或部署，README核对仍描述生产入口。

普通收入尚缺同条件战斗比较；试授最后机会弱优于同牌普通收入是额外后悔权，并非思想通过。下一阶段先补这一对照，不追加休眠C#协议或按牌名找待定副本。真实保存与变形身份仍参考G盘技术研究02未实现候选。

D84将普通收入和跳过也接入同条件固定战斗，新增六组前缀对照；当前18项纸面检查与浏览器流程通过。战后权限和机会成本分别展示，实际材料不认证知识。相同虚构手牌/敌人下结果相同，不证明原生概率或体验。下一阶段独立做德性纸面循环；知识真实副本仍未验证，不先铺保存基础设施。

## 德性坚持与撤离纸面循环（D85）

打开docs/design/socrates_retreat.html可体验普通战斗中的请求、取消、确认及合法后继。旧标准、行动目的与本次勇敢判断分别表达，不按生存或胜利判德性。虚构金币和生命保留、整场奖励丢失、节点不可重入；Boss与死亡拒绝撤离。

使用Node运行tools/design/SocratesRetreatPaperChecks.cjs，17项限定检查通过，含15种表达组合和取消/过期/死亡/Boss/后继边界。SocratesRetreatBrowserChecks.cjs接受Playwright模块路径，默认无界面msedge；浏览器流程、输入文字安全与860/360/320明暗响应通过。截图写bin/design，860/360已查看。它不证明真实战斗退出、保存、持久收益滥用或体验；本阶段不构建/PCK/部署，README核对生产未变。

下一步限定核对知识已有主牌组对象和ToSerializable输出的只读诊断可行性，先确认控制台上下文与副作用；不写保存、不生成/删除卡、不预铺全局保存补丁。需真实游戏样本的验证单列等待，禁止自动启动游戏。

## 试授卡记录限定诊断（D86）

用户自行启动游戏后，可在静默猎手单人局、非战斗位置使用已经启用的开发控制台，手动执行`socratescardprobe`，无参数。未启用控制台时不要求修改配置。命令仅转换主牌组已有、无附魔的原生StrikeSilent/DefendSilent，拒绝SavedProperty getter及转换方法Harmony补丁；同一对象转换两次，按准确引用核对独立记录、模型/升级/加入层字段与主牌组引用不变。不会写保存、生成/删牌、调用FromSerializable或登记持久身份。

OBSERVED只表示本次限定调用存在同字段多个对象且转换引用独立；INCONCLUSIVE表示没有支持牌或匹配副本；拒绝/异常不修牌。将完整结果交回后再判断必要实验。它不验证最终保存数组、真实读档、永久变形或试授安全，不能视为新能力。原生SavedProperties转换会调用getter，因此不扩展到任意卡。

10项纯报告反例及全量逻辑、Release零警告零错误、PCK与部署三文件哈希通过，见bin/Release/net9.0/verification.md；部署输入为d185152加本阶段C#/测试文件。README核对玩家生产能力未变，游戏诊断未执行。两纸面原型仍待具体体验反馈，不能以自动通过完成苏格拉底或据此扩写下一人物。

D87按G盘60深化知识后续追问：默认折叠回看原话/当前句/材料，七种明确历史状态产生不同下一问；连续固定片段演示同目标、两目标和未抽到，不是游戏三幕。新奖励不继承旧信念，自由文字仅记录新句，不认证限定。知识纸面检查现为23项，浏览器与860/360/320明暗响应通过，360暗色回看图已查看；命令同前，截图增加socrates_trial_continuity_*。本阶段无生产变化，不构建部署。体验和真实诊断各自待验证，用户明确继续，下一独立设计审德性持久收益反例。

D88按G盘61将撤离候选收紧至稳定回合首次手动行动前；旧afterAction仅折叠反例对照，切换须重置。新增harvest/harvestDanger虚构片段，击倒附属目标直接得3金币，胜利另得10；旧时点16生命/3金币，新时点收割＋防御付敌方结算后撤离12/3，立即撤离16/0，继续一条固定胜利序列4/13。合法收益不回滚，回答不改变资源，时点修订不证明平衡或真实安全。撤离纸面检查现为23项及浏览器/明暗响应通过，360暗色时点图已查看，截图socrates_retreat_timing_*。定向本机HandOfGreed/Feed出牌主体已有持久收益调用证据，未实际运行；药水/自动阶段/原生退出保存仍待验证。本阶段无生产变化，不构建部署，README核对。下一独立设计记录撤离机会与德性后续追问。

D89按G盘62在每个稳定模拟回合记录本能力撤离规则资格，渲染/查询不增加记录；Boss/机会用尽的胜利不叫主动拒绝退出，曾有资格也不证明看见/考虑。沿合法后继复制旧标准/明确目的/自评/修订和资格事实，默认折叠显示具体下一问；新理由不继承/回写，下一战局部窗口重置但幕机会不恢复。撤离28项纸面检查与浏览器/明暗响应通过，360暗图已查看，截图socrates_retreat_continuity_*。本阶段无生产变化，不构建部署，README核对；下一设计回原始对话审用途测试与知识、逃生与德性理由的思想转译边界。
