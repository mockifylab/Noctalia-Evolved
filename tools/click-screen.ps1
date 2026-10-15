# Clicks a screen point and captures the result, for checking the pop-out panels by eye.
# Panels only appear in response to a real pointer press, and a screenshot taken from the
# same script keeps the timing honest. Escape is sent last so the desktop is left as found
# unless -KeepOpen says otherwise.
#   powershell -File tools/click-screen.ps1 -X 918 -Y 60 -Out panel.png
param(
    [int]$X = 918,
    [int]$Y = 60,
    [int]$CropX = -1,
    [int]$CropY = -1,
    [int]$CropWidth = 640,
    [int]$CropHeight = 360,
    [string]$Out = "",
    [switch]$KeepOpen
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NoctaliaPointer {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint data, uint extra, IntPtr reserved);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr reserved);
}
"@

$out = if ($Out) { $Out } else { Join-Path $env:TEMP "noctalia-click.png" }
$LEFTDOWN = 0x0002
$LEFTUP = 0x0004
$ESCAPE = 0x1B

function Capture([string]$path) {
    $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bmp.Size)

    if ($CropX -ge 0 -and $CropY -ge 0) {
        $region = New-Object System.Drawing.Rectangle($CropX, $CropY, $CropWidth, $CropHeight)
        $crop = New-Object System.Drawing.Bitmap($CropWidth, $CropHeight)
        $cg = [System.Drawing.Graphics]::FromImage($crop)
        $cg.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, $CropWidth, $CropHeight)),
            $region, [System.Drawing.GraphicsUnit]::Pixel)
        $crop.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $cg.Dispose(); $crop.Dispose()
    } else {
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $g.Dispose(); $bmp.Dispose()
}

[NoctaliaPointer]::SetCursorPos($X, $Y) | Out-Null
Start-Sleep -Milliseconds 250
[NoctaliaPointer]::mouse_event($LEFTDOWN, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[NoctaliaPointer]::mouse_event($LEFTUP, 0, 0, [IntPtr]::Zero)

# Long enough for the 200ms reveal to finish, short enough that a stuck panel is obvious.
Start-Sleep -Milliseconds 700
Capture $out

if (-not $KeepOpen) {
    [NoctaliaPointer]::keybd_event($ESCAPE, 0, 0, [IntPtr]::Zero)
    [NoctaliaPointer]::keybd_event($ESCAPE, 0, 2, [IntPtr]::Zero)
}

Write-Host "saved: $out"
