$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8=[Text.UTF8Encoding]::new($false)
$nl=[Environment]::NewLine
$stage=@'

## D138 确定第欧根尼实践型问号角色

D138在89比较直接Adopt战斗赐福、Adopt无效果困难状态与Question/Keep三种结构，批准第三种为设计方向：第二幕第欧根尼实践问号保存公开说法/知识流但不替换当前机械主说；第三幕Stay结束本局哲学发展并保留此前唯一主说，或以说明卡公开犬儒传统/克拉特斯中介后Switch芝诺，由芝诺正常替换。

该结构复用Question/Keep概念，区分路线最后问题与当前机械主说，取消“每个人物必须发战斗buff”的错误前提。正式界面为两个标准事件节点，第欧根尼/芝诺两张静态图，克拉特斯不作为无素材说话角色；无群众、战斗浮层或数值奖励。两次公开行动的共同具体事实仍缺，角色架构通过不等于自足、操练、直言或人物内容完成。

阶段前HEAD6d4d6f0，61笔待推；D138仅89/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README生产内容仍准确；纯设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制/fsck规则不变，ISSUE010无恢复证据不推送。
验证：18人物用途/样例路径与19第61—62、合法终点、第欧根尼位置再次对照；三角色结构的单一主说、Stay/Switch、无材料、沉默/修订/知识不可撤回和动机反例人工走查；文档差异与G→S哈希待阶段末核对。
'@
$activity=Join-Path $root '08问题闭环\08问题与修改.md'
$t=[IO.File]::ReadAllText($activity)
$start=$t.IndexOf('### ISSUE 007 ');$m=[regex]::new('(?m)^---\r?$').Match($t,$start)
if($start -lt 0 -or -not $m.Success){throw 'ISSUE007 missing'}
$section=$t.Substring($start,$m.Index-$start)
$b=$section.IndexOf('- D137补充犬儒')
$n=[regex]::new('\r?\n\r?\n\*\*准确下一步').Match($section,$b)
if($b -lt 0 -or -not $n.Success){throw 'ISSUE007 markers missing'}
$done=@'
- D138在89批准“第二幕实践型Question/Keep，第三幕Stay或经克拉特斯中介Switch芝诺”的角色架构方向：第欧根尼保存公开说法与知识流，不发第二战斗能力；始终只有此前主说或芝诺一个生效。两个标准事件、两张静态人物图，无新增群众/战斗浮层。具体自足事实与两次公开行动仍缺，不批准事件内容或生产。
'@
$section=$section.Substring(0,$b)+$done+$section.Substring($n.Index)
$section=[regex]::Replace($section,'\*\*准确下一步：\*\*.*?(?=\r?\n\r?\n\*\*独立待验证)','**准确下一步：** 设计两次公开行动使用的同一具体事实，优先从当前路线已可靠保存的材料中选择，至少覆盖一种苏格拉底前史和一种柏拉图前史；可有不同文本，但共享接收者已知事实、玩家说法与后来修订结构。','Singleline')
$section=[regex]::Replace($section,'\*\*归属与收尾：\*\*.*$','**归属与收尾：** D138纯设计阶段基于HEAD6d4d6f0开始；仅89/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README不变，不执行Release/PCK/部署；阶段末完成镜像、S同步、差异检查及独立提交。','Singleline')
$t=$t.Substring(0,$start)+$section+$t.Substring($m.Index)
[IO.File]::WriteAllText($activity,$t,$utf8)
$checkpoint=Join-Path $root '08问题闭环\02自动接续.md'
$t=[IO.File]::ReadAllText($checkpoint)
$start=$t.IndexOf('## 准确下一动作');$end=$t.IndexOf('## Git、归属及运行操作')
$action=@'
## 准确下一动作

D138批准第欧根尼的角色架构设计方向：第二幕作为实践型Question/Keep，保存公开说法和知识流但不替换当前机械主说；第三幕Stay关闭本局后续哲学切换并保留此前唯一主说，或以说明卡公开犬儒传统/克拉特斯中介后Switch芝诺，由芝诺正常替换。两个标准事件节点、第欧根尼/芝诺两张静态图，无第二战斗能力。

**准确下一步：** 设计两次公开行动使用的同一具体事实，优先选当前路线已可靠保存的材料，至少覆盖一种苏格拉底前史和一种柏拉图前史；文本可不同，但共享“接收者已知事实、玩家明确说法、后来保持/修订/撤回”的状态结构。若只是普通最优战术、自动品德评分或需要新增群众场景，否决。

人工体验、原生卡身份/完整非胜利退出和ISSUE005新局读档各自待验证；未答不阻断独立设计。ISSUE010 TLS无新恢复证据不重复推送，恢复后推main核对。

'@
$t=$t.Substring(0,$start)+$action+$t.Substring($end)
$start=$t.IndexOf('## Git、归属及运行操作');$end=$t.IndexOf('## 接续和收尾规则')
$git=@'
## Git、归属及运行操作

D138基于HEAD6d4d6f0、61笔未推开始；仅89/85/11与阶段记录，tmp/保留，无生产代码/资源改动或运行命令。README生产表述不受影响，纯设计不执行Release/PCK/部署；阶段末待镜像、S同步、差异检查和独立提交。ISSUE010无新证据不重推。

ISSUE011状态未变：本地maintenance.geometric-repack.auto=0防止自动失败，普通提交后继续fsck；手动几何维护另待独立窗口，不在设计阶段重试。

生产部署仍为D86已验证输入，后来纸面提交不声称新游戏版本。仅提交本阶段可辨文件，既有网络阻塞独立等待。

'@
$t=$t.Substring(0,$start)+$git+$t.Substring($end)
[IO.File]::WriteAllText($checkpoint,$t,$utf8)
foreach($rel in @('02想法与机制\10西哲路线实现.md','01开发日志\20260919.md')){
 $p=Join-Path $root $rel;$v=[IO.File]::ReadAllText($p)
 if($v -notmatch '## D138 确定第欧根尼实践型问号角色'){[IO.File]::WriteAllText($p,$v.TrimEnd()+$stage+$nl,$utf8)}
}
Write-Output 'D138 stage saved to G truth'
