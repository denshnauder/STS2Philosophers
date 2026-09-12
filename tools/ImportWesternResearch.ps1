param([Parameter(Mandatory = $true)][string]$ResearchDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
$indexName = '14人物与流派索引.md'
$projectionName = '18游戏流程投影.md'
$index = @(Get-Content -LiteralPath (Join-Path $ResearchDirectory $indexName))
$projection = @(Get-Content -LiteralPath (Join-Path $ResearchDirectory $projectionName))
$people = [ordered]@{}
$inPeople = $false
for ($i = 0; $i -lt $index.Count; $i++) {
    $line = $index[$i]
    if ($line -eq '## 人物索引') { $inPeople = $true; continue }
    if ($line -match '^## ' -and $inPeople) { break }
    if ($inPeople -and $line -match '^\|\s*([^|]+)\|\s*([^|]+)\|') {
        $name = $Matches[1].Trim()
        $foreign = $Matches[2].Trim()
        if ($foreign -notmatch '[A-Za-z]') { continue }
        if ($people.Contains($name)) { throw "Duplicate indexed person: $name" }
        $people[$name] = [ordered]@{ display_name = $name; international_name = $foreign; index_source = "${indexName}:$($i + 1)" }
    }
}
$roles = [ordered]@{}
$inRoles = $false
for ($i = 0; $i -lt $projection.Count; $i++) {
    $line = $projection[$i]
    if ($line -eq '## 02人物用途分层') { $inRoles = $true; continue }
    if ($line -match '^## ' -and $inRoles) { break }
    if ($inRoles -and $line -match '^\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]+)\|') {
        $name = $Matches[1].Trim()
        if ($name -eq '人物' -or $name -match '^[- ]+$') { continue }
        if ($roles.Contains($name)) { throw "Duplicate projected person: $name" }
        $roles[$name] = [ordered]@{
            primary_use = $Matches[2].Trim()
            secondary_uses = @($Matches[3].Trim().Split('、') | Where-Object { $_ -ne '无' })
            problem_and_stage = $Matches[4].Trim()
            design_assessment = $Matches[5].Trim()
            projection_source = "${projectionName}:$($i + 1)"
        }
    }
}
if ($people.Count -ne 89 -or $roles.Count -ne 89) { throw 'Expected the approved 89-person baseline; review source changes explicitly.' }
$entries = @{ '赫拉克利特' = 'HERACLITUS'; '苏格拉底' = 'SOCRATES'; '柏拉图' = 'PLATO'; '笛卡尔' = 'DESCARTES'; '亚里士多德' = 'ARISTOTLE'; '卢梭' = 'ROUSSEAU' }
$records = @()
foreach ($name in $people.Keys) {
    if (-not $roles.Contains($name)) { throw "Missing usage record: $name" }
    $record = $people[$name]
    # Research IDs use the complete conventional name; the six published catalog entries retain their IDs.
    $latin = $record.international_name.Normalize([Text.NormalizationForm]::FormD)
    $latin = -join @($latin.ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark })
    $latin = $latin.Replace('ø', 'o').Replace('Ø', 'O')
    $record.thinker_id = if ($entries.ContainsKey($name)) { $entries[$name] } else { ($latin.ToUpperInvariant() -replace '[^A-Z0-9]+', '_').Trim('_') }
    foreach ($field in $roles[$name].Keys) { $record[$field] = $roles[$name][$field] }
    $record.implementation_status = 'RESEARCH_ONLY'
    $records += $record
}
if (@($records.thinker_id | Sort-Object -Unique).Count -ne 89) { throw 'Generated research IDs are not unique.' }
$paths = @()
$entry = ''
$title = ''
for ($i = 0; $i -lt $projection.Count; $i++) {
    $line = $projection[$i]
    if ($line -match '^### (.+)入口$') { $entry = $Matches[1] }
    if ($line -match '^#### (路径[一二]：.+)$') { $title = $Matches[1] }
    if ($line -match '^第一幕固定') {
        if (-not $entries.ContainsKey($entry) -or -not $title) { throw 'A sample path is missing its entry or heading.' }
        $paths += [ordered]@{
            sample_id = "SAMPLE_$('{0:D2}' -f ($paths.Count + 1))"
            entry_thinker_id = $entries[$entry]
            title = $title
            source = "${projectionName}:$($i + 1)"
            original_sequence = $line
            implementation_status = 'REQUIRES_EDGE_REVIEW'
        }
    }
}
if ($paths.Count -ne 12) { throw 'Expected twelve sample paths; do not silently omit a source path.' }
$catalog = [ordered]@{
    schema_version = 1
    scope = 'Research and design baseline, not a playable graph or reward catalog.'
    source_hashes = @($indexName, $projectionName | ForEach-Object { [ordered]@{ file = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $ResearchDirectory $_)).Hash } })
    people = $records
    sample_paths = $paths
}
$catalog | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $repository 'config/western_research.json') -Encoding UTF8
Write-Host 'Western research import passed: 89 matched people, 12 preserved sample paths.'
