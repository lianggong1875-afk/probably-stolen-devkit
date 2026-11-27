#Requires -Version 5.1
<#
.SYNOPSIS
    兼容入口：组装《深空电玩》零能力正式发布包（内部转发至 New-ReleasePackage.ps1 -Mod Arcade）。
#>
[CmdletBinding()]
param(
    [string]$RunId = 'release-pkg',
    [string]$ManagedDir = 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
)
$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
& (Join-Path $here 'New-ReleasePackage.ps1') -Mod Arcade -RunId $RunId -ManagedDir $ManagedDir
exit $LASTEXITCODE
