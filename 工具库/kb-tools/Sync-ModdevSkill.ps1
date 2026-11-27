#Requires -Version 5.1
<#
.SYNOPSIS
    对齐工具双副本（conflict_scan.py / inspect_mod.py）并同步 probably-stolen-moddev 到 DSH_HOME，逐文件校验 SHA-256。
.PARAMETER DryRun
    仅检查差异，不写入文件。
#>
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$workspace = (Resolve-Path -LiteralPath (Join-Path $here '..\..')).Path
$localSkill = Join-Path $workspace '工具库\skills\probably-stolen-moddev'
$dshHome = $env:DSH_HOME
if ([string]::IsNullOrWhiteSpace($dshHome)) {
    throw '环境变量 DSH_HOME 未设置，无法定位隔离环境 skills 目录。'
}
$dshSkill = Join-Path $dshHome 'skills\probably-stolen-moddev'

# 1. 对齐脚本权威源 → 本地技能 scripts/
$scriptPairs = @(
    @{ Name = 'conflict_scan.py'; Src = (Join-Path $workspace '工具库\workshop-tools\conflict_scan.py'); Dst = (Join-Path $localSkill 'scripts\conflict_scan.py') },
    @{ Name = 'inspect_mod.py';   Src = (Join-Path $workspace '工具库\skills\decomp-api\inspect_mod.py'); Dst = (Join-Path $localSkill 'scripts\inspect_mod.py') }
)

foreach ($p in $scriptPairs) {
    $srcHash = (Get-FileHash -LiteralPath $p.Src -Algorithm SHA256).Hash
    $dstHash = if (Test-Path -LiteralPath $p.Dst) { (Get-FileHash -LiteralPath $p.Dst -Algorithm SHA256).Hash } else { '(missing)' }
    if ($srcHash -ne $dstHash) {
        if ($DryRun) {
            Write-Host "[DRY] 需同步脚本 $($p.Name): $dstHash -> $srcHash" -ForegroundColor Yellow
        } else {
            Copy-Item -LiteralPath $p.Src -Destination $p.Dst -Force
            Write-Host "[OK] 已同步脚本 $($p.Name) ($($srcHash.Substring(0,12)))" -ForegroundColor Green
        }
    } else {
        Write-Host "[OK] 脚本已一致：$($p.Name) ($($srcHash.Substring(0,12)))" -ForegroundColor DarkGreen
    }
}

# 2. 同步本地技能树 → DSH_HOME\skills\probably-stolen-moddev
$srcFiles = Get-ChildItem -LiteralPath $localSkill -Recurse -File
$srcRelMap = @{}
foreach ($f in $srcFiles) {
    $rel = $f.FullName.Substring($localSkill.Length + 1)
    $srcRelMap[$rel] = $f.FullName
}

if (-not $DryRun) {
    if (-not (Test-Path -LiteralPath $dshSkill)) {
        New-Item -ItemType Directory -Path $dshSkill -Force | Out-Null
    }
    foreach ($kv in $srcRelMap.GetEnumerator()) {
        $dstPath = Join-Path $dshSkill $kv.Key
        $dstParent = Split-Path -Parent $dstPath
        if (-not (Test-Path -LiteralPath $dstParent)) {
            New-Item -ItemType Directory -Path $dstParent -Force | Out-Null
        }
        Copy-Item -LiteralPath $kv.Value -Destination $dstPath -Force
    }
    # 清理 DSH_HOME 侧已删除的多余文件
    $dstFiles = Get-ChildItem -LiteralPath $dshSkill -Recurse -File
    foreach ($df in $dstFiles) {
        $rel = $df.FullName.Substring($dshSkill.Length + 1)
        if (-not $srcRelMap.ContainsKey($rel)) {
            Remove-Item -LiteralPath $df.FullName -Force
            Write-Host "[CLEAN] 删除目标端过期文件：$rel" -ForegroundColor Yellow
        }
    }
}

# 3. 产物级 SHA-256 逐文件核验（严守 dpx 产物核验纪律）
$mismatches = 0
foreach ($kv in $srcRelMap.GetEnumerator()) {
    $dstPath = Join-Path $dshSkill $kv.Key
    $h1 = (Get-FileHash -LiteralPath $kv.Value -Algorithm SHA256).Hash
    $h2 = if (Test-Path -LiteralPath $dstPath) { (Get-FileHash -LiteralPath $dstPath -Algorithm SHA256).Hash } else { 'MISSING' }
    if ($h1 -ne $h2) {
        Write-Host "[DIFF] $($kv.Key): local=$($h1.Substring(0,12)) vs dsh=$h2" -ForegroundColor Red
        $mismatches++
    }
}

if ($mismatches -gt 0) {
    if ($DryRun) {
        Write-Host "[DRY] 发现 $mismatches 处文件不同步（未写入）。" -ForegroundColor Yellow
        exit 0
    }
    throw "同步后仍发现 $mismatches 处 SHA-256 不一致！"
}
Write-Host "[PASS] 本地与 DSH_HOME 技能全量一致（共 $($srcRelMap.Count) 个文件，SHA-256 逐一核验通过）。" -ForegroundColor Green
