param(
    [Parameter(Mandatory = $true)][string]$Tool,
    [string]$Json = "{}",
    [switch]$Raw
)

# Bridge call helper. Keeps stdout/stderr separate (a native stderr record would
# otherwise poison ConvertFrom-Json), and stages the args file somewhere without
# spaces -- Start-Process splits unquoted paths that contain them.
$ErrorActionPreference = "Stop"
$dir   = "D:\0TavernWeave\probably-stolen\mcp-log"
$id    = [guid]::NewGuid().ToString("N")
$argF  = Join-Path $dir ("arg-$id.json")
$outF  = Join-Path $dir ("out-$id.txt")
$errF  = Join-Path $dir ("err-$id.txt")
$mcp   = "D:\0TavernWeave\tools\ps-modbridge\mcp.mjs"

[System.IO.File]::WriteAllText($argF, $Json, (New-Object System.Text.UTF8Encoding($false)))
$null = Start-Process -FilePath "node" `
    -ArgumentList @($mcp, 'tool', $Tool, "`"@$argF`"", '--compact') `
    -NoNewWindow -PassThru -Wait -RedirectStandardOutput $outF -RedirectStandardError $errF

$o = [System.IO.File]::ReadAllText($outF)
$e = [System.IO.File]::ReadAllText($errF)
Remove-Item $argF, $outF, $errF -Force -ErrorAction SilentlyContinue

if ($Raw) { return ($o + "`n--- stderr ---`n" + $e) }
if ([string]::IsNullOrWhiteSpace($o)) { throw ("NO STDOUT from " + $Tool + ". stderr=" + $e) }
$parsed = $o | ConvertFrom-Json
if (-not $parsed.ok) { throw ("BRIDGE FAILED (" + $Tool + "): " + $parsed.error + " | stderr=" + $e) }
return $parsed.result
