#requires -Version 7.0
<##
Read-only Linear delivery selector: fetch live project pages or validate a supplied
complete snapshot. Summary by default; Diagnostic preserves the original result.
Never mutates Linear or certifies deployment; Done still requires delivery evidence.
Exit codes: 0 selected/complete, 2 invalid snapshot, 3 blocked.
##>
[CmdletBinding(DefaultParameterSetName = 'Live')]
param(
    [Parameter(ParameterSetName = 'Snapshot', Mandatory)][string]$IssueSnapshotPath,
    [Parameter(ParameterSetName = 'Live')][string]$ProjectId = 'b0a5ebc4-5f7b-4ac0-a5ef-a09c01f053f4',
    [ValidateSet('Summary', 'Json', 'Diagnostic')][string]$OutputFormat = 'Summary',
    [ValidateRange(3, 4)][int]$BatchSize = 4,
    [switch]$AllowDeployment,
    [string[]]$AuthorizedEngineeringIssueId = @(),
    [string]$EngineeringAuthorizationScope,
    [string]$BaselineCommit = '',
    [string[]]$IntegratedDependency = @()
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

# These lines index acceptance evidence in Linear; they do not authenticate a
# reviewer or certify code. Release must re-read comments and verify integrated scope.
function HasReviewMarker($issue) {
    return [regex]::IsMatch([string]$issue.description, '(?m)^[ \t]*(?:待部署提交|验收通过提交)[：:]')
}
function HasMatchingAcceptance($issue) {
    if ($issue.status -ne 'In Review') { return $false }
    $text = [string]$issue.description
    foreach ($label in @('待部署提交', '验收通过提交')) {
        if ([regex]::Matches($text, "(?m)^[ \t]*${label}[：:]").Count -ne 1) { return $false }
    }
    $pendingMatch = [regex]::Match($text, '(?m)^[ \t]*待部署提交[：:][ \t]*([0-9a-fA-F]{40})[ \t]*\r?$')
    $reviewMatch = [regex]::Match($text, '(?m)^[ \t]*验收通过提交[：:][ \t]*([0-9a-fA-F]{40})[ \t]*\r?$')
    return $pendingMatch.Success -and $reviewMatch.Success -and
        $pendingMatch.Groups[1].Value -eq $reviewMatch.Groups[1].Value
}

function Get-HandoffShas($issue) {
    $found = [regex]::Matches([string]$issue.description, '(?m)^[ \t]*(?:待部署提交|验收通过提交|交付提交|实现提交)[：:][ \t]*([0-9a-fA-F]{40})[ \t]*\r?$')
    return @($found | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
}

# Development needs the prerequisite implementation in this baseline, not its
# release verdict. Acceptance, deployment and Done remain release prerequisites.
function Test-DevelopmentReady($issue) {
    if ($issue.status -eq 'Done') { return $true }
    if ($IntegratedDependency -contains $issue.id) { return $true }
    if ([string]::IsNullOrWhiteSpace($BaselineCommit)) { return $false }
    foreach ($sha in (Get-HandoffShas $issue)) {
        & git -C $repositoryRoot merge-base --is-ancestor $sha $BaselineCommit 2>$null
        if ($LASTEXITCODE -eq 0) { return $true }
    }
    return $false
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot

# The key stays in the process environment; neither responses nor credentials are logged.
$live = $PSCmdlet.ParameterSetName -eq 'Live'
function InvokeLinear([string]$query, [hashtable]$variables) {
    if ([string]::IsNullOrWhiteSpace($env:LINEAR_API_KEY)) {
        throw 'Live selection needs LINEAR_API_KEY in the environment; alternatively supply a fresh complete snapshot via IssueSnapshotPath (connected tools may export it without printing issue bodies).'
    }
    try {
        $response = Invoke-RestMethod -Uri 'https://api.linear.app/graphql' -Method Post -ContentType 'application/json' -TimeoutSec 60 `
            -Headers @{ Authorization = $env:LINEAR_API_KEY } -Body (@{ query = $query; variables = $variables } | ConvertTo-Json -Depth 10)
    } catch { throw 'Linear request failed (HTTP/auth/network); no task selected.' }
    if ($response.PSObject.Properties.Name -contains 'errors' -and @($response.errors).Count -gt 0) {
        throw 'Linear returned GraphQL errors; partial data cannot authorize work.'
    }
    if ($null -eq $response.data) { throw 'Linear returned no data.' }
    return $response.data
}
function NextCursor($connection, $seen) {
    if ($connection.pageInfo.hasNextPage -isnot [bool]) { throw 'Invalid Linear pageInfo.' }
    if (-not $connection.pageInfo.hasNextPage) { return $null }
    $cursor = [string]$connection.pageInfo.endCursor
    if ([string]::IsNullOrWhiteSpace($cursor) -or -not $seen.Add($cursor)) { throw 'Missing or repeated Linear pagination cursor.' }
    return $cursor
}
function ReadLiveSnapshot {
    $all = [Collections.Generic.List[object]]::new()
    $cursor = $null
    $seen = [Collections.Generic.HashSet[string]]::new()
    do {
        $data = InvokeLinear 'query DeliveryProject($project: String!, $after: String) {
          project(id: $project) { id issues(first: 100, after: $after, includeArchived: true) {
            nodes { identifier title description priority createdAt state { name } }
            pageInfo { hasNextPage endCursor }
          } }
        }' @{ project = $ProjectId; after = $cursor }
        if ($null -eq $data.project) { throw 'Linear project not found or inaccessible.' }
        foreach ($node in $data.project.issues.nodes) {
            $all.Add(@{ id = $node.identifier; title = $node.title; description = $node.description
                priority = $node.priority; createdAt = $node.createdAt; status = $node.state.name })
        }
        $cursor = NextCursor $data.project.issues $seen
    } while ($null -ne $cursor)
    return @{ complete = $true; totalIssueCount = $all.Count; issues = @($all) }
}
function HydrateDependencies($candidate) {
    if (-not $live -or $candidate.dependenciesKnown) { return }
    $blockers = [Collections.Generic.List[string]]::new()
    $cursor = $null
    $seen = [Collections.Generic.HashSet[string]]::new()
    do {
        $data = InvokeLinear 'query DeliveryDependencies($id: String!, $after: String) {
          issue(id: $id) { inverseRelations(first: 100, after: $after, includeArchived: true) {
            nodes { type issue { identifier state { name } } }
            pageInfo { hasNextPage endCursor }
          } }
        }' @{ id = $candidate.id; after = $cursor }
        if ($null -eq $data.issue) { throw "Missing candidate: $($candidate.id)" }
        foreach ($relation in $data.issue.inverseRelations.nodes) {
            if ($relation.type -ne 'blocks') { continue }
            $blockerId = [string]$relation.issue.identifier
            if ([string]::IsNullOrWhiteSpace($blockerId)) { throw 'Incomplete dependency response.' }
            $blockers.Add($blockerId)
            if (-not $byId.ContainsKey($blockerId)) {
                # External-project prerequisites participate in dependency checks only.
                $byId[$blockerId] = @{ id = $blockerId; status = $relation.issue.state.name }
            }
        }
        $cursor = NextCursor $data.issue.inverseRelations $seen
    } while ($null -ne $cursor)
    $candidate.blockerIds = @($blockers)
    $candidate.dependenciesKnown = $true
}

try {
    $snapshot = if ($live) { ReadLiveSnapshot } else { Get-Content -LiteralPath $IssueSnapshotPath -Raw | ConvertFrom-Json -AsHashtable }
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
    $knownStatuses = @('Backlog', 'Todo', 'In Progress', 'In Review', 'Done', 'Canceled', 'Cancelled', 'Duplicate', 'Triage')
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

    # Only independently accepted gameplay is eligible for a release batch.
    $pending = @($issues | Where-Object {
        $null -ne $_.personName -and
        $people[$byName[$_.personName]].primary_use -ne '研究' -and
        (HasMatchingAcceptance $_)
    })
    # Implementation continues while Sol reviews. In Review means implementation
    # handed off under this protocol, not an instruction to block the work queue.
    $implementedIds = @($issues | Where-Object {
        $null -ne $_.personName -and $people[$byName[$_.personName]].primary_use -ne '研究' -and
        ($_.status -eq 'In Review' -or ($_.status -eq 'In Progress' -and
        [regex]::IsMatch([string]$_.description, '(?m)^[ \t]*待部署提交[：:][ \t]*[0-9a-fA-F]{7,40}(?=[ \t\r\n、,，]|$)')))
    } | ForEach-Object { $_.id })
    $pendingIds = @($pending | ForEach-Object { $_.id })
    $result.pendingDeployment = @($pending | ForEach-Object { IssueSummary $_ })
    $earliestIssues = @()
    for ($index = 0; $index -lt $people.Count; $index++) {
        $person = $people[$index]
        $active = @($mapped[$person.display_name] | Where-Object { $_.status -notin @('Canceled', 'Cancelled', 'Duplicate') })
        $unfinished = @($active | Where-Object { $_.status -ne 'Done' -and $_.id -notin $implementedIds })
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
        $_.status -eq 'In Progress' -and $_.id -notin $implementedIds -and $null -ne $_.personName -and $null -ne $result.earliestUnfinished -and
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
    $batchReady = $AllowDeployment -and $AuthorizedEngineeringIssueId.Count -eq 0 -and $pending.Count -ge $BatchSize
    foreach ($candidate in $(if ($batchReady) { @() } else { $orderedCandidates })) {
        HydrateDependencies $candidate
        if (-not $candidate.dependenciesKnown) {
            throw "Hydrate relations.blockedBy for candidate $($candidate.id); unknown dependencies are not an empty list."
        }
        foreach ($blockerId in $candidate.blockerIds) {
            if (-not $byId.ContainsKey($blockerId)) {
                throw "Dependency missing from complete snapshot: $($candidate.id) blocked by $blockerId"
            }
        }
        if (@($candidate.blockerIds | Where-Object { -not (Test-DevelopmentReady $byId[$_]) }).Count -eq 0) {
            $eligible = @($candidate)
            break
        }
    }
    $releaseProblems = @()
    if ($AllowDeployment -and $AuthorizedEngineeringIssueId.Count -eq 0 -and $pending.Count -gt 0) {
        # Acceptance does not waive real prerequisites of release candidates.
        foreach ($releaseIssue in $pending) {
            HydrateDependencies $releaseIssue
            if (-not $releaseIssue.dependenciesKnown) { throw "Hydrate relations.blockedBy for release candidate $($releaseIssue.id)." }
            foreach ($blockerId in $releaseIssue.blockerIds) {
                if (-not $byId.ContainsKey($blockerId)) { throw "Missing release dependency: $blockerId" }
                if ($byId[$blockerId].status -ne 'Done') { $releaseProblems += "$($releaseIssue.id) blockedBy $blockerId ($($byId[$blockerId].status)); cannot release." }
            }
        }
    }
    if ($AllowDeployment -and $AuthorizedEngineeringIssueId.Count -eq 0 -and $pending.Count -gt 0 -and
        ($releaseProblems.Count -gt 0 -or $pending.Count -ge $BatchSize -or $eligible.Count -eq 0)) {
        if ($releaseProblems.Count -gt 0) { $result.status = 'blocked'; $result.reasons += $releaseProblems }
        else { $result.status = 'deployBatch' }
    } elseif ($eligible.Count -gt 0) {
        $selected = $eligible[0]
        $result.status = 'selected'
        $result.selectedIssue = [ordered]@{
            id = $selected.id; title = $selected.title; status = $selected.status; kind = $selectionKind
            personName = $selected.personName
            authorizedScope = $(if ($selectionKind -eq 'AuthorizedEngineeringException') { $EngineeringAuthorizationScope } else { $null })
        }
    } elseif ($null -eq $result.earliestUnfinished -and $implementedIds.Count -gt 0 -and $AuthorizedEngineeringIssueId.Count -eq 0) {
        $result.status = 'awaitingReview'
        $result.reasons += '现有实现已交接，审查与发布异步；无新增实现项，不持续轮询或重复汇报。'
    } elseif ($null -eq $result.earliestUnfinished -and $AuthorizedEngineeringIssueId.Count -eq 0) {
        $result.status = 'complete'
        $result.reasons += '快照中已映射人物任务均为Done；本检查器不代替原票按主要用途验收的研究成果或玩法接入、验证与部署证据。'
    } else {
        $result.status = 'blocked'
        foreach ($issue in $candidates) {
            if ($issue.status -notin @('Todo', 'In Progress')) { $result.reasons += "$($issue.id) status=$($issue.status); 未获可执行授权或等待验收。" }
            foreach ($blockerId in $issue.blockerIds) {
                if (-not (Test-DevelopmentReady $byId[$blockerId])) { $result.reasons += "$($issue.id) blockedBy $blockerId ($($byId[$blockerId].status); 实现未集成或缺失)." }
            }
        }
        if ($result.lateInProgress.Count -gt 0) { $result.reasons += '存在后序人物In Progress；只能按明确授权的必要范围安全收尾，不能自动继续其人物玩法。' }
    }
} catch {
    $result.status = 'invalidSnapshot'
    $result.selectedIssue = $null
    $result.reasons += $_.Exception.Message
}
$pendingCount = if ($result.Contains('pendingDeployment')) { @($result.pendingDeployment).Count } else { 0 }
$nextAction = switch ($result.status) {
    'selected' { if ($result.selectedIssue.status -eq 'In Progress') { '继续当前任务实现' } else { '领取当前任务并执行' } }
    'deployBatch' { '复核Sol验收评论及集成提交，综合验证后按当前部署授权交付本批；通过后关闭对应任务' }
    'awaitingReview' { '实现队列已交接；验收异步，无新任务时结束本轮，不空转轮询' }
    'complete' { '当前人物队列已完成' }
    default { '处理上述阻塞；不领取后序人物' }
}
$summary = [ordered]@{
    status = $result.status
    currentTask = $result.selectedIssue
    order = $(if ($null -ne $result.selectedIssue -and $null -ne $result.selectedIssue.personName) { $byName[$result.selectedIssue.personName] + 1 } else { $null })
    primaryUse = $(if ($null -ne $result.selectedIssue -and $null -ne $result.selectedIssue.personName) { $people[$byName[$result.selectedIssue.personName]].primary_use } else { $null })
    blockers = @($result.reasons)
    pendingDeploymentCount = $pendingCount
    pendingDeployment = @(if ($result.status -eq 'deployBatch') { $result.pendingDeployment })
    nextAction = $nextAction
}
if ($OutputFormat -eq 'Diagnostic') { $result | ConvertTo-Json -Depth 12 }
elseif ($OutputFormat -eq 'Json') { $summary | ConvertTo-Json -Depth 8 -Compress }
else {
    $task = if ($null -ne $result.selectedIssue) { "$($result.selectedIssue.id) $($result.selectedIssue.title)" } else { '无' }
    "当前任务：$task"
    "既定顺序：$(if ($null -ne $summary.order) { '第' + $summary.order + '位（' + $summary.primaryUse + '）' } else { '不适用' })"
    "状态：$(if ($null -ne $result.selectedIssue) { $result.selectedIssue.status } else { $result.status })"
    "阻塞：$(if ($result.reasons.Count -eq 0) { '无' } else { $result.reasons -join '; ' })"
    "本批待部署：$pendingCount 人"
    if ($result.status -eq 'deployBatch') { "待部署任务：$($pendingIds -join '、')" }
    "下一动作：$nextAction"
}
if ($result.status -eq 'invalidSnapshot') { exit 2 }
if ($result.status -eq 'blocked') { exit 3 }
