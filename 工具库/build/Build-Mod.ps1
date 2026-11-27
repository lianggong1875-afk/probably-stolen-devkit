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
    [string]$ManagedDir = 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed',
    [string]$RunId = '',
    [string]$SdkRoot = 'C:\Program Files\dotnet\sdk',
    [switch]$PublicRelease,
    [switch]$Scan,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'

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
if ($Mod -eq 'Arcade') {
    $projectFile = Join-Path $projectDir "$name.csproj"
    $objDir = Join-Path $outDir 'obj'
    $extra = @()
    if ($PublicRelease) { $extra = @('-p:DefineConstants=ARCADE_PUBLIC') }
    & dotnet build $projectFile -c Release --nologo "-p:ManagedDir=$managed" `
        "-p:BaseIntermediateOutputPath=$objDir/" "-p:MSBuildProjectExtensionsPath=$objDir/" "-p:OutputPath=$outDir/" @extra
} else {
    $refs = @('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','netstandard','Newtonsoft.Json',
        'UnityEngine.CoreModule','UnityEngine.InputLegacyModule','UnityEngine.TextRenderingModule','UnityEngine.AudioModule',
        'UnityEngine.UIModule','UnityEngine.UI','Unity.TextMeshPro','UnityEngine.JSONSerializeModule','UnityEngine.Physics2DModule',
        'Assembly-CSharp','Assembly-CSharp-firstpass','0Harmony')
    $references = foreach ($r in $refs) {
        $p = Join-Path $managed "$r.dll"
        if (!(Test-Path -LiteralPath $p -PathType Leaf)) { throw "缺少引用: $p" }
        "/reference:$p"
    }
    $cscArgs = @('/nologo','/target:library','/nostdlib+','/langversion:9.0','/optimize+',
        '/nowarn:1701,1702,1591','/codepage:65001','/deterministic',"/out:$dll") + $references + $sources.FullName
    & dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') @cscArgs
}

if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $dll -PathType Leaf)) { throw '编译失败' }

$hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
Write-Output ("编译成功: {0} ({1} 个源码文件)" -f $dll, $sources.Count)
Write-Output ("SHA256 : {0}" -f $hash)

if ($Scan) {
    Write-Output "--- 运行 ModScanner 能力扫描 ---"
    $scanOutDir = Join-Path $workspace '缓存文件夹\loader-scan'
    & (Join-Path $here 'Test-ArcadeLoaderScan.ps1') -Candidate $dll -OutputDir $scanOutDir -ManagedDir $managed
    if ($LASTEXITCODE -ne 0) { throw "能力扫描失败 ($LASTEXITCODE)" }
}

if ($Install) {
    $gameRoot = Split-Path -Parent (Split-Path -Parent $managed)
    $modDst = Join-Path (Join-Path $gameRoot 'Mods') $name
    [void](New-Item -ItemType Directory -Path $modDst -Force)

    Copy-Item -LiteralPath $dll -Destination (Join-Path $modDst "$name.dll") -Force
    foreach ($sub in @('Textures', 'Books', 'Localization')) {
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
