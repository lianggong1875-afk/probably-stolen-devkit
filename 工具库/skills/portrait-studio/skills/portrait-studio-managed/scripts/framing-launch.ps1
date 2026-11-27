param(
    [Parameter(Position=0)][string]$Source,
    [switch]$NoBrowser,
    [ValidateRange(1,43200)][int]$Timeout=1800
)
$ErrorActionPreference = 'Stop'
try {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'framing.py'))) {
        throw '请保留整个 scripts 文件夹，framing.py 需要与启动文件放在一起。'
    }
    $candidates = [System.Collections.Generic.List[object]]::new()
    function Add-Python([string]$Path, [string]$Prefix='') {
        if ($Path -and (Test-Path -LiteralPath $Path -PathType Leaf)) {
            $candidates.Add(@{Path=$Path; Prefix=$Prefix})
        }
    }
    # Optional portable runtime; never download or install anything automatically.
    Add-Python (Join-Path $PSScriptRoot 'python/python.exe')
    Add-Python $env:PORTRAITSTUDIO_PYTHON
    foreach ($command in 'py.exe','python.exe','python3.exe') {
        foreach ($found in @(Get-Command $command -CommandType Application -All -ErrorAction SilentlyContinue)) {
            # Windows Store aliases are not installed Python interpreters.
            if ($command -ne 'py.exe' -and $found.Source -like '*\Microsoft\WindowsApps\*') { continue }
            $prefix = if ($command -eq 'py.exe') { '-3' } else { '' }
            Add-Python $found.Source $prefix
        }
    }
    foreach ($registry in 'HKCU:\Software\Python\PythonCore','HKLM:\Software\Python\PythonCore','HKLM:\Software\WOW6432Node\Python\PythonCore') {
        foreach ($version in @(Get-ChildItem -LiteralPath $registry -ErrorAction SilentlyContinue)) {
            $key = Get-Item -LiteralPath ($version.PSPath+'\InstallPath') -ErrorAction SilentlyContinue
            if ($key) {
                Add-Python $key.GetValue('ExecutablePath')
                $directory = $key.GetValue('')
                if ($directory) { Add-Python (Join-Path $directory 'python.exe') }
            }
        }
    }
    if ($env:LOCALAPPDATA) {
        foreach ($installation in @(Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'Programs/Python') -Directory -ErrorAction SilentlyContinue)) {
            Add-Python (Join-Path $installation.FullName 'python.exe')
        }
    }
    # Codex users may already have a bundled runtime. This is an optional fallback.
    if ($env:USERPROFILE) { Add-Python (Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe') }
    $runtime = $null
    foreach ($candidate in $candidates) {
        $probe = [System.Diagnostics.Process]::new()
        try {
            $probe.StartInfo.FileName = $candidate.Path
            $probe.StartInfo.Arguments = $candidate.Prefix+' -c "import sys; sys.exit(0 if sys.version_info >= (3,10) else 1)"'
            $probe.StartInfo.UseShellExecute = $false
            $probe.StartInfo.CreateNoWindow = $true
            $probe.StartInfo.RedirectStandardOutput = $true
            $probe.StartInfo.RedirectStandardError = $true
            [void]$probe.Start()
            if (-not $probe.WaitForExit(5000)) { $probe.Kill(); continue }
            if ($probe.ExitCode -eq 0) { $runtime = $candidate; break }
        } catch { continue }
        finally { $probe.Dispose() }
    }
    if (-not $runtime) {
        throw "没有找到 Python 3.10 或更新版本。请从 https://www.python.org/downloads/windows/ 安装 Python，然后重新双击启动。安装到当前用户即可，无需管理员权限。"
    }
    if (-not $Source) {
        Add-Type -AssemblyName System.Windows.Forms
        $picker = New-Object System.Windows.Forms.FolderBrowserDialog
        try {
            $picker.Description = '选择立绘包源码文件夹（包含 portrait-studio-provider.json）'
            $picker.ShowNewFolderButton = $false
            if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 0 }
            $Source = $picker.SelectedPath
        } finally { $picker.Dispose() }
    }
    $Source = (Resolve-Path -LiteralPath $Source).Path
    if (-not (Test-Path -LiteralPath (Join-Path $Source 'portrait-studio-provider.json') -PathType Leaf)) {
        throw '所选文件夹没有 portrait-studio-provider.json。请选择立绘包的源码工程文件夹。'
    }
    Write-Host '正在打开构图页。使用期间保留此窗口，结束时按 Ctrl+C。'
    $arguments = @()
    if ($runtime.Prefix) { $arguments += $runtime.Prefix }
    $arguments += @('-B','-X','utf8',(Join-Path $PSScriptRoot 'framing.py'),'--source',$Source,'--timeout',"$Timeout")
    if ($NoBrowser) { $arguments += '--no-browser' }
    & $runtime.Path @arguments
    exit $LASTEXITCODE
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
