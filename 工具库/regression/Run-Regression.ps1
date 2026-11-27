#Requires -Version 5.1
<#
.SYNOPSIS
    双模组离线回归统一入口（只保留 tests\ 下的真实行为与不变量测试）。

.EXAMPLE
    .\工具库\regression\Run-Regression.ps1
    .\工具库\regression\Run-Regression.ps1 -Mod Freya
    .\工具库\regression\Run-Regression.ps1 -Mod Arcade -Only appraisal
    .\工具库\regression\Run-Regression.ps1 -List
#>
[CmdletBinding()]
param(
    [ValidateSet('Arcade', 'Freya', 'All')][string]$Mod = 'All',
    [string[]]$Only = @(),
    [switch]$List,
    [switch]$KeepCache
)

$ErrorActionPreference = 'Continue'
$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$workspace = (Resolve-Path -LiteralPath (Join-Path $here '..\..')).Path
$arcade = Join-Path $workspace '深空电玩'
$freya = Join-Path $workspace '深空当铺：可能是群友'
if ($Mod -ne 'Freya' -and !(Test-Path -LiteralPath (Join-Path $arcade 'DeepSpaceArcade.csproj') -PathType Leaf)) {
    Write-Host "未找到深空电玩项目: $arcade"
    exit 2
}
if ($Mod -ne 'Arcade' -and !(Test-Path -LiteralPath (Join-Path $freya 'DeepSpacePawn.csproj') -PathType Leaf)) {
    Write-Host "未找到可能是群友项目: $freya"
    exit 2
}
$cacheRoot = Join-Path $workspace '缓存文件夹\regression'

$suites = @(
    @{ mod = 'Arcade'; name = 'appraisal';       root = $arcade; script = 'tests\appraisal\Run-AppraisalRegression.ps1' }
    @{ mod = 'Arcade'; name = 'texts';           root = $arcade; script = 'tests\texts\Run-TextsRegression.ps1' }
    @{ mod = 'Freya';  name = 'freya-integrity'; root = $freya;  script = 'tests\integrity\Run-FreyaIntegrityRegression.ps1' }
)

if ($List) {
    foreach ($s in $suites) { '{0,-8} {1,-16} {2}' -f $s.mod, $s.name, $s.script }
    exit 0
}

$selected = @()
foreach ($s in $suites) {
    if ($Mod -ne 'All' -and $s.mod -ne $Mod) { continue }
    if ($Only.Count -gt 0 -and ($Only -notcontains $s.name)) { continue }
    $selected += $s
}
if ($selected.Count -eq 0) {
    Write-Host "未匹配任何套件 Mod=$Mod Only='$($Only -join ',')'; 可用: $($suites.name -join ', ')"
    exit 2
}

if (!(Test-Path -LiteralPath $cacheRoot)) { [void](New-Item -ItemType Directory -Force -Path $cacheRoot) }

function Get-Counts([string]$text) {
    $pats = @(
        'PASS=(\d+)\s*FAIL=(\d+)',
        '(\d+)\s+PASS\s*/\s*(\d+)\s+FAIL',
        '(\d+)\s+assertions?,\s*(\d+)\s+failed',
        '(\d+)\s+checks?,\s*(\d+)\s+failed',
        '(\d+)\s+passed[^\r\n]*?(\d+)\s+(?:failed|failures)'
    )
    $best = $null; $bestHits = 0
    foreach ($p in $pats) {
        $hits = [regex]::Matches($text, $p)
        if ($hits.Count -le $bestHits) { continue }
        $ok = 0; $bad = 0
        foreach ($h in $hits) { $ok += [int]$h.Groups[1].Value; $bad += [int]$h.Groups[2].Value }
        $best = @{ pass = $ok; fail = $bad }; $bestHits = $hits.Count
    }
    return $best
}

$rows = @()
foreach ($s in $selected) {
    $dir = Join-Path $cacheRoot $s.name
    if (!$KeepCache -and (Test-Path -LiteralPath $dir)) { Remove-Item -LiteralPath $dir -Recurse -Force }
    [void](New-Item -ItemType Directory -Force -Path $dir)
    $log = Join-Path $dir 'run.log'

    $scriptPath = Join-Path $s.root $s.script
    $row = [ordered]@{ suite = $s.name; exit = 'missing'; pass = ''; fail = ''; secs = 0; log = $log }
    if (!(Test-Path -LiteralPath $scriptPath)) {
        "SUITE SCRIPT MISSING: $scriptPath" | Out-File -FilePath $log -Encoding utf8
        Write-Host ("[{0}] MISSING  {1}" -f $s.name, $s.script)
        $rows += [pscustomobject]$row
        continue
    }

    Write-Host ("[{0}] running ..." -f $s.name)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $out = & $scriptPath -CacheDir $dir *>&1 | ForEach-Object { $_.ToString() }
        $exit = $LASTEXITCODE
        if ($null -eq $exit) { $exit = 0 }
    } catch {
        $out = @("EXCEPTION: " + $_.Exception.Message)
        $exit = 'throw'
    }
    $sw.Stop()

    $out | Out-File -FilePath $log -Encoding utf8
    $c = Get-Counts ($out -join "`n")

    $row.exit = $exit
    $row.secs = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    if ($c) { $row.pass = $c.pass; $row.fail = $c.fail }

    $tail = ($out | Where-Object { $_ -match '^\s*(FAIL|FAILED)' } | Select-Object -First 6)
    Write-Host ("[{0}] exit={1} pass={2} fail={3} {4}s" -f $s.name, $exit, $row.pass, $row.fail, $row.secs)
    foreach ($t in $tail) { Write-Host ("    " + $t.Trim()) }

    $rows += [pscustomobject]$row
}

$summary = Join-Path $cacheRoot 'summary.log'
$lines = @("run at " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$lines += ("{0,-12} {1,4} {2,6} {3,5} {4,6}  {5}" -f 'suite', 'exit', 'pass', 'fail', 'secs', 'log')
foreach ($r in $rows) {
    $lines += ("{0,-12} {1,4} {2,6} {3,5} {4,6}  {5}" -f $r.suite, $r.exit, $r.pass, $r.fail, $r.secs, $r.log)
}
$bad = @($rows | Where-Object { $_.exit -ne 0 })
$lines += ""
if ($bad.Count -eq 0) { $lines += "ALL SUITES PASS ($($rows.Count))" } else { $lines += "FAILED SUITES: " + (($bad | ForEach-Object { $_.suite + '(exit=' + $_.exit + ')' }) -join ', ') }
$lines | Out-File -FilePath $summary -Encoding utf8
$lines | ForEach-Object { Write-Host $_ }

if ($bad.Count -eq 0) { exit 0 } else { exit 1 }
