#Requires -Version 5.1
<#
    真机基线快照：一条命令把「当前世界 / 存档 / 模组加载状态」抓下来落盘。

        .\snap-runtime.ps1                 # 游戏必须开着，桥接在跑
        .\snap-runtime.ps1 -Out <目录>

    存到 `缓存文件夹\_evidence\runtime-snapshots\<时间戳>-<tool>.json`，并在屏幕上打一行摘要。
    上机验收前先跑一次，事后对得上 slot/day/world 才算这次验的是同一个世界。
#>
[CmdletBinding()]
param(
    [string]$Out = '',
    [string]$Url = 'http://127.0.0.1:18774/mcp'
)
$ErrorActionPreference = 'Continue'

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$mcp = Join-Path $here 'mcp_cli.py'
if (!(Test-Path -LiteralPath $mcp)) { Write-Host "mcp_cli.py not found next to this script"; exit 3 }

$toolsRoot = Split-Path -Parent (Split-Path -Parent $here)      # 工具库
$workspace = Split-Path -Parent $toolsRoot
if ([string]::IsNullOrWhiteSpace($Out)) {
    $Out = Join-Path $workspace '缓存文件夹\_evidence\runtime-snapshots'
}
[void](New-Item -ItemType Directory -Force -Path $Out)

$env:MODBRIDGE_URL = $Url
$env:MODBRIDGE_TMP = Join-Path $here 'tmp'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

function Snap([string]$tool, [string]$argsJson) {
    $file = Join-Path $Out ($stamp + '-' + $tool + '.json')
    $raw = & python $mcp $tool $argsJson 2>&1 | Out-String
    $raw | Out-File -FilePath $file -Encoding utf8
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    try { return ($raw | ConvertFrom-Json) } catch { return $null }
}

Write-Host ('snapshot -> ' + $Out)

$status = Snap 'get_game_status' '{}'
if ($null -eq $status) {
    Write-Host 'no answer from the bridge — is the game running with ModDevBridgeNext?' -ForegroundColor Yellow
    Write-Host ('URL: ' + $Url)
    exit 1
}

$ready = $status.ready
$slot = $status.saveSlot
$day = $status.day
$world = $status.worldId
$scene = $status.scene
Write-Host ('game: ready={0} scene={1} slot={2} day={3} world={4}' -f $ready, $scene, $slot, $day, $world)
if ($ready -and $slot -ne 1) {
    Write-Host 'WARNING: this is NOT slot 1. Slot 0 must never be entered or written.' -ForegroundColor Yellow
}

$runtime = Snap 'inspect_runtime' '{}'
if ($null -ne $runtime) {
    $text = ($runtime | ConvertTo-Json -Depth 6 -Compress)
    if ($text.Length -gt 400) { $text = $text.Substring(0, 400) + '...' }
    Write-Host ('runtime: ' + $text)
}

$env2 = Snap 'get_development_environment' '{}'
if ($null -ne $env2) {
    $text = ($env2 | ConvertTo-Json -Depth 4 -Compress)
    if ($text.Length -gt 300) { $text = $text.Substring(0, 300) + '...' }
    Write-Host ('env: ' + $text)
}

Write-Host ('files: ' + $stamp + '-*.json')
exit 0
