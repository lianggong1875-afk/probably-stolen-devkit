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

# 编码铁律：本机是中文 Windows + Windows PowerShell 5.1。
# 不显式设 UTF-8 时，Python（kb_pipeline / conflict_scan）写出的中文会按 GBK 解码成一片乱码，
# 既没法读日志，也会让下面基于正则的判定在含中文的输出上失准。
# PYTHONUTF8 让 Python 用 UTF-8 处理 argv 与 stdio；OutputEncoding 让 PowerShell 也按 UTF-8 解回来。
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# 是否运行在 git worktree（链接式工作树）里：git-dir 与 git-common-dir 不同即为 worktree。
# 这类检出常用于「主工作区正被并发编辑时，在干净快照上复验」，它的技能副本天生落后于 DSH_HOME。
$inLinkedWorktree = $false
try {
    $gd = (& git rev-parse --git-dir 2>$null)
    $gcd = (& git rev-parse --git-common-dir 2>$null)
    if ($gd -and $gcd -and ($gd.Trim() -ne $gcd.Trim())) { $inLinkedWorktree = $true }
} catch { }
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

# 选定一个「真能用」的 Python：不仅要存在，还要能在本工作区写文件。
#
# 为什么必须自检：本机曾经把 D:\0TavernWeave\tools\python314\python.exe（另一个项目的 3.14）
# 硬编码在候选最前。它存在、能 import、能读文件，但在某些目录（例如 git worktree 的 缓存文件夹）
# 写盘会被拒（PermissionError: Errno 13），于是 conflict_scan 一落报告就崩，
# 门禁 4 报成「扫描脚本没输出 ERROR= 计数」——看起来像代码冲突，实际是解释器选错。
# 现在改为：PATH 上的 python 优先，别项目的路径只作最后兜底，且每个候选都必须过写盘自检。
$script:ResolvedPython = $null
function Resolve-Python {
    if ($script:ResolvedPython) { return $script:ResolvedPython }

    $candidates = @(
        $env:PS_PYTHON,
        (Get-Command python -ErrorAction SilentlyContinue | Select-Object -First 1).Source,
        (Get-Command python3 -ErrorAction SilentlyContinue | Select-Object -First 1).Source,
        'D:\0TavernWeave\tools\python314\python.exe'
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

    $probeDir = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) '缓存文件夹'
    if (-not (Test-Path -LiteralPath $probeDir)) {
        New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    }

    foreach ($c in $candidates) {
        $probe = Join-Path $probeDir ('_pychk_' + [guid]::NewGuid().ToString('N').Substring(0, 8) + '.tmp')
        # 用环境变量传路径：避免中文路径被嵌进 -c 字符串时引发二次转义问题。
        $env:DSH_PY_PROBE = $probe
        try {
            & $c -c "import os,io;io.open(os.environ['DSH_PY_PROBE'],'w',encoding='utf-8').write('ok')" 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $probe)) {
                $script:ResolvedPython = $c
                return $c
            }
        } catch { }
        finally {
            Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
            Remove-Item Env:\DSH_PY_PROBE -ErrorAction SilentlyContinue
        }
        Write-Host "[WARN] 跳过不可用的 python（写不进工作区缓存目录）：$c" -ForegroundColor Yellow
    }
    return $null
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
            # 预建报告目录：干净克隆 / 新建 worktree 里没有 缓存文件夹，
            # Python 会因写不进报告抛异常，然后被误判成「没输出 ERROR= 计数」。
            $reportDir = Split-Path -Parent $reportPath
            if ($reportDir -and -not (Test-Path -LiteralPath $reportDir)) {
                New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
            }

            # 注意：不能叫 $args —— 那是 PowerShell 的自动变量（函数的参数数组），
            # 覆盖它会让 @splat 行为在别的作用域里变味。用 $cliArgs。
            $cliArgs = @($scanScript,
                '--dir', '深空电玩\src',
                '--dir', '深空当铺：可能是群友\src',
                '--dir', '深空桥接器\src',
                '--gate',
                '--out', $reportPath)
            if ($IncludeWorkshop) { $cliArgs += @('--dir', '缓存文件夹\decomp_ws') }

            $raw = (& $py @cliArgs *>&1 | Out-String)
            $scanExit = $LASTEXITCODE
            Write-Host $raw
            if ($raw -match 'ERROR=(\d+)') {
                $errCount = [int]$Matches[1]
                if ($errCount -gt 0) { Record-Fail $gate "有 $errCount 条 ERROR（详见 $reportPath）" '' }
                else { Record-Ok $gate }
            }
            elseif ($raw -match 'Traceback \(most recent call last\)') {
                # 区分「扫描器自身崩了」和「扫描完了但没给计数」——两者处置完全不同。
                Record-Fail $gate "扫描脚本自身崩溃（Python 异常，退出码 $scanExit）" ''
            }
            else { Record-Fail $gate "扫描脚本没输出 ERROR= 计数（退出码 $scanExit），无法判定" $raw }
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
                $auditPass = ($LASTEXITCODE -eq 0 -and $raw -match 'ERROR=0')

                # worktree 伪影：本脚本常被用来在「干净检出」上复验（主工作区正被别人改时）。
                # 这种链接式 worktree 里的 工具库/skills 是旧快照，而 DSH_HOME 是主工作区那份，
                # 「不同步」是必然现象、不代表技能真的脏。剔除这一条后若再无 ERROR，就判通过并说明。
                if (-not $auditPass -and $inLinkedWorktree -and ($raw -match '本地技能与 DSH_HOME 不同步')) {
                    $meaningful = @($raw -split "`r?`n" | Where-Object {
                        $_ -match '\[ERROR\]' -and $_ -notmatch '本地技能与 DSH_HOME 不同步'
                    })
                    if ($meaningful.Count -eq 0) {
                        Write-Host '[WARN] 处于 git worktree：本地技能快照与 DSH_HOME 本就不同步，属预期，跳过该条判定。' -ForegroundColor Yellow
                        $auditPass = $true
                    }
                }

                if (-not $auditPass) { Record-Fail $gate 'kb_pipeline audit 未通过' $raw }
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
