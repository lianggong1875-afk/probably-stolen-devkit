<#
.SYNOPSIS
    多路备份：本地裸仓镜像 + git bundle 单文件 + 美术/文案资产包。

.DESCRIPTION
    规范见 共享开发\分支与备份规范.md 第 4 节。备份要「多路」，缺一路不算备份：

      ① 本地裸仓镜像   把整仓（全部分支 + 全部 tag）推到另一块物理盘的裸仓库
                        —— 防的是「工作区 .git 所在盘坏掉」
      ② bundle 单文件  把整仓压成一个 .bundle
                        —— 防的是「裸仓和云端都没了」，可拷到网盘/移动硬盘
      ③ 资产包         美术与文案打 zip + 生成 SHA-256 清单
                        —— 防的是「git 全毁」，图与文案是重画成本最高的东西

    云端三仓（devkit / arcade / pawn）由 共享开发\云备份.ps1 负责，本脚本不重复做。

.PARAMETER MirrorRoot
    裸仓镜像的根目录。默认按 Z: Y: E: F: 顺序找第一个存在且不是工作区所在盘的路径。
    找不到就跳过 ① 并提示你手动指定。

.PARAMETER OutDir
    bundle 与资产 zip 的输出目录，默认 缓存文件夹\backups（已在 .gitignore 里）。

.PARAMETER Keep
    bundle 保留份数（默认 5，超出的最旧的删掉）。

.EXAMPLE
    .\工具库\build\Backup-All.ps1
    .\工具库\build\Backup-All.ps1 -MirrorRoot "E:\backup\ps-git"
    .\工具库\build\Backup-All.ps1 -SkipAssets
#>
param(
    [string]$MirrorRoot,
    [string]$OutDir,
    [int]$Keep = 5,
    [switch]$SkipMirror,
    [switch]$SkipBundle,
    [switch]$SkipAssets
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $OutDir) { $OutDir = Join-Path $repoRoot '缓存文件夹\backups' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmm'

function Step([string]$m) { Write-Host "[..] $m" -ForegroundColor Cyan }
function Ok([string]$m) { Write-Host "[OK] $m" -ForegroundColor Green }
function Warn([string]$m) { Write-Host "[WARN] $m" -ForegroundColor Yellow }

function Format-Size([long]$bytes) {
    if ($bytes -ge 1GB) { return ('{0:N2} GB' -f ($bytes / 1GB)) }
    if ($bytes -ge 1MB) { return ('{0:N1} MB' -f ($bytes / 1MB)) }
    return ('{0:N0} KB' -f ($bytes / 1KB))
}

Push-Location $repoRoot
try {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot '.git'))) { throw "不是 git 工作区：$repoRoot" }

    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    $head = (& git rev-parse --short HEAD).Trim()
    $tagCount = ((& git tag) | Measure-Object).Count
    $branchCount = ((& git branch) | Measure-Object).Count
    Write-Host "工作区：$repoRoot" -ForegroundColor White
    Write-Host "当前：$branch @ $head ｜ 分支 $branchCount 个 ｜ tag $tagCount 个" -ForegroundColor Gray

    $dirty = (& git status --porcelain)
    if ($dirty) {
        Warn "工作区有未提交改动（$((($dirty | Measure-Object).Count)) 处）—— 备份只含已提交内容"
    }

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    # ---------------- ① 本地裸仓镜像 ----------------
    if (-not $SkipMirror) {
        Step '① 裸仓镜像'
        $resolvedMirror = $MirrorRoot
        if (-not $resolvedMirror) {
            $driveOfRepo = (Split-Path -Qualifier $repoRoot).TrimEnd(':')
            foreach ($d in @('Z', 'Y', 'E', 'F', 'G')) {
                if ($d -eq $driveOfRepo) { continue }
                if (Test-Path -LiteralPath "${d}:\") {
                    $resolvedMirror = "${d}:\backup\ps-git"
                    break
                }
            }
        }

        if (-not $resolvedMirror) {
            Warn '找不到可用的第二块盘，跳过裸仓镜像。请用 -MirrorRoot "X:\backup\ps-git" 指定。'
        }
        else {
            $bare = Join-Path $resolvedMirror 'ps-workspace.git'
            try {
                if (-not (Test-Path -LiteralPath $bare)) {
                    New-Item -ItemType Directory -Force -Path $resolvedMirror | Out-Null
                    Step "初始化裸仓：$bare"
                    # 关键：显式指定主分支，否则裸仓 HEAD 指向 master，而我们用 main
                    # → 真出事时 clone 出来是空工作区（实测踩过）
                    & git init --bare --initial-branch=main $bare 2>$null | Out-Null
                    if ($LASTEXITCODE -ne 0) { & git init --bare $bare | Out-Null }
                }
                # --mirror 会同步全部分支与 tag，并删掉远端多余引用（这才是"镜像"）
                $prevEap3 = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
                $out = (& git push --mirror $bare *>&1 | Out-String)
                $ErrorActionPreference = $prevEap3
                if ($LASTEXITCODE -ne 0) { Warn "裸仓镜像失败：`n$out" }
                else {
                    # push --mirror 不会修正裸仓的 HEAD 符号引用；它必须指向 main，否则 clone 是空的
                    & git -C $bare symbolic-ref HEAD refs/heads/main 2>$null | Out-Null
                    $mirrorHead = (& git -C $bare symbolic-ref --short HEAD 2>$null)
                    $size = (Get-ChildItem $bare -Recurse -File | Measure-Object Length -Sum).Sum
                    if ($mirrorHead -and $mirrorHead.Trim() -eq 'main') {
                        Ok "裸仓镜像已同步：$bare（$(Format-Size $size)）｜HEAD=main"
                    } else {
                        Warn "裸仓镜像已同步但 HEAD 不是 main（当前 '$mirrorHead'），clone 会得到空工作区"
                    }
                }
            }
            catch { Warn "裸仓镜像异常：$($_.Exception.Message)" }
        }
    }
    else { Write-Host '[SKIP] 裸仓镜像' -ForegroundColor Yellow }

    # ---------------- ② bundle 单文件 ----------------
    if (-not $SkipBundle) {
        Step '② git bundle'
        try {
            $bundle = Join-Path $OutDir ("ps-workspace-{0}.bundle" -f $stamp)
            $prevEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
            & git bundle create $bundle --all *>&1 | Out-Null
            $ErrorActionPreference = $prevEap
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $bundle)) {
                Warn 'bundle 创建失败'
            }
            else {
                $prevEap2 = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
                $verify = (& git bundle verify $bundle *>&1 | Out-String)
                $ErrorActionPreference = $prevEap2
                if ($verify -match 'is okay|ok$') { Ok "bundle 已生成并通过 verify：$bundle（$(Format-Size (Get-Item $bundle).Length)）" }
                else { Warn "bundle 生成了但 verify 没通过：`n$verify" }

                # 只留最近 $Keep 份
                $old = Get-ChildItem $OutDir -Filter 'ps-workspace-*.bundle' | Sort-Object LastWriteTime -Descending | Select-Object -Skip $Keep
                foreach ($f in $old) { Remove-Item $f.FullName -Force; Write-Host "    清理旧 bundle：$($f.Name)" -ForegroundColor DarkGray }
            }
        }
        catch { Warn "bundle 异常：$($_.Exception.Message)" }
    }
    else { Write-Host '[SKIP] bundle' -ForegroundColor Yellow }

    # ---------------- ③ 资产包 ----------------
    if (-not $SkipAssets) {
        Step '③ 美术/文案资产包'
        $assetRels = @(
            '深空电玩\Textures', '深空电玩\art', '深空电玩\Books',
            '深空当铺：可能是群友\Textures', '深空当铺：可能是群友\art', '深空当铺：可能是群友\Books', '深空当铺：可能是群友\writing'
        )
        $existing = @()
        foreach ($r in $assetRels) {
            $p = Join-Path $repoRoot $r
            if (Test-Path -LiteralPath $p) { $existing += $p }
        }
        if (-not $existing) {
            Warn '没找到任何资产目录，跳过资产包'
        }
        else {
            try {
                $zip = Join-Path $OutDir ("ps-assets-{0}.zip" -f $stamp)
                if (Test-Path -LiteralPath $zip) { Remove-Item $zip -Force }
                Compress-Archive -Path $existing -DestinationPath $zip -CompressionLevel Optimal -Force

                # SHA-256 清单（恢复后用它核验图有没有被换掉）
                $listPath = "$zip.sha256.txt"
                $lines = New-Object System.Collections.Generic.List[string]
                $lines.Add("# 资产 SHA-256 清单（$stamp）")
                $lines.Add("# 目录数 $($existing.Count)")
                foreach ($p in $existing) {
                    $files = Get-ChildItem -LiteralPath $p -Recurse -File -ErrorAction SilentlyContinue
                    $lines.Add("## " + ($p.Substring($repoRoot.Length).TrimStart('\')))
                    foreach ($f in $files) {
                        $h = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
                        $rel = $f.FullName.Substring($repoRoot.Length).TrimStart('\')
                        $lines.Add("$h  $rel")
                    }
                }
                [System.IO.File]::WriteAllLines($listPath, $lines, (New-Object System.Text.UTF8Encoding($false)))
                Ok "资产包：$zip（$(Format-Size (Get-Item $zip).Length)），清单 $(Format-Size (Get-Item $listPath).Length)"

                $oldZip = Get-ChildItem $OutDir -Filter 'ps-assets-*.zip' | Sort-Object LastWriteTime -Descending | Select-Object -Skip $Keep
                foreach ($f in $oldZip) {
                    Remove-Item $f.FullName -Force
                    $side = "$($f.FullName).sha256.txt"
                    if (Test-Path -LiteralPath $side) { Remove-Item $side -Force }
                    Write-Host "    清理旧资产包：$($f.Name)" -ForegroundColor DarkGray
                }
            }
            catch { Warn "资产包异常：$($_.Exception.Message)" }
        }
    }
    else { Write-Host '[SKIP] 资产包' -ForegroundColor Yellow }

    Write-Host ''
    Write-Host '备份结果：' -ForegroundColor White
    Write-Host "  ① 裸仓镜像：$(if($SkipMirror){'已跳过'}elseif($resolvedMirror){"$resolvedMirror"}else{'未找到第二块盘'})"
    Write-Host "  ② bundle  ：$(if($SkipBundle){'已跳过'}else{"$OutDir"})"
    Write-Host "  ③ 资产包  ：$(if($SkipAssets){'已跳过'}else{"$OutDir"})"
    Write-Host ''
    Write-Host '别忘了云端三仓：.\共享开发\云备份.ps1 -Branch main（合并到主干之后跑）' -ForegroundColor Gray
}
finally {
    Pop-Location
}
