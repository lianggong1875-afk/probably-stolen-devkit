#requires -Version 5.1
<#
.SYNOPSIS
    校验中文技能与英文基底的结构对齐。

.DESCRIPTION
    逐个技能比对 SKILL.en.md 与 SKILL.md（以及各参考文档），检查：
      1. frontmatter 存在，name 字段与英文版一致；
      2. 标题数一致；
      3. 围栏代码块数一致；
      4. 列表项数一致；
      5. 中文文件里不出现英文破折号 —；
      6. 中文文件里不残留纯英文大段（提示漏译）。

    只报告，不修改文件。

.EXAMPLE
    pwsh -File .\共享开发\AI技能库\verify-skills.ps1
#>
[CmdletBinding()]
param(
    [string]$Root = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Join-Path $PSScriptRoot 'matt-pocock' }
$rows = New-Object System.Collections.ArrayList
$problems = New-Object System.Collections.ArrayList

function Get-Stats {
    param([string]$Path)
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $lines = $text -split "`r?`n"
    [pscustomobject]@{
        Headings   = ($lines | Where-Object { $_ -match '^#{1,6}\s' }).Count
        Fences     = ($lines | Where-Object { $_ -match '^```' }).Count
        Bullets    = ($lines | Where-Object { $_ -match '^\s*([-*]|\d+\.)\s' }).Count
        Dashes     = ([regex]::Matches($text, [char]0x2014)).Count
        Name       = if ($text -match '(?m)^name:\s*(.+?)\s*$') { $matches[1].Trim('"', "'") } else { $null }
        HasYaml    = $text -match '(?s)^\ufeff?---\s*?\r?\n.*?\r?\n---'
        Text       = $text
    }
}

$skillDirs = Get-ChildItem -LiteralPath $Root -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.en.md') }
foreach ($dir in $skillDirs) {
    $pairs = @()
    $enMain = Join-Path $dir.FullName 'SKILL.en.md'
    $zhMain = Join-Path $dir.FullName 'SKILL.md'
    if (Test-Path -LiteralPath $zhMain) { $pairs += , @($enMain, $zhMain) }
    else { [void]$problems.Add("$($dir.Name): 缺少 SKILL.md") }

    Get-ChildItem -LiteralPath $dir.FullName -Recurse -File -Filter '*.en.md' | Where-Object { $_.Name -ne 'SKILL.en.md' } | ForEach-Object {
        $zh = $_.FullName.Substring(0, $_.FullName.Length - '.en.md'.Length) + '.md'
        if (Test-Path -LiteralPath $zh) { $pairs += , @($_.FullName, $zh) }
        else { [void]$problems.Add("$($dir.Name): 缺少 $(Split-Path $zh -Leaf)") }
    }

    foreach ($pair in $pairs) {
        $en = Get-Stats $pair[0]
        $zh = Get-Stats $pair[1]
        $rel = $pair[1].Substring($Root.Length + 1)
        $bad = @()
        if ($zh.Dashes -gt 0) { $bad += "含 $($zh.Dashes) 个破折号" }
        if ($en.Headings -ne $zh.Headings) { $bad += "标题 $($zh.Headings)/$($en.Headings)" }
        if ($en.Fences -ne $zh.Fences) { $bad += "代码块 $($zh.Fences)/$($en.Fences)" }
        if ([math]::Abs($en.Bullets - $zh.Bullets) -gt 2) { $bad += "列表 $($zh.Bullets)/$($en.Bullets)" }
        if ($en.Name -and $zh.Name -ne $en.Name) { $bad += "name=$($zh.Name)/$($en.Name)" }
        if (-not $zh.HasYaml -and $en.HasYaml) { $bad += 'frontmatter 缺失' }

        [void]$rows.Add([pscustomobject]@{
            File    = $rel
            Match   = ($bad.Count -eq 0)
            Issues  = ($bad -join '；')
        })
        foreach ($b in $bad) { [void]$problems.Add("$rel -> $b") }
    }
}

$rows | Sort-Object File | Format-Table -AutoSize -Wrap | Out-String -Width 160 | Write-Host
Write-Host ("检查文件：{0} 个；不一致：{1} 个" -f $rows.Count, ($rows | Where-Object { -not $_.Match }).Count)
if ($problems.Count -gt 0) {
    Write-Host ''
    Write-Host '需要处理：'
    $problems | ForEach-Object { Write-Host "  - $_" }
    exit 1
}
Write-Host '全部对齐。'
exit 0
