#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Candidate,
    [string]$OutputDir = '',
    [string]$ContentDir = '',
    [string]$ManagedDir = '',
    [string]$ExpectedCapabilities = '',
    [switch]$PublicRelease
)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ManagedDir)) {
    $ManagedDir = if ($env:PS_GAME_MANAGED) { $env:PS_GAME_MANAGED } else { 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed' }
}

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$workspace = (Resolve-Path -LiteralPath (Join-Path $here '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $workspace '缓存文件夹\loader-scan'
}
if (!(Test-Path -LiteralPath $OutputDir)) { [void](New-Item -ItemType Directory -Path $OutputDir -Force) }

$dir = (Resolve-Path -LiteralPath $OutputDir).Path
$candidatePath = (Resolve-Path -LiteralPath $Candidate).Path
$dllFileName = [IO.Path]::GetFileName($candidatePath)
$stage = Join-Path $dir 'scan-package'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
[void](New-Item -ItemType Directory -Path $stage -Force)

if ([string]::IsNullOrWhiteSpace($ContentDir)) {
    $projectDir = if ($dllFileName -ieq 'DeepSpacePawn.dll') {
        Join-Path $workspace '深空当铺：可能是群友'
    } else {
        Join-Path $workspace '深空电玩'
    }
    $srcManifest = Join-Path $projectDir 'manifest.xml'
    if (Test-Path -LiteralPath $srcManifest -PathType Leaf) {
        Copy-Item -LiteralPath $srcManifest -Destination (Join-Path $stage 'manifest.xml') -Force
    }
    foreach ($sub in @('Textures', 'Books', 'Localization', 'Audio', 'Data')) {
        $srcSub = Join-Path $projectDir $sub
        if (Test-Path -LiteralPath $srcSub -PathType Container) {
            Copy-Item -LiteralPath $srcSub -Destination (Join-Path $stage $sub) -Recurse -Force
        }
    }
} else {
    $root = (Resolve-Path -LiteralPath $ContentDir).Path
    Remove-Item -LiteralPath $stage -Recurse -Force
    Copy-Item -LiteralPath $root -Destination $stage -Recurse
}

if ($PublicRelease) {
    $stagedManifest = Join-Path $stage 'manifest.xml'
    if (Test-Path -LiteralPath $stagedManifest -PathType Leaf) {
        [xml]$m = Get-Content -LiteralPath $stagedManifest -Encoding UTF8
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
        $w = [Xml.XmlWriter]::Create($stagedManifest, $settings)
        try { $m.Save($w) } finally { $w.Dispose() }
    }
}

$game = (Resolve-Path -LiteralPath $ManagedDir).Path

$stagedDll = Join-Path $stage $dllFileName
Copy-Item -LiteralPath $candidatePath -Destination $stagedDll -Force

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$sdk = (Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\sdk' -Directory | Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).FullName
$ref = (Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' -Directory | Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).FullName
$out = Join-Path $dir 'ArcadeLoaderScanProbe.dll'
$refs = @(Get-ChildItem -LiteralPath (Join-Path $ref 'ref\net10.0') -Filter '*.dll' | ForEach-Object { '/r:' + $_.FullName })
& $dotnet (Join-Path $sdk 'Roslyn\bincore\csc.dll') /nologo /nostdlib+ /target:exe /langversion:9.0 ('/out:' + $out) @refs (Join-Path $here 'ArcadeLoaderScanProbe.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $dir 'ArcadeLoaderScanProbe.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}', $utf8)
$log = Join-Path $dir 'loader-scan.utf8.log'
$writer = [IO.StreamWriter]::new($log, $false, $utf8)
$expectedArg = if ([string]::IsNullOrWhiteSpace($ExpectedCapabilities)) { '-' } else { $ExpectedCapabilities }
try {
    & $dotnet exec $out $game $stagedDll $stage $expectedArg | ForEach-Object { $writer.WriteLine($_); $_ }
    $result = $LASTEXITCODE
} finally { $writer.Dispose() }
exit $result
