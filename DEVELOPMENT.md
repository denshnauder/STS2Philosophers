# 开发说明

本文档面向参与 STS2 Minimal Mod 开发与验证的同学。玩家玩法、安装方法和当前限制请先阅读 [README.md](./README.md)。

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

当前覆盖木铎回合规则、青玉佩仁行与奖励策略、熊掌回合触发、绳墨序列、墨色竹简的相利状态、守城图的守御窗口与伤害来源过滤，以及“诸子观照”三阶段页面流、导航零副作用、根遗物互斥、二层过滤与替换、德继承、重复回调门控、插入防重和中英本地化键。

## 构建 DLL

只构建程序集，不生成 PCK，也不部署：

```powershell
dotnet build -c Release -p:DeployOnBuild=false
```

输出位于 `bin\Release\net9.0\STS2MinimalMod.dll`。

## 构建与校验 PCK

先按 `local.props` 设置本机路径，再调用构建脚本：

```powershell
$godotExecutable = 'C:\path\to\Godot_console.exe'
$dotNetRoot = 'C:\path\to\dotnet'

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\BuildContentPck.ps1 `
  -GodotExecutable $godotExecutable `
  -DotNetRoot $dotNetRoot `
  -ProjectDirectory .\content `
  -OutputPath .\bin\Release\net9.0\STS2MinimalMod.pck
```

独立加载并检查内容包：

```powershell
$env:DOTNET_ROOT = $dotNetRoot
$env:DOTNET_ROLL_FORWARD = 'Major'
$verifierProject = (Resolve-Path .\tools\PckVerifier).Path
$verifierScript = (Resolve-Path .\tools\PckVerifier\VerifyContentPck.gd).Path
$pckPath = (Resolve-Path .\bin\Release\net9.0\STS2MinimalMod.pck).Path

& $godotExecutable --headless `
  --path $verifierProject `
  --script $verifierScript `
  -- $pckPath
```

校验器会确认本地化文件和事件、遗物纹理已经打包且可以加载。

## 部署

`DeployOnBuild` 未设为 `false` 时，普通 Release 构建会生成 PCK，并把以下文件复制到 `$(GameDirectory)\mods\STS2MinimalMod`：

- `STS2MinimalMod.dll`
- `STS2MinimalMod.pck`
- `STS2MinimalMod.json`

按仓库规则，自动部署前必须先完成相关测试、DLL 构建与 PCK 校验，并确认游戏进程未运行。部署后应对源文件和目标文件进行哈希或等价校验。不要自动启动 Godot GUI、Steam 或游戏本体。

## 游戏内调试

必须从 Steam 启动游戏。启用 Mod 后，初始化日志应包含：

```text
[STS2MinimalMod] Initialized successfully.
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
- `mozishouchengtu`：授予守城图。
- `mengzixiongzhang virtue [amount]`：查看或设置当前玩家的德；持有熊掌时读写熊掌自身继承的德，否则读写青玉佩。

## 项目结构

- `src\Events`：事件模型和纯逻辑策略。
- `src\Patches`：Harmony 插入点。
- `src\Relics`：按思想家划分的遗物、状态与控制台命令。
- `content\STS2MinimalMod`：中英文本地化和纹理资源。
- `tests\ModLogicChecks`：不依赖游戏进程的逻辑检查。
- `tools`：内容包构建、校验和占位资源工具。

新增或重命名内容前必须遵守 [NAMING.md](./NAMING.md)，仓库协作和验证规则见 [AGENTS.md](./AGENTS.md)。
