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

## 结论

- 不是 staging 路径问题，也不是 PCK 路径表问题；问题集中在 PckPacker 的 PNG→ctex 转换。
- 直接接入 csproj 会产出“打包成功但纹理加载挂起”的伪成功包，不能作为 ENC-69 交付。
- 未修改 csproj，未提交 packer 接入代码。

## 可行后续

1. 保留 Godot 导入缓存：纯 JSON 改动走 PckWriter；PNG 改动仍回退 Godot。只覆盖部分验收，不完整。
2. 用与 Godot 4.5.1 兼容的 ctex 编码器替换 `CtexConverter`，并做纹理加载回归。
3. 等待 PckPacker 修复后重新评估。

在 1/2 完成前，ENC-69 保持阻塞。
