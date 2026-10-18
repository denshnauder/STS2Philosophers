$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8NoBom=[System.Text.UTF8Encoding]::new($false)

$gActivity=Join-Path $root '08问题闭环\08问题与修改.md'
$t=[IO.File]::ReadAllText($gActivity)
$start=$t.IndexOf('### ISSUE 007 ')
$m=[regex]::new('(?m)^---\r?$').Match($t,$start)
if($start -lt 0 -or -not $m.Success){ throw 'G activity ISSUE007 markers not found' }
$end=$m.Index
$section=$t.Substring($start,$end-$start)
$bidx=$section.IndexOf('- D133只读核实RelicCmd')
if($bidx -lt 0){ throw 'G activity D133 bullet not found' }
$m=[regex]::new('\r?\n\r?\n\*\*准确下一步：\*\*').Match($section,$bidx)
if(-not $m.Success){ throw 'G activity next marker not found' }
$next=$m.Index
$newDone=@'
- D134补齐犬儒研究关系：操练是形成能力的过程，自足是较少受供给/许可牵制的条件，直言是在具体关系中承担表达风险；三者相关但非固定三阶段。单一事件、战斗、奖励或商店界面各有缺口，不能用贫穷/苦难/台词同一计量条代替。当前允许的正式骨架仅为既定Question邀请、正常局面重复行动、第三幕既定Question/Switch或停留表达同一事实；中心训练材料仍未选定，不批准能力、命名、美术或原型。
'@
$section=$section.Substring(0,$bidx)+$newDone+$section.Substring($next)
$section=[regex]::Replace($section,'\*\*准确下一步：\*\*.*?(?=\r?\n\r?\n\*\*独立待验证)','**准确下一步：** 比较一个跨正常局面存在的训练材料，必须具体说明原依赖、非禁用/非奖励交换的替代动作、失效与恢复，以及后继哪个既有对象能理解并产生关系后果；若需要新增群众/权势者场景才成立则停止。先设计，不写代码。','Singleline')
$section=[regex]::Replace($section,'\*\*归属与收尾：\*\*.*$','**归属与收尾：** D134研究与设计文档阶段，基于HEAD35858f1开始；仅24/85/11及活动记录，tmp/保留，无生产输入或运行命令。待阶段末完成镜像、S单向同步、差异检查与独立提交；纯研究不执行Release/PCK/部署。','Singleline')
$t=$t.Substring(0,$start)+$section+$t.Substring($end)
[IO.File]::WriteAllText($gActivity,$t,$utf8NoBom)

$checkpoint=Join-Path $root '08问题闭环\02自动接续.md'
$t=[IO.File]::ReadAllText($checkpoint)
$start=$t.IndexOf('## 准确下一动作')
$end=$t.IndexOf('## Git、归属及运行操作')
if($start -lt 0 -or $end -lt 0){ throw 'checkpoint action markers not found' }
$action=@'
## 准确下一动作

D134已补犬儒关系研究：操练是反复形成能力的过程，自足是较少受外在供给/许可牵制的生活条件，直言是在具体接收者和风险中表达；三者相互支持但不是唯一历史阶段，不得压成贫穷、苦难或台词计量条。单一Question、普通战斗、奖励/休息点、商店和第三幕后继各只能承载一部分；允许的低成本方向是同一对象/事实贯穿“正常局面操练/自足 + 既定后继关系表达”，目前没有批准中心材料或能力。

**准确下一步：** 比较一个跨正常局面存在的训练材料，先写玩家具体依赖、非禁用且非奖励交换的替代动作、失效/恢复，再写第三幕既定Question/Switch或停留中哪个对象能理解并产生关系后果。若只能靠新增群众、权势者或专属场景成立则否决；不先命名、画图或写代码。

人工体验、原生卡身份/完整非胜利退出和ISSUE005新局读档各自待验证；未答不阻断独立设计。ISSUE010 TLS无新恢复证据不重复推送，恢复后推main核对。

'@
$t=$t.Substring(0,$start)+$action+$t.Substring($end)
$start=$t.IndexOf('## Git、归属及运行操作')
$end=$t.IndexOf('## 接续和收尾规则')
$git=@'
## Git、归属及运行操作

D134基于HEAD35858f1、57笔未推开始；本阶段仅24/85/11及阶段记录，tmp/保留，无生产代码/资源改动或运行命令。纯研究与设计边界修订，README生产表述不受影响，不执行Release/PCK/部署；阶段末待镜像、S同步、差异检查、提交。ISSUE010无新证据不重推。

生产部署仍为D86已验证输入，后来纸面提交不声称新游戏版本。仅提交本阶段可辨文件，既有网络阻塞独立等待。

'@
$t=$t.Substring(0,$start)+$git+$t.Substring($end)
[IO.File]::WriteAllText($checkpoint,$t,$utf8NoBom)
Write-Output 'G truth D134 activity and checkpoint updated'
