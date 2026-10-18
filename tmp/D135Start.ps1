$ErrorActionPreference='Stop'
$root='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$utf8=[Text.UTF8Encoding]::new($false)

$activity=Join-Path $root '08问题闭环\08问题与修改.md'
$t=[IO.File]::ReadAllText($activity)
$old='**准确下一步：** 比较一个跨正常局面存在的训练材料，必须具体说明原依赖、非禁用/非奖励交换的替代动作、失效与恢复，以及后继哪个既有对象能理解并产生关系后果；若需要新增群众/权势者场景才成立则停止。先设计，不写代码。'
$new='**准确下一步：** D135审查“放下当前主说的机械赐福”载体：先写既定Question节点、局外恢复入口、状态界面和静态素材，再检查暂停/恢复真实效果、被动困难模式、立即反悔与强弱构筑反例；不以数值补偿救候选，不先代码。'
if(-not $t.Contains($old)){ throw 'ISSUE007 next marker missing' }
$t=$t.Replace($old,$new)
if($t -notmatch '### ISSUE 011 Git几何维护'){
$issue=@'

### ISSUE 011 Git几何维护在包文件碰撞时失败

**状态：** 待处理
**优先级：** 低，不阻断ISSUE007设计与普通提交。

**现象与事实：** D134提交74f86d2成功后，自动geometric-repack先报multi-pack-index权限错误；重建派生索引后又遇到既有同名pack及只读idx的覆盖失败。检查时无Git进程或锁文件，提交对象连通性和工作区状态正常。失败产生的冗余包已移入.git/objects/pack/d134_failed_geometric，旧索引在同目录保留可回退备份；这些均为Git内部派生文件，不进入提交。

**当前处置：** 仓库本地配置maintenance.geometric-repack.auto=0，git maintenance run --auto与git fsck --connectivity-only均返回0，避免每次提交重复触发同一失败。手动geometric-repack仍未闭环，不在设计阶段继续循环尝试。

**闭环条件：** 独立维护窗口中确认Windows只读包/同名包处理方式，手动几何重打包成功，随后fsck通过并安全清理隔离包与索引备份；处理前普通提交仍须复查对象连通性。

'@
$idx=$t.IndexOf('## ISSUE 010 ')
if($idx -lt 0){ throw 'ISSUE010 marker missing' }
$t=$t.Substring(0,$idx)+$issue+$t.Substring($idx)
}
[IO.File]::WriteAllText($activity,$t,$utf8)

$checkpoint=Join-Path $root '08问题闭环\02自动接续.md'
$t=[IO.File]::ReadAllText($checkpoint)
$old='**准确下一步：** 比较一个跨正常局面存在的训练材料，先写玩家具体依赖、非禁用且非奖励交换的替代动作、失效/恢复，再写第三幕既定Question/Switch或停留中哪个对象能理解并产生关系后果。若只能靠新增群众、权势者或专属场景成立则否决；不先命名、画图或写代码。'
$new='**准确下一步：** D135审查“放下当前主说的机械赐福”载体：先写第二幕既定Question、局外恢复入口、状态界面与素材上限，再检查暂停/恢复真实效果、被动困难模式、立即反悔和强弱构筑反例。候选未通过，不命名能力、画图或写代码。'
if(-not $t.Contains($old)){ throw 'checkpoint next marker missing' }
$t=$t.Replace($old,$new)
$marker='生产部署仍为D86已验证输入，后来纸面提交不声称新游戏版本。仅提交本阶段可辨文件，既有网络阻塞独立等待。'
$note=@'

D134提交74f86d2成功，当前58笔未推；提交后自动几何维护因派生索引和只读同名包碰撞失败，但无Git进程/锁，fsck连通性持续通过。冗余包与旧索引仅在.git内部隔离备份；本地maintenance.geometric-repack.auto=0后自动维护返回0。ISSUE011记录手动维护未闭环，设计阶段不重复尝试，不影响普通提交和ISSUE007。
'@
if($t -notmatch 'D134提交74f86d2成功'){
  if(-not $t.Contains($marker)){ throw 'checkpoint git marker missing' }
  $t=$t.Replace($marker,$marker+$note)
}
[IO.File]::WriteAllText($checkpoint,$t,$utf8)
Write-Output 'D135 start and ISSUE011 saved to G truth'
