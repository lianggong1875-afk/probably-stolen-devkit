#Requires -Version 5.1
<#
.SYNOPSIS
    分支 / 云端 / 主干 三方对账体检：一次说清「云端到底有没有这条分支」「main 是否与已发布代码脱节」。

.DESCRIPTION
    读三处事实源并逐条对账：
      · 本地：当前分支、全部分支、与 main 的领先/落后提交数、HEAD 是否已并入 main；
      · 云端：三个私有 GitHub 仓实际存在的分支（git ls-remote --heads 直问远端，不信本地缓存）；
      · 发布：两个模组 manifest.xml 里的 ModVersion（用来判断「发出去的版本是否已在主干上」）。

    退出码：0 = 全绿；1 = 有缺口（缺云端分支 / 有提交未并入 main / 分支落后 main）。

.EXAMPLE
    .\工具库\build\Test-BranchSync.ps1
    .\工具库\build\Test-BranchSync.ps1 -Branch feat/freya-visitor-cards-and-exclusive-items
#>
[CmdletBinding()]
param(
    # 要重点体检的分支；默认取当前 HEAD 所在分支
    [string]$Branch = '',
    # 基准主干
    [string]$Main = 'main'
)

$ErrorActionPreference = 'Continue'
$owner = 'lianggong1875-afk'
$repoList = @('probably-stolen-devkit', 'deepspace-arcade', 'probably-stolen')

$ws = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $ws
$problems = @()

function Say([string]$text, [string]$color = 'Gray') { Write-Host $text -ForegroundColor $color }

try {
    if (-not (Test-Path -LiteralPath (Join-Path $ws '.git'))) { throw "不是 git 仓库：$ws" }

    $current = (& git rev-parse --abbrev-ref HEAD).Trim()
    if (-not $Branch) { $Branch = $current }
    $head = (& git rev-parse --short HEAD).Trim()

    Say ''
    Say '==== 1. 本地分支状态 ====' Cyan
    $localBranches = @(& git for-each-ref --format='%(refname:short)' refs/heads)
    Say ("  当前 HEAD      : $current @ $head")
    Say ("  本地分支($($localBranches.Count))    : " + ($localBranches -join ', '))

    & git rev-parse --verify --quiet "refs/heads/$Branch" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "本地不存在分支：$Branch" }

    $ahead = [int](& git rev-list --count "$Main..$Branch").Trim()
    $behind = [int](& git rev-list --count "$Branch..$Main").Trim()
    Say ("  $Branch vs ${Main} : 领先 $ahead / 落后 $behind")

    & git merge-base --is-ancestor $Branch $Main | Out-Null
    $mergedIntoMain = ($LASTEXITCODE -eq 0)
    if ($ahead -gt 0 -and -not $mergedIntoMain) {
        $problems += "★ $Branch 有 $ahead 个提交尚未并入 $Main —— 云端 $Main 不会包含它们"
    }
    if ($behind -gt 0) {
        $problems += "★ $Branch 落后 $Main $behind 个提交 —— 主干上别人的改动还没进来（直接合并可能冲突/覆盖）"
    }

    Say ''
    Say '==== 2. 云端分支实况（ls-remote 直问远端） ====' Cyan
    $cloudHeads = @{}
    foreach ($repo in $repoList) {
        $url = "https://github.com/$owner/$repo.git"
        $refs = @(& git ls-remote --heads $url 2>$null)
        if ($LASTEXITCODE -ne 0) {
            $problems += "★ 云端仓 $repo 读不到（网络或凭据问题）"
            Say ("  {0,-24} : 读取失败" -f $repo) Red
            continue
        }
        $names = @()
        foreach ($line in $refs) {
            $s = [string]$line
            if ($s -match 'refs/heads/(.+)$') { $names += $Matches[1] }
        }
        $cloudHeads[$repo] = $names
        Say ("  {0,-24} : {1}" -f $repo, ($(if ($names.Count) { $names -join ', ' } else { '(空)' })))
    }

    Say ''
    Say '==== 3. 云端分支覆盖对账 ====' Cyan
    Say ("  待核对分支: $Branch")
    foreach ($repo in $repoList) {
        if (-not $cloudHeads.ContainsKey($repo)) { continue }
        $has = $cloudHeads[$repo] -contains $Branch
        if ($has) { Say ("  [OK]   {0,-24} 有 {1}" -f $repo, $Branch) Green }
        else {
            Say ("  [MISS] {0,-24} 缺 {1}" -f $repo, $Branch) Red
            $problems += "★ 云端 $repo 缺分支 $Branch（这块内容没有异地副本）"
        }
    }

    Say ''
    Say '==== 4. 发布版本 vs 主干 ====' Cyan
    $manifests = @(
        @{ Name = '深空电玩'; Path = (Join-Path $ws '深空电玩\manifest.xml') },
        @{ Name = '可能是群友'; Path = (Join-Path $ws '深空当铺：可能是群友\manifest.xml') }
    )
    foreach ($m in $manifests) {
        if (-not (Test-Path -LiteralPath $m.Path)) { continue }
        $ver = ''
        try {
            $xml = [xml](Get-Content -LiteralPath $m.Path -Raw -Encoding UTF8)
            $ver = $xml.Manifest.ModVersion
        } catch { $ver = '(解析失败)' }
        Say ("  {0,-10} manifest 版本 : {1}" -f $m.Name, $ver)
    }
    if (-not $mergedIntoMain -and $ahead -gt 0) {
        Say '  ⚠ 发布前必须先把分支 merge --no-ff 进 main，再跑 云备份.ps1 -Branch main' Yellow
    }
}
finally {
    Pop-Location
}

Say ''
if ($problems.Count -gt 0) {
    Say '==== 结论：有缺口 ====' Red
    foreach ($p in $problems) { Say "  $p" Red }
    Say ''
    Say '处置：先补齐云端分支（云备份.ps1 -Branch <名字>），' Yellow
    Say '      再把过期的主干合并进来 / 把分支合并回主干，最后重跑本脚本确认全绿。' Yellow
    exit 1
}

Say '==== 结论：全绿（云端分支齐全，主干与分支已同步） ====' Green
exit 0