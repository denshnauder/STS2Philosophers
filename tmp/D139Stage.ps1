$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8=[Text.UTF8Encoding]::new($false)
$nl=[Environment]::NewLine
$stage=@'

## D139 第欧根尼公开说明双前史核心候选

D139在90把D138角色架构填入具体内容：苏格拉底德性前史使用稳定机会、继续/撤离与目的/标准分离记录；柏拉图前史使用购买/匿名取用/离开、价格/实付/取得与私下理由分离记录。第二幕玩家向第欧根尼公开事实与理由、只公开事实或沉默；第三幕按接收者知识保持、补充、修订或撤回理由，再独立Stay/Switch。

事实公开不可撤回，理由可变；未公开私有材料不能进入NPC台词，商人不会自动知道披露。所有公开状态均可停留或转芝诺，不用“谦虚台词”发门票。标准事件页与第欧根尼/芝诺两张静态图，无新场景、战斗buff或资源交换。该候选首次形成双前史完整循环，可继续四路径纸面审查，但公开差异可能只剩文本长度，且两前史生产保存未统一，未批准对白原型或实现。

阶段前HEAD75f93cc，62笔待推；D139仅90/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README生产内容仍准确；纯设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制/fsck规则不变，ISSUE010无恢复证据不推送。
验证：苏格拉底58/61/62/65知识边界与柏拉图79/83/84交易/匿名边界对照；七字段共享状态、三公开范围、无材料、接收者知识不可回退、理由修订和Stay/Switch独立性反例走查。阶段末完成文档差异与G→S哈希核对。
'@
$activity=Join-Path $root '08问题闭环\08问题与修改.md'
$t=[IO.File]::ReadAllText($activity)
$start=$t.IndexOf('### ISSUE 007 ');$m=[regex]::new('(?m)^---\r?$').Match($t,$start)
if($start -lt 0 -or -not $m.Success){throw 'ISSUE007 missing'}
$section=$t.Substring($start,$m.Index-$start)
$b=$section.IndexOf('- D138在89批准')
$n=[regex]::new('\r?\n\r?\n\*\*准确下一步').Match($section,$b)
if($b -lt 0 -or -not $n.Success){throw 'ISSUE007 markers missing'}
$done=@'
- D139在90形成公开说明双前史完整核心候选：苏格拉底德性行动/目的或柏拉图交易作不可变事实，第二幕向第欧根尼公开事实与理由、只公开事实或沉默；第三幕按知识边界保持/补充/修订/撤回，再独立Stay/Switch。事实公开不可撤回、理由可变，无数值/门票；待四条纸面路径证明关系差异不只是文本长度。
'@
$section=$section.Substring(0,$b)+$done+$section.Substring($n.Index)
$section=[regex]::Replace($section,'\*\*准确下一步：\*\*.*?(?=\r?\n\r?\n\*\*独立待验证)','**准确下一步：** 走查四条完整纸面路径：苏格拉底公开目的后修订、苏格拉底沉默后首次开口、柏拉图只公开事实后补理由、柏拉图公开后撤回理由；每条分别走Stay与Switch。若只改变文本长短而不改变后继问题，降为辅助对白。','Singleline')
$section=[regex]::Replace($section,'\*\*归属与收尾：\*\*.*$','**归属与收尾：** D139纯设计阶段基于HEAD75f93cc开始；仅90/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README不变，不执行Release/PCK/部署；阶段末完成镜像、S同步、差异检查及独立提交。','Singleline')
$t=$t.Substring(0,$start)+$section+$t.Substring($m.Index)
[IO.File]::WriteAllText($activity,$t,$utf8)
$checkpoint=Join-Path $root '08问题闭环\02自动接续.md'
$t=[IO.File]::ReadAllText($checkpoint)
$start=$t.IndexOf('## 准确下一动作');$end=$t.IndexOf('## Git、归属及运行操作')
$action=@'
## 准确下一动作

D139在90形成第欧根尼公开说明双前史完整核心候选：苏格拉底德性行动/目的或柏拉图交易作为不可变事实，第二幕公开事实与理由、只公开事实或沉默；第三幕依接收者所知保持、补充、修订或撤回理由，再独立Stay/Switch。事实公开不可撤回，私有理由不泄露，公开程度不作芝诺门票。

**准确下一步：** 走查四条完整纸面路径：苏格拉底公开目的后修订、苏格拉底沉默后首次开口、柏拉图只公开事实后补理由、柏拉图公开后撤回理由；每条分别走Stay和Switch。若最终只增加文本长度、不改变后继问题，公开说明降为辅助对白而非核心。

人工体验、原生卡身份/完整非胜利退出和ISSUE005新局读档各自待验证；未答不阻断独立设计。ISSUE010 TLS无新恢复证据不重复推送，恢复后推main核对。

'@
$t=$t.Substring(0,$start)+$action+$t.Substring($end)
$start=$t.IndexOf('## Git、归属及运行操作');$end=$t.IndexOf('## 接续和收尾规则')
$git=@'
## Git、归属及运行操作

D139基于HEAD75f93cc、62笔未推开始；仅90/85/11与阶段记录，tmp/保留，无生产代码/资源改动或运行命令。README生产表述不受影响，纯设计不执行Release/PCK/部署；阶段末待镜像、S同步、差异检查和独立提交。ISSUE010无新证据不重推。

ISSUE011状态未变：本地maintenance.geometric-repack.auto=0防止自动失败，普通提交后继续fsck；手动几何维护另待独立窗口，不在设计阶段重试。

生产部署仍为D86已验证输入，后来纸面提交不声称新游戏版本。仅提交本阶段可辨文件，既有网络阻塞独立等待。

'@
$t=$t.Substring(0,$start)+$git+$t.Substring($end)
[IO.File]::WriteAllText($checkpoint,$t,$utf8)
foreach($rel in @('02想法与机制\10西哲路线实现.md','01开发日志\20260919.md')){
 $p=Join-Path $root $rel;$v=[IO.File]::ReadAllText($p)
 if($v -notmatch '## D139 第欧根尼公开说明双前史核心候选'){[IO.File]::WriteAllText($p,$v.TrimEnd()+$stage+$nl,$utf8)}
}
Write-Output 'D139 stage saved to G truth'
