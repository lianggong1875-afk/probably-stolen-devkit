#Requires -Version 5.1
<#
.SYNOPSIS
    统一构建脚本（深空电玩 / 深空当铺：可能是群友）。
    默认输出到 <项目>\build\latest（公开版为 build\public），允许直接覆盖，不再强制起新 RunId。

.EXAMPLE
    .\工具库\build\Build-Mod.ps1 -Mod Arcade
    .\工具库\build\Build-Mod.ps1 -Mod Arcade -PublicRelease -Scan
    .\工具库\build\Build-Mod.ps1 -Mod Arcade -PublicRelease -Install
    .\工具库\build\Build-Mod.ps1 -Mod Freya
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Freya','Arcade')][string]$Mod,
    [string]$ManagedDir = '',
    [string]$RunId = '',
    [string]$SdkRoot = 'C:\Program Files\dotnet\sdk',
    [switch]$PublicRelease,
    [switch]$Scan,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ManagedDir)) {
    $ManagedDir = if ($env:PS_GAME_MANAGED) { $env:PS_GAME_MANAGED } else { 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed' }
}

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$workspace = (Resolve-Path -LiteralPath (Join-Path $here '..\..')).Path
$name = if ($Mod -eq 'Freya') { 'DeepSpacePawn' } else { 'DeepSpaceArcade' }
$projectDir = if ($Mod -eq 'Freya') { Join-Path $workspace '深空当铺：可能是群友' } else { Join-Path $workspace '深空电玩' }
if (!(Test-Path -LiteralPath $projectDir -PathType Container)) { throw "未找到项目目录: $projectDir" }

$srcDir = Join-Path $projectDir 'src'
$sources = @(Get-ChildItem -LiteralPath $srcDir -File -Filter '*.cs' -Recurse | Sort-Object FullName)
if (!$sources.Count) { throw "源码目录为空: $srcDir" }

$managed = (Resolve-Path -LiteralPath $ManagedDir).Path
$sdk = Get-ChildItem -LiteralPath $SdkRoot -Directory | Where-Object {
    $_.Name -match '^\d+\.\d+\.\d+$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'Roslyn\bincore\csc.dll') -PathType Leaf)
} | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (!$sdk) { throw "未在 $SdkRoot 找到 Roslyn 编译器" }

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = if ($PublicRelease) { 'public' } else { 'latest' }
}

$outDir = Join-Path (Join-Path $projectDir 'build') $RunId
if (Test-Path -LiteralPath $outDir) { Remove-Item -LiteralPath $outDir -Recurse -Force }
[void](New-Item -ItemType Directory -Path $outDir -Force)

$dll = Join-Path $outDir "$name.dll"
$projectFile = Join-Path $projectDir "$name.csproj"
$objDir = Join-Path $outDir 'obj'
$extra = @()
if ($PublicRelease) {
    $defineSym = if ($Mod -eq 'Freya') { 'FREYA_PUBLIC' } else { 'ARCADE_PUBLIC' }
    $extra = @("-p:DefineConstants=$defineSym")
}
& dotnet build $projectFile -c Release --nologo "-p:ManagedDir=$managed" `
    "-p:BaseIntermediateOutputPath=$objDir/" "-p:MSBuildProjectExtensionsPath=$objDir/" "-p:OutputPath=$outDir/" @extra

if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $dll -PathType Leaf)) { throw '编译失败' }

$hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
Write-Output ("编译成功: {0} ({1} 个源码文件)" -f $dll, $sources.Count)
Write-Output ("SHA256 : {0}" -f $hash)

if ($Scan) {
    Write-Output "--- 运行 ModScanner 能力扫描 ---"
    $scanOutDir = Join-Path $workspace '缓存文件夹\loader-scan'
    $expectedCaps = ''
    if (-not $PublicRelease) {
        $srcMan = Join-Path $projectDir 'manifest.xml'
        if (Test-Path -LiteralPath $srcMan -PathType Leaf) {
            [xml]$mDoc = Get-Content -LiteralPath $srcMan -Encoding UTF8
            $capNodes = $mDoc.SelectNodes('/Manifest/Capabilities/Capability')
            if ($capNodes -and $capNodes.Count -gt 0) {
                $expectedCaps = (@($capNodes | ForEach-Object { $_.InnerText.Trim() } | Where-Object { $_ }) -join ',')
            }
        }
    }
    $scanParams = @{
        Candidate            = $dll
        OutputDir            = $scanOutDir
        ManagedDir           = $managed
        ExpectedCapabilities = $expectedCaps
    }
    if ($PublicRelease) { $scanParams['PublicRelease'] = $true }
    & (Join-Path $here 'Test-ArcadeLoaderScan.ps1') @scanParams
    if ($LASTEXITCODE -ne 0) { throw "能力扫描失败 ($LASTEXITCODE)" }
}

if ($Install) {
    $gameRoot = Split-Path -Parent (Split-Path -Parent $managed)
    $modDst = Join-Path (Join-Path $gameRoot 'Mods') $name
    [void](New-Item -ItemType Directory -Path $modDst -Force)

    Copy-Item -LiteralPath $dll -Destination (Join-Path $modDst "$name.dll") -Force
    foreach ($sub in @('Textures', 'Books', 'Localization', 'Audio', 'Data')) {
        $srcSub = Join-Path $projectDir $sub
        if (Test-Path -LiteralPath $srcSub -PathType Container) {
            $dstSub = Join-Path $modDst $sub
            if (Test-Path -LiteralPath $dstSub) { Remove-Item -LiteralPath $dstSub -Recurse -Force }
            Copy-Item -LiteralPath $srcSub -Destination $dstSub -Recurse -Force
        }
    }
    $srcPreview = Join-Path $projectDir 'preview.png'
    if (Test-Path -LiteralPath $srcPreview -PathType Leaf) {
        Copy-Item -LiteralPath $srcPreview -Destination (Join-Path $modDst 'preview.png') -Force
    }

    $srcManifest = Join-Path $projectDir 'manifest.xml'
    $dstManifest = Join-Path $modDst 'manifest.xml'
    if ($PublicRelease) {
        [xml]$m = Get-Content -LiteralPath $srcManifest -Encoding UTF8
        $caps = $m.Manifest.SelectSingleNode('Capabilities')
        if ($null -eq $caps) {
            $caps = $m.CreateElement('Capabilities')
            [void]$m.Manifest.AppendChild($caps)
        }
        while ($caps.HasChildNodes) { [void]$caps.RemoveChild($caps.FirstChild) }
        $caps.IsEmpty = $true
        $settings = [Xml.XmlWriterSettings]::new()
        $settings.Indent = $true
        $settings.Encoding = [Text.UTF8Encoding]::new($false)
        $w = [Xml.XmlWriter]::Create($dstManifest, $settings)
        try { $m.Save($w) } finally { $w.Dispose() }
    } else {
        Copy-Item -LiteralPath $srcManifest -Destination $dstManifest -Force
    }
    Write-Output ("已同步安装到: {0}" -f $modDst)
}
