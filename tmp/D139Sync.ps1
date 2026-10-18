$ErrorActionPreference='Stop'
$sourceRoot='G:\我的云端硬盘\STS2mod\STS2 Mod 开发'
$targetRoot='S:\01_Workspace\Obsidian\STS2mod\STS2 Mod 开发'
$backupRoot='S:\01_Workspace\Obsidian备份\STS2mod\20260919_D139'
$rels=@(
 '01开发日志\20260919.md',
 '02想法与机制\10西哲路线实现.md',
 '02想法与机制\11西哲玩法设计.md',
 '02想法与机制\85第欧根尼人物设计.md',
 '02想法与机制\90第欧根尼公开说明双前史.md',
 '08问题闭环\02自动接续.md',
 '08问题闭环\08问题与修改.md'
)
$changed=@()
foreach($rel in $rels){
 $src=Join-Path $sourceRoot $rel;$dst=Join-Path $targetRoot $rel
 if(-not(Test-Path -LiteralPath $src)){throw "missing $src"}
 $same=(Test-Path -LiteralPath $dst) -and ((Get-FileHash -LiteralPath $src).Hash -eq (Get-FileHash -LiteralPath $dst).Hash)
 if(-not $same){
  if(Test-Path -LiteralPath $dst){$bak=Join-Path $backupRoot $rel;New-Item -ItemType Directory -Force -Path (Split-Path -Parent $bak)|Out-Null;Copy-Item -LiteralPath $dst -Destination $bak}
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst)|Out-Null
  Copy-Item -LiteralPath $src -Destination $dst
  if((Get-FileHash -LiteralPath $src).Hash -ne (Get-FileHash -LiteralPath $dst).Hash){throw "hash mismatch $rel"}
  $changed+=$rel
 }
}
Write-Output ('Synced '+$changed.Count+' files');$changed
