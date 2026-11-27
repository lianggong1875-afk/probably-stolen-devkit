#requires -Version 5.1
<#
.SYNOPSIS
    把本目录里的中文技能同步进当前 dsh-dpx 环境的 DSH 技能目录。

.DESCRIPTION
    源：<工作区>\共享开发\AI技能库\matt-pocock\<技能名>\*.md（中文版）
    目标：$env:DSH_HOME\skills\<技能名>\

    规则：
      1. 只同步中文产物，*.en.md 英文基底留在工作区做对照，不进技能目录；
      2. 每个技能目录先清空再复制，避免旧文件残留；
      3. apps 私有文件（agents/、_ 开头的说明文件）不同步。

.EXAMPLE
    pwsh -File sync-skills.ps1 -DryRun
    pwsh -File sync-skills.ps1
#>
[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$src = Join-Path $PSScriptRoot 'matt-pocock'
$dshHome = $env:DSH_HOME
if ([string]::IsNullOrWhiteSpace($dshHome)) {
    throw '环境变量 DSH_HOME 为空，无法定位 DSH 技能目录。请在 dsh-dpx 环境里运行本脚本。'
}
$dst = Join-Path $dshHome 'skills'

if (-not (Test-Path -LiteralPath $src)) { throw "源目录不存在：$src" }
if (-not (Test-Path -LiteralPath $dst)) { throw "技能目录不存在：$dst" }

$skills = Get-ChildItem -LiteralPath $src -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') }
if ($skills.Count -eq 0) { throw "源目录里没有任何含 SKILL.md 的技能：$src" }

Write-Host "源：$src"
Write-Host "目标：$dst"
Write-Host "技能数：$($skills.Count)"
Write-Host ''

$totalFiles = 0
foreach ($skill in $skills) {
    $targetDir = Join-Path $dst $skill.Name
    $files = Get-ChildItem -LiteralPath $skill.FullName -Recurse -File | Where-Object {
        $_.Name -notlike '*.en.md' -and
        $_.Name -notlike '_*' -and
        $_.FullName -notmatch '\\agents\\'
    }
    Write-Host ("{0,-34} {1,2} 个文件" -f $skill.Name, $files.Count)
    if ($DryRun) { continue }

    if (Test-Path -LiteralPath $targetDir) { Remove-Item -LiteralPath $targetDir -Recurse -Force }
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($skill.FullName.Length).TrimStart('\')
        $destFile = Join-Path $targetDir $rel
        $destParent = Split-Path -Parent $destFile
        if (-not (Test-Path -LiteralPath $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
        Copy-Item -LiteralPath $f.FullName -Destination $destFile -Force
    }
    $totalFiles += $files.Count
}

Write-Host ''
if ($DryRun) {
    Write-Host '（DryRun）未写入任何文件。'
} else {
    Write-Host "已同步 $($skills.Count) 个技能，共 $totalFiles 个文件。"
    Write-Host '新技能在下一个会话的对话轮次里才会出现在技能清单中；当前会话已加载的技能表不变。'
}
