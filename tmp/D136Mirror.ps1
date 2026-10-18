$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8=[Text.UTF8Encoding]::new($false)
$nl=[Environment]::NewLine
$g85=Join-Path $root '02想法与机制\85第欧根尼人物设计.md'
$g87=Join-Path $root '02想法与机制\87第欧根尼放下主说载体.md'
[IO.File]::WriteAllText('docs\design\DiogenesVirtueDesign.md','G85为维护真源，本文件为当前判断镜像。'+$nl+$nl+[IO.File]::ReadAllText($g85),$utf8)
[IO.File]::WriteAllText('docs\design\DiogenesTeachingReleaseDesign.md','G87为维护真源，本文件为候选镜像。'+$nl+$nl+[IO.File]::ReadAllText($g87),$utf8)
$ga=[IO.File]::ReadAllText((Join-Path $root '08问题闭环\08问题与修改.md'))
$ra=[IO.File]::ReadAllText('docs\issues\ACTIVE.md')
$gs=$ga.IndexOf('### ISSUE 007 ');$gm=[regex]::new('(?m)^---\r?$').Match($ga,$gs)
$rs=$ra.IndexOf('## ISSUE 007 ');$rm=[regex]::new('(?m)^## ISSUE 011 ').Match($ra,$rs)
if($gs -lt 0 -or -not $gm.Success -or $rs -lt 0 -or -not $rm.Success){throw 'ACTIVE ISSUE007 markers missing'}
$issue=$ga.Substring($gs,$gm.Index-$gs) -replace '^### ISSUE 007 ','## ISSUE 007 '
$ra=$ra.Substring(0,$rs)+$issue+'---'+$nl+$nl+$ra.Substring($rm.Index)
[IO.File]::WriteAllText('docs\issues\ACTIVE.md',$ra,$utf8)
[IO.File]::WriteAllText('docs\issues\RESUME.md',[IO.File]::ReadAllText((Join-Path $root '08问题闭环\02自动接续.md')),$utf8)
$log=[IO.File]::ReadAllText((Join-Path $root '01开发日志\20260919.md'))
$stage=[regex]::Match($log,'(?s)## D136 停止以放下主说承担主要循环.*$').Value
if([string]::IsNullOrWhiteSpace($stage)){throw 'D136 log stage missing'}
$dev=[IO.File]::ReadAllText('DEVELOPMENT.md')
if($dev -notmatch '## D136 停止以放下主说承担主要循环'){[IO.File]::WriteAllText('DEVELOPMENT.md',$dev.TrimEnd()+$nl+$nl+$stage.Trim()+$nl,$utf8)}
Write-Output 'D136 repo mirrors updated'
