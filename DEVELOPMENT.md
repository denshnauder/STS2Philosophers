# 开发说明

本文档面向参与 STS2 Philosophers 开发与验证的同学。玩家玩法、安装方法和当前限制请先阅读 [README.md](./README.md)。

## 赐福流程代码结构

- `src/Patches/NeowProceedPatch.cs` 与 `src/Patches/ActTwoPhilosophersGazePatch.cs` 分别负责第一层和第二层事件插入。
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
