$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8NoBom=[Text.UTF8Encoding]::new($false)
$nl=[Environment]::NewLine
$stage=@'

## D134 犬儒自足、操练与直言关系收束

D134补读IEP犬儒条目和SEP古代伦理犬儒段，并与既有拉尔修证言分层：操练是反复形成能力的过程，自足是较少受外在供给/许可牵制的生活条件，直言是在具体接收者和风险中表达；三者相互支持但不是可冒充历史定论的固定三阶段。贫穷、苦难次数和挑衅台词均不能作为同一计量条。

24/85列出五种既有载体的边界：单一Question没有持续实践，普通战斗没有理解陈述的受众，奖励/休息点多为私人优化，商店会重复柏拉图且保存成本未解，第三幕后继不能补写不存在的前段事实。允许的低成本骨架仅为第二幕既定Question邀请、正常局面重复行动、第三幕既定Question/Switch或停留表达同一对象与事实；中心训练材料仍缺，不批准能力、命名、美术或原型。

阶段前HEAD35858f1，57笔待推；D134仅研究、人物设计、导航和阶段记录，tmp/保留，无运行操作或生产输入变化。README生产表述仍准确；纯研究不执行Release、PCK、部署或游戏验收，ISSUE010无新恢复证据不重推。
验证：IEP伦理/自由与直言/操练段及SEP犬儒段交叉核对，明确晚期有争议证言和游戏假说边界；同果异据、失效恢复、受众风险与五载体制作反例人工审查。阶段末完成文档差异、G→S内容哈希和Git检查。
'@

foreach($rel in @('02想法与机制\10西哲路线实现.md','01开发日志\20260919.md')){
  $p=Join-Path $root $rel
  $t=[IO.File]::ReadAllText($p)
  if($t -notmatch '## D134 犬儒自足、操练与直言关系收束'){
    [IO.File]::WriteAllText($p,$t.TrimEnd()+$stage+$nl,$utf8NoBom)
  }
}

$g24=Join-Path $root '03研究与资料\西方哲学史\24第欧根尼自足与操练.md'
$g85=Join-Path $root '02想法与机制\85第欧根尼人物设计.md'
[IO.File]::WriteAllText('docs\design\DiogenesSelfSufficiencyResearch.md','G24为维护真源，本文件为研究镜像。'+$nl+$nl+[IO.File]::ReadAllText($g24),$utf8NoBom)
[IO.File]::WriteAllText('docs\design\DiogenesVirtueDesign.md','G85为维护真源，本文件为当前判断镜像。'+$nl+$nl+[IO.File]::ReadAllText($g85),$utf8NoBom)

$gActivity=[IO.File]::ReadAllText((Join-Path $root '08问题闭环\08问题与修改.md'))
$repoActivity=[IO.File]::ReadAllText('docs\issues\ACTIVE.md')
$gs=$gActivity.IndexOf('### ISSUE 007 ')
$gm=[regex]::new('(?m)^---\r?$').Match($gActivity,$gs)
$rs=$repoActivity.IndexOf('## ISSUE 007 ')
$rm=[regex]::new('(?m)^## ISSUE 010 ').Match($repoActivity,$rs)
if($gs -lt 0 -or -not $gm.Success -or $rs -lt 0 -or -not $rm.Success){ throw 'ACTIVE mirror markers missing' }
$issue=$gActivity.Substring($gs,$gm.Index-$gs)
$issue=$issue -replace '^### ISSUE 007 ','## ISSUE 007 '
$repoActivity=$repoActivity.Substring(0,$rs)+$issue+'---'+$nl+$nl+$repoActivity.Substring($rm.Index)
[IO.File]::WriteAllText('docs\issues\ACTIVE.md',$repoActivity,$utf8NoBom)

$gResume=[IO.File]::ReadAllText((Join-Path $root '08问题闭环\02自动接续.md'))
[IO.File]::WriteAllText('docs\issues\RESUME.md',$gResume,$utf8NoBom)

$dev=[IO.File]::ReadAllText('DEVELOPMENT.md')
if($dev -notmatch '## D134 犬儒自足、操练与直言关系收束'){
  [IO.File]::WriteAllText('DEVELOPMENT.md',$dev.TrimEnd()+$stage+$nl,$utf8NoBom)
}
Write-Output 'D134 stage mirrors and logs updated'
