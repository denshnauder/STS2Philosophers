#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$selector = Join-Path $PSScriptRoot '../tools/SelectDeliveryIssue.ps1'
$people = @((Get-Content -LiteralPath (Join-Path $PSScriptRoot '../config/western_research.json') -Raw | ConvertFrom-Json).people)
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('STS2DeliverySelection_' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $temporaryDirectory)
$caseCount = 0

function NewSnapshot {
    $issues = @($people | ForEach-Object -Begin { $number = 0 } -Process {
        $number++
        [ordered]@{
            id = "PERSON$number"; title = "按既定用途交付$($_.display_name)"
            description = "人物：$($_.display_name)（$($_.international_name)）`n验收：合法接入、实际内容、验证和安全部署。"
            status = 'Done'; priority = 3; createdAt = '2026-10-01T00:00:00Z'
            relations = @{ blockedBy = @() }
        }
    })
    @{ complete = $true; issues = $issues }
}

function RunCase($snapshot, [string[]]$additionalArguments = @()) {
    $path = Join-Path $temporaryDirectory 'snapshot.json'
    $snapshot | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $path -Encoding utf8
    $raw = & (Get-Process -Id $PID).Path -NoProfile -File $selector -IssueSnapshotPath $path @additionalArguments
    $code = $LASTEXITCODE
    $output = ($raw -join "`n") | ConvertFrom-Json
    if (($output.status -eq 'selected' -or $output.status -eq 'complete') -and $code -ne 0) { throw 'Successful selection returned a nonzero exit code.' }
    if ($output.status -eq 'invalidSnapshot' -and $code -ne 2) { throw 'Invalid snapshot did not return exit code 2.' }
    if ($output.status -eq 'blocked' -and $code -ne 3) { throw 'Blocked selection did not return exit code 3.' }
    return $output
}

function AssertCase($condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:caseCount++
}

try {
    $snapshot = NewSnapshot
    $snapshot.issues[1].status = 'Todo'; $snapshot.issues[1].priority = 4
    $snapshot.issues[27].status = 'Todo'; $snapshot.issues[27].priority = 1
    $snapshot.issues[27].createdAt = '2026-01-01T00:00:00Z'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON2' -and $output.earliestUnfinished.order -eq 2) 'Later high-priority/older person displaced the earlier low-priority person.'

    $snapshot.issues[1].priority = @{ value = 4; name = 'Low' }
    $snapshot.issues[27].priority = @{ value = 1; name = 'Urgent' }
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON2') 'Native Linear priority objects changed the original person order.'

    $snapshot.issues[50].Remove('relations')
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON2') 'Unknown dependencies on an unrelated unselected issue blocked the current hydrated candidate.'

    $snapshot.issues[1].Remove('relations')
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'invalidSnapshot' -and $null -eq $output.selectedIssue -and ($output.reasons -join '') -match 'Hydrate') 'Unknown dependencies of the actual candidate were treated as an empty list.'
    $snapshot.issues[1].relations = @{ blockedBy = @() }

    $snapshot.issues[27].status = 'In Progress'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON2' -and $output.lateInProgress.Count -eq 1) 'A later person In Progress bypassed the original order.'

    $snapshot.issues[1].status = 'Backlog'
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'blocked' -and $null -eq $output.selectedIssue -and $output.earliestUnfinished.order -eq 2) 'Earlier Backlog was ignored while selecting a later person.'

    $snapshot = NewSnapshot
    $snapshot.issues[1].status = 'Todo'
    $snapshot.issues[1].relations.blockedBy = @(@{ id = 'ENGINEERING' })
    $snapshot.issues += @{ id = 'ENGINEERING'; title = '共享入口修复'; description = '公共修复'; status = 'Todo'; priority = 1; createdAt = '2026-10-01T00:00:00Z'; relations = @{ blockedBy = @() } }
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'blocked' -and ($output.reasons -join '') -match 'blockedBy ENGINEERING') 'An unfinished real dependency did not block the character.'

    $snapshot.issues[89].status = 'Done'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON2') 'A completed dependency did not release the earliest authorized person.'

    $snapshot.issues[89].status = 'Canceled'
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'blocked') 'Canceled prerequisite was treated as delivered.'

    $snapshot.issues = @($snapshot.issues | Where-Object { $_.id -ne 'ENGINEERING' })
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'invalidSnapshot' -and $null -eq $output.selectedIssue) 'A dependency absent from the complete snapshot was silently ignored.'

    $snapshot = NewSnapshot
    $snapshot.complete = $false
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'invalidSnapshot') 'An incomplete snapshot was accepted.'

    $snapshot = NewSnapshot
    $snapshot.totalIssueCount = 90
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'invalidSnapshot') 'A truncated snapshot count was accepted.'

    $snapshot = NewSnapshot
    $snapshot.issues = @($snapshot.issues | Where-Object { $_.id -ne 'PERSON1' })
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'invalidSnapshot' -and ($output.reasons -join '') -match '泰勒斯') 'Missing earlier person mapping did not fail closed.'

    $snapshot = NewSnapshot
    $snapshot.issues[0].description = '人物：泰勒斯（Thales）' + "`n无合法入池方式则完成角色定义。"
    $snapshot.issues[27].status = 'In Progress'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON28' -and $output.earliestUnfinished.order -eq 28) 'Completed research was reopened solely because of research wording.'

    $snapshot.issues[0].status = 'Todo'
    $snapshot.issues[0].title = '完成并交付泰勒斯既定人物用途'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON1' -and -not $output.earliestUnfinished.researchScopeConflict) 'An authorized research primary use required another approval because of its delivery title.'

    $snapshot.issues[27].id = 'ENC123'
    $scope = '仅公共生产入口修复，不新增马基雅维利玩法或认定其人物交付。'
    $output = RunCase $snapshot @('-AuthorizedEngineeringIssueId', 'ENC123', '-EngineeringAuthorizationScope', $scope)
    AssertCase ($output.status -eq 'selected' -and $output.selectedIssue.id -eq 'ENC123' -and $output.selectedIssue.kind -eq 'AuthorizedEngineeringException' -and $output.selectedIssue.authorizedScope -eq $scope -and $output.earliestUnfinished.order -eq 1) 'ENC123 engineering exception lost its explicit scope or falsely released the later character.'

    $output = RunCase $snapshot @('-AuthorizedEngineeringIssueId', 'ENC123')
    AssertCase ($output.status -eq 'invalidSnapshot' -and $null -eq $output.selectedIssue) 'An engineering exception without a user authorization scope was accepted.'

    $snapshot = NewSnapshot
    $snapshot.issues[3].description += "`n旧模板：若研究专用或无当前合法入池方式，则完成角色定义。"
    $snapshot.issues[4].status = 'Todo'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON5' -and $output.earliestUnfinished.order -eq 5) 'Generic research wording falsely reopened an already Done non-research person.'

    $snapshot = NewSnapshot
    $snapshot.issues[0].description = "人物：泰勒斯（Thales）`n按原定研究用途完成，无部署。"
    $snapshot.issues[0].title = '完成并交付泰勒斯既定人物用途'
    $snapshot.issues[1].status = 'Todo'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON2') 'Completed primary research required a renamed task or repeated individual permission.'

    $snapshot = NewSnapshot
    $snapshot.issues[7].status = 'Todo'
    $snapshot.issues[7].title = '完成并交付留基伯既定人物用途'
    $snapshot.issues[7].description = "人物：留基伯（Leucippus）`n仅研究。"
    $snapshot.issues[8].status = 'Todo'; $snapshot.issues[8].priority = 1
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON8') 'A later primary research person was skipped or blocked by gameplay requirements.'
    $snapshot.issues[7].status = 'Done'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON9') 'The queue did not continue after the next primary research delivery.'

    $snapshot = NewSnapshot
    $snapshot.issues[1].status = 'Todo'
    $snapshot.issues[1].description += "`n研究兼任；快照RESEARCH_ONLY；尚未接入。"
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON2' -and $output.earliestUnfinished.primaryUse -eq '问号') 'Secondary research or RESEARCH_ONLY snapshot was treated as completed primary research.'

    $snapshot = NewSnapshot
    $snapshot.issues[21].description = ''; $snapshot.issues[21].title = '交付塞克斯都理由并置'; $snapshot.issues[21].status = 'Todo'
    $output = RunCase $snapshot
    AssertCase ($output.selectedIssue.id -eq 'PERSON22' -and $output.earliestUnfinished.displayName -eq '塞克斯都恩披里柯') 'The existing shortened Sextus title was not mapped to the original roster.'

    $snapshot = NewSnapshot
    $snapshot.issues[0].description = '人物：未知人物（Unknown）'
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'invalidSnapshot') 'An unknown explicit person mapping was guessed from the title.'

    $snapshot = NewSnapshot
    $output = RunCase $snapshot
    AssertCase ($output.status -eq 'complete' -and $null -eq $output.selectedIssue) 'A complete terminal queue was not reported without fabricating work.'
    "Delivery selection checks passed: $caseCount cases."
} finally {
    # Remove only this test's fresh, absolute temporary directory.
    if (-not $temporaryDirectory.StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test temporary directory.' }
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
}
