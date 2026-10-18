$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8=[Text.UTF8Encoding]::new($false)
$nl=[Environment]::NewLine
$stage=@'

## D137 锁定第欧根尼公开关系层

D137补研究24的公开标签/羞耻边界：犬儒名称来源存在竞争解释，后世传统对可能的侮辱称呼作主动挪用，但不能把辱名直接授给玩家或把粗野/冒犯计作德性。88比较五种关系载体，否决战斗敌人听众、新街市/群众/权势者场景和复用柏拉图商人核心。

保留最低成本关系外壳：第二幕既定Question由第欧根尼接收玩家明确说法；第三幕可合法停留，或以说明卡公开犬儒传统/克拉特斯中介后向芝诺陈述事实并转向。正式成本为标准事件页及第欧根尼/芝诺两张静态图，克拉特斯不作为无素材说话角色。该层只保证具体接收者、路线后果和历史边界，仍无操练动作、自足替代或完整parrhēsia，不批准实现/对白原型。

阶段前HEAD2aa3a83，60笔待推；D137仅24/88/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README生产内容仍准确；纯研究设计不执行Release、PCK、部署或游戏验收。ISSUE011自动维护抑制/fsck规则不变，ISSUE010无恢复证据不推送。
验证：IEP名称23—25、伦理34—49与既有拉尔修资料分层释义；18路径、19第59—62/合法终点/克拉特斯中介复核；五载体的节点、接收者、界面、静态素材和关系后果反例审查。阶段末完成文档差异与G→S哈希核对。
'@
$activity=Join-Path $root '08问题闭环\08问题与修改.md'
$t=[IO.File]::ReadAllText($activity)
$start=$t.IndexOf('### ISSUE 007 ');$m=[regex]::new('(?m)^---\r?$').Match($t,$start)
if($start -lt 0 -or -not $m.Success){throw 'ISSUE007 missing'}
$section=$t.Substring($start,$m.Index-$start)
$b=$section.IndexOf('- D136以城邦印章')
$n=[regex]::new('\r?\n\r?\n\*\*准确下一步').Match($section,$b)
if($b -lt 0 -or -not $n.Success){throw 'ISSUE007 result markers missing'}
$done=@'
- D137补充犬儒对公开标签/羞耻的有限研究，88比较五种关系载体后保留最低成本外壳：第二幕第欧根尼接收说法，第三幕合法停留或用说明卡公开犬儒传统/克拉特斯中介后向芝诺陈述并转向；只需标准事件与两张静态人物图。它没有操练动作、自足替代或完整政治直言，仍不批准实现/对白原型。
'@
$section=$section.Substring(0,$b)+$done+$section.Substring($n.Index)
$section=[regex]::Replace($section,'\*\*准确下一步：\*\*.*?(?=\r?\n\r?\n\*\*独立待验证)','**准确下一步：** 只设计一个在第二幕第欧根尼与第三幕停留/芝诺之间造成真实差异的行动：改变玩家与一种公开评价或惯例的关系，允许后来修订，并能被两个接收者理解；不牺牲物件/增益，不贴犬/圣人/自足者标签，不记录普通最优战术。','Singleline')
$section=[regex]::Replace($section,'\*\*归属与收尾：\*\*.*$','**归属与收尾：** D137纯研究设计阶段基于HEAD2aa3a83开始；仅24/88/85/11与阶段记录，tmp/保留，无生产输入或运行命令。README不变，不执行Release/PCK/部署；阶段末完成镜像、S同步、差异检查及独立提交。','Singleline')
$t=$t.Substring(0,$start)+$section+$t.Substring($m.Index)
[IO.File]::WriteAllText($activity,$t,$utf8)

$checkpoint=Join-Path $root '08问题闭环\02自动接续.md'
$t=[IO.File]::ReadAllText($checkpoint)
$start=$t.IndexOf('## 准确下一动作');$end=$t.IndexOf('## Git、归属及运行操作')
$action=@'
## 准确下一动作

D137补犬儒公开标签/羞耻边界并比较五种关系载体。保留的最低成本外壳为：第二幕既定Question由第欧根尼接收明确说法；第三幕可停留，或以说明卡公开犬儒传统/克拉特斯中介后向芝诺陈述事实并转向。只用标准事件页与第欧根尼/芝诺两张静态图，克拉特斯不作为无素材说话角色，不增群众、街市、权势者或旅人。

**准确下一步：** 只设计一个在两个节点间造成真实差异的行动：改变玩家与一种公开评价/惯例的关系，可被第欧根尼和芝诺分别理解并允许修订；不牺牲物件/增益，不给玩家贴“犬”“圣人”或“自足者”标签，不记录普通最优战术。关系层本身没有操练动作，不能单独实现。

人工体验、原生卡身份/完整非胜利退出和ISSUE005新局读档各自待验证；未答不阻断独立设计。ISSUE010 TLS无新恢复证据不重复推送，恢复后推main核对。

'@
$t=$t.Substring(0,$start)+$action+$t.Substring($end)
$start=$t.IndexOf('## Git、归属及运行操作');$end=$t.IndexOf('## 接续和收尾规则')
$git=@'
## Git、归属及运行操作

D137基于HEAD2aa3a83、60笔未推开始；仅24/88/85/11与阶段记录，tmp/保留，无生产代码/资源改动或运行命令。README生产表述不受影响，纯研究设计不执行Release/PCK/部署；阶段末待镜像、S同步、差异检查和独立提交。ISSUE010无新证据不重推。

ISSUE011状态未变：本地maintenance.geometric-repack.auto=0防止自动失败，普通提交后继续fsck；手动几何维护另待独立窗口，不在设计阶段重试。

生产部署仍为D86已验证输入，后来纸面提交不声称新游戏版本。仅提交本阶段可辨文件，既有网络阻塞独立等待。

'@
$t=$t.Substring(0,$start)+$git+$t.Substring($end)
[IO.File]::WriteAllText($checkpoint,$t,$utf8)
foreach($rel in @('02想法与机制\10西哲路线实现.md','01开发日志\20260919.md')){
 $p=Join-Path $root $rel;$v=[IO.File]::ReadAllText($p)
 if($v -notmatch '## D137 锁定第欧根尼公开关系层'){[IO.File]::WriteAllText($p,$v.TrimEnd()+$stage+$nl,$utf8)}
}
Write-Output 'D137 stage saved to G truth'
