# 命名规范

本规范适用于本仓库后续新增与重命名的代码、内容、资源、测试和笔记。玩家显示名称与内部实现名称相互独立。

1. 属于特定思想家的模型统一使用“思想家 + 内容名”。
2. C# 类型和代码文件使用 `PascalCase`。
3. 内部 ID 和本地化键使用 `UPPER_SNAKE_CASE`。
4. 图片等资源使用 `lower_snake_case`。
5. 自定义文件名、文件夹名、内部 ID、资源名和自定义命令中禁止使用连字符 `-`。
6. 中文思想家与专有概念采用拼音；外国思想家采用通行的拉丁字母名称。
7. 玩家显示名称由本地化文件决定，不与内部名称绑定；不得为了统一内部名称而给玩家显示的遗物名称添加思想家前缀。
8. 辅助类型使用“所属模型 + 职责”，例如 `KongziQingYuPeiState`、`KongziQingYuPeiRewardPolicy`、`MengziXiongZhangConsoleCmd`。
9. 共享事件、补丁和系统使用清晰的英文语义名称。
10. `ModelId` 一旦进入公开版本即视为稳定接口；除非有明确迁移方案，否则禁止修改。
11. 受游戏引擎或上游 API 强制规定的名称可以例外，但必须在相关代码中说明原因。
12. 笔记使用 `00总览`、`01机制` 形式：保留两位数字序号，不加日期，不使用连字符。

本项目标准示例：

- C# 模型：`KongziMuduo`、`KongziQingYuPei`、`MengziXiongZhang`、`PhilosophersGaze`
- 内部 ID：`KONGZI_MUDUO`、`KONGZI_QING_YU_PEI`、`MENGZI_XIONG_ZHANG`、`PHILOSOPHERS_GAZE`
- 资源：`kongzi_muduo.png`、`kongzi_qing_yu_pei.png`
- 自定义命令：`kongzimuduo`、`kongziqingyupei`、`mengzixiongzhang`
