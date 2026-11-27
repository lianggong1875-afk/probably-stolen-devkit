<#
.SYNOPSIS
    合并门禁：把「编译 / 零能力扫描 / 离线回归 / 跨模组冲突 / 工作区洁净」五道检查一次跑完。

.DESCRIPTION
    规范见 共享开发\分支与备份规范.md 第 3 节。任何一道红就停下（除非 -KeepGoing），
    并把该道的原始输出原样打印出来——不吞错误、不概括结论。

    真机验证（Slot 1）不在这里做：那需要人进游戏，脚本替不了。
    发布到工坊之前必须有真机结论，没验就在 PROGRESS.md 写「未真机验证」。

.PARAMETER Mod
    Arcade / Freya / All（默认 All，两个项目都过一遍门禁）。

.PARAMETER KeepGoing
    某一道失败也继续跑后面的（便于一次看全所有问题）。

.PARAMETER IncludeWorkshop
    冲突扫描时把 缓存文件夹\decomp_ws 也纳入（仅在反编译产物还在时有用，通常已清空）。

.EXAMPLE
    .\工具库\build\Invoke-PreMergeGate.ps1 -Mod Arcade
#>
param(
    [ValidateSet('Arcade', 'Freya', 'All')][string]$Mod = 'All',
    [switch]$KeepGoing,
    [switch]$IncludeWorkshop,
    [switch]$SkipConflictScan
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$buildScript = Join-Path $repoRoot '工具库\build\Build-Mod.ps1'
$regScript = Join-Path $repoRoot '工具库\regression\Run-Regression.ps1'
$scanScript = Join-Path $repoRoot '工具库\workshop-tools\conflict_scan.py'
$kbScript = Join-Path $repoRoot '工具库\kb-tools\kb_pipeline.py'
$reportPath = Join-Path $repoRoot '缓存文件夹\conflict-report-gate.md'

$failures = New-Object System.Collections.ArrayList
$gateNo = 0

function Header([string]$title) {
    Write-Host ''
    Write-Host ('=' * 62) -ForegroundColor DarkGray
    Write-Host $title -ForegroundColor White
    Write-Host ('=' * 62) -ForegroundColor DarkGray
}

function Record-Fail([string]$gate, [string]$detail, [string]$raw) {
    [void]$failures.Add("$gate：$detail")
    Write-Host "[FAIL] $gate — $detail" -ForegroundColor Red
    if ($raw) { Write-Host "----- 原始输出 -----" -ForegroundColor DarkYellow; Write-Host $raw }
    else { }
}

function Record-Ok([string]$gate) { Write-Host "[PASS] $gate" -ForegroundColor Green }

function Resolve-Python {
    $candidates = @(
        $env:PS_PYTHON,
        'D:\0TavernWeave\tools\python314\python.exe',
        (Get-Command python -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    return ($candidates | Select-Object -First 1)
}

Push-Location $repoRoot
try {
    # ---------------- 门禁 1+2：编译（开发版 / 正式候选+零能力） ----------------
    $mods = if ($Mod -eq 'All') { @('Arcade', 'Freya') } else { @($Mod) }

    foreach ($m in $mods) {
        $gateNo++
        $gate = "门禁1 开发版编译[$m]"
        Header $gate
        $raw = ''
        try {
            $raw = (& $buildScript -Mod $m *>&1 | Out-String)
            Write-Host $raw
            if ($raw -notmatch '编译成功') { Record-Fail $gate '未出现「编译成功」' $raw; if (-not $KeepGoing) { break } }
            else { Record-Ok $gate }
        }
        catch {
            Record-Fail $gate $_.Exception.Message $raw
            if (-not $KeepGoing) { break }
        }

        $gateNo++
        $gate = "门禁2 正式候选+零能力[$m]"
        Header $gate
        $raw = ''
        try {
            $raw = (& $buildScript -Mod $m -PublicRelease -Scan *>&1 | Out-String)
            Write-Host $raw
            $refusedOk = ($raw -match 'Refused:\s*False')
            # Capabilities 行后面必须为空（允许后面还有 Extra capabilities 行）
            $capOk = ($raw -match '(?m)^Capabilities:\s*$')
            if (-not $refusedOk) { Record-Fail $gate 'Refused 不是 False（携带未声明能力）' $raw; if (-not $KeepGoing) { break } }
            elseif (-not $capOk) { Record-Fail $gate 'Capabilities 非空（正式候选必须零能力）' $raw; if (-not $KeepGoing) { break } }
            else { Record-Ok $gate }
        }
        catch {
            Record-Fail $gate $_.Exception.Message $raw
            if (-not $KeepGoing) { break }
        }
    }

    $shouldStop = ($failures.Count -gt 0 -and -not $KeepGoing)

    # ---------------- 门禁 3：离线回归 ----------------
    if (-not $shouldStop) {
        $gateNo++
        $gate = "门禁3 离线回归[$Mod]"
        Header $gate
        if (-not (Test-Path -LiteralPath $regScript)) {
            Record-Fail $gate "找不到 $regScript" ''
        }
        else {
            $raw = (& $regScript -Mod $Mod *>&1 | Out-String)
            Write-Host $raw
            if ($raw -match 'ALL SUITES PASS') { Record-Ok $gate }
            else { Record-Fail $gate '未出现 ALL SUITES PASS' $raw }
            if ($failures.Count -gt 0 -and -not $KeepGoing) { $shouldStop = $true }
        }
    }

    # ---------------- 门禁 4：跨模组冲突扫描 ----------------
    if (-not $shouldStop) {
        $gateNo++
        $gate = '门禁4 跨模组冲突扫描'
        Header $gate
        $py = Resolve-Python
        if ($SkipConflictScan) {
            Write-Host "[SKIP] 按 -SkipConflictScan 跳过冲突扫描" -ForegroundColor Yellow
        }
        elseif (-not $py) {
            Record-Fail $gate '找不到 python（设置 PS_PYTHON 或确保 python 在 PATH 中；临时跳过请显式传 -SkipConflictScan）' ''
            if ($failures.Count -gt 0 -and -not $KeepGoing) { $shouldStop = $true }
        }
        elseif (-not (Test-Path -LiteralPath $scanScript)) {
            Record-Fail $gate "找不到 $scanScript" ''
            if ($failures.Count -gt 0 -and -not $KeepGoing) { $shouldStop = $true }
        }
        else {
            $args = @($scanScript,
                '--dir', '深空电玩\src',
                '--dir', '深空当铺：可能是群友\src',
                '--dir', '深空桥接器\src',
                '--gate',
                '--out', $reportPath)
            if ($IncludeWorkshop) { $args += @('--dir', '缓存文件夹\decomp_ws') }

            $raw = (& $py @args *>&1 | Out-String)
            Write-Host $raw
            if ($raw -match 'ERROR=(\d+)') {
                $errCount = [int]$Matches[1]
                if ($errCount -gt 0) { Record-Fail $gate "有 $errCount 条 ERROR（详见 $reportPath）" '' }
                else { Record-Ok $gate }
            }
            else { Record-Fail $gate '扫描脚本没输出 ERROR= 计数，无法判定' $raw }
            if ($failures.Count -gt 0 -and -not $KeepGoing) { $shouldStop = $true }
        }
    }

    # ---------------- 门禁 4b：知识库 / 模板联动校验（仅相关改动触发） ----------------
    if (-not $shouldStop -and (Test-Path -LiteralPath $kbScript)) {
        $changed = @()
        try {
            $c1 = (& git diff --name-only main...HEAD 2>$null)
            $c2 = (& git diff --name-only 2>$null)
            $c3 = (& git diff --cached --name-only 2>$null)
            $changed = @($c1) + @($c2) + @($c3) | Where-Object { $_ } | Select-Object -Unique
        } catch { $changed = @() }
        $touchKb = @($changed | Where-Object { $_ -match '(模组制作经验库|工具库/skills/|工具库/templates/mod-patterns/)' })
        $touchTpl = @($changed | Where-Object { $_ -match '工具库/templates/mod-patterns/' })
        if ($touchKb.Count -gt 0) {
            $gateNo++
            $gate = '门禁4b 知识库审计[kb_pipeline]'
            Header $gate
            $py = Resolve-Python
            if (-not $py) {
                Record-Fail $gate '触及经验库/技能/模板但找不到 python' ''
            }
            else {
                $raw = (& $py $kbScript 'audit' *>&1 | Out-String)
                Write-Host $raw
                if ($LASTEXITCODE -ne 0 -or $raw -notmatch 'ERROR=0') {
                    Record-Fail $gate 'kb_pipeline audit 未通过' $raw
                }
                else { Record-Ok $gate }

                if ($touchTpl.Count -gt 0 -and ($failures.Count -eq 0 -or $KeepGoing)) {
                    $gateNo++
                    $gate2 = '门禁4c 模板编译与零能力实测[verify-templates]'
                    Header $gate2
                    $raw2 = (& $py $kbScript 'verify-templates' *>&1 | Out-String)
                    Write-Host $raw2
                    if ($LASTEXITCODE -ne 0) { Record-Fail $gate2 'verify-templates 失败' $raw2 }
                    else { Record-Ok $gate2 }
                }
            }
            if ($failures.Count -gt 0 -and -not $KeepGoing) { $shouldStop = $true }
        }
    }

    # ---------------- 门禁 5：工作区洁净 ----------------
    if (-not $shouldStop) {
        $gateNo++
        $gate = '门禁5 工作区洁净'
        Header $gate
        $dirty = (& git status --porcelain)
        if ($dirty) {
            Write-Host ($dirty -join "`n")
            Record-Fail $gate '有未提交改动（合并前应 commit 或明确排除）' ''
        }
        else { Record-Ok $gate }
    }

    # ---------------- 汇总 ----------------
    Write-Host ''
    Write-Host ('=' * 62) -ForegroundColor DarkGray
    if ($failures.Count -eq 0) {
        Write-Host '门禁结果：全部通过（PASS）' -ForegroundColor Green
        Write-Host '下一步：git switch main ; git merge --no-ff <分支> ; 再跑 Backup-All.ps1' -ForegroundColor Gray
        exit 0
    }
    else {
        Write-Host ("门禁结果：未通过（{0} 项失败）" -f $failures.Count) -ForegroundColor Red
        $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        exit 1
    }
}
finally {
    Pop-Location
}
