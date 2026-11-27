#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Candidate,
    [string]$OutputDir = '',
    [string]$ContentDir = '',
    [string]$ManagedDir = 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
)
$ErrorActionPreference = 'Stop'

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$workspace = (Resolve-Path -LiteralPath (Join-Path $here '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $workspace '缓存文件夹\loader-scan'
}
if (!(Test-Path -LiteralPath $OutputDir)) { [void](New-Item -ItemType Directory -Path $OutputDir -Force) }

$dir = (Resolve-Path -LiteralPath $OutputDir).Path
$candidatePath = (Resolve-Path -LiteralPath $Candidate).Path
$dllFileName = [IO.Path]::GetFileName($candidatePath)
if ([string]::IsNullOrWhiteSpace($ContentDir)) {
    $modFolder = if ($dllFileName -ieq 'DeepSpacePawn.dll') { 'DeepSpacePawn' } else { 'DeepSpaceArcade' }
    $ContentDir = Join-Path 'D:\steam\steamapps\common\Probably Stolen\Mods' $modFolder
}
$game = (Resolve-Path -LiteralPath $ManagedDir).Path
$root = (Resolve-Path -LiteralPath $ContentDir).Path
$stage = Join-Path $dir 'scan-package'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
Copy-Item -LiteralPath $root -Destination $stage -Recurse

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
try {
    & $dotnet exec $out $game $stagedDll $stage 'files' | ForEach-Object { $writer.WriteLine($_); $_ }
    $result = $LASTEXITCODE
} finally { $writer.Dispose() }
exit $result
