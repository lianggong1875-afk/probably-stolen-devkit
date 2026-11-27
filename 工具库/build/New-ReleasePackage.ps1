#Requires -Version 5.1
<#
.SYNOPSIS
    统一组装零能力 Steam 创意工坊发布包（支持《深空电玩》与《深空当铺：可能是群友》）。
    输出纯净、可直接指向 SteamUGC SetItemContent 的目录，含零能力校验、封面校验与文件纯净度校验。

.EXAMPLE
    .\工具库\build\New-ReleasePackage.ps1 -Mod All
    .\工具库\build\New-ReleasePackage.ps1 -Mod Arcade -RunId release-0.2.0
    .\工具库\build\New-ReleasePackage.ps1 -Mod Freya -RunId release-0.7.0
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'Arcade', 'Freya')][string]$Mod = 'All',
    [string]$RunId = 'release-pkg',
    [string]$ManagedDir = 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$workspace = (Resolve-Path -LiteralPath (Join-Path $here '..\..')).Path
$utf8NoBom = [Text.UTF8Encoding]::new($false)

function Build-OnePackage([string]$TargetMod) {
    $isArcade = ($TargetMod -eq 'Arcade')
    $name = if ($isArcade) { 'DeepSpaceArcade' } else { 'DeepSpacePawn' }
    $projectDir = if ($isArcade) { Join-Path $workspace '深空电玩' } else { Join-Path $workspace '深空当铺：可能是群友' }
    if (!(Test-Path -LiteralPath $projectDir -PathType Container)) { throw "未找到项目目录: $projectDir" }

    Write-Host ""
    Write-Host "================ 封装 $name ($TargetMod) ================"

    $buildRoot = Join-Path $projectDir 'build'
    $pkgRoot = Join-Path $buildRoot $RunId
    if (Test-Path -LiteralPath $pkgRoot) { Remove-Item -LiteralPath $pkgRoot -Recurse -Force }
    $pkg = Join-Path $pkgRoot $name

    # 1. 公开零能力构建
    $buildId = "$RunId-build"
    Write-Host "[1/5] 公开零能力构建 -> $buildId"
    & (Join-Path $here 'Build-Mod.ps1') -Mod $TargetMod -ManagedDir $ManagedDir -RunId $buildId -PublicRelease | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$name 公开版构建失败 ($LASTEXITCODE)" }
    $candidate = Join-Path (Join-Path $buildRoot $buildId) "$name.dll"
    if (!(Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "未找到构建产物: $candidate" }

    # 2. 组装干净上传目录（DLL + 自闭合 <Capabilities/> manifest + preview.png + 资源子目录）
    Write-Host '[2/5] 组装干净工坊上传目录'
    [void](New-Item -ItemType Directory -Path $pkg -Force)
    Copy-Item -LiteralPath $candidate -Destination (Join-Path $pkg "$name.dll") -Force

    $srcManifest = Join-Path $projectDir 'manifest.xml'
    [xml]$m = Get-Content -LiteralPath $srcManifest -Encoding UTF8
    $modId = $m.Manifest.ID
    $modName = $m.Manifest.Name
    $version = $m.Manifest.ModVersion
    $declaredAsms = @($m.Manifest.Assemblies.Assembly | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    $caps = $m.Manifest.SelectSingleNode('Capabilities')
    if ($null -eq $caps) {
        $caps = $m.CreateElement('Capabilities')
        [void]$m.Manifest.AppendChild($caps)
    }
    while ($caps.HasChildNodes) { [void]$caps.RemoveChild($caps.FirstChild) }
    $caps.IsEmpty = $true
    $capsOut = Join-Path $pkg 'manifest.xml'
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = $utf8NoBom
    $writer = [Xml.XmlWriter]::Create($capsOut, $settings)
    try { $m.Save($writer) } finally { $writer.Dispose() }

    # preview.png（必填，1280x720，< 1MB）
    $srcPreview = Join-Path $projectDir 'preview.png'
    if (!(Test-Path -LiteralPath $srcPreview -PathType Leaf)) { throw "$name 缺少封面图: $srcPreview" }
    $dstPreview = Join-Path $pkg 'preview.png'
    Copy-Item -LiteralPath $srcPreview -Destination $dstPreview -Force

    foreach ($sub in @('Textures', 'Books', 'Localization')) {
        $from = Join-Path $projectDir $sub
        if (Test-Path -LiteralPath $from -PathType Container) {
            Copy-Item -LiteralPath $from -Destination (Join-Path $pkg $sub) -Recurse
        }
    }

    # 3. 校验封面、扩展名白名单、资源文件数与程序集声明
    Write-Host '[3/5] 校验封面规格、文件纯净度与资源清单'
    $previewLen = (Get-Item -LiteralPath $dstPreview).Length
    $img = [System.Drawing.Image]::FromFile($dstPreview)
    try {
        $pw = $img.Width
        $ph = $img.Height
    } finally { $img.Dispose() }
    if ($pw -ne 1280 -or $ph -ne 720) { throw "preview.png 尺寸应为 1280x720，实际为 ${pw}x${ph}" }
    if ($previewLen -ge 1048576) { throw "preview.png 体积 ($previewLen bytes) 超过 Steam 1MB 限制" }
    Write-Host ("       preview.png: {0}x{1}, {2:N1} KB OK" -f $pw, $ph, ($previewLen / 1KB))

    $allowedExt = @('.dll', '.xml', '.png', '.json', '.txt', '.md', '.wav')
    $allFiles = @(Get-ChildItem -LiteralPath $pkg -Recurse -File | Sort-Object FullName)
    foreach ($f in $allFiles) {
        $ext = $f.Extension.ToLowerInvariant()
        if ($allowedExt -notcontains $ext) {
            throw "发布包中存在禁止的文件类型 [$ext]: $($f.FullName)"
        }
    }
    $pkgDlls = @($allFiles | Where-Object { $_.Extension -ieq '.dll' } | ForEach-Object { $_.Name })
    if (($pkgDlls -join ',') -ne ($declaredAsms -join ',')) {
        throw "DLL 列表与 manifest <Assemblies> 不一致: 包内 [$($pkgDlls -join ',')] vs 清单 [$($declaredAsms -join ',')]"
    }

    foreach ($rel in @('Textures', 'Textures\Items', 'Textures\Paper', 'Books')) {
        $srcDir = Join-Path $projectDir $rel
        $dstDir = Join-Path $pkg $rel
        $srcCount = if (Test-Path -LiteralPath $srcDir) { @(Get-ChildItem -LiteralPath $srcDir -File).Count } else { 0 }
        $dstCount = if (Test-Path -LiteralPath $dstDir) { @(Get-ChildItem -LiteralPath $dstDir -File).Count } else { 0 }
        if ($srcCount -ne $dstCount) { throw "${rel} 文件数不一致: 源 $srcCount vs 包 $dstCount" }
        if ($srcCount -gt 0) { Write-Host ("       {0}: {1} 文件 OK" -f $rel, $srcCount) }
    }

    # 4. 真实 ModScanner 零能力校验（直接扫描干净发布包目录）
    Write-Host '[4/5] 运行 ModScanner 校验（要求零能力）'
    $scanDir = Join-Path $pkgRoot 'scan'
    [void](New-Item -ItemType Directory -Path $scanDir -Force)
    $scanOut = & (Join-Path $here 'Test-ArcadeLoaderScan.ps1') -Candidate $candidate -ContentDir $pkg -OutputDir $scanDir -ManagedDir $ManagedDir 2>&1
    $scanOut | Out-Host
    $scanText = ($scanOut | Out-String)
    if ($scanText -match 'Refused:\s*True') { throw "$name 被 ModScanner 拒绝" }
    $capLine = @($scanText -split "`r?`n" | Where-Object { $_ -match '^Capabilities:' } | Select-Object -First 1)
    if ($capLine.Count -eq 0) { throw "扫描输出缺少 Capabilities 行" }
    if (($capLine[0] -replace '^Capabilities:\s*', '').Trim().Length -gt 0) { throw "公开版仍存在能力需求: $($capLine[0])" }
    $extraLine = @($scanText -split "`r?`n" | Where-Object { $_ -match '^Extra capabilities:' } | Select-Object -First 1)
    if ($extraLine.Count -gt 0 -and ($extraLine[0] -replace '^Extra capabilities:\s*', '').Trim().Length -gt 0) {
        throw "公开版存在额外能力: $($extraLine[0])"
    }

    # 5. 输出包外校验摘要（不污染上传包内部）
    Write-Host '[5/5] 生成发布包摘要'
    $lines = foreach ($f in $allFiles) {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash, $f.FullName.Substring($pkg.Length + 1)
    }
    [IO.File]::WriteAllLines((Join-Path $pkgRoot 'package-sha256.txt'), [string[]]$lines, $utf8NoBom)
    Write-Host ("发布包就绪: {0}" -f $pkg)
    Write-Host ("  ID={0} | Name={1} | Version={2} | Files={3}" -f $modId, $modName, $version, $allFiles.Count)
}

$targets = if ($Mod -eq 'All') { @('Arcade', 'Freya') } else { @($Mod) }
foreach ($t in $targets) {
    Build-OnePackage $t
}
