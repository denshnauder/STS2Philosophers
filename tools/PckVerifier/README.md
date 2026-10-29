# PCK 校验器

`VerifyContentPck.gd` 在 Godot 中加载构建出的内容 PCK，确认它不仅能被打包，也能被运行时加载。

## 用法

```sh
Godot --headless --path tools/PckVerifier \
  --script "$PWD/tools/PckVerifier/VerifyContentPck.gd" -- /path/to/STS2Philosophers.pck
```

`--path` 必须指向 `tools/PckVerifier` 而不是 `content`：以 `content` 为资源根时，
缺失的包内文件会退回到源目录读取，校验会误判为通过。

快速打包路径（JSON/PNG）不启动 Godot，因此 `BuildContentPck.sh` 和
`BuildContentPck.ps1` 只在 Godot fallback 路径中调用本校验器；
`tools/VerifyMod.ps1`、`TestPckVerifier.sh` 和 `VerifyEventPortraits.sh`
仍会在验收时单独运行它。

## 头像验收

普通校验通过不等于事件房间能打开：每个 `EventModel` 的原生默认头像路径也必须在包里。构建后运行：

```sh
GODOT=/path/to/Godot tools/PckVerifier/VerifyEventPortraits.sh /path/to/STS2Philosophers.pck
```

脚本枚举 mod 程序集中全部具体 `EventModel`，解析真实 `InitialPortraitPath`，加载包内纹理并调用游戏自身的 `CreateInitialPortrait`；新增事件忘记加头像别名时会在此失败。当前基线为 54/54。


## 退出码

| 码 | 含义 |
| --- | --- |
| `0` | 全部检查通过 |
| `2` | 参数数量错误 |
| `3` | PCK 无法加载 |
| `4` | 必需文件、纹理或 `.import` 元数据缺失 |
| `5` | 必需纹理无法作为 `Texture2D` 加载 |
| `6` | 本地化 JSON 语法、结构、类型、空值或中英键集合不合法 |

## 检查范围

- 四个本地化 JSON 和全部必需纹理存在。
- 每个必需纹理有对应的 `.import` 元数据。
- JSON 可解析、根为对象、值为字符串、键和值非空。
- `events.json` 与 `relics.json` 的 `eng`/`zhs` 键集合一致。
- 每个必需纹理能通过 `ResourceLoader` 加载。

## 回归测试

```sh
GODOT=/path/to/Godot tools/PckVerifier/TestPckVerifier.sh
```

覆盖基线、非法 JSON、缺失本地化文件、构建失败保留旧产物、缺失纹理、错误 JSON 根、中英键不一致、非字符串值、空值、缺失 `.import`、空键、不可加载纹理、不可加载 PCK、缺失 PCK 参数、多余 PCK 参数共 15 条路径。
