#requires -Version 7.0
<##
Read-only guard for a complete Linear project snapshot. It never changes Linear or
certifies deployment; Done is still subject to the delivery evidence in the issue.
Exit codes: 0 selected/complete, 2 invalid snapshot, 3 blocked.
##>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IssueSnapshotPath,
    [string[]]$AuthorizedEngineeringIssueId = @(),
    [string]$EngineeringAuthorizationScope
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$result = [ordered]@{
    status = 'invalidSnapshot'
    selectedIssue = $null
    earliestUnfinished = $null
    lateInProgress = @()
    unmappedIssues = @()
    reasons = @()
}

function IssueSummary($issue) {
    [ordered]@{ id = $issue.id; title = $issue.title; status = $issue.status }
}

try {
    $snapshot = Get-Content -LiteralPath $IssueSnapshotPath -Raw | ConvertFrom-Json -AsHashtable
    if ($snapshot.complete -isnot [bool] -or -not $snapshot.complete -or -not $snapshot.ContainsKey('issues')) {
        throw 'Snapshot must explicitly declare complete=true and contain the complete project issues array.'
    }
    $issues = @($snapshot.issues)
    if ($snapshot.ContainsKey('totalIssueCount') -and [int]$snapshot.totalIssueCount -ne $issues.Count) {
        throw 'Snapshot issue count does not match totalIssueCount.'
    }
    $baselinePath = Join-Path $PSScriptRoot '../config/western_research.json'
    $people = @((Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json -AsHashtable).people)
    if ($people.Count -ne 89) { throw 'Expected the existing approved 89-person baseline; do not substitute a second roster.' }
    $byName = @{}
    $mapped = @{}
    for ($index = 0; $index -lt $people.Count; $index++) {
        $name = [string]$people[$index].display_name
        if ($byName.ContainsKey($name)) { throw "Duplicate baseline display name: $name" }
        $byName[$name] = $index
        $mapped[$name] = [Collections.Generic.List[object]]::new()
    }
    # One shortened name used by existing issue titles; no second person/order map.
    $aliases = @{ '塞克斯都' = '塞克斯都恩披里柯' }
    $byId = @{}
    $unmapped = [Collections.Generic.List[object]]::new()
    $validationProblems = [Collections.Generic.List[string]]::new()
    $knownStatuses = @('Backlog', 'Todo', 'In Progress', 'In Review', 'Done', 'Canceled', 'Cancelled', 'Duplicate')
    foreach ($issue in $issues) {
        foreach ($field in @('id', 'title', 'description', 'status', 'priority', 'createdAt')) {
            if (-not $issue.ContainsKey($field)) { throw "Issue is missing required field '$field'." }
        }
        if ([string]::IsNullOrWhiteSpace($issue.id) -or $byId.ContainsKey([string]$issue.id)) {
            throw "Missing or duplicate issue id: $($issue.id)"
        }
        if ($issue.status -is [Collections.IDictionary]) { $issue.status = $issue.status.name }
        if ($issue.status -notin $knownStatuses) { throw "Unknown status for $($issue.id): $($issue.status)" }
        $issue.dependenciesKnown = $issue.ContainsKey('relations') -and
            $issue.relations -is [Collections.IDictionary] -and $issue.relations.ContainsKey('blockedBy') -and
            $null -ne $issue.relations.blockedBy
        if ($issue.priority -is [Collections.IDictionary]) { $issue.priority = $issue.priority.value }
        $priority = 0
        if (-not [int]::TryParse([string]$issue.priority, [ref]$priority) -or $priority -lt 0 -or $priority -gt 4) {
            throw "Invalid priority for $($issue.id)."
        }
        $issue.priority = $priority
        $issue.createdAt = [DateTimeOffset]::Parse([string]$issue.createdAt, [Globalization.CultureInfo]::InvariantCulture)
        $byId[[string]$issue.id] = $issue
        $issue.personName = $null
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $headers = [regex]::Matches([string]$issue.description, '(?m)^\s*(?:[-*]\s+)?(?:\*\*)?人物\s*[:：]\s*(?:\*\*)?([^（(\r\n]+)')
        if ($headers.Count -gt 0) {
            foreach ($header in $headers) {
                $name = $header.Groups[1].Value.Trim().Trim('*').Trim()
                if ($aliases.ContainsKey($name)) { $name = $aliases[$name] }
                if (-not $byName.ContainsKey($name)) {
                    $validationProblems.Add("Unknown explicit person mapping in $($issue.id): $name")
                } else { [void]$names.Add($name) }
            }
        } else {
            foreach ($name in $byName.Keys) {
                if ([string]$issue.title -like "*$name*") { [void]$names.Add($name) }
            }
            foreach ($alias in $aliases.Keys) {
                if ([string]$issue.title -like "*$alias*") { [void]$names.Add($aliases[$alias]) }
            }
        }
        if ($names.Count -eq 1) {
            $issue.personName = @($names)[0]
            $mapped[$issue.personName].Add($issue)
        } else {
            if ($names.Count -gt 1 -and $issue.status -notin @('Done', 'Canceled', 'Cancelled', 'Duplicate') -and
                $issue.id -notin $AuthorizedEngineeringIssueId) {
                $unmapped.Add((IssueSummary $issue))
                $validationProblems.Add("Ambiguous active person issue $($issue.id); add an accurate existing '人物：' field after review.")
            }
        }
    }
    foreach ($person in $people) {
        if ($mapped[$person.display_name].Count -eq 0) {
            $validationProblems.Add("Missing person issue mapping: $($person.display_name)")
        }
    }
    foreach ($issue in $issues) {
        $issue.blockerIds = $null
        if (-not $issue.dependenciesKnown) { continue }
        $issue.blockerIds = @($issue.relations.blockedBy | ForEach-Object {
            if ($_ -is [string]) { $_ }
            elseif ($_ -is [Collections.IDictionary] -and $_.ContainsKey('id')) { [string]$_.id }
            else { throw "Invalid blockedBy entry in $($issue.id)." }
        })
    }
    $result.unmappedIssues = @($unmapped)
    if ($validationProblems.Count -gt 0) { throw ($validationProblems -join '; ') }

    $earliestIssues = @()
    for ($index = 0; $index -lt $people.Count; $index++) {
        $person = $people[$index]
        $active = @($mapped[$person.display_name] | Where-Object { $_.status -notin @('Canceled', 'Cancelled', 'Duplicate') })
        $unfinished = @($active | Where-Object { $_.status -ne 'Done' })
        # Linear records completion under the baseline primary use. Research
        # delivery is authorized project-wide; titles do not require gameplay.
        if ($unfinished.Count -gt 0 -or $active.Count -eq 0) {
            $result.earliestUnfinished = [ordered]@{
                order = $index + 1
                displayName = $person.display_name
                thinkerId = $person.thinker_id
                primaryUse = $person.primary_use
                issues = @($active | ForEach-Object { IssueSummary $_ })
                researchScopeConflict = $false
            }
            $earliestIssues = $unfinished
            if ($active.Count -eq 0) { $result.reasons += '最早人物仅有Canceled或Duplicate记录，没有有效交付完成证据。' }
            break
        }
    }
    $result.lateInProgress = @($issues | Where-Object {
        $_.status -eq 'In Progress' -and $null -ne $_.personName -and $null -ne $result.earliestUnfinished -and
        ($byName[$_.personName] + 1) -gt $result.earliestUnfinished.order
    } | ForEach-Object { IssueSummary $_ })

    if ($AuthorizedEngineeringIssueId.Count -gt 0) {
        if ([string]::IsNullOrWhiteSpace($EngineeringAuthorizationScope)) {
            throw 'An engineering exception requires an explicit EngineeringAuthorizationScope from the current user authorization.'
        }
        $candidates = @($AuthorizedEngineeringIssueId | ForEach-Object {
            if (-not $byId.ContainsKey($_)) { throw "Authorized engineering issue is absent from snapshot: $_" }
            $byId[$_]
        })
        $selectionKind = 'AuthorizedEngineeringException'
    } else {
        if (-not [string]::IsNullOrWhiteSpace($EngineeringAuthorizationScope)) {
            throw 'EngineeringAuthorizationScope requires an explicit AuthorizedEngineeringIssueId.'
        }
        $candidates = $earliestIssues
        $selectionKind = 'CharacterDelivery'
    }
    $orderedCandidates = @($candidates | Where-Object { $_.status -in @('Todo', 'In Progress') } |
        Sort-Object @{Expression = { if ($_.status -eq 'In Progress') { 0 } else { 1 } }},
            @{Expression = { if ($_.priority -eq 0) { 5 } else { $_.priority } }}, createdAt, id)
    $eligible = @()
    foreach ($candidate in $orderedCandidates) {
        if (-not $candidate.dependenciesKnown) {
            throw "Hydrate relations.blockedBy for candidate $($candidate.id); unknown dependencies are not an empty list."
        }
        foreach ($blockerId in $candidate.blockerIds) {
            if (-not $byId.ContainsKey($blockerId)) {
                throw "Dependency missing from complete snapshot: $($candidate.id) blocked by $blockerId"
            }
        }
        if (@($candidate.blockerIds | Where-Object { $byId[$_].status -ne 'Done' }).Count -eq 0) {
            $eligible = @($candidate)
            break
        }
    }
    if ($eligible.Count -gt 0) {
        $selected = $eligible[0]
        $result.status = 'selected'
        $result.selectedIssue = [ordered]@{
            id = $selected.id; title = $selected.title; status = $selected.status; kind = $selectionKind
            personName = $selected.personName
            authorizedScope = $(if ($selectionKind -eq 'AuthorizedEngineeringException') { $EngineeringAuthorizationScope } else { $null })
        }
    } elseif ($null -eq $result.earliestUnfinished -and $AuthorizedEngineeringIssueId.Count -eq 0) {
        $result.status = 'complete'
        $result.reasons += '快照中已映射人物任务均为Done；本检查器不代替原票按主要用途验收的研究成果或玩法接入、验证与部署证据。'
    } else {
        $result.status = 'blocked'
        foreach ($issue in $candidates) {
            if ($issue.status -notin @('Todo', 'In Progress')) { $result.reasons += "$($issue.id) status=$($issue.status); 未获可执行授权或等待验收。" }
            foreach ($blockerId in $issue.blockerIds) {
                if ($byId[$blockerId].status -ne 'Done') { $result.reasons += "$($issue.id) blockedBy $blockerId ($($byId[$blockerId].status))." }
            }
        }
        if ($result.lateInProgress.Count -gt 0) { $result.reasons += '存在后序人物In Progress；只能按明确授权的必要范围安全收尾，不能自动继续其人物玩法。' }
    }
} catch {
    $result.status = 'invalidSnapshot'
    $result.selectedIssue = $null
    $result.reasons += $_.Exception.Message
}
$result | ConvertTo-Json -Depth 12
if ($result.status -eq 'invalidSnapshot') { exit 2 }
if ($result.status -eq 'blocked') { exit 3 }
