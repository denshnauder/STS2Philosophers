$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8=[Text.UTF8Encoding]::new($false)
$nl=[Environment]::NewLine
$stage=@'

## D136 停止以放下主说承担主要循环

D136用同规则复查三类上游：现有城邦印章若本战无能力牌/无触发机会，暂停不代表放下真实帮助；确实依赖下一回合能量/格挡时，局外取回迫使玩家在见到抽牌和敌情前选择困难模式；当前柏拉图无名环只留下药水、金币、付款和历史，没有持续赐福可暂停；永久改牌/属性同样不可逆。

“触发时再问”虽能让帮助实际出现，却要求第欧根尼当前主说下继续运行旧规则的影子计算，逐效果增加战斗模态、焦点、同步和即时恢复，且仍不适用无名环。因此停止87作为主要能力、采用条件或芝诺资格，仅保留未来具体主说天然有安全拒绝时的一次条件性表达。下一从既有事件关系、接收者、实际言行与后继后果找材料，不再丢物、关增益或牺牲资源。

阶段前HEADcf79e84，59笔待推；D136仅87/85/11与阶段记录，tmp/保留，无生产输入改动或运行命令。README生产内容仍准确；纯设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制与fsck状态未变，ISSUE010无恢复证据不推送。
验证：WesternPracticeRelic战斗起止清理、唯一主说门槛、待发奖励消费逻辑与PlatoCivicSeal继承关系只读核对；70当前无名环的药水/金币/付款不回滚边界复核；四处境和触发弹窗制作/单主说反例人工走查。阶段末完成文档差异与G→S哈希检查。
'@

$activity=Join-Path $root '08问题闭环\08问题与修改.md'
$t=[IO.File]::ReadAllText($activity)
$start=$t.IndexOf('### ISSUE 007 ')
$m=[regex]::new('(?m)^---\r?$').Match($t,$start)
if($start -lt 0 -or -not $m.Success){throw 'ISSUE007 missing'}
$section=$t.Substring($start,$m.Index-$start)
$b=$section.IndexOf('- D135在87形成')
$n=[regex]::new('\r?\n\r?\n\*\*准确下一步').Match($section,$b)
if($b -lt 0 -or -not $n.Success){throw 'ISSUE007 result markers missing'}
$done=@'
- D136以城邦印章、当前柏拉图无名环及不可逆效果做同规则反例：无真实触发机会时是假操练，依赖旧效果时是战前困难开关，无名环交易事实没有持续赐福可暂停，触发弹窗会保留隐形第二主说且增加逐效果战斗流程。停止87作为主要能力、采用条件或芝诺资格，只保留未来有天然安全拒绝时的一次表达。
'@
$section=$section.Substring(0,$b)+$done+$section.Substring($n.Index)
$section=[regex]::Replace($section,'\*\*准确下一步：\*\*.*?(?=\r?\n\r?\n\*\*独立待验证)','**准确下一步：** 从既有事件关系出发，比较玩家能对具体接收者说什么、做什么，以及表达如何改变停留或后继；先列正式节点、接收者、实际行动、界面/素材和关系后果，不再以丢物、关增益或牺牲资源作为中心。','Singleline')
$section=[regex]::Replace($section,'\*\*归属与收尾：\*\*.*$','**归属与收尾：** D136纯设计/只读核对阶段基于HEADcf79e84开始；仅87/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README不变，不执行Release/PCK/部署；阶段末完成镜像、S同步、差异检查及独立提交。','Singleline')
$t=$t.Substring(0,$start)+$section+$t.Substring($m.Index)
[IO.File]::WriteAllText($activity,$t,$utf8)

$checkpoint=Join-Path $root '08问题闭环\02自动接续.md'
$t=[IO.File]::ReadAllText($checkpoint)
$start=$t.IndexOf('## 准确下一动作')
$end=$t.IndexOf('## Git、归属及运行操作')
$action=@'
## 准确下一动作

D136停止“放下当前主说机械赐福”作为第欧根尼主要能力、采用条件或芝诺资格。城邦印章没有触发机会时会制造假操练，依赖它时局外取回只是战前困难开关；当前柏拉图无名环的药水/金币/付款是已发生事实，没有持续赐福可暂停。触发时弹窗需保留旧规则影子计算、逐效果战斗模态与即时恢复，违反单一主说方向且仍不适用无名环。

**准确下一步：** 从既有事件关系出发，比较玩家能对具体接收者说什么、做什么，以及表达如何真实改变停留或后继；先列正式节点、接收者、实际行动、界面/素材和关系后果，不再从丢物、关增益或牺牲资源找载体。87仅保留未来天然有安全拒绝时的一次条件性表达。

人工体验、原生卡身份/完整非胜利退出和ISSUE005新局读档各自待验证；未答不阻断独立设计。ISSUE010 TLS无新恢复证据不重复推送，恢复后推main核对。

'@
$t=$t.Substring(0,$start)+$action+$t.Substring($end)
$start=$t.IndexOf('## Git、归属及运行操作')
$end=$t.IndexOf('## 接续和收尾规则')
$git=@'
## Git、归属及运行操作

D136基于HEADcf79e84、59笔未推开始；仅87/85/11与阶段记录，tmp/保留，无生产代码/资源改动或运行命令。README生产表述不受影响，纯设计不执行Release/PCK/部署；阶段末待镜像、S同步、差异检查和独立提交。ISSUE010无新证据不重推。

ISSUE011状态未变：本地maintenance.geometric-repack.auto=0防止自动失败，普通提交后继续fsck；手动几何维护另待独立窗口，不在设计阶段重试。

生产部署仍为D86已验证输入，后来纸面提交不声称新游戏版本。仅提交本阶段可辨文件，既有网络阻塞独立等待。

'@
$t=$t.Substring(0,$start)+$git+$t.Substring($end)
[IO.File]::WriteAllText($checkpoint,$t,$utf8)
foreach($rel in @('02想法与机制\10西哲路线实现.md','01开发日志\20260919.md')){
 $p=Join-Path $root $rel;$v=[IO.File]::ReadAllText($p)
 if($v -notmatch '## D136 停止以放下主说承担主要循环'){[IO.File]::WriteAllText($p,$v.TrimEnd()+$stage+$nl,$utf8)}
}
Write-Output 'D136 stage saved to G truth'
