<#
.SYNOPSIS
    为一个新功能同时在「本地仓库」和「云端」建立分支。

.DESCRIPTION
    规范见 共享开发\分支与备份规范.md 第 2 节。本脚本做四件事：
      1) 校验当前在基准分支（默认 main）且工作区干净；
      2) 校验分支名符合命名规则（feat/ fix/ exp/ release/ 前缀、全小写、无中文）；
      3) 建本地分支并切过去；
      4) 调 共享开发\云备份.ps1 -Branch <名字> 把这条分支推到云端
         （等于立刻多一份异地副本，本地硬盘出问题也不怕）。

.PARAMETER Name
    分支名，必须带前缀。例：feat/arcade-burner-animation、fix/freya-siren-purity

.PARAMETER From
    从哪个分支切出来，默认 main。

.PARAMETER SkipCloud
    只建本地分支，不推云端（离线、或云端暂时不可用时用）。

.PARAMETER Force
    工作区不干净也硬建（不推荐，除非你清楚未提交的改动会被带过去）。

.EXAMPLE
    .\工具库\build\New-FeatureBranch.ps1 -Name feat/arcade-burner-animation
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$From = 'main',
    [switch]$SkipCloud,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$cloudScript = Join-Path $repoRoot '共享开发\云备份.ps1'

function Fail([string]$msg) {
    Write-Host "[FAIL] $msg" -ForegroundColor Red
    exit 1
}
function Step([string]$msg) { Write-Host "[..] $msg" -ForegroundColor Cyan }
function Ok([string]$msg) { Write-Host "[OK] $msg" -ForegroundColor Green }

# ---- 1. 命名与前置校验 ----------------------------------------------------
if ($Name -notmatch '^(feat|fix|exp|release|docs)/[a-z0-9][a-z0-9\-\./]*$') {
    Fail "分支名 '$Name' 不合规。必须形如 feat|fix|exp|release|docs/<域>-<短句>，全小写、连字符分词、不含中文。域建议 arcade / freya / tools / kb。"
}
if ($Name -match '[^\x00-\x7F]') {
    Fail "分支名不能含非 ASCII 字符（跨平台与工具链会出问题）：'$Name'"
}

Push-Location $repoRoot
try {
    $current = (& git rev-parse --abbrev-ref HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { Fail "这不是一个 git 仓库：$repoRoot" }

    $dirty = (& git status --porcelain)
    if ($dirty -and -not $Force) {
        $count = ($dirty | Measure-Object).Count
        Fail "工作区不干净（$count 处未提交改动）。先 commit 或用 -Force。`n$($dirty -join "`n")"
    }

    & git rev-parse --verify --quiet "refs/heads/$Name" | Out-Null
    if ($LASTEXITCODE -eq 0) { Fail "本地已存在分支 $Name" }

    & git rev-parse --verify --quiet "refs/heads/$From" | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "基准分支不存在：$From" }

    if ($current -ne $From) {
        # 防呆：若基准分支落后于当前分支（当前分支包含 $From 没有的提交），说明 $From 是旧的，
        # 从它切出去会丢掉别人已经提交但还没合进 $From 的工作。必须显式 -Force 才放行。
        & git merge-base --is-ancestor $From $current | Out-Null
        if ($LASTEXITCODE -ne 0 -and -not $Force) {
            $ahead = (& git rev-list --count "$From..$current").Trim()
            Fail "基准分支 $From 落后于当前分支 $current（差 $ahead 个提交）。从 $From 切出会丢掉这些改动；先把 $From 合并到最新，或用 -Force 明确承担风险。"
        }
        Step "从 $current 切到基准分支 $From"
        & git switch $From | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "切不到 $From" }
    }

    # ---- 2. 建本地分支 ----------------------------------------------------
    Step "创建并切换到 $Name"
    & git switch -c $Name | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "创建分支失败" }
    Ok "本地分支已就绪：$Name（基准 $From @ $((& git rev-parse --short $From).Trim())）"

    # ---- 3. 推云端 --------------------------------------------------------
    if ($SkipCloud) {
        Write-Host "[SKIP] 按 -SkipCloud 跳过云端分支" -ForegroundColor Yellow
    }
    elseif (-not (Test-Path -LiteralPath $cloudScript)) {
        Write-Host "[WARN] 找不到 云备份.ps1，云端分支未推：$cloudScript" -ForegroundColor Yellow
    }
    else {
        Step "推云端分支（共享开发\云备份.ps1 -Branch $Name）"
        # 必须用子进程：云备份.ps1 末尾有 exit，直接 & 调用会把当前会话一起退掉。
        # 本机只有 Windows PowerShell 5.1，没有 pwsh（PowerShell 7），所以取 $PSHOME 下的 powershell.exe。
        $psExe = Join-Path $PSHOME 'powershell.exe'
        if (-not (Test-Path -LiteralPath $psExe)) { $psExe = 'powershell.exe' }
        & $psExe -NoProfile -ExecutionPolicy Bypass -File $cloudScript -Branch $Name
        if ($LASTEXITCODE -ne 0) {
            Fail "云端推送失败（$LASTEXITCODE）。云端没有这条分支 = 本地硬盘炸了就没有异地副本。先修网络/凭据再重跑，或显式加 -SkipCloud 并在恢复后立刻补推。"
        }

        # 回读验证：不看脚本退出码，直接问远端「这条分支到底存不存在」。
        # 三个云端仓都必须有同名分支，缺一个就等于那块内容没有异地副本。
        $owner = 'lianggong1875-afk'
        $repos = @('probably-stolen-devkit', 'deepspace-arcade', 'probably-stolen')
        $missing = @()
        foreach ($repo in $repos) {
            $url = "https://github.com/$owner/$repo.git"
            $ref = (& git ls-remote --heads $url "refs/heads/$Name" 2>$null)
            if (-not $ref) { $missing += $repo }
        }
        if ($missing.Count -gt 0) {
            Fail "回读验证失败：云端缺少分支 $Name 的仓：$($missing -join ', ')。本地分支已建好，请重跑 云备份.ps1 -Branch $Name 补齐后再开工。"
        }
        Ok "云端分支已建立并回读验证（3/3 仓）：$Name"
    }

    Write-Host ""
    Write-Host "接下来：" -ForegroundColor White
    Write-Host "  1) 写代码 / 画图 / 写文案，每完成一小块就 commit"
    Write-Host "  2) 自测：.\工具库\build\Build-Mod.ps1 -Mod <Arcade|Freya>"
    Write-Host "  3) 过门禁：.\工具库\build\Invoke-PreMergeGate.ps1 -Mod <Arcade|Freya>"
    Write-Host "  4) 回主干：git switch main ; git merge --no-ff $Name"
    Write-Host "  5) 备份：.\工具库\build\Backup-All.ps1"
}
finally {
    Pop-Location
}
