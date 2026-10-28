# PCK 校验器

`VerifyContentPck.gd` 在 Godot 中加载构建出的内容 PCK，确认它不仅能被打包，也能被运行时加载。

## 用法

```sh
Godot --headless --path content \
  --script tools/PckVerifier/VerifyContentPck.gd -- /path/to/STS2Philosophers.pck
```

`BuildContentPck.sh` 和 `BuildContentPck.ps1` 会在替换目标 PCK 前自动调用它。

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

覆盖基线、非法 JSON、缺失本地化文件、构建失败保留旧产物、缺失纹理、错误 JSON 根、中英键不一致、非字符串值、空值、缺失 `.import`、空键、不可加载纹理、不可加载 PCK 共 13 条路径。
