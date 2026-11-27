#Requires -Version 5.1
<#
    screenshot.ps1 - capture the game's client area to a PNG.

    Why: the MOD's MCP exposes the UI tree (labels, refs, rects) but no pixels.
    To actually look at the game we need the real framebuffer.

    Two modes:
      default    CopyFromScreen of the window's client rect - requires the window
                 to be visible and on top (we bring it forward first).
      -PrintWindow  PrintWindow API - works while occluded, but DirectX games
                 often return a black frame; try only if the default fails.

    Pure ASCII on purpose (Windows PowerShell 5.1 decodes BOM-less files as ANSI).
#>
[CmdletBinding()]
param(
    [string]$Out = "D:\0TavernWeave\probably-stolen\shots\game.png",
    [switch]$NoFocus,
    [switch]$PrintWindow,
    [switch]$FullScreen
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class ShotNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
}
"@

function Get-GameProcess {
    $p = Get-Process 'Probably Stolen' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { throw 'game process "Probably Stolen" not found' }
    $p.Refresh()
    if ($p.MainWindowHandle -eq 0) { throw 'game has no main window handle' }
    return $p
}

function Set-GameForeground($hwnd) {
    [void][ShotNative]::ShowWindow($hwnd, 9)      # SW_RESTORE
    Start-Sleep -Milliseconds 150
    [void][ShotNative]::BringWindowToTop($hwnd)

    if ([ShotNative]::GetForegroundWindow() -eq $hwnd) { return $true }

    $fg = [ShotNative]::GetForegroundWindow()
    $fgThread = [ShotNative]::GetWindowThreadProcessId($fg, [IntPtr]::Zero)
    $myThread = [ShotNative]::GetCurrentThreadId()
    $attached = $false
    if ($fgThread -ne 0 -and $fgThread -ne $myThread) {
        $attached = [ShotNative]::AttachThreadInput($myThread, $fgThread, $true)
    }
    try {
        [void][ShotNative]::SetForegroundWindow($hwnd)
        [void][ShotNative]::BringWindowToTop($hwnd)
        [void][ShotNative]::SetFocus($hwnd)
    }
    finally {
        if ($attached) { [void][ShotNative]::AttachThreadInput($myThread, $fgThread, $false) }
    }

    if ([ShotNative]::GetForegroundWindow() -ne $hwnd) {
        # ALT tap: makes the shell treat this as a user-initiated activation
        [ShotNative]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
        [ShotNative]::keybd_event(0x12, 0, 0x0002, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 100
        [void][ShotNative]::SetForegroundWindow($hwnd)
    }
    Start-Sleep -Milliseconds 300
    return ([ShotNative]::GetForegroundWindow() -eq $hwnd)
}

$proc = Get-GameProcess

if ([ShotNative]::IsIconic($proc.MainWindowHandle)) {
    [void][ShotNative]::ShowWindow($proc.MainWindowHandle, 9)
    Start-Sleep -Milliseconds 400
}

$focused = $true
if (-not $NoFocus) { $focused = Set-GameForeground $proc.MainWindowHandle }

$cr = New-Object ShotNative+RECT
[void][ShotNative]::GetClientRect($proc.MainWindowHandle, [ref]$cr)
$clientW = $cr.Right - $cr.Left
$clientH = $cr.Bottom - $cr.Top

$origin = New-Object ShotNative+POINT
$origin.X = 0; $origin.Y = 0
[void][ShotNative]::ClientToScreen($proc.MainWindowHandle, [ref]$origin)

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

if ($PrintWindow) {
    $bmp = New-Object System.Drawing.Bitmap($clientW, $clientH)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    try {
        $ok = [ShotNative]::PrintWindow($proc.MainWindowHandle, $hdc, 2)   # PW_RENDERFULLCONTENT
    }
    finally {
        $g.ReleaseHdc($hdc)
        $g.Dispose()
    }
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $mode = "PrintWindow ok=$ok"
}
else {
    if ($FullScreen) {
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bmp.Size)
    }
    else {
        $bmp = New-Object System.Drawing.Bitmap($clientW, $clientH)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($origin.X, $origin.Y, 0, 0, (New-Object System.Drawing.Size($clientW, $clientH)))
    }
    $g.Dispose()
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $mode = "CopyFromScreen"
}

$file = Get-Item $Out
[pscustomobject]@{
    mode        = $mode
    file        = $file.FullName
    bytes       = $file.Length
    clientSize  = "$($clientW)x$($clientH)"
    clientOrigin= "$($origin.X),$($origin.Y)"
    focused     = $focused
} | ConvertTo-Json
