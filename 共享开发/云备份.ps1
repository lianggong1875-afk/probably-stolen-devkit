#requires -Version 5.1
<#
.SYNOPSIS
    把本地工作区同步到 GitHub 上的三个私有备份仓库。

.DESCRIPTION
    三个仓库的分工固定：

      probably-stolen-devkit  <- 共享开发 + 工具库 + 深空桥接器 + 根规范文件（快照提交）
      deepspace-arcade        <- 深空电玩（保留该目录自己的提交历史）
      probably-stolen         <- 深空当铺：可能是群友（保留该目录自己的提交历史）

    模组两个仓库用 git subtree split 从主仓库拆出各自历史后推送，所以它们的提交历史
    就是真实开发历史。devkit 是多目录合并，用快照提交，历史以主仓库为准。

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

    [switch]$DryRun
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

function Sync-Devkit {
    Write-Host '--- probably-stolen-devkit（经验 + 技能 + 工具库）' -ForegroundColor Cyan
    $dir = Join-Path $staging 'devkit'
    $zip = Join-Path $staging 'devkit.zip'

    if ($DryRun) {
        Write-Host "  [DryRun] 会导出 共享开发 / 工具库 / 深空桥接器 / AGENTS.md / README.md / .gitignore 到 $dir 并推送"
        return
    }

    if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    New-Item -ItemType Directory -Path $dir -Force | Out-Null

    git -C $ws archive --format=zip -o $zip HEAD -- 共享开发 工具库 深空桥接器 AGENTS.md README.md .gitignore 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'git archive 失败' }
    Expand-Archive -LiteralPath $zip -DestinationPath $dir -Force
    Remove-Item -LiteralPath $zip -Force

    git -C $dir init -q -b main 2>$null
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

    git -C $dir remote remove origin 2>$null
    git -C $dir remote add origin $repos.devkit
    git -C $dir push -q -u origin main 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'devkit 推送失败' }
    Write-Host "  已推送：$($repos.devkit)"
}

function Sync-Module {
    param(
        [string]$Name,
        [string]$Prefix,
        [string]$Branch,
        [string]$Url
    )
    Write-Host "--- $Name（$Prefix）" -ForegroundColor Cyan

    if ($DryRun) {
        Write-Host "  [DryRun] 会 git subtree split --prefix=$Prefix -b $Branch 并推送 $Url"
        return
    }

    git -C $ws branch -D $Branch 2>$null | Out-Null
    $split = git -C $ws subtree split --prefix=$Prefix -b $Branch 2>&1
    if ($LASTEXITCODE -ne 0) { throw "subtree split 失败：$split" }
    $commits = git -C $ws rev-list --count $Branch 2>$null

    git -C $ws push -q $Url "$Branch`:refs/heads/main" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$Name 推送失败" }
    Write-Host "  $commits 次提交已推送：$Url"
    git -C $ws branch -D $Branch 2>$null | Out-Null
}

Assert-GitRepo
Write-Host "工作区：$ws"
if ($DryRun) { Write-Host '模式：DryRun（不会写盘、不会推送）' -ForegroundColor Yellow }
Write-Host ''

if ($Target -in @('all', 'devkit')) { Sync-Devkit }
if ($Target -in @('all', 'arcade')) {
    Sync-Module -Name 'deepspace-arcade' -Prefix '深空电玩' -Branch 'cloud/arcade' -Url $repos.arcade
}
if ($Target -in @('all', 'pawn')) {
    Sync-Module -Name 'probably-stolen' -Prefix '深空当铺：可能是群友' -Branch 'cloud/pawn' -Url $repos.pawn
}

Write-Host ''
if ($DryRun) { Write-Host 'DryRun 结束，未做任何改动。' } else { Write-Host '同步完成。' }
