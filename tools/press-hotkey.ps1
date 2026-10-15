# Presses a key combination the way a keyboard does, to check the global hotkey by eye.
# RegisterHotKey bindings only fire for injected input that arrives with the modifiers held,
# so this presses them down, taps the key, and releases in reverse order. The screenshot is
# taken while the result is on screen; Escape is sent last so the desktop is left as found
# unless -KeepOpen says otherwise.
#   powershell -File tools/press-hotkey.ps1 -Ctrl -Alt -Vk 0x20 -Out hotkey.png
param(
    [switch]$Ctrl,
    [switch]$Alt,
    [switch]$Shift,
    [switch]$Win,

    # A Windows virtual-key code, e.g. 0x20 for Space, 0x44 for D, 0x70 for F1.
    [int]$Vk = 0x20,

    [string]$Out = "",
    [switch]$KeepOpen
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NoctaliaKeys {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr reserved);
}
"@

$out = if ($Out) { $Out } else { Join-Path $env:TEMP "noctalia-hotkey.png" }
$KEYUP = 0x0002

function Press([int]$code) { [NoctaliaKeys]::keybd_event([byte]$code, 0, 0, [IntPtr]::Zero) }
function Release([int]$code) { [NoctaliaKeys]::keybd_event([byte]$code, 0, $KEYUP, [IntPtr]::Zero) }

$held = @()
if ($Ctrl)  { $held += 0x11 }
if ($Alt)   { $held += 0x12 }
if ($Shift) { $held += 0x10 }
if ($Win)   { $held += 0x5B }

foreach ($code in $held) { Press $code; Start-Sleep -Milliseconds 40 }
Press $Vk
Start-Sleep -Milliseconds 40
Release $Vk
foreach ($code in [Array]::Reverse($held)) { Release $code; Start-Sleep -Milliseconds 40 }

Start-Sleep -Milliseconds 400
$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bmp.Size)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

if (-not $KeepOpen) {
    Release $Vk
    [NoctaliaKeys]::keybd_event(0x1B, 0, 0, [IntPtr]::Zero)
    [NoctaliaKeys]::keybd_event(0x1B, 0, $KEYUP, [IntPtr]::Zero)
}

Write-Output $out
