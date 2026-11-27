#Requires -Version 5.1
<#
.SYNOPSIS
    通过本机已登录的 Steam 客户端，把《深空电玩》/《深空当铺：可能是群友》的零能力发布包上传到 Steam 创意工坊。

.DESCRIPTION
    先用 New-ReleasePackage.ps1 生成干净发布包，本脚本再编译临时上传工具（工具库\build\WorkshopUploader）
    并调用 Steamworks.NET 的 SteamUGC API 完成 CreateItem / StartItemUpdate / SubmitItemUpdate。
    全程不读取 Steam 密码或 API key，身份来自当前 Steam 会话。

.EXAMPLE
    .\工具库\build\Publish-Workshop.ps1 -Mod Arcade -Download
    .\工具库\build\Publish-Workshop.ps1 -Mod Freya -Id 3846263756 -Visibility 0 -Note "v0.7.1 修复…"
    .\工具库\build\Publish-Workshop.ps1 -Mod Arcade -VerifyOnly -Id 3846263529
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Arcade', 'Freya')][string]$Mod,
    # 0=公开 1=仅好友 2=私有 3=未列出
    [ValidateSet(0, 1, 2, 3)][int]$Visibility = 0,
    # 0 表示新建物品；非 0 表示更新已有物品
    [UInt64]$Id = 0,
    [string]$Note = '',
    [string]$Language = 'schinese',
    [switch]$SkipPackage,
    [switch]$VerifyOnly,
    # 默认**不碰**线上简介：工坊页面的简介是作者在网页上手工维护的，
    # 每次上传都用本地 docs\workshop-desc.txt 覆盖会把作者写的东西顶掉（踩过一次）。
    # 确实要用本地文件改简介时，显式加这个开关。
    [switch]$SetDescription,
    [string]$ManagedDir = 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
)
$ErrorActionPreference = 'Stop'

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$workspace = (Resolve-Path -LiteralPath (Join-Path $here '..\..')).Path
$name = if ($Mod -eq 'Arcade') { 'DeepSpaceArcade' } else { 'DeepSpacePawn' }
$projectDir = if ($Mod -eq 'Arcade') { Join-Path $workspace '深空电玩' } else { Join-Path $workspace '深空当铺：可能是群友' }
$pkg = Join-Path $projectDir "build\release-pkg\$name"
$preview = Join-Path $pkg 'preview.png'
$descFile = Join-Path $projectDir 'docs\workshop-desc.txt'
$toolSrc = Join-Path $here 'WorkshopUploader'
$toolBin = Join-Path $workspace '缓存文件夹\workshop-uploader\bin'
$toolObj = Join-Path $workspace '缓存文件夹\workshop-uploader\obj'

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet)) { throw "未找到 dotnet: $dotnet" }

# 原生程序往 stderr 写日志时，PS 5.1 在 $ErrorActionPreference='Stop' 下会抛 NativeCommandError 直接中断，
# 这里统一走 Run-Native：合并 stderr 到信息流、返回真实退出码。
function Run-Native {
    param([string]$Exe, [string[]]$Arguments)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Exe @Arguments 2>&1 | ForEach-Object {
            $line = if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.Exception.Message } else { [string]$_ }
            Write-Host $line
        }
        return $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prev
    }
}

Write-Host '[1/3] 编译上传工具'
# 中间产物用 SDK 默认位置（项目目录下的 obj/bin，已被 .gitignore 的 **/obj/ **/bin/ 排除）。
# 之前为「不污染工具库」把 obj 挪到 缓存文件夹，结果 SDK 的默认 Compile 通配与
# BaseIntermediateOutputPath 的排除规则对不上，生成出来的 AssemblyInfo.cs 被当成源码又收一遍，
# 报一堆 CS0579「特性重复」。用默认位置最省事，编译前把旧 obj/bin 清掉。
foreach ($junk in @('obj', 'bin')) {
    $p = Join-Path $toolSrc $junk
    if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
}
[void](New-Item -ItemType Directory -Path $toolBin -Force)
$buildCode = Run-Native $dotnet @(
    'build', (Join-Path $toolSrc 'WorkshopUploader.csproj'), '-c', 'Release', '-o', $toolBin, '--nologo',
    "-p:ManagedDir=$ManagedDir"
)
if ($buildCode -ne 0) { throw "上传工具编译失败 ($buildCode)" }
$steamApi = Join-Path $ManagedDir '..\Plugins\x86_64\steam_api64.dll'
if (Test-Path -LiteralPath $steamApi) {
    Copy-Item -LiteralPath $steamApi -Destination $toolBin -Force
} else {
    throw "未找到 steam_api64.dll: $steamApi"
}
$tool = Join-Path $toolBin 'WorkshopUploader.dll'

if ($VerifyOnly) {
    if ($Id -eq 0) { throw "-VerifyOnly 需要 -Id" }
    Write-Host "[2/3] 跳过打包，仅查询物品 $Id"
    exit (Run-Native $dotnet @($tool, '--verify', '--id', $Id))
}

if (!$SkipPackage) {
    Write-Host "[2/3] 生成零能力发布包 ($Mod)"
    & (Join-Path $here 'New-ReleasePackage.ps1') -Mod $Mod -ManagedDir $ManagedDir | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "发布包生成失败 ($LASTEXITCODE)" }
} else {
    Write-Host '[2/3] 跳过打包，使用既有发布包'
    if (!(Test-Path -LiteralPath $pkg -PathType Container)) { throw "发布包不存在: $pkg" }
}

Write-Host '[3/3] 上传到 Steam 创意工坊'
$title = ([xml](Get-Content -LiteralPath (Join-Path $projectDir 'manifest.xml') -Encoding UTF8)).Manifest.Name
$argsList = @(
    $tool,
    '--content', $pkg,
    '--preview', $preview,
    '--title', $title,
    '--lang', $Language,
    '--visibility', $Visibility,
    '--id', $Id
)
if ($SetDescription) {
    if (!(Test-Path -LiteralPath $descFile)) { throw "缺少简介文件: $descFile" }
    $argsList += @('--desc-file', $descFile)
    Write-Host "      注意：-SetDescription 已启用，会用 $descFile 覆盖线上简介"
} else {
    Write-Host '      线上简介保持不动（要改简介请加 -SetDescription）'
}
if (![string]::IsNullOrWhiteSpace($Note)) { $argsList += @('--note', $Note) }

$code = Run-Native $dotnet $argsList
if ($code -ne 0) { Write-Host "上传未成功，退出码 $code" }
exit $code
