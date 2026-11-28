#requires -Version 5.1
<#
.SYNOPSIS
    把本地工作区同步到 GitHub 上的三个私有备份仓库。

.DESCRIPTION
    三个仓库的分工固定：

      probably-stolen-devkit  <- 共享开发 + 工具库 + 深空桥接器 + 根规范文件（快照提交，本地留一份工作副本续历史）
      deepspace-arcade        <- 深空电玩（用 git subtree split 拆出该目录自己的提交历史）
      probably-stolen         <- 深空当铺：可能是群友（同上）

    模组两个仓库的提交历史就是真实开发历史。devkit 是多目录合并，每次提交一个新快照，
    历史在它自己的仓库里连续累积，主仓库则保留全部原始历史。

    认证走系统 Git Credential Manager 里已保存的 GitHub 账号，脚本不碰 token。

.EXAMPLE
    pwsh -File .\共享开发\云备份.ps1 -DryRun
    pwsh -File .\共享开发\云备份.ps1 -Target devkit
    pwsh -File .\共享开发\云备份.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('all', 'devkit', 'arcade', 'pawn')]
    [string]$Target = 'all',

    [switch]$DryRun,

    # 要同步的工作区分支（默认 main）。云端会建立同名分支：
    #   -Branch main            → 推云端 main（原有行为）
    #   -Branch feat/arcade-xxx → 推云端 feat/arcade-xxx，本地分支一开就能有异地副本
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Continue'
$env:GIT_TERMINAL_PROMPT = '0'

$ws = Split-Path -Parent $PSScriptRoot
$owner = 'lianggong1875-afk'
$staging = Join-Path $ws '缓存文件夹\cloud-repos'

$repos = @{
    devkit = "https://github.com/$owner/probably-stolen-devkit.git"
    arcade = "https://github.com/$owner/deepspace-arcade.git"
    pawn   = "https://github.com/$owner/probably-stolen.git"
}

function Assert-GitRepo {
    if (-not (Test-Path (Join-Path $ws '.git'))) { throw "工作区不是 git 仓库：$ws" }
}

# 防呆：往云端推 main 的前提是「本地 main 就是最新成果」。
# 若当前分支领先 main，说明有提交还没合并回主干 —— 此时推 main 会让云端 main 落后于已发布的代码。
function Test-MainSyncRisk {
    param([string]$Branch)

    if ($Branch -ne 'main') { return }

    $current = (& git -C $ws rev-parse --abbrev-ref HEAD).Trim()
    $ahead = (& git -C $ws rev-list --count "main..HEAD" 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $ahead) { return }

    $ahead = [int]$ahead.Trim()
    if ($ahead -le 0) { return }

    Write-Host ''
    Write-Host '  [WARN] 当前在分支 ' -NoNewline -ForegroundColor Yellow
    Write-Host $current -NoNewline -ForegroundColor Yellow
    Write-Host '，它比 main 多 ' -NoNewline -ForegroundColor Yellow
    Write-Host "$ahead 个提交" -NoNewline -ForegroundColor Yellow
    Write-Host '。' -ForegroundColor Yellow
    Write-Host '         这次推的云端 main 只含本地 main 的内容，**不包含这些提交**。' -ForegroundColor Yellow
    Write-Host '         若这批提交已经发布（发了工坊 / 打了 tag），先把分支合并回 main 再重跑，' -ForegroundColor Yellow
    Write-Host '         否则云端 main 会与线上实际代码脱节。' -ForegroundColor Yellow
    Write-Host ''
}

function Sync-Devkit {
    Write-Host '--- probably-stolen-devkit（经验 + 技能 + 工具库）' -ForegroundColor Cyan
    $dir = Join-Path $staging 'devkit'
    $zip = Join-Path $staging 'devkit.zip'

    if ($DryRun) {
        Write-Host "  [DryRun] 会导出 共享开发 / 工具库 / 深空桥接器 / AGENTS.md / README.md / .gitignore 到 $dir 并推送"
        return
    }

    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    # 工作副本：首次从远端克隆以续上历史，远端为空时退化为 init
    if (-not (Test-Path -LiteralPath (Join-Path $dir '.git'))) {
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
        git clone -q $repos.devkit $dir 2>$null
        if ($LASTEXITCODE -ne 0) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            git -C $dir init -q -b main 2>$null
        }
    }

    # 清空工作树，只保留 .git，保证历史连续
    Get-ChildItem -LiteralPath $dir -Force | Where-Object { $_.Name -ne '.git' } |
        Remove-Item -Recurse -Force

    git -C $ws archive --format=zip -o $zip $Branch -- 共享开发 工具库 深空桥接器 AGENTS.md README.md .gitignore 2>$null
    if ($LASTEXITCODE -ne 0) { throw "git archive 失败（分支 $Branch 是否存在？）" }
    Expand-Archive -LiteralPath $zip -DestinationPath $dir -Force
    Remove-Item -LiteralPath $zip -Force

    git -C $dir config user.name 'qianxia'
    git -C $dir config user.email 'qianxia@local'
    git -C $dir add -A 2>$null

    $changed = (git -C $dir status --porcelain 2>$null | Measure-Object).Count
    if ($changed -gt 0) {
        $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm'
        git -C $dir commit -q -m "chore: 同步工作区（经验 + 技能 + 工具库）$stamp" 2>$null
        Write-Host "  本地快照提交：$(git -C $dir log -1 --format='%h')  $changed 项变化"
    } else {
        Write-Host '  内容无变化'
    }

    git -C $dir remote get-url origin 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { git -C $dir remote add origin $repos.devkit 2>$null }
    # 云端目标分支 = 源分支名（默认 main，就是原有行为）。
    # 注意：devkit 是「快照仓」，历史由它自己累积；这里只能推 $dir 的快照提交。
    # 标签不往三仓推 —— 它们由 Backup-All.ps1 的 git push --mirror 落到裸仓镜像与 bundle（含全部分支与标签）。
    if ($Branch -eq 'main') {
        $out = git -C $dir push -u origin main 2>&1
    } else {
        $out = git -C $dir push -u origin "HEAD:refs/heads/$Branch" 2>&1
    }
    if ($LASTEXITCODE -ne 0) {
        throw "devkit 推送失败（云端分支 $Branch）：$($out -join ' ')（远端历史与本地不连续时，删掉 缓存文件夹\cloud-repos\devkit 重跑一次）"
    }
    Write-Host "  已推送：$($repos.devkit) [$Branch]"
}

function Sync-Module {
    param(
        [string]$Name,
        [string]$Prefix,
        [string]$Branch,
        [string]$Url,
        [string]$SourceBranch = 'main'
    )
    Write-Host "--- $Name（$Prefix）[$SourceBranch]" -ForegroundColor Cyan

    if ($DryRun) {
        Write-Host "  [DryRun] 会 git subtree split --prefix=$Prefix <$SourceBranch> -b $Branch 并推送 $Url [$SourceBranch]"
        return
    }

    $rev = (git -C $ws rev-parse $SourceBranch 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $rev) { throw "源分支不存在：$SourceBranch" }

    git -C $ws branch -D $Branch 2>$null | Out-Null
    $split = git -C $ws subtree split --prefix=$Prefix $rev -b $Branch 2>&1
    if ($LASTEXITCODE -ne 0) { throw "subtree split 失败：$split" }
    $commits = git -C $ws rev-list --count $Branch 2>$null

    # 云端目标分支与源分支同名
    $out = git -C $ws push $Url "$Branch`:refs/heads/$SourceBranch" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "$Name 推送失败：$($out -join ' ')" }
    Write-Host "  $commits 次提交已推送：$Url [$SourceBranch]"
    git -C $ws branch -D $Branch 2>$null | Out-Null
}

Assert-GitRepo
Write-Host "工作区：$ws"
if ($DryRun) { Write-Host '模式：DryRun（不会写盘、不会推送）' -ForegroundColor Yellow }
Write-Host ''

Test-MainSyncRisk -Branch $Branch

$failed = @()
if ($Target -in @('all', 'devkit')) {
    try { Sync-Devkit } catch { $failed += "devkit"; Write-Host "  失败：$($_.Exception.Message)" -ForegroundColor Red }
}
if ($Target -in @('all', 'arcade')) {
    try { Sync-Module -Name 'deepspace-arcade' -Prefix '深空电玩' -Branch 'cloud/arcade' -Url $repos.arcade -SourceBranch $Branch }
    catch { $failed += "arcade"; Write-Host "  失败：$($_.Exception.Message)" -ForegroundColor Red }
}
if ($Target -in @('all', 'pawn')) {
    try { Sync-Module -Name 'probably-stolen' -Prefix '深空当铺：可能是群友' -Branch 'cloud/pawn' -Url $repos.pawn -SourceBranch $Branch }
    catch { $failed += "pawn"; Write-Host "  失败：$($_.Exception.Message)" -ForegroundColor Red }
}

Write-Host ''
if ($DryRun) { Write-Host 'DryRun 结束，未做任何改动。'; exit 0 }
if ($failed.Count -gt 0) {
    Write-Host "同步未全部成功：$($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host '同步完成。'
exit 0
