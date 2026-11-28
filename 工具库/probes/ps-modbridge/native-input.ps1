#Requires -Version 5.1
<#
    native-input.ps1 - drive the real Windows input stack at the game window.

    Why this exists: a good part of Probably Stolen's UI is drawn with its own
    PixelWindow/PixelElement system and reads Input.GetMouseButton / GetKey
    by polling, so a synthetic Unity EventSystem dispatch (what the MOD's
    operate_ui does) cannot reach it. For those nodes - and for keyboard keys
    like ESC, which the MCP surface does not expose at all - we post real input.

    Coordinates: the MCP tools report Unity screen space (origin bottom-left).
    This script converts to Windows screen pixels (origin top-left) using the
    window's client rect, so both spaces can be used side by side.

    Pure ASCII on purpose: Windows PowerShell 5.1 decodes BOM-less files as ANSI.
#>
[CmdletBinding()]
param(
    [ValidateSet('key', 'type', 'click', 'mousemove', 'drag', 'info')]
    [string]$Action = 'info',
    [string]$Key = '',
    [string]$Text = '',
    [int]$X = -1,
    [int]$Y = -1,
    [int]$ToX = -1,
    [int]$ToY = -1,
    [int]$HoldMs = 40,
    [int]$SettleMs = 350,
    [switch]$NoFocus
)

$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

# Windows only lets the foreground process hand focus over; a background process
# calling SetForegroundWindow alone is silently ignored. Attaching our input
# queue to the current foreground thread (and tapping ALT) is the reliable way.
function Set-GameForeground($hwnd) {
    [void][NInput]::ShowWindow($hwnd, 9)          # SW_RESTORE
    Start-Sleep -Milliseconds 120
    [void][NInput]::BringWindowToTop($hwnd)

    $fg = [NInput]::GetForegroundWindow()
    if ($fg -eq $hwnd) { return $true }

    $fgThread = [NInput]::GetWindowThreadProcessId($fg, [IntPtr]::Zero)
    $myThread = [NInput]::GetCurrentThreadId()
    $attached = $false
    if ($fgThread -ne 0 -and $fgThread -ne $myThread) {
        $attached = [NInput]::AttachThreadInput($myThread, $fgThread, $true)
    }
    try {
        [void][NInput]::SetForegroundWindow($hwnd)
        [void][NInput]::BringWindowToTop($hwnd)
        [void][NInput]::SetFocus($hwnd)
    }
    finally {
        if ($attached) { [void][NInput]::AttachThreadInput($myThread, $fgThread, $false) }
    }

    if ([NInput]::GetForegroundWindow() -ne $hwnd) {
        # ALT tap: convinces the shell this is a user-initiated activation
        [NInput]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
        [NInput]::keybd_event(0x12, 0, 0x0002, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 80
        [void][NInput]::SetForegroundWindow($hwnd)
    }
    Start-Sleep -Milliseconds 250
    return ([NInput]::GetForegroundWindow() -eq $hwnd)
}

function Get-GameWindow {
    $p = Get-Process 'Probably Stolen' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { throw 'game process "Probably Stolen" not found' }
    $p.Refresh()
    if ($p.MainWindowHandle -eq 0) { throw 'game has no main window handle yet' }
    return $p
}

function Get-Geometry($p) {
    $cr = New-Object NInput+RECT
    [void][NInput]::GetClientRect($p.MainWindowHandle, [ref]$cr)
    $pt = New-Object NInput+POINT
    $pt.X = 0; $pt.Y = 0
    [void][NInput]::ClientToScreen($p.MainWindowHandle, [ref]$pt)
    return [pscustomobject]@{
        ClientWidth  = $cr.Right - $cr.Left
        ClientHeight = $cr.Bottom - $cr.Top
        OriginX      = $pt.X
        OriginY      = $pt.Y
    }
}

$proc = Get-GameWindow
$geo = Get-Geometry $proc

if ($Action -eq 'info') {
    $fg = [NInput]::GetForegroundWindow()
    $cur = New-Object NInput+POINT
    [void][NInput]::GetCursorPos([ref]$cur)
    [pscustomobject]@{
        pid           = $proc.Id
        hwnd          = $proc.MainWindowHandle
        clientSize    = "$($geo.ClientWidth)x$($geo.ClientHeight)"
        clientOrigin  = "$($geo.OriginX),$($geo.OriginY)"
        isForeground  = ($fg -eq $proc.MainWindowHandle)
        cursor        = "$($cur.X),$($cur.Y)"
    } | ConvertTo-Json
    exit 0
}

# Remember who owned the screen before we steal it, so a drag can hand
# the cursor and the foreground back to the user afterwards.
$prevFg = [NInput]::GetForegroundWindow()

if (-not $NoFocus) {
    $focused = Set-GameForeground $proc.MainWindowHandle
    if (-not $focused) {
        # Never fire input blind: it lands in whatever window the user is using.
        Write-Warning 'game window did not take focus; refusing to send input'
        [pscustomobject]@{ action = $Action; aborted = $true; reason = 'focus_not_acquired' } | ConvertTo-Json
        exit 2
    }
}

if ($Action -eq 'type') {
    if (-not $Text) { throw 'type action needs -Text' }
    Add-Type -AssemblyName System.Windows.Forms
    # SendKeys drives SendInput under the hood, so the game sees real keystrokes.
    # Characters +^%~(){}[] are special in SendKeys syntax; this simple path is for
    # plain identifiers (letters, digits, dash, underscore, space).
    if ($Text -match '[+^%~()\{\}\[\]]') { throw "text contains SendKeys metacharacters; extend the script before using it: $Text" }
    [System.Windows.Forms.SendKeys]::SendWait($Text)
    Start-Sleep -Milliseconds $SettleMs
    [pscustomobject]@{ action = 'type'; text = $Text; foreground = ([NInput]::GetForegroundWindow() -eq $proc.MainWindowHandle) } | ConvertTo-Json
    exit 0
}

if ($Action -eq 'key') {
    if (-not $Key) { throw 'key action needs -Key (e.g. ESC, SPACE, F1)' }
    # http://www.kbdedit.com/manual/low_level_vk_list.html
    $vkMap = @{
        'ESC' = 0x1B; 'ESCAPE' = 0x1B; 'SPACE' = 0x20; 'ENTER' = 0x0D; 'TAB' = 0x09
        'BACKSPACE' = 0x08; 'DELETE' = 0x2E; 'UP' = 0x26; 'DOWN' = 0x28; 'LEFT' = 0x25; 'RIGHT' = 0x27
        'F1' = 0x70; 'F2' = 0x71; 'F3' = 0x72; 'F4' = 0x73; 'Q' = 0x51; 'E' = 0x45; 'O' = 0x4F
        'M' = 0x4D; 'S' = 0x53; 'L' = 0x4C; 'I' = 0x49; 'C' = 0x43; 'R' = 0x52
    }
    $upper = $Key.ToUpperInvariant()
    if (-not $vkMap.ContainsKey($upper)) { throw "unsupported key '$Key' (add it to the vkMap table)" }
    $vk = [byte]$vkMap[$upper]
    [NInput]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $HoldMs
    [NInput]::keybd_event($vk, 0, 0x0002, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $SettleMs
    [pscustomobject]@{ action = 'key'; key = $upper; vk = $vk; foreground = ([NInput]::GetForegroundWindow() -eq $proc.MainWindowHandle) } | ConvertTo-Json
    exit 0
}

if ($X -lt 0 -or $Y -lt 0) { throw 'click/mousemove needs -X and -Y in Unity screen space (origin bottom-left)' }

# Unity screen space (bottom-left) -> Windows screen pixels (top-left)
$screenX = $geo.OriginX + $X
$screenY = $geo.OriginY + ($geo.ClientHeight - $Y)

if ($Action -eq 'drag') {
    if ($ToX -lt 0 -or $ToY -lt 0) { throw 'drag action needs -ToX and -ToY in Unity screen space' }
    $toScreenX = $geo.OriginX + $ToX
    $toScreenY = $geo.OriginY + ($geo.ClientHeight - $ToY)
    $steps = 14
    $saved = New-Object NInput+POINT
    [void][NInput]::GetCursorPos([ref]$saved)
    try {
        [void][NInput]::SetCursorPos($screenX, $screenY)
        Start-Sleep -Milliseconds 90
        [NInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
        Start-Sleep -Milliseconds 90
        for ($i = 1; $i -le $steps; $i++) {
            $t = $i / [double]$steps
            $mx = [int]($screenX + ($toScreenX - $screenX) * $t)
            $my = [int]($screenY + ($toScreenY - $screenY) * $t)
            [void][NInput]::SetCursorPos($mx, $my)
            Start-Sleep -Milliseconds 18
        }
        Start-Sleep -Milliseconds 90
        [NInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
        Start-Sleep -Milliseconds $SettleMs
    }
    finally {
        # Hand the cursor AND the foreground back to whoever had them.
        [void][NInput]::SetCursorPos($saved.X, $saved.Y)
        if ($prevFg -ne [IntPtr]::Zero -and $prevFg -ne $proc.MainWindowHandle) {
            [void][NInput]::SetForegroundWindow($prevFg)
        }
    }
    [pscustomobject]@{ action = 'drag'; from = "$X,$Y"; to = "$ToX,$ToY"; screen = "$screenX,$screenY -> $toScreenX,$toScreenY"; steps = $steps; cursorRestored = "$($saved.X),$($saved.Y)"; foregroundRestored = ([NInput]::GetForegroundWindow() -eq $prevFg) } | ConvertTo-Json
    exit 0
}

if ($Action -eq 'mousemove') {
    [void][NInput]::SetCursorPos($screenX, $screenY)
    Start-Sleep -Milliseconds $SettleMs
    [pscustomobject]@{ action = 'mousemove'; unity = "$X,$Y"; screen = "$screenX,$screenY" } | ConvertTo-Json
    exit 0
}

[void][NInput]::SetCursorPos($screenX, $screenY)
Start-Sleep -Milliseconds 120
[NInput]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
Start-Sleep -Milliseconds $HoldMs
[NInput]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
Start-Sleep -Milliseconds $SettleMs
[pscustomobject]@{ action = 'click'; unity = "$X,$Y"; screen = "$screenX,$screenY"; released = $true } | ConvertTo-Json
exit 0
