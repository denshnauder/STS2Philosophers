# 开发说明

本文档面向参与 STS2 Philosophers 开发与验证的同学。玩家玩法、安装方法和当前限制请先阅读 [README.md](./README.md)。

## 当前工作与资料真源

正常任务直接查询 Linear 授权、检查 Git 工作区并读取该 Issue 所需文件；不执行固定恢复仪式，不读迁移前 `ACTIVE`／`RESUME` 队列，也不按旧 Dxxx“下一步”自行找工作。只有真实中断且已有明确恢复点时才读取它；普通交付完成后直接回到 Linear 队列。

GitHub 仓库 `docs/` 是现行设计与版本化文档的唯一写入目标；当前入口见 [`docs/design/current/00当前设计索引.md`](docs/design/current/00当前设计索引.md)。项目不再使用 Obsidian，不维护 Vault、Remotely Save、G/S 同步或每批写回 Drive。Google Drive 保存研究资料、附件和必要历史存档。来源、去向和外部保留项见 [`docs/design/current/99迁移清单.md`](docs/design/current/99迁移清单.md)；旧段落中的 G 盘真源、同步指令、任务状态或历史下一步只作证据，不能覆盖 Linear、当前 main、README 和当前入口。

Linear 的 `Todo` 表示授权，不表示可跳过人物顺序；`In Progress` 先核对安全恢复范围，提前领取的后序人物只安全收尾原子修改及必要公共阻断。人物以 `config/western_research.json` 的 `people` 数组顺序逐位核对真实交付及 `blockedBy`，同一人物内才按优先级、创建时间选择；公共阻断 Bug 与非人物工程按其明确授权范围处理。`Backlog` 默认未授权，只有 ENC-193 的既有持续补位授权可按同一顺序补位，不复制票或改依赖。父任务也检查其子项与完成条件，不按 AutoRun、标签或指派人过滤。研究主要用途完成规定研究并提交推送即可 Done；玩法人物须完成合法可达、内容、保存、验证、构建、安全部署及回退后才 Done。玩法增量验证后可进入同批待部署，详见下节。无合法可执行项时停止；Linear 不可访问只凭真实中断记录继续此前已授权的同一任务。

## 任务选择与批量交付

`tools/SelectDeliveryIssue.ps1` 只读校验并默认输出六行摘要，不写 Linear。完整项目数据由程序处理，不打印到模型上下文；保留 89 人映射、原序、主要用途、授权及真实依赖守卫。正常继续当前合法 In Progress；本人物无进行中任务才领取 Todo。异常返回阻塞原因，未知依赖、分页失败和部分响应一律不能当作无阻塞。

直接读取实时 Linear（PowerShell 7）：

```powershell
# 在进程环境预先配置 LINEAR_API_KEY（只读权限即可）；不要把凭据写进命令、仓库或日志。
pwsh -NoProfile -File ./tools/SelectDeliveryIssue.ps1
# 默认项目为现有 STS2 Mod；必要时显式传 -ProjectId <项目 UUID>。
```

API 认证与分页遵循 [Linear 官方文档](https://linear.app/developers/graphql) 和 [分页说明](https://linear.app/developers/pagination)。脚本自动遍历项目全部分页（含归档），只给候选补取全部入向 blocks 关系；项目外依赖只参与依赖核对，不加入人物队列。检查 HTTP／GraphQL 部分错误、分页游标及重复 ID。API 不提供事务快照；领取或关闭前仍应针对选中任务确认状态和依赖没有改变。

已连接 Linear 工具但本机没有 API key 时，无需再建凭据：在工具编排代码内分页调用 `list_issues`（项目 UUID `b0a5ebc4-5f7b-4ac0-a5ef-a09c01f053f4`，`includeArchived=true`，保留 id/title/description/status/priority/createdAt），检查 hasNextPage 与 cursor；将原始返回留在程序变量中，不用 text/日志打印。对候选调用 `get_issue(includeRelations=true)` 补齐 `relations.blockedBy`，写入系统临时目录，再运行以下命令。若首次守卫提示候选依赖未知，只补该候选后重跑；不得手写空依赖。外部依赖须附其真实状态，无法核验则阻塞。完整读取后才声明 `complete=true`，并写 `totalIssueCount`。快照仅为本次临时输入，不是新的状态源，用毕删除，不使用旧快照领取。

```powershell
pwsh -NoProfile -File ./tools/SelectDeliveryIssue.ps1 -IssueSnapshotPath <临时快照路径>
# 默认 Summary；结构化短结果用 -OutputFormat Json；排错/现有测试才用 -OutputFormat Diagnostic。
```

快照兼容原格式：`{ complete: true, totalIssueCount: N, issues: [...] }`；每张票保留上述六字段，候选必须有真实 `relations: { blockedBy: [任务ID或带id对象] }`。退出码保持 0（选择／待部署批次／完成）、2（无效输入或读取失败）、3（阻塞）。摘要不代替读取选中 Issue 的具体要求，完整诊断也不代替交付证据。明确获授权的公共工程例外仍使用 `-AuthorizedEngineeringIssueId` 与 `-EngineeringAuthorizationScope`，不能据此扩大人物玩法范围。

玩法人物完成合法实现、受影响增量测试及提交推送后，在原 Linear Issue 描述加入独占一行 `待部署提交：<7～40位十六进制提交号>`，保留 In Progress。该行只表示可进入交付批次；只能在证据真实时填写，不等于 Done。研究人物不使用该标记。选择器跳过这些已实现人物以继续下一位，但其依赖仍未 Done，不能放行依赖它的任务。正常只返回待部署人数；达 `-BatchSize`（默认4，可设3）、下一人物无法执行或队列结束时返回 deployBatch 和批内任务 ID，先部署已有批次，部署后重新查询。需要运行验证时可主动提前交付。

单人物：必要材料 → 设计 → 实现 → 增量测试 → 提交推送；批次：综合验证 → 构建 → 安全部署与回退 → 清除待部署行并关闭对应任务 → 继续。只主动实现一人，允许多张已实现人物票等待同批部署，不另设标签、状态或批次文件。部署失败或游戏运行只报告受影响阻塞，不能关闭任务。当前需求范围内不创建调度或新增管理票。

记录职责固定：设计写现有人物入口，状态及待部署提交写 Linear，玩家可见变化才写 README，必要验证及部署证据写 DEVELOPMENT；不为步骤齐全而重复修改所有文件。

本次验证（2026年10月19日）：领取检查41项通过，覆盖原顺序与依赖、短输出、待部署批次、项目与依赖分页、外部依赖、重复游标／ID及GraphQL部分失败。通过已连接工具在程序内读取189张实时项目任务，仅补候选关系后，摘要选择ENC-108（第7位，In Progress），无阻塞；未写入Linear。直接API路径使用模拟响应验证，当前环境未配置LINEAR_API_KEY，未声称直连实测。未改游戏内容，不构建或部署游戏。

## 按需技术与安全细则

本节承接精简前 AGENTS 的仍有效技术保护；任务授权、批量交付和记录方式以精简后的 AGENTS 与上一节为准。历史日志只提供证据，不作为当前指令。

- 设计是第一性的，代码不是第一性的。新增或扩写玩法前，先在 GitHub 仓库 `docs/` 的当前设计入口说明：游戏内出现时点、玩家操作、具体取舍、收益与代价、失败与恢复、后继变化、反例，以及界面、素材和动画成本。自动测试通过不能替代思想转译、可玩性、路线差异和正式呈现可行性。
- 美术与制作成本是前置约束。优先复用现有战斗、卡牌、遗物、奖励和事件界面及少量静态素材；不默认 AI 美术能满足游戏风格与可读性，也不以纯文字原型成立代替正式制作可行。
- 2026年9月17日用户约束：柏拉图守灯与旅人仅保留为设计实验；在正式节点、界面和可承担素材方案明确前，不继续扩写额外场景或接入生产。若机制依赖额外场景才能成立，应重新选择载体。
- 先完成一个完整决策循环，再做相称的最小实现。复用基础设施，但不让已有代码倒逼设计，也不因已有投入保留不成立的规则。
- 始终区分：用户既定约束、研究结论或争议、候选设计、已实现并通过相关自动检查、已安全部署并交付、后续玩家反馈。冲突必须登记和处理，不能靠改标签掩盖。

- 新增存档、事件或生命周期接入、补丁诊断、本地化、配置、打包等通用能力前，先检查当前锁定版本的 RitsuLib API、文档与仓库现有适配器，并优先使用游戏原生公开 API。能直接复用就不另造包装层；只有缺少能力、语义不符、存在已确认兼容缺口，或无法满足关键原子性与恢复要求时才自建，并在同一 Issue 中简述理由。
- 人物规则、路线状态机、玩家选择和异常恢复等 Mod 业务逻辑可以自研；不为减少代码量牺牲旧档兼容、SL、一致性或幂等性。保持 RitsuLib 为唯一主框架，不因单项功能叠加 BaseLib；框架升级和公共层迁移按高风险变更处理。
- 新增或重命名内容前，先确定中文显示名、代码名、`ModelId`、本地化键和资源名，并遵守 `NAMING.md`。自定义文件名和目录名禁止使用连字符。公开后的 `ModelId` 没有明确迁移方案不得改名。
- 游戏内描述遵循《杀戮尖塔 2》的简短风格，只写触发条件与实际效果。命名重构不得顺便改变机制、数值或文案。
- 验证采用增量原则：只检查本次修改直接影响的部分。已经验证且本次没有相关修改的内容，不重复测试、审计或构建；公共基础设施变化、存档状态变化或最终部署确有需要时才扩大验证范围。
- 修改 C# 时运行受影响的纯逻辑测试；交付需要可部署程序集时再做 Release 构建。修改游戏内本地化或内容资源时校验受影响资源；交付需要部署包时构建并校验 PCK。涉及公共构建链或最终完整部署时优先使用 `tools/VerifyMod.ps1`。
- 构建使用整个工作区。运行构建或部署前确认输入中的已有改动都能安全归属；无法辨识混合产物时，停止受影响的构建或部署，不能把它描述为纯净版本。
- 验证失败时不得提交声称功能完成的结果。若验证后又修改代码、清单、资源或构建工具，只重跑受影响的检查；仅修改记录无需重复构建。
- 部署前检查游戏进程。游戏未运行且目标目录安全时，可部署到 `local.props` 配置的 MOD 目录；游戏运行时停止部署并报告。始终不自动启动 Godot 编辑器界面、Steam 或游戏本体。
- 对存档、公开状态、迁移和不可逆操作保持高风险审查；不以普通功能的轻量流程降低这些保护。

- 任一父任务都要核对其子项、真实依赖和完成条件，不能只交付部分子项就关闭整票。
- 后序 In Progress 的已有投入不授权继续新增玩法；只保全现场和必要安全收尾。公共 Bug 例外仅覆盖已获授权的必要范围，不能成为跨人物入口。
- 公共 API、框架升级、存档迁移、不可逆操作、外部权限阻断或重大产品分歧才需要独立范围或决定；普通设计、审查、测试、构建及部署不另拆票。
- 未部署的合法实现可等待批次交付；生产入口不可达、内容未接入或保存恢复不成立则仍是原人物未完成，不能作为普通试玩反馈关闭。玩家反馈后续按明确授权处理。
- 只暂存本次可辨识且验证的文件，禁止 git reset --hard、git clean 或其他可能丢失内容的命令。提交信息用简短中文，不使用连字符；推送后核对状态与未推送提交。
- GitHub docs 是现行版本化设计入口；Drive 只作研究、附件和历史归档，不实时写回。保留未核验独有资料，不覆盖或删除同名但不同内容的笔记；仅哈希一致的完全重复副本可自动清理。
- 当前人物入口保留简短的现状、流程、取舍、制作成本、完成边界和下一步；不把历史下一步当作当前授权，不默认新建 Markdown 过程页。

### ENC-190 工作流退役与自动接续核对（2026年10月16日）

本机 `C:/Users/35588/.codex/automations/sts2/automation.toml` 的既有“STS2 持续推进”提示原先要求从 Obsidian 的 `00执行状态.md` 恢复，禁止读写 Linear，并尝试 Remotely Save；这些规则已通过应用工具替换为 Linear 授权流程。本次交互执行已验证无 AutoRun 标签的 ENC-190 能从 Todo 领取。2026年10月17日继续核对并通过应用工具更新了人物顺序、真实交付、公共 Bug 和后序 WIP 收尾规则，并同步 Linear 项目总说明。读回确认 ID、目标会话、原有每日09:00频率及 `PAUSED` 均保留；旧记录中的“每小时”描述不准确。更新提示不等于启用或验证后台定时运行，当前未启动该调度。

旧 `S:/01_Workspace/Obsidian/STS2mod/STS2 Mod 开发`、G 盘历史真源以及 `docs/` 的 Vault 用法均已退役；不从这些路径恢复或写回。仓库脚本未发现旧路径写入、同步或按 AutoRun 过滤的执行器；研究导入脚本只按显式输入目录读取资料并写入仓库配置，不是同步入口。本机 Windows 计划任务中未发现匹配本项目或旧同步流程的任务。

本机遗留 `docs/.obsidian/` 只有四个基础设置文件，未发现插件目录；仍由 `.gitignore` 排除，不删除用户设置。旧 S 盘及 G 盘项目位置的 `.obsidian/` 未发现。其他设备或其他路径的软件配置未核验：若仍有指向旧项目的 Remotely Save 或双向同步，需要在原配置所在应用停用；本票未改账号、认证、远端路径或删除旧资料。退役摘要保留在 `OBSIDIAN.md`，不再列为日常指引。

设计优先于代码。自动检查只证明实现符合已写规则，不能证明思想转译、可玩性、路线差异或正式呈现成立。先完成具体选择、代价、表达机会、反例与制作成本，再实施相称的最小内容；试玩反馈另开 Issue，不作为已验证并部署交付的闸门。

新增通用基础设施前先检查当前锁定版本的 RitsuLib、游戏原生公开 API 和仓库现有适配器。只有缺少能力、语义或兼容性不符，或关键原子性／恢复要求无法满足时才自建，并在对应 Issue 记录理由。业务状态机允许自研；保持 RitsuLib 为唯一主框架，不叠加 BaseLib，不以减少代码量换取存档、SL、幂等或兼容风险。

## ENC-108／111 独立问号合批（2026年10月19日，0.5.8）

阿那克萨戈拉在第二幕德谟克利特固定事件已明确拒绝并关闭、当前仍是巴门尼德且问号未用时出现“混合中的秩序”；高尔吉亚在第一幕亚里士多德语言入口取得范畴石板、完成战斗且问号未用时出现“封箱与说辞”。两人各占一次本幕问号，只记录自身事件结论，不增图节点／边、不替换主线遗物或战斗规则。前者不与德谟克利特采纳后的恩培多克勒问号重合；后者不恢复已禁用的语言032边。设计、来源争议与反例见[阿那克萨戈拉](docs/design/current/west/16阿那克萨戈拉当前设计.md)和[高尔吉亚](docs/design/current/west/17高尔吉亚当前设计.md)。

两事件复用 RitsuLib 0.6.2 共享槽、原生 `EventRoom` 与既有准备／提交／再保存边界。新增可空业务状态，旧档缺席不伪造历史；未知版本、缺字段、矛盾路线与已占问号保持冻结，陈旧按钮不可改选。各10条完整路径、保存边界、资格、双语通过；无界面原生检查纳入十四个注册事件，地图打开与原生房间往返、RitsuLib 0.6.2及本机0.6.7兼容集成通过。Release 零警告零错误，Godot 4.5.1 PCK及纹理校验通过；没有渲染UI、完整游戏重放或读写玩家实际存档。

部署前确认游戏未运行、已安装0.5.7三文件哈希与旧交付记录一致；备份至 `bin/deploy_backups/enc108_enc111_mac_20261019_101526/` 并逐文件对比，再一次安全安装0.5.8。安装三文件与构建输入 SHA256 完全一致：DLL `B07A0418E0A01CBC26346579C9B5F03A7762154A863D228DACC06FC525410E10`；PCK `2E02741685951E1363CAAA811CA93C1CEEA2C5ED4EA88E215C0EEB8CB299DDFF`；清单 `6E1D214B9CCE13E9F5DAD0A90682BBD4D11FA82EE59E29BD4E2B747D6EBFBD89`。回退时先退出游戏，再将备份目录顶层三文件复制回当前Mod目录。德谟克利特ENC-110已于本批前复核并存在于0.5.7及0.5.8，无须另做单人部署。

## ENC-107 恩培多克勒四根问号（2026年10月19日，0.5.7）

原89人第6位主要用途为“问号”。在第二幕德谟克利特固定事件已明确采纳原子与虚空并关闭、现行节点仍为德谟克利特且本幕问号未用时，安全返图邀请“四根的配合”。先选择解释组合或分离，再对土、水、气、火各一份的文本模型作混合或分类，最后可明确记录有限并行比较或只保留原子论；拒绝与中止同样耗费本幕问号。仅记录比较提交原有 Keep 边 `WESTERN_EDGE_006`、记已见人物，不改当前节点、琴弓、后继前置或数值。与赫拉克利特留线的阿那克西曼德问号互斥；来源争议及反例见[恩培多克勒当前设计](docs/design/current/west/15恩培多克勒当前设计.md)。

复用 RitsuLib 0.6.2 既有共享槽、`RouteEventMapBoundary` 原生 `EventRoom` 与准备／提交／再保存；新状态是可空人物记录，缺席旧档不伪造邀请。版本、阶段、确认动作、待提交动作、关闭旗标、人物路线及已记边严格互验；缺字段／矛盾状态冻结，不清空重邀。没有第二框架、通用存档层、图边或六入口改动；背景只作现有静态图的PCK别名。逻辑检查覆盖10条完整路径、每步保存、前驱与问号资格、已记006精确恢复、旧／损坏档和双语。无界面 Godot 原生检查纳入第十二个注册事件，覆盖原生房间身份往返、地图调用链、锁定按钮及陈旧回调，并分别在 NuGet RitsuLib 0.6.2 与本机0.6.7／compat 0.111.0 下通过。Release 零警告零错误，Godot 4.5.1 PCK 加载及纹理校验通过；没有渲染 UI、完整游戏重放或读写玩家实际存档。

游戏未运行且原0.5.6三哈希与交付记录一致后，备份旧安装到 `bin/deploy_backups/enc107_mac_20261019/` 并安全安装最终0.5.7，三文件安装后哈希均与构建输入一致：DLL `6246F14309CC5D87D0A1B994297F93F2272875B04ADFAE13276414C3EC8057B2`；PCK `600FFF06547533E32D3FF38EA05D0C9EC1973C4AE32A943166C41300CB00573E`；清单 `A3BC5848C1E0A679A5CB4473A0AA56506ACD5C0FE6630F6DD7512FE109953FD2`。回退时先退出游戏，再将备份目录顶层三文件复制回 Mod 安装目录。部署之后只改记录，没有重做未受影响的构建。

## ENC-104 阿那克西美尼凝散实践（2026年10月19日，0.5.6）

原 89 人第 3 位主要用途为“实践”。第三幕仍在赫拉克利特节点并持琴弓、完成本幕普通战斗且单人安全返图时，仅一次邀请阿那克西美尼；是否经历第二幕阿那克西曼德均不构成资格。接受后原生事件发放辅助遗物“凝散之息”，不替换琴弓或当前节点，也不消费 Fixed／Question。每回合结束留有至少1点能量时，本回合一次记录；紧接的下回合发放1点能量并抽1张牌，战斗结束清除余额。拒绝无遗物且不重邀。设计、来源争议与体验反例见[阿那克西美尼当前设计](docs/design/current/west/14阿那克西美尼当前设计.md)。

沿用 RitsuLib 0.6.2 共享槽、原生 `SavedProperty`、`RouteEventMapBoundary` 精确事件房间及准备／提交／再保存流程；只有发放遗物成功后才确认接受，恢复时先查是否已持有再补发，结果与遗物矛盾则锁定而不重复开邀。新增业务状态与一对静态 SVG，不添第二框架、通用持久层或普通奖励池。纯逻辑检查覆盖接受／拒绝、旧档、错误结果、重入、触发边界、回合预算及双语；无界面原生检查覆盖第十一个注册事件的房间保存／恢复及 RitsuLib 0.6.2／本机0.6.7 集成。Release 零警告零错误，Godot 4.5.1 PCK 导入及内容校验通过。完整抽牌命令需要真实局对象，无界面夹具未重演；未启动游戏或声称已完成整局试玩。

部署前确认游戏未运行且原三文件哈希与 0.5.5 交付一致；旧版备份在 `bin/deploy_backups/enc104_mac_20261019/`。将 0.5.6 DLL、PCK、JSON 安装至 `SlayTheSpire2.app/Contents/MacOS/mods/STS2Philosophers` 后三文件哈希与构建输入一致：DLL `06E075A89C526A1F4F540EF50479249BFBA475366FA272C565D2B23E5E037A86`；PCK `4285213AAC9885D08FBC1E3B207665140E3FD22D25BC6F5F7A97035778D44693`；清单 `27A9A616D4D111BC80E4F89965676E0FE28FBE01EB36479D35A2DE2ECF2116CC`。需回退时停止游戏，将备份三文件复制回原 Mod 目录。

## ENC-103 阿那克西曼德第二幕问号（2026年10月18日，0.5.5）

沿原 89 人第 2 位与主要用途“问号”，不改六入口、七问题、人物顺序、琴弓效果或第一幕巴门尼德机会。第一幕问号已结束且仍在赫拉克利特节点、第二幕仍持琴弓、完成本幕战斗、单人安全返图且第二幕无固定候选时，明确保留本幕旧说并占用第二幕问号，进入标准事件。玩家检验单一可见性质的解释负担或暂缓指定优先性质，均可记录有界追问、保留观察或中止；拒绝也消费机会。仅明确记录提交 `WESTERN_EDGE_043` 的 `Question/Revise`，记已见人物而不替换赫拉克利特节点或战斗实践。来源与解释边界见[阿那克西曼德当前设计](docs/design/current/west/13阿那克西曼德当前设计.md)。

事件复用现有 RitsuLib 0.6.2 共享槽及 `RouteEventMapBoundary` 的原生房间保存恢复，没有引入第二框架。准备、保存、提交、再保存沿用现有严格边界；旧存档没有本记录时保持缺席，损坏或未知状态不重邀。逻辑检查覆盖八条完成路径、每步保存、重复提交、错误路线、旧档、双语与触发排除；48 节点实践检查将问号专用节点明确验证为无战斗奖励。Release 零警告零错误，Godot PCK 导入和内容校验通过；原生事件边界及保存集成分别在 NuGet RitsuLib 0.6.2 与本机创意工坊 RitsuLib 0.6.7／游戏兼容 0.111.0 下通过。原生检查不等于渲染 UI 或完整实机局。

本机确认游戏未运行、目标 `SlayTheSpire2.app/Contents/MacOS/mods/STS2Philosophers` 原先不存在后，部署 0.5.5 的 DLL、PCK、JSON 并逐一核对 SHA256。回退说明与哈希位于 `bin/deploy_backups/enc103_mac_20261018/`：此前没有旧安装，本次回退可在停止游戏后只移除新建的 `STS2Philosophers` 目录。DLL `41A52CCD4C0E0E9A43C33CAEDB8E329F761DFAB59348894C064B8FB0A26B0779`；PCK `4C90E18F7FC764FFFF98F5D1087077736C780D64F223C0B13ED8AC8759CBAE89`；清单 `08C50E9B81F16F8E6B6FC17AE560D2F99683FE28722CC5FF7EFF2CC61CD2B33C`。未启动游戏或改动玩家存档；玩家试玩异步。

## ENC-123 公共前置安全收尾与顺序修正（2026年10月17日，0.5.4）

本批只修已有内容的公共缺陷，不交付马基雅维利。原工作区存在人物草稿及其他未提交内容，因此从 `10335f9` 创建独立受控工作树，构建不包含马基雅维利代码、资源、本地化或测试，原工作区保留。六入口、89人数组、路线依赖、公开模型、业务保存结构及玩法数值不变；公共修复完成后原人物票回到 Todo，不能据此 Done。

`tools/SelectDeliveryIssue.ps1` 是只读领取守卫，直接读取原89人数组和完整 Linear 项目快照（包括完成状态），要求候选依赖信息已核实；不另建实时任务清单、不修改 Linear。人物先取最早未完成者，同人内才看依赖／优先级／创建时间；必要公共工程例外必须传入获授权的 Issue 与明确范围。现行检查覆盖原序、依赖、父任务、工程例外，以及研究主要用途Todo可领取、Done可接续和后续研究人物不再重复审批；兼任研究与RESEARCH_ONLY快照不改变原主要用途。研究成果按规定范围检查并提交推送后Done，其他需要玩家内容者仍须实现、验证并安全部署。旧泰勒斯范围阻断已由用户本次确认解除；本轮不启用或更改后台调度。

九个既有事件改用 `RouteEventMapBoundary` 的真实 `NMapScreen.Open` 边界，检查完成房间、奖励继续确认、叠层、地图旅行及恢复门禁，依次只派发一个合格事件。`EnterRoomInternal(MapRoom)` 无法覆盖普通战后返回。进入事件使用原生界面清理与 `EventRoom`，保存后核对原生房间 ID／类型／未完成标志及完整共享编码。各事件由原生等待的 `AfterEventStarted` 恢复同一未决操作后才开放按钮，旧实例、旧阶段和旧确认次数的回调不能推进。业务上的准备→提交→保存、人物路线与异常恢复继续保留：框架的保存槽不能替代这些领域规则。

继续直接复用 RitsuLib 0.6.2 的 `BeginModDataRegistration`、`GetRunSavedDataStore(...).Register`、`RunSavedData<T>.TryGet/Set`，写策略改为 `RunSavedDataWritePolicy.WhenSet`，有效现场每次序列化前重新绑定并 Set，未知对象载荷由框架保留原文档，不误当空槽重建。原生 `MigrationManager.LoadWithAggressiveRecovery<SerializableRun>` 的精确输入记录来源，避免读另一份磁盘文件冒充加载结果。槽为非对象、父容器损坏、来源不明或原生备份与 Ritsu 主档不一致时，窄范围 `SaveManager.SaveRun(AbstractRoom,bool)` 保护返回失败 Task，保留现有源文件；不接管最终 JSON 写入，也不把保存失败报告成成功。可证明原样保留的未知对象仅停用本 Mod 路线，不阻止原生保存。生产代码不调用 Ritsu 内部导入器；真实导入／导出反射只存在于测试。

框架没有提供“标准地图叠层事件＋精确原生房间身份＋原子路线提交”这一整套业务语义，因此保留窄入口补丁与状态校验。既有原生遗物生命周期、`SavedProperty`、共享槽、Codec 与人物规则不迁移；通用生命周期订阅及补丁诊断的进一步复用留在既有 ENC-186 范围，不随本批升级框架、叠加 BaseLib 或重写全仓库。

最终 `VerifyMod.ps1 -SkipDeploy` 通过21项领取检查、完整纯逻辑、原生运行时、Release零警告零错误、PCK加载及纹理检查。运行时在独立无界面 Godot 场景中实际加载 NuGet RitsuLib 0.6.2（`1bebdb0`），同一DLL再以本机0.6.7／compat 0.111.0（`8ca62f9`）运行全部集成检查；覆盖真实导入／导出、九事件原生房间往返、危险保存拒绝、陈旧按钮及当前原生调用链。没有渲染UI、完整游戏重放或读写玩家实际存档，不能据此称为实机试玩通过。

17:33确认游戏未运行后，部署同一批0.5.4三文件并逐一核对SHA256；回退备份为该受控工作树的 `bin/deploy_backups/verified_20261017_173334_541/`，报告为 `bin/Release/net9.0/verification.md`，本机0.6.7日志为 `bin/runtime067_verification.log`。DLL `851645EC8E06AFFE6B72CDD098B15AD041BADE9836FF5A71EAC46F02DCC19338`；PCK `9CF76C878A70DB7FF9D1543C1BCBA3D8A83D662179F04103966B2B0FFB7D7E9F`；清单 `120A2747102E2250BA09E8AC0C7CC60AE83B205009961EED70956C38BA6B7842`。部署后只修改记录；无实质差异的 Godot `.import` 行尾不纳入提交。

## ENC-92 合并未来试玩脚本（2026年10月16日）

[芝诺当前入口的一次试玩脚本](docs/design/current/west/06芝诺当前设计.md)把九张历史票归并为八步主线与按样本选做的分支：普通单人德性局串起两轮保存加载、第欧根尼去留、芝诺延迟出现、页面输入、主动SL、结果离开和玩家反馈；停留对照、旧档迁移、Boss／离幕兜底分别要求独立局或独立测试副本，不通过反复改同一已提交路线覆盖互斥结果。

按现行代码与中文按钮核对：正式入口要求苏格拉底德性教义和路线节点一致，不能只给一件遗物就声称覆盖；当前普通流程没有主动撤离及公开说法生产UI，正常无材料路径只允许“不作同意”。`zenoassenttest`也仅提供独立开发无材料入口。真实继续／撤离事实、保留／缩小／撤回和异常载荷等分支在缺少合法样本时明确未覆盖；2026年9月26日最小RitsuLib探针的既有实机通过结论保留，不外推到迁移或芝诺全链。

本批次只更新现有当前入口及文档链接，没有启动游戏、部署、构建、运行实机步骤或读写用户存档；README的玩家能力描述无需改动。九张源票状态不变，未测项不记通过；脚本完成不等待玩家试玩，也不阻塞后续开发。验证采用中文文案／源代码交叉核对、相对链接检查与`git diff --check`，不为纯文档改动重复游戏构建。

## ENC-121 阿奎那角色定义与问题面核对（2026年10月17日）

角色定义位于[西哲人物用途基线](docs/design/current/shared/01西哲人物用途基线.md)，亚里士多德入口互链。本票固定／转线／内批及存在、德性、制度三问题面没有合法运行节点；图中另有语言029／030仍启用，031退休仅使原样例SourceOnly，不连带删除029。当前无阿奎那资格、事件或泛型候选UI，旧四张牌练习不构成人物实现；制度016不能冒充语言前驱。按本票无当前合法入池方式的例外完成角色定义，显式保留研究与图的问题面差异。

以《神学大全》q.46、q.62、q.90界定创造与时间开端、理性与启示、自然德性与神学德性、法律与共同善，并保留多语翻译注释网络。没有新增可玩阿奎那，没有改配置、代码、资源、玩家存档或部署；README维持已实现能力。只做相关只读核对、链接与diff检查，不以本票Done冒充奥卡姆前驱已经游戏内可达。

## ENC-119 奥古斯丁角色定义与前置核对（2026年10月17日）

[西哲人物用途基线](docs/design/current/shared/01西哲人物用途基线.md)已补齐固定／转线／内批、意志与有限公共和平的职责及反例，柏拉图当前入口互链。与普罗提诺不同，奥古斯丁已有020第二幕Fixed节点，但必要019制度芝诺无生产入口；泛型图候选方法没有生产UI调用，旧三张牌给格挡练习未进入人物实际玩法。按Issue“当前无合法入池方式则完成角色定义”的范围收口，不删除required、不冒用德性芝诺、不挤进已经消费的第二幕Fixed。

这阶段玩家还玩不到奥古斯丁新内容。原89人用途、图及资格状态保留，文本传播作者待核，流溢／创造和回归／恩典分开，程序不判玩家属于二城或得救。只有角色定义与生产缺口核对完成，未新增代码、事件、资源或部署；README保持已可玩事实。只运行相关只读核对、相对链接及diff检查，不重复构建或要求试玩。

## ENC-118 普罗提诺角色定义（2026年10月17日）

按本票“无合法入池方式则完成角色定义”的明确范围，在[西哲人物用途基线](docs/design/current/shared/01西哲人物用途基线.md)补齐主固定、兼转线／回响、三个问题面与接入边界，研究材料与柏拉图当前入口互链。当前图解析为普罗提诺节点0、关联边0，遗物资格／事件均未注册；020只是柏拉图制度→奥古斯丁制度及压缩文本传统，不能当作普罗提诺事件。保留89人、六入口、原用途与`RESEARCH_ONLY`，未为凑交付创建运行节点。

这阶段玩家还玩不到普罗提诺新内容。定义区分一者／理智／灵魂、存在依赖与时间生成、回归与恩典，也修正研究中“越统一越删除差异”的简化；只完成角色边界，不把实现、素材、构建或部署称为完成。README核对后保持已有可玩事实；相对链接、运行图／资格只读核对和diff检查完成，本批纯文档未运行游戏构建或部署，未改变既有玩家存档。

## ENC-110 德谟克利特空隙与排列（2026年10月16日）

当前设计见 [`12德谟克利特当前设计`](docs/design/current/west/12德谟克利特当前设计.md)。单人第二幕、已完成001且当前为巴门尼德存在／变化节点、仍持琴弓、完成本幕战斗后，使用现有标准事件。研究运动或性质、具体模型操作与最后理论承诺分别保存；四条比较都能明确采纳原子与虚空或保留巴门尼德，没有隐藏答对奖励。只有明确采纳提交已有005；保留、拒绝和中止消费第二幕Fixed，不消费Question，不改当前战斗教义。留基伯共同归属保留，006／007未提前称为可玩。

核对RitsuLib 0.6.2与既有适配器后，复用共享槽、Codec、写后读取器和原生事件；新增可空人物记录。版本、六个保存字段及行动重放不一致时保留并停用，不清空重邀。原生`SaveRun(null)`不自动序列化地图叠层，本模块显式保存精确`EventRoom`，读回完整共享编码并核验原生房间ID／类型／完成标志；从暂停菜单退出不另写null。首次保存后才插入事件，原生重载可成为同坐标单层事件，使用被原生等待的`AfterEventStarted`完成同一待提交动作，再开放按钮。陈旧按钮、重复回调与未知保存结果均不能推进另一选择。此修复仅限新事件，没有顺改其他人物。

12条完整路径、准备／提交双边界、精确已提交结果、Closed恢复、六字段缺失／未知版本／非法阶段、遗物与前驱资格、双语全部通过；原生托管检查证明真实房间序列化往返及错误身份拒绝，不证明渲染UI或真实整局SL。`VerifyMod.ps1 -SkipDeploy`于18:09完成全逻辑、原生托管、Release零警告零错误、PCK加载与纹理检查。之后游戏未运行，备份并安全部署同一批产物，三文件SHA256一致。回退点`bin/deploy_backups/enc110_20261016_181038/`；DLL `4DA3AB0FAB4A0E719D69DE2ECD94E7A782A2C1F6788DEAA5967A307ADABB6CE0`，PCK `F13FDD12D42F5C176D4A5CEE2DDBD8BA91B13D78C6761D1CFC7783233F380CAA`，清单同ENC-106。已有两份研究文档改动不嵌入构建、tmp C#被项目排除，均未暂存或覆盖；未启动游戏或读写玩家实际存档。

2026年10月19日 Mac 复核：此前重开本票的公共战后地图入口现已改为原生 `NMapScreen.Open`，当前派发链登记德谟克利特事件；其第一幕巴门尼德001→第二幕保留琴弓、完成战斗、未用Fixed的资格未改变。现行逻辑检查再次通过12条完整路径和安全资格；Godot原生检查通过真实 `EventRoom` 序列化与地图打开／奖励返图调用链，RitsuLib共享存档检查通过。安装中的0.5.7已包含该公共入口修复和德谟克利特内容，三文件SHA256仍与本页ENC-107的安装记录一致：DLL `6246F14309CC5D87D0A1B994297F93F2272875B04ADFAE13276414C3EC8057B2`，PCK `600FFF06547533E32D3FF38EA05D0C9EC1973C4AE32A943166C41300CB00573E`，清单 `A3BC5848C1E0A679A5CB4473A0AA56506ACD5C0FE6630F6DD7512FE109953FD2`。既有回退点为 `bin/deploy_backups/enc107_mac_20261019/`；这次仅复核并记录，不覆盖安装，也未执行整局真人试玩。

## ENC-105 赫拉克利特琴弓（2026年10月16日）

当前设计为 [`09赫拉克利特当前设计`](docs/design/current/west/09赫拉克利特当前设计.md)。复用原有 `HeraclitusLyre`、`HERACLITUS_LYRE`、图标与邀请，不修改九人池、路线关系或后继资格。起手抽牌绑定一次具体实例，真实手动相反类型使其具备消耗换牌资格；直接使用、保留到下回合和消耗换牌各有独立代价。

检查锁定 RitsuLib 0.6.2 与现有适配器后，沿用原生 `SavedProperty` 和既有 `PracticePayload`，没有新增通用存档层。载荷版本仍为1，新增可空规则版本与本回合事实；旧活动载荷无法证明一次抽牌预算与相反类型时，仅暂停当前标记，到下一真实回合恢复。其他六问题的默认字段与业务行为保持兼容。未知或损坏状态不产生新收益；没有读写玩家实际存档，也没有迁移共享路线槽。

本机原生 DLL SHA256 为 `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`。定向读取证实 `fromHandDraw` 仅指回合开始抽牌，出牌结果堆在 `BeforeCardPlayed` 之前已确定，且牌已进入 Play 堆。因此琴弓通过 `ModifyCardPlayResultLocation` 提前冻结消耗去向，在 Play 堆保持同一实例引用；之后只在同一真实手动结算、最终结果为消耗且机会未消费时抽1。`TryModifyKeywordsInCombat`、`NCard.UpdateVisuals`、`ExtraHoverTips` 与 `CardHoverTip` 负责原生保留显示、标记实例和计数1／2提示。悬浮只读取状态，不领取或恢复机会；弃牌、自动出牌和同名副本不重开窗口。

验证：254种不同短出牌顺序及保留、同名副本、自动行为、重复回调、旧／损坏载荷和双语提示检查通过；完整既有逻辑检查通过。`tools/VerifyMod.ps1` 于本地08:51完成 Release 零警告零错误、PCK 加载／纹理校验和游戏未运行时的安全部署，三文件 SHA256 一致，报告在 `bin/Release/net9.0/verification.md`。旧部署三文件和哈希保留于 `bin/deploy_backups/enc105_20261016_085132/`，可回退。构建使用本工作区，已有两份人物文档修改保持原样且不嵌入程序集或PCK，`tmp/` C# 被项目排除；不把工作区描述为纯净检出。未启动游戏或执行人工实机验收，后续体验问题独立登记。

## ENC-106 巴门尼德事件交付状态（2026年10月16日）

2026年10月19日按 Linear 重新开放原因复核可达性：旧 `EnterRoomInternal(MapRoom)` 入口确实不能覆盖普通战后返图，ENC-123 已把公共派发改为原生 `NMapScreen.Open`。现行 `RouteEventMapBoundary` 注册 `ParmenidesInquiry` 并在安全返图时调用其第一幕邀请；仍要求赫拉克利特节点、琴弓、已完成战斗和未使用的问号，不补造入口。重新运行完整纯逻辑检查，巴门尼德10条路径、两次保存边界和资格检查通过；无界面 Godot 原生检查覆盖已注册事件、地图调用链、精确 `EventRoom` 往返与恢复，并在 RitsuLib 0.6.2 和本机 0.6.7／compat 0.111.0 下通过。检查没有渲染界面或执行真实整局，不称人工试玩。

本票没有修改程序集、资源、本地化或保存结构，沿用已经构建、校验并安全部署的 0.5.6，不为记录性修订重复 Release／PCK 或覆盖安装。2026年10月19日本机安装三文件 SHA256 仍分别为 DLL `06E075A89C526A1F4F540EF50479249BFBA475366FA272C565D2B23E5E037A86`、PCK `4285213AAC9885D08FBC1E3B207665140E3FD22D25BC6F5F7A97035778D44693`、清单 `27A9A616D4D111BC80E4F89965676E0FE28FBE01EB36479D35A2DE2ECF2116CC`；回退点为 `bin/deploy_backups/enc104_mac_20261019/`。

唯一设计入口为 [`10巴门尼德当前设计`](docs/design/current/west/10巴门尼德当前设计.md)。`ParmenidesInquiry` 使用标准事件与诸子观照背景PCK别名；第一幕当前赫拉克利特节点、持有琴弓、完成战斗且问号未使用时邀请，先预约并保存一次问号。只有明确 `AdoptBoundary` 才完成既有 `WESTERN_EDGE_001`；保留、拒绝和中止不转向，均不发战斗或资源奖励，不改九人池。对象选择约束后续论证路径，杯与铜的区别不被当成完整存在论答案。

已检查锁定的 RitsuLib 0.6.2 和现有共享适配器，新增状态继续由现有共享局内槽、Codec、原生 `EventRoom` 与 `SaveRun` 保存，不新增通用框架。阶段必须与完整确认行动序列的重放结果一致；两次保存间的同一待提交行动可恢复，已有同一转向只确认一次，矛盾路线保留待提交并拒绝继续。旧档没有新状态时保持缺席；未知或矛盾新状态不重建邀请。

10条完整决策路径及准备／提交保存边界、错误路线、已提交边重入、非法阶段、未知版本、旧档、双语与资格检查通过；完整既有逻辑检查通过。`VerifyMod.ps1` 于10:05完成Release零警告零错误及PCK加载／纹理检查，首次因游戏运行停止部署；用户确认退出后，重新检查进程并安全部署同一批已验证产物，三文件SHA256一致，报告见 `bin/Release/net9.0/verification.md`。DLL为 `11F1D25F5435E5152316171AF2880FF3A9B03A558A219959B3FBE456D90E2816`，PCK为 `D0113B7E27DDB6AB25B0063DD2E97CBD1CEC02E0B0E89C8F40AA954196172B22`，清单为 `A828CA96B92B0BBEBF2E1DCCDEC9F9448C6EDBEB3A24F3878A0F92398E81B9A4`。旧部署备份位于 `bin/deploy_backups/enc106_20261016_100509/`，可回退；验证后仅调整记录，未重复构建。当前尚无本事件的人工游戏体验证据。

## ENC-112 苏格拉底用途核对与砝码修复（2026年10月16日）

当前范围见 [`01苏格拉底当前设计`](docs/design/current/west/01苏格拉底当前设计.md)：同一入口内知识／德性互斥，现有诘问杯、审思砝码及各自后继满足本票既定用途；休眠的试授、撤离及C3协议不追加接入。README只明确“手动打出”条件，没有新增收益或改数值。

核对本机原生 `CardModel` 证实：牌先进入Play堆，再确定 `resultLocation`／`CardPlay.ResultPile`，随后才调用 `BeforeCardPlayed`。砝码旧实现既在过晚的回调设置 `ExhaustOnNextPlay`，又只在手牌堆保留实例引用，可能丢失选中实例并漏掉消耗。现复用已存在的原生 `ModifyCardPlayResultLocation` 决定该实例的手动消耗去向，Play堆期间保留同一引用，结算后清除承诺；自动出牌不强制消耗，不追踪同名副本。没有新增存档字段、通用包装层或框架，继续使用RitsuLib 0.6.2与既有适配器。

既有纯逻辑检查通过，包含互斥入口、砝码等待降费／消耗承诺与保存边界；它们不证明原生适配器的实际游戏体验。Release零警告零错误，PCK加载／纹理检查通过。首次部署因游戏运行及用户暂缓要求停止；用户恢复工作后，核对代码和产物哈希未变、游戏未运行，安全部署同批三文件并核对一致，不重复构建。旧部署回退点为 `bin/deploy_backups/enc112_20261016_101518/`，已验证产物及更新报告保存在 `bin/delivery/enc112_verified/`。DLL为 `FFE4661B91A73D2DDB2D2C6D2032BBBA067376692FC4D0E1710A39F65BDE5429`，PCK与清单同ENC-106。原有两份无关人物文档修改及tmp未改变、未暂存；没有改玩家存档或启动游戏。

## ENC-113 亚里士多德制度批判（2026年10月16日）

当前入口见 [`11亚里士多德当前设计`](docs/design/current/west/11亚里士多德当前设计.md)。`AristotleCivicInquiry`在第二幕既有柏拉图自由／制度节点、仍持城邦印章、完成战斗后使用标准地图叠层；与苏格拉底德性后的柏拉图事件分开。集中与轮替先公开，后续四种具体让权／保留结果不可倒退；只有后果页的明确接受才用既有 `WESTERN_EDGE_016` 转向。保留、中止及拒绝由原生图策略记录第二幕固定机会拒绝，均不消耗问号、不修改当前战斗遗物。禁用002与032保持原状。

检查RitsuLib 0.6.2与现有事件适配器后，复用共享Codec、原生EventRoom与SaveRun。新字段为可空事件实例，旧档缺席；阶段由完整行动序列重放验证，两次保存之间恢复同一待提交动作。路线已提交只接受精确相同的接受／拒绝结果；矛盾路线冻结，不重建邀请。背景为现有诸子观照图的PCK别名，没有新场景或美术。

12条完整路径、两次保存边界、已提交路线重入、非法阶段／版本、旧档、不同问题来源排除与双语检查通过。`VerifyMod.ps1`于本地10:31完成Release零警告零错误、PCK加载／纹理校验及游戏未运行时安全部署，三文件SHA256一致；报告留在 `bin/delivery/enc113/verification.md`，旧部署回退点 `bin/deploy_backups/enc113_20261016_103128/`。DLL为 `DA524FF7FD19E2882E2C85F6463D6E18C025DACDADC13B41C298EF927327BC6D`，PCK为 `6E5CA5B0C90572B7302F69714CD646570CD678167C9C42ED77981F5A6D79863B`，清单同ENC-106。范畴石板此前已部署，本票不改其规则或资源；未为其他问题面扩入口，也未接入其他未授权人物。构建使用本工作区，原有两份无关文档不嵌入产物，tmp的C#被项目排除；均保持原样。未修改玩家存档，未启动游戏或执行人工体验。

2026年10月19日 Mac 复核：公共战后入口已采用原生 `NMapScreen.Open`，现行安全返图派发仍包含 `AristotleCivicInquiryPatch`；`WESTERN_EDGE_016` 仍启用且只从柏拉图制度节点进入。已验证的12条逻辑路径、原生房间身份往返与地图打开／奖励返图检查覆盖本事件；源码与这些能力未在0.5.8合批中改变。安装中的0.5.8三文件与本页合批记录的 SHA256 相同，包含亚里士多德制度事件和公共入口修复；既有0.5.7回退点 `bin/deploy_backups/enc108_enc111_mac_20261019_101526/` 可恢复前一安装。此次仅复核记录，不重复构建或部署，不把自动检查称为整局试玩。

## ENC-191 沉思镜原生费用链回归（2026年10月16日）

原生`PlayCardAction.ExecuteAction`先`SpendResources`，后`OnPlayWrapper`设置`CurrentTarget`；旧费用钩子用后者判目标，导致非攻击分支在扣费前失效。现优先读取当前原生出牌行动及其卡牌实例的目标，牌面同步预览与`CanPlayTargeting`以有异常清理的同步作用域传递原生目标，未指定时仅对唯一存活敌人作确定预览。只给单体敌方目标优惠，己方召唤物不计为敌人；原有技能分支和预算不改。RitsuLib 0.6.2和原生钩子没有费用目标参数，因此补充这两个窄UI／可支付性补丁，没有新存档层。悬浮提示披露当前分支与一次性机会。

集成还复现公共适配器以同名牌的`PlayIndex/PlayCount`生成重复身份，导致第二次真实出牌不结算预算。现所有入口共用原生CardPlay引用防重与保存序号标识。旧载荷保持版本1及既有字段，历史ID仍可保留；不把临时引用保存进玩家档案。ENC-192需要独立复现惯例场景后单独关闭。

`tests/NativeRuntimeChecks`引用本机锁定原生DLL与生产MOD程序集，真实调用原生CardEnergyCost→Hook→生产遗物→SpendResources／LoseEnergy，再按原生时序写目标、移至Play堆并触发遗物回调。Player壳、回合循环启动与网络卡牌身份表由反射搭建，以避开完整游戏／Godot日志启动；不替换费用或扣能量算法。原生预览方法和可支付性补丁由Harmony实际绑定，费用getter在相同同步目标作用域下检查；没有创建渲染NCard、执行完整GameAction调度／伤害动画或读写真实存档文件。保存覆盖仅为生产PracticePayload往返，不能声称实机重放或完整SL验收。

失败证据在 `bin/delivery/enc191/before_fix.log`（第二回合费用仍1）及`before_shared_identity_fix.log`（已用机会未消费）；修复后同一链回归覆盖未锁定／唯一敌人／多敌人／己方单位、两种意图、选目标前后的费用与真实付款、0能量可支付性、重复回调、同名不同实例、跨回合、目标死亡与载荷恢复。当时使用控制台直接执行原生托管检查；0.5.4新增真实Ritsu保存检查后，统一改用 `tools/RunNativeRuntimeChecks.ps1` 的独立无界面Godot宿主并纳入VerifyMod，避免Ritsu日志调用未初始化的原生引擎。

VerifyMod于本地16:49完成逻辑与原生托管集成检查、Release零警告零错误、PCK加载／纹理校验，游戏未运行时安全部署，三哈希一致。报告 `bin/delivery/enc191/verification.md`；回退点 `bin/deploy_backups/enc191_20261016_164850/`。DLL `4BAA4C600F973A5EFB9DCB247C5BF4C7CDB5FCC29589268B681FDFB3C59E3A1C`，PCK `5BAE4C7053595D091478761CDF95FCA80D7B14A22704C57AC21A39F47683A2F5`，清单同ENC-106。验证后仅更新记录，已有两份无关人物文档与tmp保持原样。本次修复已安装可用，尚无修复后人工实机体验。

## ENC-192 未琢之石独立回归（2026年10月16日）

此票单独使用`RousseauUncarvedStoneRuntimeChecks`复现第6回合两张不同实例原生StrikeSilent与一张Survivor、第7回合优惠。用同一新回归测试运行ENC-191部署前的备份程序集（DLL SHA256 `DA524FF7FD19E2882E2C85F6463D6E18C025DACDADC13B41C298EF927327BC6D`）时，第二张打击被旧公共身份误判重复，`打击×2`断言真实失败；日志 `bin/delivery/enc192/before_fix.log`，基线哈希 `baseline_hash.json`。只临时替换测试输出目录中的MOD引用，finally恢复并验哈希，未回退工作区或安装目录。当前共享修复后同一测试通过，不另造人物特例计数补丁。

当前回归真实调用原生费用getter、Hook、生产遗物、SpendResources／LoseEnergy和PowerCmd／StrengthPower：普通＋强化打击同牌种计2；第7回合首张正费用打击显示／付款0；存活敌人各+1力量，死亡敌人与己方不加；重复回调、第二张打击与载荷恢复不重复付代价；局部0费不消费，未抽到不付代价，同回合稍后抽入仍可用，跨回合预算及战斗结束清理通过。原生TestMode只跳过图形等待，不替换扣费或力量算法；没有执行原生卡牌的伤害／弃牌效果、完整GameAction调度、渲染界面或实际存档文件，不能称实机重放。失败与成功使用同一托管原生组合，测试已加入VerifyMod入口。

悬浮使用现有CardHoverTip展示当前惯例牌种和未形成／可用／已用状态。描述补明早已存在并被既有逻辑检查锁定的“无新赢家沿用旧惯例”“强化同牌种”“只计手动”，不改数值、规则或存档字段；0费和未使用不付力量代价。

VerifyMod于本地16:58完成既有纯逻辑及两件遗物各自原生托管回归、Release零警告零错误、PCK加载／纹理校验，游戏未运行时安全部署且三哈希一致。报告 `bin/delivery/enc192/verification.md`；回退点 `bin/deploy_backups/enc192_20261016_165756/`。DLL `BA8D1338395C38B3F4254497E3E78DE685327C38CF721E4253960305C67DE9CD`，PCK `A8B316B07FB4708C957C8115C91B79C92A057F203BA21A1EAE4F56A08391D5F7`，清单同ENC-106。本票独立场景通过，当前安装已含修复及提示，尚无修复后人工实机体验；原有两份无关人物文档和tmp未改变、未提交。

## 开局事实检查

运行 node tools/design/StartingHandOpportunityChecks.cjs，读取 docs/design/native_starting_facts.json，枚举五个原始牌组共1800个首手组合的直接格挡能力。事实绑定本机原生DLL哈希，仅摘录数据；原生反编译源码留在忽略目录bin/design/native。范围排除进阶额外牌、升级、额外遗物与状态变化；不模拟完整战斗、星辰或Osty，不把组合比例当游戏触发率。此开发数据不嵌入PCK，单独更新时无需MOD构建。当前解释边界以 [`docs/design/current/west/01苏格拉底当前设计.md`](docs/design/current/west/01苏格拉底当前设计.md) 为准；Drive 的“开局表达机会”只作历史来源。

## 有限承诺C3协议

SocratesBoundedCommitmentState与独立SocratesBoundedCommitmentStateCodec提供C3内存及版本1保存协议，两项专用检查随纯逻辑入口验证。13字段均必需，未知/重复字段与非法状态关系拒绝，恢复失败不返回可领奖的新状态。适配器应依次打开正常回合、领取旧奖励、冻结公开事实、声明/保留准则、锁定行动、结束观察；战斗结束或角色死亡必须清空待发奖励。C3两项均抽1，允许下一回合再次修订，当轮修订不产生新收益。该协议与 Drive 的有限承诺规格都只作历史设计和实现证据，不是当前诘问杯规则；当前人物入口见 [`docs/design/current/west/01苏格拉底当前设计.md`](docs/design/current/west/01苏格拉底当前设计.md)。D13及D15在排除6个W3D2草稿的独立验证目录通过VerifyMod并部署，无游戏验收。D16定向核对发现普通存档不保存战斗牌堆：首版应在原生新战斗建立时初始化临时C3状态，不单独恢复某轮协议并额外强制保存。Codec只证明独立协议恢复，未提供全游戏快照。抽牌遵循原生满手/禁抽限制，不重试或积存；详细技术证据继续保留在 Drive 只读归档。

## 西哲纸面试玩

本节以下 Dxxx 内容保存迁移前的纸面实验、验证命令和实现沿革，不是当前玩法规格或任务入口；当前人物与路线只从 [`docs/design/current/`](docs/design/current/00当前设计索引.md) 读取。

D9保留C1全额防护/单体解围并加入C2半额防护/冻结攻击者集合对照；运行同一专用检查包含19项规则检查与两版各123条相同行动前缀，浏览器检查覆盖规则切换、目标控件和收益边界。增加可达机会不代表设计定稿；延续/修订价值、原生结束阶段和真实战斗仍未验证。

当前西哲图配置为schema3：保留12条来源样例，8条为Executable、4条为SourceOnly；后者保留旧序列与撤回理由，不参与可执行路径校验。5条已撤边继续保留ID和来源，enabled=false禁止新生成及旧候选接受。局内存档格式未改；“可执行样例”只指图状态检查，不代表完整可玩路线。

未完成载体草稿仍在主目录时，本次图修订在独立工作树`S:/01_Workspace/Projects/SlayTheSpire2Verification`验证，输入为186fe2c加本阶段图配置、解析及测试文件，排除6个草稿。该目录`bin/Release/net9.0/verification.md`保存本次部署证据；不要把主目录旧报告当作最新，也不要直接在含草稿的工作区构建部署。

`docs/design/socrates_commitment.html`是苏格拉底候选设计的历史交互片段；当前人物入口为 [`docs/design/current/west/01苏格拉底当前设计.md`](docs/design/current/west/01苏格拉底当前设计.md)。Drive 的有限承诺规格与旧页面只保留设计沿革。默认C3推演固定手牌的六回合片段与三个边界情境，C1/C2单轮对照折叠保留。抽牌仅计请求，不生成真实牌堆的手牌；不是游戏引擎、完整战斗、存档或平衡验收。它不进入PCK，不替换诘问杯。

C3模型检查：`node tools/design/SocratesC3PaperChecks.cjs`，15项覆盖持续保留、反复修订、领取时点、隐藏信息、身份和终局；对应浏览器检查为`SocratesC3BrowserChecks.cjs`，参数与下面一致。320/360/760像素及明暗主题已检查。

历史模型检查：`node tools/design/SocratesCommitmentChecks.cjs`。覆盖12固定样本的123条合法行动前缀，以及冻结、修订、死亡、防重复等边界。

浏览器检查：先用visualize技能的`scripts/render.py`将片段包装到被忽略的`bin/design/socrates_commitment_preview.html`，再运行`node tools/design/SocratesCommitmentBrowserChecks.cjs <预览路径> <Playwright包路径> msedge`。包路径由本机运行时确定；脚本只打开并关闭独立无界面浏览器，不操作已有窗口。截图在预览同目录，未提交。仅修改纸面试玩时运行其专用检查，不调用游戏构建。

## 赐福流程代码结构

### ENC-64 隔离接入与验证边界

`DiogenesStaySwitchEntryPolicy`只从真实苏格拉底德性主说／节点生成第三幕幕初邀请，不调用图边补走历史。`DiogenesStaySwitchEntryRecord`保留版本、局身份、源房间、完整只读事实（含真实成本）、原材料摘要和关闭标记；默认共享Codec可严格往返，未知／不匹配的记录不重建。事实来源及固定行动ID进入代码拥有的SaveRestore目录，无材料时不生成说法或公开历史。

`DiogenesStaySwitchEntrySession`在原生幕初MapRoom完成后先保存并读回同一共享编码，再叠加标准事件；准备／提交仍复用芝诺每局事务。读档只在完整LoadIntoLatestMapCoord结束后恢复幕初缺失叠层，不能在恢复基础房间时插入第二个事件。第欧根尼EventRoom在初始选项前重绑OnStart并重试同一个去留事务；Leave先持久关闭标记再只退出同身份叠层。`ZenoRouteNativeTriggerPolicy`持久排除入口源房间，幕初MapRoom本来也不生成完成回执；后续普通房间、Boss、离幕沿原调度保证出现。

该入口已合入`main`并与后续人物路线共存；正式芝诺静态图也已替换开发占位图。`tools/VerifyMod.ps1`已完成全量逻辑、Release、PCK纹理校验和安全部署。第二幕披露、第三幕说法修订与苏格拉底主动撤离玩法仍不在当前交付内；没有合法事实时正常进入NoMaterial，不从当前资源反推历史。后续游戏体验问题按独立反馈处理，不作为本次部署完成的闸门。

- `src/Patches/NeowProceedPatch.cs` 与 `src/Patches/ActTwoPhilosophersGazePatch.cs` 分别负责第一层和第二层事件插入。
- 第一层插入会拦截已经结束的涅奥事件；必须先对底层涅奥 `EventRoom` 调用 `MarkPreFinished` 并保存，再用嵌套房间进入“诸子观照”。否则退出读档会恢复一个仍可结算的涅奥房间，造成奖励重复领取。
- `src/Events/PhilosophersGaze.cs` 负责事件页面、遗物授予与替换、拒绝处理和保存。
- `src/Events/PhilosophersGazeFlowPolicy.cs` 定义页面、选项和结果转换。
- `src/Philosophy/LaterActRelicEligibilityCatalog.cs` 是第二、三幕后继的统一资格表。每个人物声明幕索引与接受的前置遗物；运行时每次从 `Player.Relics` 的当前 `ModelId.Entry` 重新计算，不保存派生候选，因此读档、返回地图和遗物替换后不会沿用已经失去的旧资格。
- `src/Events/PhilosophersGazeContinuationPolicy.cs` 负责第二层东哲事件门控；六条“根遗物 → 固定后继”由 `RegisteredRelicContinuationCandidateSource` 从同一资格表映射为现有选项。混入不同东哲根家族的损坏／调试状态仍静默跳过，同一家族双根的调试状态保留两个明确候选。
- 柏拉图、笛卡尔、伊壁鸠鲁和韩非的生产触发同样调用 `LaterActRelicEligibilityRuntime`；韩非只接受第三幕当前仍持有的绳墨。第欧根尼与芝诺登记为第三幕审思砝码后继，但各自特殊路线状态仍须另行满足，状态只能收紧资格。开发专用控制台不代表生产候选。
- 资格表没有兜底人物：本幕没有已登记且当前持有遗物命中的人物时返回空集合，调用方保持正常地图流程。新增人物只登记合法关系；尚无关系的人物不进入池。
- `src/Philosophy/` 保存新赐福流程的局内哲学状态、第一层候选策略与序列化逻辑。新存档通过RitsuLib的`PHILOSOPHY_RUN_STATE`局内槽保存既有Codec输出，不再把自定义`ModelId`写入`EventsSeen`。旧`STS2PhilosophersRunState.V1_*`标记仍作为只读兼容入口：合法旧状态在首次正常保存时迁入RitsuLib；版本未知、结构损坏、互相冲突或无法验证的旧标记按原顺序保留，禁止替代重写。
- `SocratesVirtueUpstreamRecord`在同一共享运行状态中保存苏格拉底德性分支的一次真实撤离机会和已提交行动。生产入口只接受已建立的苏格拉底德性节点；继续与成功非胜利撤离分别固化当时未恢复的生命损失、已消耗药水、撤离机会和胜利奖励结果，重复回调或读档重试只能得到不变结果，冲突事实拒绝。只读快照不含目的、理由、勇敢判断或后来胜负；旧档、取消请求和未提交机会都不给第欧根尼适配器提供材料。主动撤离本身尚未接入游戏，所以当前正常游玩不会产生这份材料。
- 芝诺持久确认通过`IZenoRoutePersistenceConfirmationAdapter`边界返回已确认、明确失败或结果未知。保存完成后会经游戏当前`ISaveStore`读取真实`current_run.save`，严格解析RitsuLib sidecar并用既有Codec核对相同局、检查点、操作序号、阶段、修订和候选摘要；旧档尚未迁移时才回退读取旧标记。单纯等待`SaveRun`返回、读回不一致、读取失败或无法证明未落盘的错误都保持结果未知。
- `ZenoRoutePersistenceCoordinator`为单局芝诺路线串行准备与提交检查点。准备明确失败且证实未持久时才释放源选择；准备或提交未知会冻结，同一请求只能显式重试；准备已确认后即使提交明确失败也不能回到源选择。`ZenoRouteGamePersistenceAdapter`只调用一次`SaveRun`，随后以磁盘sidecar读回确认；损坏的RitsuLib槽不会被旧标记或当前内存状态覆盖。
- `ZenoRouteRuntimeService`以`RunState`弱引用保存每局唯一的`ZenoRoutePersistenceRuntime`，同一容器拥有共享状态、协调器、校验目录与游戏保存适配器。读档中的合法准备记录会恢复为同一确定性请求并等待显式重试；运行时结果始终回写同一共享状态，状态容器被替换时旧运行时失效。早期加载因尚无生产目录而隔离的合法材料标记，可在运行时取得目录后原样重验并恢复；未知或损坏标记仍保持隔离。
- `ZenoRouteRecoveryPlanner`只读取已验证路线、可选写前记录以及事件／恢复目的地现场，输出一个无副作用动作。无准备事务时可恢复第欧根尼选择、等待间隔／领取、续建同一事件、恢复Z0／对应Zr或保持终态；六类普通准备只重试同一操作，关闭准备单独完成原目的地恢复。事件身份、目的地与阶段矛盾时只输出隔离。计划携带冻结的操作序号、事件身份、结局和目的地，不查询当前Boss或地图；当前尚未接现场探测、七类业务回调或页面执行。
- `ZenoRoutePersistenceRuntime.RecoverAsync`在同一每局锁内先读取现场、生成恢复计划，再只执行一个动作。合法写前记录由运行时用原请求身份重试，不交给页面适配器；普通页面／等待动作只接受不可变计划。现场读取失败、页面执行失败与矛盾现场均不改路线事实；事件关闭与目的地恢复属于外部效果，现阶段明确阻断并保留原写前记录。调用方可组合真实房间栈探测器与芝诺页面恢复适配器，触发调度仍未接入。
- `ZenoRouteRunSceneProbe`只读`RunState`的当前／基础房间和房间数量，把显式第欧根尼／芝诺事件身份与冻结目的地转换成规划器现场；房间为空、超过两层、重复路线事件、实例不匹配、活动芝诺阶段叠加其他事件或非终止阶段遇到局终事实都会冲突隔离。`ZenoClosed`阶段本身证明原目的地已消费，后来进入其他合法房间不会把已闭合路线误判损坏。`ZenoAssentBoundary`已通过`IZenoRouteEventSceneIdentity`提供同一事件实例身份，目的地仍由只读`IZenoRouteRoomDestinationReader`适配；调度器尚未建立具体事件房间。
- `ZenoAssentPageFlow`只从已验证的冻结材料裁剪Z0、Zp、Z1和五类确认页，从已提交结局恢复唯一Zr；确认前往返不写路线事实，确认只产生一个持久化意图。`ZenoAssentBoundary`使用锁定的`ZENO_ASSENT_BOUNDARY`模型身份与本地化键矩阵，`ZenoAssentBoundaryRecoveryAdapter`只恢复当前房间中的同一实例。`ZenoAssentBoundaryStateHost`把最终确认转换成`PrepareOutcome`并交给同一每局持久运行时；只有准备和提交检查点都确认后才允许页面进入对应Zr，明确失败、结果未知、冻结、非法结果和错误实例均不伪装成功或另开保存。`LEAVE`关闭与目的地恢复继续阻断。
- Phase 2A 在 `ActBehaviorState` 中分开保存通用游戏事实、表达机会与行为印象。事实可在一场战斗内累计；同一表达机会和同一行为印象每场最多结算一次。`ActiveCombat` 支持战斗中途随局内状态往返，旧 Phase 1 存档缺少的新字段会恢复为空集合。
- `src/Patches/BehaviorObservationPatch.cs` 监听游戏全局战斗开始、出牌完成与战斗结束钩子，仅在单人局调用 `BehaviorObservationRecorder`。当前事实包括战斗开始/完成、出牌总数及攻击、技能、能力、状态、诅咒、任务等牌类型；事实不直接生成行为印象，也不参与候选。
- 每场被记录的单人战斗结束后，日志会输出 `[STS2Philosophers] Behavior observation:` 摘要；字段按固定顺序排列，事实键按代码序排序，可用于核对跨战斗累计与读档恢复。
- `config/thinker_proposals.json` 是“人物 + 具体思想提案”的配置雏形，以嵌入资源进入 DLL。Phase 1 只登记六件现有根遗物；`qualification_rule_ids` 与 `resonance_tags` 保持为空，等待后续行为候选设计。
- 六件根遗物分别保存第二层是否已经处理；12 件遗物的战斗状态保存在各自实例上。当前唯一显式跨遗物数据迁移是青玉佩的 `Virtue` 在替换时写入熊掌的 `InheritedVirtue`。
- `tests/ModLogicChecks/Program.cs` 覆盖页面流、根遗物互斥、第二层过滤与替换、德继承、重复回调和本地化键。

## 韩非第三幕公开标准事件

`HanFeiPublicStandardPatch`只在单人第三幕、玩家当前持有绳墨且本幕至少完成一场战斗后，于安全返回地图时插入原生标准事件。资格来自统一后续幕登记表；没有绳墨时不创建状态或空事件。事件复用诸子观照背景，不新增地图节点、人物素材、动画或遗物。

`HanFeiPublicStandardState`把页面、预先公开的数量／公共存量标准、最终荀子／韩非路线、规则撤回历史和关闭状态保存在共享局内状态。每次选择先保存 `PendingAction`，再提交并二次保存；读档只重放同一动作。坚持荀子会完成情境判断终点；执行预先公开的标准会转向韩非；看见个案后撤回会回到荀子但保留撤回历史。所有结果都不发数值奖励、不替换绳墨，也不因用户尚未试玩而保持未完成。

以上是 Phase 1 开始时的兼容基线，不代表未来动态候选架构；当前设计见 `docs/design/current/00当前设计索引.md`，任务和待决问题以 Linear 为准。

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

该入口使用 `local.props` 中的 SDK，依次运行领取规则检查、纯逻辑测试、无界面Godot中的原生运行时检查、禁止部署的 Release 构建、无界面的 PCK 打包及独立加载校验。全部通过且游戏未运行时才备份并校验现有部署，复制三个 MOD 文件并逐一核对 SHA256；游戏运行时保留校验结果并报告部署受阻。传入 `-SkipDeploy` 可明确只检查。任何测试、构建或内容包错误都会停止后续部署。

每次执行先重置 `bin/Release/net9.0/verification.md` 为进行中；失败记录具体阶段和错误，避免误读上次成功。成功报告保存开始/结束时的工作区状态、源代码基准提交与产物哈希，已跟踪差异保存在同目录 `verification_changes.patch`；未跟踪文件仅列名，不归档内容。构建使用整个工作区，不能把报告中的基准提交当成部署版本的完整描述。脚本不启动游戏、Steam 或 Godot 界面，自动检查不能代替游戏内验收。

阶段收尾按 [AGENTS.md](./AGENTS.md) 的固定顺序执行。审查、纳入本阶段的 Godot 生成配置与清单修改也应随阶段提交；仅记录其他窗口的改动，不擅自提交。构建后再次检查这些文件，避免打包生成的新差异被遗漏。

以下检查不启动游戏：

```powershell
dotnet run --project .\tests\ModLogicChecks\ModLogicChecks.csproj -c Release
```

单独运行领取规则与原生集成（PowerShell 7）：

```powershell
pwsh -NoProfile -File ./tests/DeliverySelectionChecks.ps1
./tools/RunNativeRuntimeChecks.ps1
```

宿主为 `tests/GodotRuntimeChecks` 的独立无界面场景及独立用户目录，不启动游戏或编辑器。可用 `-RitsuRuntimeDirectory @('实际版本兼容程序集目录', '实际版本shared目录')` 在临时测试目录替换整组Ritsu程序集，检查输出记录实际加载路径／版本；不覆盖NuGet或已安装Mod。领取时另将最新完整Linear快照传给 `tools/SelectDeliveryIssue.ps1 -IssueSnapshotPath ...`，不能把模拟测试或旧快照当成实时领取结果。

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

`config/western_routes.json`与`WesternRouteCatalog`登记七问题六入口。ENC-12后，西哲不再拥有独立候选命名空间：`PhilosophersGazeActOneCandidatePolicy`把三位东哲和六位西哲登记为一个九人池，用`ACT_ONE_THINKERS`保存三人组合；`WesternEntryPolicy`只允许统一候选中实际出现的人物提供其既定问题。旧`WESTERN_ACT_ONE_THINKERS`存档字段不再读取，也不会与当前三人集合拼接。后续阶段见 `docs/design/current/west/00西哲当前路线.md`；旧 `10西哲路线实现` 只保留历史推演。

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


## 思想转译审查（D90）

D90思想转译审查完成：19修正未承诺却称原话、必要条件误作充分条件、成功充分条件反推失败无知三处正文/表格；撤回D24相关通过判决。试授只检验有限用途材料，不认证知识；德性必要条件为游戏改写，不冒充《拉凯斯》原句。两原型只改边界提示，知识23/德性28项纸面及全部浏览器检查通过。无生产C#/游戏资源改动，README核对旧能力仍准确，无游戏构建/PCK/部署或人工验收。当前人物尚未完成思想与乐趣验证，独立设计继续。

可恢复审查摘要见docs/design/SocratesTranslationReview.md，G盘63为维护真源。下一步先设计用途与根据的区分及表达负担，不先新增生产协议。


D91按64先设计再完成知识材料追问：未抽到、抽到未用、实际命中次数/目标、格挡因果和单场比较各有具体问题；无当前肯定句不追记信念，旧副本快照不被新材料覆盖。未新增表格/必填项/弹窗/资源门槛，回看只保留一句处理说明加一个材料问题；视觉检查后去掉重复泛问。知识28项纸面及全浏览器/明暗窄屏通过，360暗图已查看；德性此前28项未变。纯纸面，无游戏构建/PCK/部署，README生产未变，人工体验与原生证据独立待验证。
下一D92同样成功与不同根据的设计反例；G盘64为真源。原有tmp/保留，ISSUE010等待新连接恢复证据，不重试推送。


D92德性命题范围已修：修订句仅否定“不后退”为勇敢通用必要条件，不扩大为所有形式无关；范围从未认可/已认可/明确撤回分别记，复用原勾选框。先保留未定再撤回范围时，后续追问尊重最新范围处理，旧标准与自评不被撤回；重新认可则恢复同范围问题，旧历史不抹掉。32项纸面及浏览器/明暗窄屏通过；首轮浏览器过时正则已同步后重跑通过。纯纸面，无游戏构建/PCK/部署，README生产未变；实际体验和原生验证独立等待。
下一D93同果异据设计，见G盘65当前范围；原有tmp/保留，ISSUE010无新连接证据不重试推送。


## 同果异据设计（D93）

D93同果异据设计已完成：甲乙完全相同的8生命局部句和击倒结果，分别有规则说明/明确猜测；不由成功或说明长度判知识。9生命新对象不能反驳旧局部句，整场补攻击获胜不证明连击独自击倒。否决知识分/解释长度换权限与强制根据标签，保留有限承诺审問候选，未新增人物/状态/HTML。具体循环、收益代价、失败与下一最小片段见66；纯设计无测试构建部署，README核对生产未变。

下一D94固定9生命片段，未实现；原有tmp/保留，ISSUE010无新连接证据不重试推送。


D94已按66设计增加nine固定9生命单目标片段：连击两次共8伤害后目标剩1，仍需基础攻击才正常胜利；普通收入/试授前缀同结果、权限独立，旧局部句不被改写或登记已反驳/已知道。知识29项纸面及浏览器通过，德性此前32项不变。仅HTML/两知识检查与记录；纯纸面无游戏构建/PCK/部署，README生产未变。当前历史材料尚未显式保留试授行动后目标剩余生命，下一D95先设计准确观察记录，避免回看把整场击倒归因单张牌。
下一D95先设计观察快照；ISSUE010独立等待、tmp/保留，无运行操作。


D95仅保存行动观察设计，G盘67为真源，摘要docs/design/SocratesObservationPlan.md；未改HTML，不重复测试/游戏构建部署。下一按67最小实现实际命中前后生命及快照。D94提交64e20e6，原有tmp/保留，ISSUE010独立等待。


## D95实现结果

D95行动观察最小实现已完成：每次实际命中保存准确hpBefore/hpAfter，只记录所选副本；8生命8→4→0、9生命9→5→1、4生命4→0及基础攻击前置/同名额外副本分别验证。战后和后续回看明确命中后状态，后来其他牌击倒不倒写；未抽/未用/纯格挡不造目标材料。33项纸面与浏览器流程、自由文字安全、860/360/320明暗响应通过。未连接游戏，不证明知识、必要性、平衡、真实保存或人物乐趣；无生产变动、无游戏构建/PCK/部署，README核对生产未变。

下一D96收束当前人物循环、长期停留与既定后继，G59为设计入口。未把纸面通过写为实际游戏能力。


## 苏格拉底人物候选收束（D96）

D96已在59汇集苏格拉底两条完整设计候选：核心问题/独特动作/收益代价/反例/失败恢复、跨幕方法、最终停留与句子可修订、旧事务处理后切换及既定后继LocalPlanning边界。达到逐人物核心候选可继续研究下一人物的判准；未宣布人物发布或乐趣验收，不把已写HTML强留为成立规则。真实身份/非胜利退出与纸面体验独立待验证。下一普罗泰戈拉既定问号先审《普罗泰戈拉》与公共教育问题，不先写数值或礼包。

摘要docs/design/SocratesCurrentDesign.md，维护真源G59。纯设计不重复测试/构建部署；README生产未变，旧杯/砝码保留。


## 普罗泰戈拉教学候选（D97）

设计先行摘要docs/design/ProtagorasTeachingDesign.md，G68维护真源，原典研究见G研究21。当前完整循环候选未有可操作原型，Question/Keep不加第二主说。下一仅低保真资源分配/教学迁移/有代价接回决定与自用对照，不注册游戏模型或新增生产内容；资源公平不是思想正确性分数。


D98纸面文件docs/design/protagoras_teaching.html；bundled Node执行tools/design/ProtagorasTeachingPaperChecks.cjs和ProtagorasTeachingBrowserChecks.cjs（传bundled Playwright路径），12项/48条组合与浏览器明暗窄屏通过。只验证模拟指令、资源守恒、机会成本和一次结算，不证明真实学习/德性/乐趣/生产安全。没有C#/游戏资源改动，无VerifyMod/部署；README生产未变。下一D99先审NPC参与与实际玩家价值，G68维护。


D99设计复审：当前D98 HTML通过12项/浏览器仅证明按指令分配，受教者自主判断不足，暂不接德性问号生产。G68/ProtagorasTeachingDesign.md最新方案为明确意见、知道但拒绝代办、有限替案、亲自承担或结束共用；尚未接HTML，下一D100最小实现。无生产构建部署或新的游戏验收。


D100已将G68 D99局部意见/拒绝/替案/次轮结束共用接纸面。16项与浏览器明暗窄屏通过；拒绝提案只能保留预测，不登记最终所得，亲自执行不替同行者登记新学习或赞同。首轮旧断言更新后通过，原指令/声音记录冻结。纯纸面，无游戏构建部署；下一D101先改展示负担，权限/数值不变。


D101只纸面展示：两委托分支比较，个人接管数学明细按需展开，结束共用完整预览；模型与4012ca8相同。16项和浏览器键盘/预测/明暗窄屏通过，不改资源/权限。无游戏构建部署；下一先G68审人物具体引入/实际材料回扣，候选非生产验收。


D102仅设计/研究记录：不能以未指定操作指令排除日常教育；G68新开场/结果归属明确为游戏改写，尚未接HTML。15只修当前候选状态，不在基线导入输入。既有Keep/拒绝记录与EDGE009门槛未改，纸面不调用图或认证思想正确性；下一D103修相称纸面表述，不加机制/数值。


D103加游戏组织者/结果发言归属，并将自用/退出断言收窄到明确指令与实践未观测；不排除日常教育。17项和浏览器明暗窄屏/键盘/文字安全通过，分配与愿意条件未改；来源/G68判断与游戏改写分开。无游戏构建部署；下一只收束当前用途候选与未证门槛。


D104当前人物用途维护G68/ProtagorasTeachingDesign.md，阶段旧稿完整留G69/ProtagorasTeachingReview.md，原典与推理边界见ProtagorasTeachingResearch.md/G21。核心候选可继续独立研究下一人物，不等于进入游戏/乐趣验收。17项/浏览器覆盖仅当前虚构片段；事件预算、真实治疗/读档和Question互斥待证明，不自动接入生产。下一柏拉图德性后继先原典/具体问题面。


## D105柏拉图德性研究

D105已复核《理想国》正文419a–421c、441e–444e、518b–519a，区分整体照料、内在安排与敏锐能力可服务坏目标。研究22与设计70只保留“课程改变未来奖励选择”的待检验载体，拒绝三类牌等于三部分灵魂、最大收益等于善与另一套三人分配。尚无完整能力或代码；下一D106先算两个课程、两份同条件奖励与变化局面的完整循环，若取舍不成立就换载体。六入口/七问题/89人及关系图未动，原型体验与生产安全独立等待。

D104=7363791，阶段前27笔待推；本阶段4新研究/设计记录及恢复镜像为本任务。原典定位与设计推论已分开，Markdown差异与G/S内容哈希核对，无需代码测试或生产构建；未游戏验收、未部署，TLS独立等待。


## D106课程载体暂不成立

D106已在71完成课程的两份奖励/变化局面/普通收入与跳过对照，具体收益损失、退课不回滚和后继边界。Node有限六回合复算通过：专攻可及时胜利却不能存活新局面，多种应对可先不攻击再安全胜利；这些只支持构筑取舍，不支持整体善或教育转向。判决暂停课程作主能力，不投入可操作HTML或生产实现；D107回到具体目的冲突与统领方式，先比较载体/代价/反例。70是当前判断，71留完整审查；未改变既定规划或生产。

可复算工具tools/design/PlatoCurriculumCounterexample.cjs仅虚构输入，有限6回合与跳过组合，不调用游戏API；不能证实正式牌数值/战斗/乐趣。原典研究22仍有效，暂停的是载体。D105=873d62d，阶段前28笔待推，TLS独立等待。


## D107目的牵制的载体复审

D107补读439b–440c并收窄为“眼前目标是否占用之后改变安排的余地”；72比较自动目标出牌、未来意图观察与跨实际窗口预支。前两者不选本用途，预支仅保留D108两场连续战斗试算，尚无批准主能力/教育成长或模型。明确胜利不可免债、终战无兑现窗口不得借、小战低成本与目的改口不清债；若只剩通用能量优化仍否决。70已整理为当前判断，71课程审查保留，不用旧稿继续铺代码。

D106=75b1068，阶段前29笔未推。仅原典补读/设计判断/索引，未修改复算工具或游戏；不重复旧测试或构建部署。具体当前入口70，研究22与审查72，G/S备份同步，TLS独立等待。


## D108预支的成本与载体判决

D108在72写完跨两场的预支/不用/小战低成本偿还/新局面死亡对照，有限八轮Node复算9/17/8生命输入通过：实际未来损失可为0、4生命或无法存活。胜利不免债、改口不清债、无兑现窗口不借。判决不作当前主能力：有预算取舍但尚无具体目的统领/教育改变。D109改读519–521，先设计学习所得与非自身报酬职责的冲突，不再改贷款参数；六入口/既定图/生产不变。

D107=5f7999f，阶段前30笔未推。bundled Node只有限计算能量3/2的固定手牌输入；全路径或死亡已在前三轮，不证明游戏数值/保存/乐趣或哲学。无API调查、主要能力或游戏资源修改，不重复生产检查；G/S备份同步，TLS独立等待。


## D109学习与有限共同职责的新处境

D109复核519c–520e，提出73守护轮值新载体：学到的改向权限可服务自己或共同设施，教师/维护者提出有限要求但玩家可拒绝，双方实际损失持续。不是三牌型灵魂或额外抽牌格挡；历史论证与虚构设施明确分开。尚未批准主能力，教育成长/修复/长期停留未完成。D110先计算两场三个方向路径，呈现实际职责和用途，检查免费替身/第二血条及后续变难的反例；规划/生产不变。

D108=0bba10c，阶段前31笔未推。原典《理想国》正文519c–520e定位与虚构角色/能力分开，无API调查或主要能力实现，不重复游戏构建。当前G70、候选73、研究22，G/S备份同步；TLS独立等待。


## D110风险承担与实际通行

D110在73完成两场改向实验：维护者提出两次照亮通路的有限要求，旅人实际通行/等待，不做善恶分；正常结局自己生命4/灯3、自己2/灯5均让两人通行，自己5/灯0则两人等待。变难时早烧灯会死亡，保留灯后再放弃可保命，但未完成第二次通行。逐段有限Node复算实际生命/格挡/过量伤害与未执行攻击一致。载体值得继续，尚未批准主能力；D111先设计可拒绝的维护/修复工作、真实机会成本及后续等待，不先代码。

D109=61d9fca，阶段前32笔待推。Node只复算固定命中序列，目标失去后本轮未执行与下轮明示改目标分开；死亡实际损失按剩余生命截断。没有新增游戏目标/怪物/资源或API调查，不重复生产检查，G/S备份同步，TLS独立等待。


## D111 可拒绝的维护与实际恢复

D111完成73维护循环：维护者自己维修，玩家协助劳动或同时间休整；灯失去需两人重建，未参与仍可等邻居未来帮助。满血放弃回复实际成本0、满灯无假工作；修复不回写甲曾等待，新目的不抹旧损失。正常与变难维护路径有限Node复算一致，真实用途/代价/恢复及未执行帮助分开。D112先审完整循环思想转译/玩家价值及正式战斗范围，再判可否做限定纸面，不接生产。

D110=c9ffa03，D111前33笔未推，收尾后按Git。G70/73及当前设计/实验、开发和活动镜像为本任务；tmp/保留，无运行操作，README生产准确，无游戏构建/PCK/部署。G/S备份同步，ISSUE010无新恢复证据不重试。
Node有限维护矩阵与手算一致；变难路径的真实风险承担继续沿D110顺序。不把未来邻居帮助记为实际修复，不给伦理分或玩家奖励，不改89人用途/关系图。


## D112 完整循环审查与限定纸面范围

D112在73审完整循环，明确早替受可能被另一工作安排支配、多牺牲不叫更好；烧灯重建在私人指标可能更优，公共差别是甲实际多等，不能偷偷罚金币逼正确。批准范围仅D113固定虚构输入纸面，不是完整主能力；正式地图/普通怪物目标/持续教养与交接仍未设计，禁止先给原生怪加攻击部署。停留不刷新资源/叠能力，换主说不删共同世界或旧结果，后继关系不动。

D111=d4e4937，D112前34笔未推，收尾后按Git。G70/73及当前设计/审查、开发和活动镜像为本任务；tmp/保留，无运行操作，README生产准确，无游戏构建/PCK/部署。G/S备份同步，ISSUE010无新恢复证据不重试。
仅审已有固定案例与原典定位，未改复算规则，不重复测试/游戏检查。后继关系和其他窗口内容保留。


## D113 守护轮值的限定可操作纸面

D113已实现固定纸面plato_guard_duty.html：同权限回应、单段改向、逐段实际损失、通行/等待、维护者工作和可空句。15组/42路径及浏览器键盘、明暗窄屏、预测实际、0回复成本、延后通行/旧到场、死亡后未观测与文本安全通过；360暗维护/860亮战斗图已查看。浏览器首次折叠innerText断言纠正后通过，不改模型掩盖。仅验证虚构规则；完整主能力/正式地图未定，无游戏构建/部署。D114先审默认全算式负担，不改规则/权限。

D112=9f55568，D113前35笔未推，收尾后按Git。新增plato_guard_duty.html及PlatoGuardDutyPaperChecks/BrowserChecks与G70/73、当前设计/实验、开发和活动镜像归本任务；15组/42路径与浏览器通过，无运行遗留。tmp/保留，README生产准确，无游戏构建/PCK/部署。G/S备份同步，ISSUE010无新恢复证据不重试。
bundled Node/Playwright headless msedge执行；浏览器已finally关闭。截图忽略bin/design/plato_guard_duty_*，已查看360暗维护/860亮战斗。实际/预测、死亡后未观测与维护工作边界分开，未游戏验收。


## D114 风险比较先于全量计算

D114按先前设计简化默认预测，明确是哪一段/谁承担、双方损失与格挡，逐段计算默认折叠并可键盘展开；所有权限保留。模型原文与D113一致，15组/42路径及浏览器展开计算/历史、预测实际、安全文字与明暗窄屏通过，360暗/860亮图已查看。仅展示，无原生/资源改变；D115先审人物实际目的和论证归属，避免甲乙只是状态数、柏拉图只在标题，不用数值奖罚制造关心。

D113=20d369e，D114前36笔未推，收尾后按Git。仅HTML展示/浏览器检查与G70/73、当前设计/审查、开发活动镜像本任务；模型原文相同，15组/42路径与浏览器通过，无运行遗留。tmp/保留，README生产准确，无游戏构建/PCK/部署。G/S备份同步，ISSUE010无新恢复证据不重试。
明暗360/320与860、闭合/展开计算及历史不溢出；已查看360暗/860亮战斗图。headless msedge finally关闭；没有全仓/API/游戏重复审计。


## D115 最新制作约束与载体可行性

D115已读另一窗口cf1a41d最新用户制作约束，优先于旧人物扩写计划。74列正式节点/操作/界面/素材：普通战斗和专属两场仍需新增目标/设施场景，纯事件页会失去核心动作；停止守灯作主能力推进，仅保留HTML/检查及研究实验。约束从仍完整的活动真源恢复到接续/70/73，其他窗口规则提交保留。D116先比较现有卡牌出场安排与互斥用途改写，先成本/实例再判设计，不增新世界对象或美术/代码。

HEAD阶段前9ece750，38笔待推，其中cf1a41d是另一窗口规则/制作约束提交且保留；阶段后按Git。D115只74/PlatoProductionReview、G70/73及当前设计/开发/活动镜像/索引；tmp/保留，无运行操作，无原型/游戏代码或资源改动。README生产准确，无新测试/构建/PCK/部署。G/S备份同步，ISSUE010无新证据不重试。
已核对新HEAD/另一窗口状态、AGENTS与ProductionConstraints和G活动最新约束；恢复同一用户附录并改准确下一步。只制作需求与成本判断，不重跑未变纸面测试。


## D116 按制作与思想双重标准调整设计

D116按用户新标准完成75设计与呈现审查、76柏拉图卡牌载体比较：先写正式节点/操作/界面/素材/动画，再审完整取舍、思想差异与反例。开局排牌只解决波动，封存范本有通用复制/压缩牌组与身份归还风险，均未批准；守灯仅实验冻结。低素材成本不替代人物设计，不先铺代码或美术。

HEAD阶段前c829d48，39笔待推；cf1a41d其他窗口制作约束提交保留。D116仅新标准/卡牌比较/当前人物与开发活动镜像，G11/75/76同步；tmp/原有归属保留。无运行操作，无游戏代码/资源改动。README已核对生产体验不变，无重复构建/PCK/部署。阶段后按Git复查；ISSUE010 TLS无恢复证据不重试推送。
验证：真实原生卡牌基值/Survivor弃牌条件与既有保存源码核对；两候选的零代价/策略支配/复制与哲学反例审查；文档差异检查及G→S内容哈希核对。未执行游戏验收，未把候选写成完整可玩。


## D117 修正真实代价并收束正义问题

D117用五个受控原生牌基值反例复核：Survivor弃牌不一定损失可用行动，能量不足/孤张该成本可为零，76表述已修；先击倒会取消敌人阶段，不能只比较格挡数。77区分战术目的切换与尚缺正义/整体内容，暂停自选目的换用途，不批准新能力。守灯仍冻结，新标准不因低素材或自动复算放松。

HEAD阶段前85ffdca，40笔待推；其他窗口notLoaded，无并行实现。D117仅76真实代价修订、77及当前人物/日志/开发活动镜像；tmp/原有保留，无运行构建/游戏操作，平台CUA常驻进程不动。用量查询失败但请求可执行，照常推进，不兑换/购买/绕限。README生产不变，无游戏测试/构建/PCK/部署。阶段后按Git；ISSUE010无恢复证据不重试。
验证：原生Survivor.OnPlay/基值再次核对，五受控情形有限计算一致，明确能量2非原生默认、敌人/手牌是假设、只比本轮。SEP伦理3.1–3.2作学术解释，原典与设计分层；非历史新结论。文档差异与G/S哈希检查，未游戏验收。


## D118 区分正义挑战与商店载体用途

D118研究23区分《理想国》第二卷三类善、格劳孔挑战与阿得曼托斯要求，不将强化不义论证视为柏拉图推荐。79选既有商店关系：同一实物、正常购买或匿名取用有不同真实支付，明确正价/贫穷/满槽/失败/重复条件与开发风险。可留一次思想挑战，未批准持续主能力；不以隐藏奖惩弥补正义内容，不写商店补丁或美术。

HEAD阶段前5c6f9bf，41笔待推；其他窗口未加载，tmp/既有保留，无运行构建/游戏。D118仅研究23/匿名案例79与当前人物/开发日志/活动镜像；所有生产代码/资源不改，README生产体验不变。用量最近读取未知但请求可执行，不据此停止，不兑换券。阶段后Git复查；ISSUE010无恢复证据不重复推送。
验证：第二卷正文主题/发言者与SEP解释对照；商店三选择120→20/120/120及0价/不足款/满槽反例人工复核。候选获取及回滚规则未实现，未称交易API安全或哲学完成。文档差异与G/S哈希核对；无新游戏测试/构建/PCK/部署或游戏验收。


## D119 检验共同职责的既有界面载体

D119明确79仅一次性后继对话材料，不能当持续主机制。80共用器具候选先列既有战斗/休息点、敌人原生攻击后真实生命损失的一次选择、持久耐久与修理/交还；不增敌人攻击、第二目标或额外场景，素材目标一图标。两场/休息修理实例复核，交还不伪造送达受益；共同目的仍缺实际他人行动，不批准主能力或纸面实现。

HEAD阶段前c7fb688，42笔待推；D119仅80/当前人物/日志开发活动镜像，tmp/既有保留；无运行构建/游戏，无生产代码资源改动，README不变。最新用量请求允许，继续授权设计，不消耗重置券。阶段后按Git；ISSUE010 TLS无新证据不重推。
验证：L=6/5两次分配、耐久上限9/修复4及真实治疗上限12有限算术核对；零伤/多段/来源/击倒/耗尽/交还继续条件人工审查。原生敌伤时点和选择控件未验证，不称接口可部署。文档差异/G→S内容哈希检查；无游戏自动检查/部署/验收。


## D120 停止借用挡伤并转向规则控制取舍

D120审80仍是有限护具挡伤、共同用途只承诺，停止推进，不补故事/中断接口。81新候选先正式呈现/素材/流程：公开委托规则自动选实际牌和目标，或自行选择，费用减免上限2/1但不是直接送能量；原生Bash/Defend三/四手牌两处境与正常费用基线已复核。不同控制动作可做限定一轮纸面，未批准完整主能力或哲学解释，不把控制=理性、委托=欲望。

HEAD阶段前87c9e26，43笔待推；D120仅80停止判决、81/当前人物/日志开发活动镜像，tmp/保留，无运行操作，无生产代码或资源改动。README仍准确，不重复游戏测试/构建/PCK/部署。其他窗口既有制作约束保留；阶段后按Git，ISSUE010无证据不重推。
验证：原生Bash.cs成本2/伤害8/施加易伤2与DefendIronclad.cs成本1/格挡5核对；五具体方案损失4/0/3/7/3复算，B自行还有全防选择、A余能量/基线实用收益可零如实说明。敌人/小手牌有限假设，非真实遭遇或全局最优证明。文档差异/G/S哈希核对，未游戏验收。


## D121 交付统领方式有限控制纸面

D121实现81限定一轮纸面plato_rule_authority.html：A/B受控输入、正常费用基线、委托/自行/不用、真实原牌/费用/目标/实际敌伤记录与可空回应，零费减免不虚报。模型204合法前缀及无GUI Edge键盘/文本安全/明暗860/360/320通过，截图已看，默认范围收简、原生短名与输入/制作边界按需查看。没有绑定游戏自动出牌/存档，没有证明正义/理性/幸福或乐趣，不批准完整主能力。

HEAD阶段前bda9ffb，44笔待推；D121仅统领纸面/两个纯纸面检查脚本、81/当前人物及开发活动镜像；tmp/保护保留。无运行浏览器/构建/游戏，其他窗口未加载。README生产准确，无游戏C#/本地化/资源/构建工具变化，无Release/PCK/部署。阶段后按Git；ISSUE010 TLS无新证据不重推。
验证：node纯模型204合法前缀，A/B具体方案/基线/单次减免/死亡停止/非法操作原子拒绝/回应权限；默认shell缺失的首次浏览器失败改复用msedge后通过，键盘/费用/文本安全/明暗860360320及18截图，无横向溢出。已看860浅色、360/320深色实际页。原生复杂牌/后续易伤倍率/完整战斗/自动API/游戏验收均不在结果内。


## D122 将控制实验与正义关系候选分清

D122设计审查82：81/HTML提供一般控制/费用取舍，仍没有正义/整体内容，不批准主要能力或扩实现。79D122优先候选无名环沿既定Adopt与单主说，在第二/三幕既有商店每幕一次合法正价药水；购买/匿名取用/离开、可当场补价、真实库存与付款、不回滚旧记录。取得药水是唯一战斗资源结果，程序不判善恶/幸福、不附常驻数值主能力。可做受控两幕交易纸面，未批准发布。

HEAD阶段前c02fac3，45笔待推；D122仅79当前规则/82判决/81用途说明及当前人物、日志开发活动镜像，原型与原有tmp/保留；无运行操作，无C#/游戏资源/构建工具变化。README生产准确，无重复测试构建/PCK/部署；阶段后按Git，ISSUE010无证据不重推。
验证：既有02赐福系统单一当前主说约束与79资源来源对照；120/100购买20、取用120、补价20及80不足/0价赠品/满槽/失败/换主说边界人工复查。不称经济主循环已体验足够或原生API已安全，原典/游戏开放问题区分，未历史改图或游戏验收。文档差异/G→S哈希核对。


## D123 交付两幕无名交易真实支付纸面

D123交付无名交易两幕纸面plato_unseen_exchange.html：购买/匿名取用/离开与当场补价，单件库存、槽位、真实金币/原价实付与本幕机会全显示；跨幕保留物品/旧支付/理由，不向新卖方补旧款，补价不再得物。898有限路径及无GUI Edge键盘/正常对照/满槽/免费/文本安全/明暗860360320通过，18截图及实图复查。免费货所有权说明与旧回应历史显示已修；账本/药水为模型，不证明原生库存/保存或哲学/乐趣完成。

HEAD阶段前948c104，46笔待推；D123仅无名交易HTML/两个纯纸面检查脚本、79/当前人物/导航11及日志开发活动镜像；tmp/原有保留，无运行浏览器/构建/游戏。README生产准确，所有游戏C#/资源/本地化/构建输入不变，无Release/PCK/部署或游戏验收。其他窗口规则保留；阶段后按Git，ISSUE010无TLS恢复证据不重推。
验证：898有限完整路径，真实扣款/单件取得/原价快照/JSON模型往返/卖方归属/资格不刷新/跨幕保留/非法操作原子拒绝；Edge键盘/补价无复制/跨幕实付与旧理由/正常对照/满槽/免费所有权/安全字面回应及明暗860360320无溢出，18截图。已看360深色收款/860浅色结束页；这些是纸面模型，非原生NPC账户或保存链。最终差异/G→S哈希核对。


## D124 审清匿名材料与付款正义边界

D124审查83明确程序事实/卖方观察/私人笔记/授课说法/后继自述边界；匿名为实验假设，不是原生视线验收，不让NPC自动读心或未分享记录变公开声誉。研究23补第一卷归还武器反例，原价/补价不是正义定义，不推一次行为诊断人格。70重写当前问题为财富与较好生活，旧载体统一历史，不让D115局部目标冒充最新。循环核心候选保留，哲学/玩法/正式安全不等于898路径；下一有界商店制作读源。

HEAD阶段前09ea92f，47笔待推；D124仅83/研究23/70当前重写、无名纸面范围与私人回应展示及开发活动镜像；tmp/既有保留，无运行操作。所有游戏输入不改，README生产准确；纯模型未变不重跑，受影响Edge界面检查已通过，无Release/PCK/部署。阶段后按Git；ISSUE010无恢复证据不重推。
验证：MIT第一卷借存武器反例主题与发言者核对，正义≠还债/原价；83知识流/赠品/满槽/自述反例审查。纸面模型未改；界面新范围/私人记笔记/授课时说法后Edge键盘/交易/免费/历史安全及明暗860360320通过，未称原生NPC账户/匿名或保存链已验证。最终差异与G/S哈希核对，未游戏验收。


## D125 定时接续遵守制作前置与动态人物恢复

D125已更新并读取实际配置验证既有sts2-2描述：制作成本、正式节点/界面/素材与动画前置；守灯/旅人冻结；从最新真源恢复当前人物，去除固定苏格拉底顺序，保留双分支单主说约束。仍每四小时唤醒同一任务，名称/状态/创建与目标不变，未新增任务。D124无名环仍为核心候选，不把纸面或自动通过视为正式安全/完整路线。下一步只读商店制作可行性。

阶段前HEAD22ebe77，48笔未推；D125仅既有自动化描述及接续/活动/阶段记录，tmp/既有保留，无运行命令。README与生产体验一致，游戏输入不变，无需Release/PCK/部署；配置正文逐字比对及原频率/状态/目标/创建字段校验通过。阶段后以Git为准；ISSUE010网络条件未变，不重试推送。
实际自动化文件已核验新描述，不以渲染卡片当更新成功；普通请求可执行，额度查询98%周用量不触发提前停止。S盘仅同步内容变化并备份/hash；D124人物正文不改。


## D126 商店载体只读审查与原生边界修正

D126有界读取七个原生商店/药水类型并核对程序集哈希；84记录动态价格、禁止/满槽失败、采购异步、补货、鼠标/手柄与商店不可恢复房间堆栈。免费采购仍购买历史/同步/回调，不能直接当匿名取用；79/70修正原实例取得不强清新补货。无名环仍候选未批生产。下一先设计就地面板、交易语义与部分完成恢复，不先代码。

阶段前HEAD7409c2f，49笔未推；D126仅84/79/70设计审查与阶段记录，tmp/保留。首次ilspy需.NET8而SDK9，命令内Major兼容运行成功，无安装/持久环境修改；最后检索Windows通配路径错误已改显式路径，七类型输出均可读，无运行命令。无采购/保存/游戏启动，README生产准确，未改游戏输入、无需Release/PCK/部署；ISSUE010未变不重推。
源码只证明相关方法行为；未核实游戏存档完整重建、控制器呈现或多回调安全，不把Godot对象序列化视为存档。输出仅忽略bin目录，提交为自己的设计结论。


## D127 就地界面成本与交易语义先于代码

D127补84的就地面板/交易/失效恢复规范：鼠标手柄同入口、默认结束焦点、返回不当拒绝、旧报价/实例失效不替换、模态冲突不抢占；读源确认通用弹窗仅两选，三按钮需自定义布局。未购取得与补价不伪触发购买奖励、原生取得规则保留，补货/自动采购边界待核实。反例为设计走查，不是自动/体验验证。下一仅只读存档恢复与局内记录判断，仍不批准生产。

阶段前HEAD3f3f84f，50笔未推；D127仅84/70设计规范与阶段记录，tmp/保留，无运行操作。只读另两UI类型，未实例化/调用交易；反例为纸面审查。README准确，游戏输入未改，无Release/PCK/部署；ISSUE010未变不重试。
NModalContainer.Add已有模态只返回，NGenericPopup.WaitForConfirmation只有是/否且DefaultFocusedControl为空；来源为本机同哈希程序集，不把接口存在当控制器呈现已验证。S只同步内容变化备份/hash。


## D128 商店保存边界与正式接入独立等待

D128只读保存源码与本仓库局内标记，84明确商店无完整库存序列化/房间恢复，SaveRun捕获异常、await不保证落盘。局内标记可复用不证取得/支付/库存原子一致；暂缓真实交易与强加商店保存。正常商店购买/退出/读档同局人工样本独立等待，不自动启动。下一按既定用途继续第欧根尼原始资料与问题设计，不将人工待样本阻塞整个ISSUE007。

阶段前HEADbc8338f，51笔未推；D128仅84/70设计判决与阶段记录，tmp/保留，无运行操作。源读六相关类型和仓库现有保存/状态文件，未执行保存/交易。README准确，游戏输入未改，无Release/PCK/部署；ISSUE010未变不重试。
相关存档字段/创建路径为源码证据，完整退出/读档未实测；现有活动问题保持处理中，不把未知接法登记为已确认原生缺陷。S备份变化文件并核对hash。


## D129 恢复第欧根尼用途与原始证言边界

D129恢复第欧根尼既定问号/可停留终点用途及克拉特斯中介；研究24区分拉尔修后世证言和本人著作，85先定真实依赖/操练/公开三个问题。否决丢物换抽牌格挡与贫穷计分、无受众直言、普通删牌直称自足，不先机制代码。11当前导航改为85；苏格拉底/普罗泰戈拉体验与柏拉图商店人工样本仍独立等待。下一比较两个既有载体的制作与完整取舍，未称人物完成。

阶段前HEAD90890f3，52笔未推；D129仅新研究24/人物85、11导航及阶段镜像记录，tmp/保留，无运行操作。18/19真实用途与当前图已对照，原始来源已读且未使用直接引文；无批准机制/美术/代码，README准确，无Release/PCK/部署；ISSUE010未变不重试。
拉尔修VI.26/37/69/70—71仅有限释义，后世轶事不作为确定历史规则；EDGE010转向不作直接师承，EDGE011可选并公开中介，停留不失败。S变化文件备份/hash。


## D130 拒绝自设限制与空事件包装人物

D130在85比较战斗用具依赖/标准问号需要修订的节点、操作、界面、素材、代价恢复与反例；两者均不批准：暂不用药水为自设限制且重复试行，事件尚无真实对象/替代/代价。停止做原型，不用低素材或奖励找补。00索引改当前85与柏拉图商店独立等待。下一收束一种有现成真实替代的需要并审帮助/依赖、简化/匮乏、展示/持续；人物仍未完成。

阶段前HEADde3c8aa，53笔未推；D130仅85两载体判决、00阅读索引及活动镜像/日志，tmp/保留，无运行操作。设计反例走查不冒称自动/游戏验收；README准确，无生产输入变化、无需Release/PCK/部署；ISSUE010未变不重复推送。
两载体均没有批准，不写发布能力、注册ID或生成美术；原始资料无需重复浏览摘抄。S仅同步变化文件备份/hash。


## D131 有限备用动作的材料草案与继续审查

D131在85收束有限需要：缺进攻手段时稳定基本动作“简式/简击”，先列既定Question/正常战斗卡界面与两项静态素材预算，再写接受/实际选择/成本/修订/跨战/停留循环草案。未罚用药水或贫穷计分；仍可能只是每回合额外已知牌，公开实践尚缺，不批准实现。下一好/坏手牌同条件实际代价与用途审查，不改数值找补。

阶段前HEADbb915c9，54笔未推；D131仅85有限草案及活动镜像/日志，tmp/保留，无运行操作。命名仅候选未注册/生成素材，未做程序或体验验证；README准确，无游戏输入改动、无需Release/PCK/部署；ISSUE010未变不重推。
1能量/6伤害为纸面讨论参数，循环草案不代表人物设计审查通过；素材目标、原生暂存/变形/次数安全均未实证。S仅变化文件备份/hash。


## D132 简式同条件反例与停止主要能力接法

D132在85用好/坏手牌及0能量四种虚构单回合材料复查简式：有真实备用和生存取舍，但好手牌亦额外增益且可作弃牌/计数资源，仍是已知攻击牌收益，公开自足/需要修订缺对象。停止该主能力接法，不改数值找补、不做原型。下一只读核实放下附带代价的真实凭借、失去收益/恢复原有能力的对象与安全界面。

阶段前HEADe5295b5，55笔未推；D132仅85同条件反例与活动镜像/日志，tmp/保留，无运行操作。四材料为受控手算，不声称纯逻辑/浏览器/完整游戏验证；README准确，无游戏输入改动，无Release/PCK/部署；ISSUE010未变不重推。
简式草案留历史归属，不注册/制作；基础Strike/Defend事实沿现有已核实材料，敌值明确虚构，胜负/最优不推断。S变化文件备份/hash。


## D133 放下凭借载体审查

D133只读核实RelicCmd、当前监听者、三件远古遗物与选择界面；异质/添水/天鹅绒项圈被放下后失去1点能量并恢复未来金币/药水/第7张牌权限，过去损失不返还。86记录现有事件选项比“选择遗物”原生屏语义更合适。完整源码统计102个远古类型，三件对象不保证存在；扩大任意遗物会变纯损失或不可逆风险，因此否决其主能力/采用条件，仅保留未来有条件表达机会。下一回到犬儒自足、操练、直言的关系研究，不继续换物件凑机制。

阶段前HEAD4bc55a8，56笔未推；D133仅设计与只读源码，反编译输出忽略，不改生产输入。README仍准确；无Release、PCK、部署或游戏验收。
## D134 犬儒自足、操练与直言关系收束

D134补读IEP犬儒条目和SEP古代伦理犬儒段，并与既有拉尔修证言分层：操练是反复形成能力的过程，自足是较少受外在供给/许可牵制的生活条件，直言是在具体接收者和风险中表达；三者相互支持但不是可冒充历史定论的固定三阶段。贫穷、苦难次数和挑衅台词均不能作为同一计量条。

24/85列出五种既有载体的边界：单一Question没有持续实践，普通战斗没有理解陈述的受众，奖励/休息点多为私人优化，商店会重复柏拉图且保存成本未解，第三幕后继不能补写不存在的前段事实。允许的低成本骨架仅为第二幕既定Question邀请、正常局面重复行动、第三幕既定Question/Switch或停留表达同一对象与事实；中心训练材料仍缺，不批准能力、命名、美术或原型。

阶段前HEAD35858f1，57笔待推；D134仅研究、人物设计、导航和阶段记录，tmp/保留，无运行操作或生产输入变化。README生产表述仍准确；纯研究不执行Release、PCK、部署或游戏验收，ISSUE010无新恢复证据不重推。
验证：IEP伦理/自由与直言/操练段及SEP犬儒段交叉核对，明确晚期有争议证言和游戏假说边界；同果异据、失效恢复、受众风险与五载体制作反例人工审查。阶段末完成文档差异、G→S内容哈希和Git检查。

## D135 放下当前主说机械赐福候选

D135在87形成完整但未批准的循环：第二幕既定Question显示当前真实主说效果，玩家可保留离开或暂停机械赐福；采用后用原有牌组与正常资源进入普通战斗，安全局外时点可取回并结束实践；第三幕可停留，或公开犬儒传统/克拉特斯中介后转向季蒂昂芝诺。不给攻击、格挡、抽牌、能量或资源补偿，不禁药水遗物，不把取回写成失败。

节点、操作、界面、静态素材上限、表达边界、保存恢复和六组同果异据已先写。候选对象在既定路径内确定存在，避免随机库存；但仍有两项未过：旧主说的持续/永久效果未证完整暂停恢复，循环也可能只剩被动困难模式。下一只用强构筑、弱构筑和不可逆旧效果做同规则纸面反例；若强者无感、弱者退出就停止，不用数值奖励救活。

阶段前HEAD74f86d2，58笔待推；本阶段仅87/85/11、ISSUE011和阶段记录，tmp/保留，无生产代码/资源改动。D134提交后Git自动几何维护失败已隔离派生文件、fsck通过并以仓库本地maintenance.geometric-repack.auto=0停止自动重试；手动维护另列低优先ISSUE011。README生产内容仍准确；纯设计不执行Release、PCK、部署或游戏验收，ISSUE010无恢复证据不推送。
验证：既定18路径、19第59—62/合法终点/第欧根尼主位置复核；D134五项硬条件、单主说、恢复/无机会/立即反悔、强弱构筑和不可逆效果反例人工走查；文档差异及G→S哈希待阶段末核对。

## D136 停止以放下主说承担主要循环

D136用同规则复查三类上游：现有城邦印章若本战无能力牌/无触发机会，暂停不代表放下真实帮助；确实依赖下一回合能量/格挡时，局外取回迫使玩家在见到抽牌和敌情前选择困难模式；当前柏拉图无名环只留下药水、金币、付款和历史，没有持续赐福可暂停；永久改牌/属性同样不可逆。

“触发时再问”虽能让帮助实际出现，却要求第欧根尼当前主说下继续运行旧规则的影子计算，逐效果增加战斗模态、焦点、同步和即时恢复，且仍不适用无名环。因此停止87作为主要能力、采用条件或芝诺资格，仅保留未来具体主说天然有安全拒绝时的一次条件性表达。下一从既有事件关系、接收者、实际言行与后继后果找材料，不再丢物、关增益或牺牲资源。

阶段前HEADcf79e84，59笔待推；D136仅87/85/11与阶段记录，tmp/保留，无生产输入改动或运行命令。README生产内容仍准确；纯设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制与fsck状态未变，ISSUE010无恢复证据不推送。
验证：WesternPracticeRelic战斗起止清理、唯一主说门槛、待发奖励消费逻辑与PlatoCivicSeal继承关系只读核对；70当前无名环的药水/金币/付款不回滚边界复核；四处境和触发弹窗制作/单主说反例人工走查。阶段末完成文档差异与G→S哈希检查。

## D137 锁定第欧根尼公开关系层

D137补研究24的公开标签/羞耻边界：犬儒名称来源存在竞争解释，后世传统对可能的侮辱称呼作主动挪用，但不能把辱名直接授给玩家或把粗野/冒犯计作德性。88比较五种关系载体，否决战斗敌人听众、新街市/群众/权势者场景和复用柏拉图商人核心。

保留最低成本关系外壳：第二幕既定Question由第欧根尼接收玩家明确说法；第三幕可合法停留，或以说明卡公开犬儒传统/克拉特斯中介后向芝诺陈述事实并转向。正式成本为标准事件页及第欧根尼/芝诺两张静态图，克拉特斯不作为无素材说话角色。该层只保证具体接收者、路线后果和历史边界，仍无操练动作、自足替代或完整parrhēsia，不批准实现/对白原型。

阶段前HEAD2aa3a83，60笔待推；D137仅24/88/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README生产内容仍准确；纯研究设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制/fsck规则不变，ISSUE010无恢复证据不推送。
验证：IEP名称23—25、伦理34—49与既有拉尔修资料分层释义；18路径、19第59—62/合法终点/克拉特斯中介复核；五载体的节点、接收者、界面、静态素材和关系后果反例审查。阶段末完成文档差异与G→S哈希核对。

## D138 确定第欧根尼实践型问号角色

D138在89比较直接Adopt战斗赐福、Adopt无效果困难状态与Question/Keep三种结构，批准第三种为设计方向：第二幕第欧根尼实践问号保存公开说法/知识流但不替换当前机械主说；第三幕Stay结束本局哲学发展并保留此前唯一主说，或以说明卡公开犬儒传统/克拉特斯中介后Switch芝诺，由芝诺正常替换。

该结构复用Question/Keep概念，区分路线最后问题与当前机械主说，取消“每个人物必须发战斗buff”的错误前提。正式界面为两个标准事件节点，第欧根尼/芝诺两张静态图，克拉特斯不作为无素材说话角色；无群众、战斗浮层或数值奖励。两次公开行动的共同具体事实仍缺，角色架构通过不等于自足、操练、直言或人物内容完成。

阶段前HEAD6d4d6f0，61笔待推；D138仅89/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README生产内容仍准确；纯设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制/fsck规则不变，ISSUE010无恢复证据不推送。
验证：18人物用途/样例路径与19第61—62、合法终点、第欧根尼位置再次对照；三角色结构的单一主说、Stay/Switch、无材料、沉默/修订/知识不可撤回和动机反例人工走查；文档差异与G→S哈希待阶段末核对。

## D139 第欧根尼公开说明双前史核心候选

D139在90把D138角色架构填入具体内容：苏格拉底德性前史使用稳定机会、继续/撤离与目的/标准分离记录；柏拉图前史使用购买/匿名取用/离开、价格/实付/取得与私下理由分离记录。第二幕玩家向第欧根尼公开事实与理由、只公开事实或沉默；第三幕按接收者知识保持、补充、修订或撤回理由，再独立Stay/Switch。

事实公开不可撤回，理由可变；未公开私有材料不能进入NPC台词，商人不会自动知道披露。所有公开状态均可停留或转芝诺，不用“谦虚台词”发门票。标准事件页与第欧根尼/芝诺两张静态图，无新场景、战斗buff或资源交换。该候选首次形成双前史完整循环，可继续四路径纸面审查，但公开差异可能只剩文本长度，且两前史生产保存未统一，未批准对白原型或实现。

阶段前HEAD75f93cc，62笔待推；D139仅90/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README生产内容仍准确；纯设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制/fsck规则不变，ISSUE010无恢复证据不推送。
验证：苏格拉底58/61/62/65知识边界与柏拉图79/83/84交易/匿名边界对照；七字段共享状态、三公开范围、无材料、接收者知识不可回退、理由修订和Stay/Switch独立性反例走查。阶段末完成文档差异与G→S哈希核对。

## D140 公开说明四路径与八结尾审查

D140在90走查苏格拉底公开目的后修订、沉默后首次开口、柏拉图只公开事实后补理由、公开理由后撤回四条路径，各自分别Stay/Switch。四类后继问题分别落在有限理由、公开时点、理由何时约束行动、失去辩护后是否急于替换；不是只增加文本长度。八结尾均不按公开范围发资格/数值，事实快照一致，接收者知识和当前理由分开。

公开说明因此保留为完整叙事决策核心候选，不降辅助对白；仍无正式短文案/交互纸面/玩家体验，两前史生产保存和静态人物素材未完成。下一审标准事件信息架构、鼠标/手柄焦点、文本长度、安全显示及第欧根尼/芝诺两张静态图尺寸/状态/来源条件，不先代码或生成美术。

阶段前HEADe0c8062，63笔待推；D140仅90/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README生产内容准确；纯设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制/fsck规则不变，ISSUE010无恢复证据不推送。
验证：四条前史/披露/修订知识流逐项走查，八个Stay/Switch结尾的事实一致、问题差异、无门票与动机不可推断检查；阶段末完成文档差异与G→S哈希。

## D141 第欧根尼公开说明事件信息架构

D141只读核对现有标准事件实现、本地化长度和`philosophers_gaze.png`：现有图为1600×900，项目只证明一个事件模型的一张静态背景，没有证明页内换图、默认焦点或取消回调。91据此把D140四路径压为第二幕第欧根尼、第三幕回看/芝诺两个标准事件节点；不可逆披露和路线替换各自有确认页，首项安全返回，披露提交后不能撤销接收者知识。

正式候选不用自由文本，理由只取预写类别；正文45—60字目标/82字纸面上限，标题4—8/10字，选项说明12—32/44字，单页最多4项。素材固定为第欧根尼、芝诺各一张1600×900静态PNG，无动画/群众/克拉特斯肖像；来源须原创、明确委托授权或许可兼容，不默认AI生成可用。信息架构通过继续纸面设计，代码、美术、生产保存、手柄实际焦点和游戏体验均未批准或验证。

阶段前HEAD05761ee、64笔待推；D141仅91/85/11、两份仓库设计镜像和阶段记录，`tmp/`保留。启动检查发现Steam已由用户环境运行，本阶段不构建、不部署且未启动/停止任何程序；README生产表述仍准确。ISSUE010无新恢复证据不重推，ISSUE011自动维护抑制不变。下一编写四路径共用短文案矩阵、知识前后和提交边界，再做鼠标/手柄纸面线框与恢复表。

## D142 第欧根尼公开说明短文案矩阵

D142在92为四条代表路径写出A—F页面正文、选项、确认文本、接收者知识和状态提交边界。正文36—51字、标题4—9字、说明12—19字，均低于D141纸面上限；选项说明目标依据实际清晰短句修正为12—32字。路径一原先返回/保持/限缩/修订/撤回五项，现把限缩并入预写修订，压到四项，不靠隐藏滚动或缩字解决。

文案只使用枚举事实、预写理由及原生本地化对象/整数，任意文本不得进入NPC回应。披露、第三幕说法和Stay/Switch分别确认，查看/返回不提交，私有理由、商人状态和旧事实不可被越权改写。全部台词标为游戏创作，不冒充历史原话。该阶段未改本地化/代码/资源，未验证焦点、取消键、读档、第二事件接续或玩家体验。

阶段前HEAD ec9fbca、65笔待推；D142仅92/91/85/11、三份仓库设计文档和阶段记录，`tmp/`保留。Steam仍是用户环境既有进程，不构建、不部署、不启动/停止程序；README仍准确。ISSUE010无新恢复证据不重推，ISSUE011状态不变。下一画A—F鼠标/手柄焦点线框和恢复表，逐个模拟取消、返回、退出读档、重复确认及第二事件接续。

## D143 第欧根尼公开说明交互与恢复线框

D143在93为A—F页固定鼠标/手柄初始焦点、显式返回、取消降级、持久边界和重入页。所有不可逆确认页第一项为返回；取消无法安全拦截时无动作。11个模拟覆盖第二幕三披露、第三幕四处理、Stay/Switch、重复确认、退出读档和替换失败；纸面上知识不倒退、旧说不删除、重复回调不重复写入。

制作口径从含混的“两个事件节点”修正为两个计划出现时点、至少两个标准事件实例；第三幕不能页内换图而仍需芝诺图时，最多第三个连续实例但不新增地图节点。动态换图、默认焦点/取消回调、连续事件串接均未证。下一只读核实游戏程序集与现有项目API，列已证/未知/最小探针，不先接生产。

阶段前HEAD957d05d、66笔待推；D143仅93/92/91/85/11、四份仓库设计文档和阶段记录，`tmp/`保留。Steam为既有进程，纯设计不构建、不部署、不启动/停止程序；README仍准确。ISSUE010无新恢复证据不重推，ISSUE011状态不变。

## D144 第欧根尼标准事件API判决

D144使用临时未跟踪反射/IL探针只读检查本机`sts2.dll`，未启动游戏。`NEventLayout.DefaultFocusedControl`返回当前第一项，`NEventRoom.SetOptions`重建按钮后抓取该焦点，因此确认页首项安全返回可直接复用原生行为。事件类型没有可复用取消回调，按钮输入只处理`select`；取消键继续无动作，不能承担任何哲学选择。

`NEventRoom.SetPortrait(Texture2D)`公开并立即更新带人物槽的标准事件布局；当前方案收束为第二幕、第三幕各一个事件实例，第三幕Fz页内切芝诺图，返回F切回第欧根尼图。换图本身不进入存档，重入必须由已提交状态重建页面并重设人物图。`EnterRoomWithoutExitingCurrentRoom`确实能压入新房间，但普通Proceed只打开地图；由于页内换图已成立，制作不再引入第三个嵌套事件。

`EventRoom.ToSerializable`只写事件ID和预完成标记，普通`SetEventState`不保存页面，普通事件状态变化也不自动保存。披露、说法、Stay和Switch必须使用自有局内状态、应用层幂等检查和显式`SaveRun`。Switch下一步先设计`未决 → 切换准备 → 已提交`契约：准备态保存原主说与目标，替换后核验并再次保存；读档遇到准备态按真实主说完成或回退。D144只批准两事件/页内换图/显式返回/自有恢复骨架，不等于生产实现或游戏验收。

## D145 第欧根尼持久状态与切换恢复

D145只在既有交互文档内定义未来状态，没有新增想法页或代码。状态分为前史来源与不可变事实快照、预写私有理由、第二幕披露、第三幕说法前后、路线选择及Switch阶段；接收者知识由披露和说法推导，不另存可冲突副本。上游字段未通过生产保存时只能降级`NoMaterial`，不能从当前资源反推旧经历。

披露、第三幕说法和Stay都在内存保留旧值，写终态并显式保存，失败恢复旧值且不进入结果页。Switch先保存Prepared及原/目标主说，再执行替换、核验唯一目标，最后写Committed并第二次保存。12种读档组合明确：Prepared+原主说回退到路线未决，Prepared+唯一芝诺补提交，混合/未知或Committed不一致进入安全阻塞；重复回调只返回既有结果。

该阶段只证明状态边界和恢复权限不倒退，尚无苏格拉底德性、柏拉图交易的生产适配器，也没有芝诺主说命名/载体。下一继续定义两个上游最低输入、缺项降级及芝诺替换前置，不写第欧根尼状态代码。

## D146 第欧根尼上游适配与芝诺替换前置

D146仍只改既有设计文档。上游适配器只接受已提交并通过来源自身校验的不可变记录：苏格拉底最低需要德性分支、真实撤离机会、已提交继续/撤离和实际代价；柏拉图最低需要合法正价报价、已提交购买/匿名取用/离开和完成的资源事务。目的或理由可为空，核心行动或事务证明缺失时只能返回`NoMaterial`，不能从当前生命、金币、库存或路线节点反推历史。

同局零个来源走无材料路径，一个直接展示，两个先在A0标准事件页选择本次谈论材料。A0复用同一张第欧根尼图和原生选项，不增加地图节点、素材或奖励；选择只锁定事实快照，尚未向第欧根尼披露。

芝诺沿既有`ZENO_OF_CITIUM__VIRTUE_AND_HAPPINESS__CORE`和`WESTERN_EDGE_011`继续，保留克拉特斯中介，不把路线图ID当主说`ModelId`。Stay要拒绝第三幕Fixed后保留终点；Switch要先保存Prepared，再应用该边、替换唯一机械主说并核验，最后保存Committed。图前进而载体未换、载体已换而图未前进都进入安全阻塞。

第欧根尼现在是依赖明确的完整纸面候选，但三个上游生产依赖均未完成，因此不写状态或事件代码。下一先在85当前人物页写芝诺的目标问题与制作边界，形成独立完整决策循环后再建立唯一的芝诺当前入口；不先注册名称、`ModelId`或战斗数值。

## D147 芝诺问题与制作边界

D147没有新增想法页，先在85当前入口暂存芝诺草案。德性与幸福线的问题限定为：玩家怎样区分眼前印象、自己同意的实践判断、随后行动和后来结果，并在事实变化时有理由地修订。它采用芝诺“生活一致”和早期斯多亚同意—冲动—行动关系，不把更晚的爱比克泰德控制二分冒充芝诺原话，也不按胜负认证判断正确。

正式入口复用第三幕第欧根尼Switch及`WESTERN_EDGE_011`，后续机械只占合格普通战斗。呈现上限为已经预算的芝诺1600×900静态图、现有主说状态入口和机制成立后的一枚图标；无新房间、背景、NPC、敌人或动画。战斗选牌、锁定目标、延迟结算与取消恢复尚未核API。

首轮比较中，首行动标记缺少真实代价，容易成为每回合多点一次；战后回看没有战斗中的权限变化，两者均未批准。延迟行动槽可能产生承诺、结果分离和修订，但也可能只是减费、复制强牌或高级保留，暂只进入三个具体回合的反例检查。现有`SSA→6格挡`继续视为待替换通用基础设施，本阶段不顺手修改生产规则。

D148分别检查原目标被移除、高压防守动作延迟、强牌槽位套利，写正常打法、采用后的收益代价、撤回条件和手柄步骤。载体若只能靠数值优惠成立就停止，不注册命名或`ModelId`。

## D148 芝诺同回合载体停止判决

D148以权限和时点复查三个具体回合。原目标被其他效果移除时，首行动标记可零成本改选则没有承诺，禁止改选则惩罚合理重估；已付的同回合延迟行动若作废、自动换目标或退款，分别破坏公平、行动身份或真实成本。高压防守中，回合末防御与最后正常出牌没有结构差异；强牌场景中，不补偿则延迟严格更差，减费或复制则退化为寻找最高价值牌。

首行动标记和同回合延迟槽因此停止，战后回看只保留为可选只读说明。四步以上的额外手柄操作也没有换来新的决定。现有`SSA→6格挡`仍不在纸面阶段顺手删除。

反例指出判断与修订必须跨过真实的新信息边界。下一候选改成跨回合预付：本回合从手中选择合格牌、支付完整费用并绑定必要目标，下一回合在新意图、抽牌和自动效果稳定后执行或修订；不退款、不减费、不复制。它可能提供跨回合安排，也可能只是储能和保留的复合强工具，尚未批准。

D149先定义牌种、目标、费用、执行与修订、消耗/虚无、战斗结束和读档清理，再以强弱构筑各两个回合检查能量银行与永不使用反例；不先做API或代码。

## D149 芝诺跨回合预付停止判决

D149为比较固定一次/战的最小合同：完整支付一张合法非X费牌，保留实体和目标并占手牌容量；下一回合新信息稳定后按原目标免费执行，或不退款地把牌恢复正常费用。异常结束必须把实体送回唯一合法牌区。

强构筑会把本回合剩余能量搬到下一回合，并指定保留高价值牌；Boss、无目标牌和长期存活目标可稳定规避修订风险。弱构筑在高压回合没有预付余量，关键防御也不能延后。目标失效时，退款会取消承诺成本，不退款则让合理修订承担双重费用，玩家会主动避开需要修订的牌。

该载体还需每次六到八步手柄操作和真实卡跨回合实体恢复，高制作成本最终只得到储能与保留的复合工具。跨回合预付及预付能量、指定保留、免费执行、目标后返还等近邻方案停止，不用数值微调复活。

D150离开手牌操作，只比较“延后但绝不免除的一次外部结果”和“跨房间行动标准”。前者若成为免伤或魔法时间，后者若只剩任务清单或问卷，直接停止；先做纸面正式呈现与滥用检查，不做API或代码。

## 当前笔记分层纠偏

用户指出项目早期将资料、任务、设计想法和过程记录严格分开。前一阶段为了控制想法文件数量，把芝诺D147至D149写进第欧根尼当前页，文件数量减少但人物与文档职责混在一起。

当前职责边界已经迁入仓库：第欧根尼唯一入口为 [`docs/design/current/west/05第欧根尼当前设计.md`](docs/design/current/west/05第欧根尼当前设计.md)，芝诺唯一入口为 [`docs/design/current/west/06芝诺当前设计.md`](docs/design/current/west/06芝诺当前设计.md)。`DiogenesVirtueDesign.md`、`ZenoVirtueDesign.md`及 Drive 旧人物页继续保留详细证据；任务状态和准确下一步只看 Linear，研究原稿与开发日志不能恢复授权。

本阶段只调整文档归属和导航，不改变D146至D149的设计判决，也不修改代码、资源、数值或公开ID。

随后按中国思想路线的目录方式完成拓扑整理：`02想法与机制`根目录只保留八份公共设计和中西两条目录入口；中国路线基线09归入`06中国思想流派`；西哲八份当前入口归入`10西方思想流派`顶层，31份支撑材料进入`80设计附录`，45份阶段文件和一份旧索引进入`90历史推演`。旧文件内容保留，只修复移动后的明确路径链接。

## 芝诺事件实例建立边界

`ZenoAssentBoundaryFactory`只在同一每局运行时仍持有严格有效、无待处理操作的`OpeningClaimed`时构造事件。构造计划沿用路线中冻结的事件实例身份；实例缓存保证同一工厂的重复回调返回同一可变模型，竞争身份不能覆盖或并列创建。

工厂把事件绑定到原运行时、原`ZenoRouteValidationCatalog`及`ZenoAssentBoundaryStateHost`。该边界只准备模型，不调用`PrepareEventEstablished`、不进入事件房间，也不执行地图、Boss、离幕、关闭或目的地恢复效果。项目显式排除`tmp/**/*.cs`，避免本地API探针生成的源码被SDK默认编译进生产程序集；探针内容本身不归本阶段修改。

## 芝诺事件建立持久事务

`ZenoAssentBoundaryEstablishment`把已缓存实例与既有`PrepareEventEstablished`、每局`ZenoRoutePersistenceRuntime`串成单一门。只有准备与提交检查点都由适配器确认，且最终状态是相同事件身份的稳定`EventActive`、没有写前操作或冻结请求时，结果才携带可进入房间的实例。

准备明确失败会释放回`OpeningClaimed`并保留缓存对象供显式重试；准备或提交结果未知只返回`Frozen`，不暴露对象且重复调用不自动重试。完成后的重复回调返回同一对象而不再保存。该层仍不调用游戏房间API，也不执行地图、Boss、离幕、关闭或恢复目的地效果。

## 芝诺标准事件房间进入

`ZenoAssentBoundaryRoomEntry`在同一每局持久运行时锁内重新检查稳定`EventActive`、事件实例身份、无写前操作、无冻结保存请求和真实房间栈。只有基础房间仍是已冻结目的地、当前没有其他模态房间时，才允许调用一次原生`EnterRoomWithoutExitingCurrentRoom`；当前已经是同身份芝诺事件时直接返回既有成功，不重复压栈。

游戏的`EventSynchronizer.BeginEvent`会从传入模型克隆本地事件，因此`ZenoAssentBoundaryRoomEntryAdapter`通过`EventRoom.OnStart`把克隆模型重新绑定到同一事件实例身份、每局运行时、校验目录和页面状态主机。错误身份、非`EventActive`、其他事件、异常房间层数、错误目的地、入口抛错或进入后现场不匹配均不改路线事实；关闭事件、恢复目的地及地图、Boss、离幕触发仍留给后续阶段。

## 芝诺保证事件纯调度

`ZenoRouteSchedulingPolicy`把同一过渡现场中的局终、离幕、Boss和普通安全信号压成一个确定计划，优先级固定为局终、离幕、Boss、普通安全点。普通路径只有拿到后续非路线房间回执后才会从`WaitingInterval`进入`ReadyToClaim`并领取；只有地图目的地而没有回执时保持等待，避免Switch退出后立即连播。Boss和离幕兜底可直接从等待态领取，不伪造普通房间回执。

同一局的事件实例身份只从局、路线边和终点身份确定性派生，所有入口共享；一旦`OpeningClaimed`或更后阶段存在，后来回调只读取原赢家，不再保存或改写触发来源。`ZenoRouteSchedulingRuntime`串行执行计划，并复用既有写前状态服务与持久运行时；普通路径依次确认间隔完成和开场领取，局终则持久进入`RunTerminated`且不创建事件身份。原生房间、Boss和离幕回调仍由后续阶段接入。

## 芝诺原生安全触发

`ZenoRouteNativeTriggerPolicy`只为第三幕单层房间栈生成原生过渡凭据。地图安全点以当前基础房间ID冻结地图目的地；只有房间已预完成且不是第欧根尼／芝诺路线事件时才生成后续房间回执，所以Switch退出后第一次返图只恢复旅行，不会立即播放芝诺。无关的普通事件可以在完成后满足间隔，芝诺事件本身和异常模态栈不会触发。

`ZenoRouteMapSafeBoundaryPatch`在原生地图打开后暂时关闭旅行输入，等待调度、事件建立和标准房间入口完成；无需处理时恢复旅行，保存或入口冻结时保守保持禁用供读档恢复。`ZenoRouteBossGatePatch`在`EnterMapCoord`调用原生`AddVisitedMapCoord`之前识别Boss坐标，`ZenoRouteActExitGatePatch`在`EnterNextAct`改变幕之前拦截；两者只有在没有芝诺工作时才以旁路标记续调原方法，避免递归和提前消费目的地。

`ZenoRouteNativeTriggerSession`按`RunState`复用同一持久运行时、校验目录、调度器、事件工厂、冻结目的地读取器与房间入口。它只对已存在且可由当前生产校验目录验证的芝诺路线生效，不创建预约、不进入随机事件池，也不替换地图节点或其他人物。事件关闭与原目的地续行尚未接入；这些触发成功后会停在芝诺事件内等待后继关闭阶段。

## 芝诺事件关闭与目的地续行

芝诺结果页的`LEAVE`现在走显式关闭事务，不再使用原生无条件继续按钮。关闭准备先作为`EventClosed`写前记录单独持久确认；只有该记录确认后才允许退出同一芝诺房间并恢复冻结目的地，外部动作成功后再提交`ZenoClosed`并持久确认。恢复自磁盘的关闭准备被视为已经确认的外部动作检查点，不会由普通事务重试提前提交。

地图路径只退出嵌套事件并在最终关闭确认后恢复旅行输入；Boss路径从冻结ID还原原坐标并调用原生`EnterMapCoord`；离幕路径只在第三幕调用原生`EnterNextAct`。错误事件身份、错误坐标、异常幕、准备保存未知或最终保存未知都会保持冻结，不改去另一个目的地。生产读档恢复入口尚未挂接；当前关闭可在同一运行会话内完成，下一阶段需把已保存的`OpeningClaimed`、`EventActive`、`OutcomeCommitted`和`EventClosed`接回同一运行时与事件实例。

## 芝诺生产读档恢复入口

芝诺原生触发会先续完已保存的普通写前事务，再继续同一调度赢家；稳定`EventActive`在运行时缓存丢失后可以按冻结身份重新收养事件模型，不生成新身份。载入芝诺事件房间时，`EventRoom.EnterInternal`前置恢复会先重试已保存的事件建立或结局事务，再把原生克隆重新绑定到同一运行时、校验目录、事件身份和关闭回调，因此Z0或唯一Zr从持久状态恢复。

地图打开时若发现已保存的`EventClosed`准备记录，会保持旅行禁用并续完同一目的地与最终提交；普通事务重试不会越过关闭外部动作。恢复失败只记录阻断并保持原现场。生产恢复入口不处理旧第欧根尼页面，也不生成Switch材料；该上游生产入口仍需单独实现。

## 芝诺开发期人工测试入口

开发控制台命令`zenoassenttest`用于在正式第欧根尼第三幕入口完成前验证芝诺事件。它只允许单人局、第三幕、战斗外、已完成且房间栈安全的地图边界；当前局已有芝诺状态或保留的存档标记时会拒绝执行，不会重置或覆盖既有路线。

命令建立明确标记为`DEVELOPMENT_TEST_ENTRY`的无材料记录，先经过真实的路线保存事务和间隔完成回执，再进入与正式链相同的芝诺标准事件。它不加入随机事件池、不伪造苏格拉底或柏拉图上游事实，也不替代正式入口。可用它检查无材料页、翻页与返回、确认、结果页、关闭以及各保存边界的退出重载恢复。

`zeno_assent_boundary.png`现为独立的1600×900芝诺事件静态图：单人、悬笔、无文字，以未闭合且不发光的边界表现“尚未替玩家作出判断”。资产由项目内置图像生成流程原创生成并经人工检查尺寸、构图与界面可读性，不再复用`philosophers_gaze.png`占位图；后续试玩若发现裁切或风格问题，按独立反馈处理。

## 第欧根尼第三幕去留事件模型

`DiogenesStaySwitch`只承载第三幕已经定稿的去留段：回看当前说法、Stay确认、犬儒传统与克拉特斯的中介说明、Switch确认和本地结果页。它不实现第二幕披露或第三幕说法修订，也不直接打开`ZenoAssentBoundary`；Switch成功只把既有路线事务推进到`WaitingInterval`，后续仍由芝诺安全调度链领取。

页面模型把只读回看或返回放在每个不可逆页面的第一项，复用原生重建选项后的默认首项焦点；鼠标与控制器确认都进入同一个选项回调。原生没有可安全复用的取消回调，因此取消保持无动作。只有最终确认会由`DiogenesStaySwitchStateHost`调用既有`PrepareStay`或`PrepareSwitch`并经过同一双检查点持久事务；失败、未知结果、冻结和竞争回调都不能进入成功页，重复同一确认只读取已经提交的赢家。

当前阶段只完成事件模型、双语短文案和纯逻辑／持久事务检查，尚未建立生产事件房间、正式第欧根尼素材、上游披露与说法生产链，也未部署覆盖芝诺待验收包，所以玩家现在还不能从正常流程进入该事件。

## ENC66 存档加载故障

2026年9月26日的真实失败日志证明Continue已经进入`RunState.FromSerializable`，随后在`Player.LoadInventory → RelicModel.FromSerializable → SavedProperties.Fill`抛出`Property set method not found`。失败存档中唯一带自定义保存属性的遗物是`SOCRATES_QUESTION_CUP`；其`PracticePayload`定义在`WesternPracticeRelic`基类且原为私有setter。对实际编译程序集的只读反射确认：从具体`SocratesQuestionCup`查询继承属性时setter为空，而从声明基类查询时私有setter存在。现将该setter公开，使原版从具体遗物类型恢复属性时可以找到它，并增加源码契约检查防止回退。

同一日志中的`Unknown ModelId ... during serialization, writing NONE`来自把共享状态标记放进`EventsSeen`，它是需要后续迁移的网络／包序列化风险，但不是本次Continue异常堆栈；磁盘JSON仍保留完整标记，`RunState.FromSerializable`前缀也会在原版事件历史恢复前移除它。本次不改写用户存档或以删除marker绕过故障。

早期读档使用空芝诺校验目录会隔离合法开发测试路线。固定、代码自有的`DEVELOPMENT_TEST_ENTRY`现登记到读档期内建目录，使开发版NoMaterial路线在原生触发恢复之前就能严格校验；动态正式材料仍必须等待其生产者提供权威目录，不能因本修复放宽为接受任意ID。自动逻辑、Release构建和PCK装载通过不等于真实SL；验收仍须在启用Mod后完成新局保存、回主菜单加载并连续再SL至少两次。

## ENC68 RitsuLib局内保存探针

项目现以RitsuLib 0.6.2作为唯一主框架，并在清单中声明运行时依赖；BaseLib、PckPacker和RitsuLib Analyzer不属于本原型阶段。`PhilosophyRunState`、芝诺状态转换、事务、幂等和恢复规则均未迁移，现有`EventsSeen` marker与Harmony存档补丁也暂时保留，避免在基础链路得到游戏内证据前扩大改动面。

独立槽`ENC68_RUN_PERSISTENCE_PROBE`使用`GetRunSavedDataStore`保存固定token、schema和递增写入次数。开发控制台命令`ritsurunprobe write`写入或递增槽，`ritsurunprobe read`只读取并严格检查三项数据。它不读取、写入或转换任何哲学路线状态。

人工验收必须使用新测试局并完成两轮：先执行`ritsurunprobe write`得到`writes=1`，正常保存并回主菜单，Load后执行`ritsurunprobe read`确认`writes=1`；再执行一次`write`得到`writes=2`，再次保存、回主菜单和Load，最后`read`确认`writes=2`。任何自动测试、构建、PCK校验或部署成功都不能代替这两轮真实SL。只有验收通过后，才允许另开迁移阶段替换`PhilosophyRunState`最底层载体。

2026年9月26日用户完成上述两轮游戏内验收并确认通过。ENC68因此证明RitsuLib的独立run-scoped槽可以在当前游戏与Mod组合中连续保存、返回主菜单和加载；该结论只覆盖最小探针，不把尚未迁移的`PhilosophyRunState`、芝诺恢复链或旧存档兼容性描述为已验收。后继迁移必须保留现有状态机与事务语义，只替换最底层保存载体，并单独验证旧marker读取与新sidecar写入的过渡策略。

## PhilosophyRunState迁移到RitsuLib

共享状态仍使用原有`PhilosophyRunStateCodec`，芝诺状态机、事务、幂等、调度和恢复规则不变。RitsuLib槽只保存Codec编码结果，并在导出时从绑定的实时共享状态重新编码，避免状态变更晚于框架快照。`RunManager.ToSave`前置只负责确保绑定已经建立；sidecar的导入、导出和落盘均由RitsuLib负责。

载入时RitsuLib合法槽优先；没有新槽时才采用旧marker状态并绑定到新槽，下一次正常保存会写sidecar并移除合法旧marker。损坏的新槽、未知版本和隔离中的旧marker均不会被覆盖。芝诺写后确认直接通过当前游戏`ISaveStore`读取正在使用的存档JSON，RitsuLib槽存在但结构无效时明确拒绝回退成旧marker成功。

自动检查已覆盖实时状态编码、既有Codec恢复、sidecar JSON定位、大小写兼容、缺槽、损坏JSON和不完整槽；Release编译零警告零错误。尚未完成游戏内迁移验收：需要分别验证新局两轮SL，以及含合法旧marker的备份局载入后保存、再次载入，确认状态保留且新存档已转入RitsuLib。探针暂时保留到迁移验收通过。

## 普罗泰戈拉共同教育事件

`ProtagorasSharedDeliberationPatch`只在单人第一幕、苏格拉底德性节点、至少完成一场战斗且Question尚未占用时，于返回地图后压入一个原生标准事件；它不加入随机事件池或新增地图节点。事件使用`PROTAGORAS_SHARED_DELIBERATION`，复用诸子观照背景的PCK纹理别名，不新增人物素材。

`ProtagorasSharedDeliberationState`把接受占位、原法示范、准确复述、拒绝代言、同伴请求、玩家修订、授权范围、实际暂停、停止未来委托、实践完成、边完成和关闭分别保存。按钮动作采用待提交再提交的两次保存；读档只续同一待提交动作。`WesternRouteGraph.TryReserveQuestion`在接受时占位，`TryCompleteReservedQuestion`只允许有效最终回应补既有`WESTERN_EDGE_008`，因此中途退出不会伪装完成，重复回调也不会重复补边。

相关纯逻辑检查覆盖首屏拒绝、接受后未完成、七条完成路径、限缩授权的保存恢复、触发资格与双语键完整性。2026年10月14日全量逻辑、Release零警告、PCK加载和部署三文件哈希通过；没有自动启动游戏，也没有把实际试玩当成交付闸门。

## 柏拉图两项职责事件

`PlatoWholeArrangementPatch`只在单人第二幕、苏格拉底德性节点、已完成`WESTERN_EDGE_008`、至少完成一场战斗且第二幕 Fixed 尚未处理时，于返回地图后压入一个原生标准事件。事件使用`PLATO_WHOLE_ARRANGEMENT`，复用诸子观照背景的PCK纹理别名；不加入随机事件池，不新增商店事务、地图节点、角色、场景或动画。

`PlatoWholeArrangementState`把邀请、Fixed处理、独立项选择、依赖项承诺、同伴拒绝共同署名、同伴已选择、看见回应后交还、履行、撤回、实践完成、边提交和关闭分别保存。按钮继续采用待提交再提交的两次保存；读档只续同一待提交动作。首屏拒绝或接受后停止会用`TryDecline`消耗第二幕 Fixed 而不补边；保留各自成果、看见回应后交还、履行依赖承诺或同伴选择后撤回，都会且只会提交一次既有`WESTERN_EDGE_009`。

`WESTERN_EDGE_009`把路线节点推进到柏拉图，但事件不调用`RecordCurrentDoctrine`：当前没有新的柏拉图战斗遗物，伪造机械教义会让保存元数据和实际装备分裂。苏格拉底审思砝码继续作为继承工具，路线节点与战斗工具分别表达。相关检查覆盖两种未完成结果、四条完成路径、事实保存恢复、触发资格、双语键与“不得伪造机械教义”的源码契约。

2026年10月15日全量逻辑、Release零警告零错误、PCK内容与柏拉图图片别名加载、部署三文件SHA256均通过；游戏未运行，未自动启动Steam、游戏或Godot界面。部署即本次开发交付，后续试玩问题按独立反馈处理。

## 第一幕统一思想家入口

`PhilosophersGazeActOneCandidatePolicy`维护九位合法入口人物的唯一注册表，并预生成全部84种无序三人组合；运行种子只选择其中一个组合。候选仍在涅奥房间标记完成后、进入嵌套事件前写入`PhilosophyRunState`并保存，返回人物页、重复回调和Save/Load都复用同一对象。旧的东哲三选二和西哲六选三两套生成器已经合并，`NeowProceedPatch`只生成一次候选。

“诸子观照”首页按保存顺序直接显示三个人物与拒绝项。孔子、墨子、老子继续进入原有双观点页；六位西哲入口人物直接进入既有问题页，人物页的返回项回到同一三人首页，不再经过“西方来客”中间页。苏格拉底仍只占一个人物槽，再在人物页选择两个问题；获得遗物、当前主说、西哲旅程和第二幕以后逻辑均未改动。

逻辑检查覆盖84种组合无重复、九人等覆盖、损坏旧候选重建、候选保存往返、人物与问题授权，以及源码中不存在旧`WESTERN_ENTRY`页面入口。该改动只复用现有事件页面、本地化和素材，不新增ModelId、图片、动画或地图节点。

2026年10月15日全量逻辑、Release零警告零错误、PCK内容与纹理加载、游戏未运行时的部署三文件SHA256均通过。实际试玩作为独立反馈渠道，不阻塞本次交付或后续授权队列。

## 伊壁鸠鲁共同余地事件

`EpicurusSharedMarginPatch`只在单人第三幕、当前路线节点为柏拉图、已完成`WESTERN_EDGE_009`、仍持有苏格拉底审思砝码且至少完成一场战斗时，于返回地图后压入一个原生标准事件。事件使用`EPICURUS_SHARED_MARGIN`，复用诸子观照背景的PCK纹理别名；不加入随机事件池，不新增地图节点、角色、场景或动画。

`EpicurusSharedMarginState`要求玩家在同伴提出具体变化前先选彼此担保或各留余地。担保分支随后允许履行或拒绝同伴的延后请求；分开分支只披露同伴的变化，不提出请求，玩家仍可自愿调整或维持原约。状态保存邀请、承诺、请求或披露、四种结果与关闭；按钮采用待提交再提交的两次保存，读档只续同一待提交动作。首屏拒绝或接受后停止不会伪装实践完成。

该事件不改写`WesternJourney`、不调用`RecordCurrentDoctrine`、不发数值奖励，也不把伊壁鸠鲁描述成柏拉图路线的历史后继。它只在现有可玩路线中提供一次与柏拉图安排观相竞争的幸福实践，保留审思砝码作为原有战斗工具。相关检查覆盖四条完成路径、未完成退出、保存往返、触发资格、双语键，以及不得修改教义或发放奖励的源码契约。

2026年10月15日全量逻辑、Release零警告零错误、PCK内容与伊壁鸠鲁图片别名加载、游戏未运行时的部署三文件SHA256均通过。部署即本次开发交付；后续试玩中的UI、文案、平衡或思想表达问题按独立反馈处理。

## 塞克斯都两盏灯下事件

`SextusOpposedAppearancesPatch`只在单人第一幕、苏格拉底知识节点、仍持有诘问杯、至少完成一场战斗、本幕Question尚未使用且当前原生地图点有两个以上子出口时压入标准事件。建立实例前调用`TryReserveQuestion`占用本幕唯一问号并保存，避免拒绝、退出或读档后反复刷题；事件使用`SEXTUS_OPPOSED_APPEARANCES`并复用诸子观照背景的PCK纹理别名，不修改地图或增加节点。

`SextusOpposedAppearancesState`保存邀请、玩家是否提交判断、五类明确处理、待提交动作、原生分叉未改动事实与关闭状态。按钮采用待提交再提交的两次保存，读档只续同一动作。五类处理是同意甲、同意乙、暂缓同意、继续寻找校准和拒绝当前提案；它们共享同一个返回地图出口，不按选择发奖励或惩罚。

事件关闭后完全交还原生地图旅行，玩家仍须在当前自然分叉中选择一条路线并失去另一条机会。实现不调用`TryCompleteReservedQuestion`或`RecordCurrentDoctrine`，不提交`WESTERN_EDGE_012`，也不从地图结果推断哪套理由胜出；因此“正式可玩挑战”和“正式采用塞克斯都路线”保持分离，笛卡尔仍需自己的合法入口。

2026年10月15日全量逻辑、Release零警告零错误、PCK内容与塞克斯都图片别名加载、游戏未运行时的部署三文件SHA256均通过。部署即本次开发交付；后续试玩中的分叉节奏、文本可读性或判断取舍问题按独立反馈处理。

## 笛卡尔重建起点事件

`DescartesMethodicReconstructionPatch`只在单人第二幕、当前仍是苏格拉底知识节点、持有诘问杯、塞克斯都事件已经关闭、至少完成一场战斗、第二幕Fixed尚未处理且当前原生地图点有两个以上子出口时压入标准事件。建立实例前用`TryDecline(..., "Fixed", 2)`消费本幕Fixed并保存，防止拒绝、退出或读档后重复；事件使用`DESCARTES_METHODIC_RECONSTRUCTION`和复用背景别名，不修改地图。

`DescartesMethodicReconstructionState`分别保存共同底稿是否撤下或暂用、地标／独立足迹二选一的核验、是否只恢复核验覆盖的有限判断、未证主张是否继续撤下、待提交动作与关闭状态。按钮采用待提交再提交的两次保存。四条核心完成路径由两种核验和两种重建处理组合；拒绝、保留旧底稿或中途停止均保留事实，但不伪装完成方法。

实现不提交`WESTERN_EDGE_013`，不调用`RecordCurrentDoctrine`，不按撤下主张数量发奖励，也不让地图结果倒证方法正确。塞克斯都的一项窄命题悬置、笛卡尔为重建而处理共同基础、主体／身心用途中的行动归属保持三种不同动作结构。事件关闭后完全交还原生地图选择。

2026年10月15日全量逻辑、Release零警告零错误、PCK内容与笛卡尔图片别名加载、游戏未运行时的部署三文件SHA256均通过。部署即本次开发交付；后续试玩中的信息密度、分叉节奏或方法取舍问题按独立反馈处理。
