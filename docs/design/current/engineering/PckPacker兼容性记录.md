# PckPacker 兼容性记录（ENC-69）

## 目的

评估用 `BSchneppe.StS2.PckPacker` 替代 Godot 启动来构建 PCK，重点验证纯 JSON+PNG 改动能否直接产出可加载包。

## 实测环境

- Godot `4.5.1.stable.mono`
- PckPacker `0.1.1`（同时试过 `0.1.0`）
- staging 仅含 `Test/localization/eng/events.json` 与 `Test/images/kongzi_muduo.png`

## 结果

1. CLI 的 `sourceDir` 必须是资产根（`.../Test`），不是其父目录；此前把父目录传入会得到错误路径。按正确参数后：
   - `ProjectSettings.load_resource_pack` 成功；
   - `res://Test/localization/eng/events.json` 可读。
2. PNG 路径不可用：
   - `res://Test/images/kongzi_muduo.png` 本身不存在（PckPacker 转为 ctex 是预期）；
   - 访问 `.import` 或纹理时 Godot 挂起，而非正常报错。
3. 直接用 `PckWriter` 写入 `.import` 与 `CtexConverter.Convert` 生成的 ctex，路径可读，但 `ResourceLoader.load(..., "Texture2D")` 同样挂起；把该 ctex 单独放进最小 Godot 工程也挂起。
4. 生成的 ctex 头部为 `GST2`，但大小与 Godot 自己导入的 ctex 不同（测试图 2904 vs 3794 字节），说明 `CtexConverter` 的输出与当前 Godot 4.5.1 的 ctex 读取路径不兼容。

## 结论与修正

- 上述“ctex 不兼容”结论不成立：单独把 ctex 放进最小工程可以正常加载，完整 fast PCK 也能通过纹理检查。
- 真正原因是 **PCK 中包含 `.godot/` 路径条目**。Godot 4.5.1 挂载这种 PCK 后会挂起；把 ctex 改放到非隐藏的 `STS2Philosophers/imported/...`，并在 `.import` remap 中指向该路径，纹理即可正常加载。
- 另需把 `PckPacker` 的 `sourceDir` 设为资产根，而不是其父目录；否则路径会多一层。
- 该结论已在 `tools/ContentPckPacker` 落地，并接入 `BuildContentPck.ps1` 的快速路径；SVG 源哈希过期时返回 exit 3，回退原 Godot 导入+打包。

## 已落地

- JSON/PNG 改动：`ContentPckPacker` 直接生成 PCK，不启动 Godot。
- SVG 改动：`ContentPckPacker` 校验 `.md5` sidecar 后复用缓存；缺失或过期则回退 Godot。
- 验证：完整 fast PCK 216 entries，PckVerifier 通过；修改 JSON+PNG 后仍通过；修改 SVG 后正确回退并通过。
