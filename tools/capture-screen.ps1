Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screen = [System.Windows.Forms.Screen]::PrimaryScreen
if (-not $screen) { Write-Error "no primary screen"; exit 1 }
$bounds = $screen.Bounds
Write-Host ("primary bounds: " + $bounds.Width + "x" + $bounds.Height + " at " + $bounds.X + "," + $bounds.Y)

$bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bmp.Size)
$out = Join-Path $env:TEMP "noctalia-bar.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host ("saved: " + $out)

# A 1920x1080 screenshot puts the bar in the top few percent of the image, so save a
# top strip as well: it is the version worth opening.
$stripHeight = [Math]::Min(260, $bounds.Height)
$strip = New-Object System.Drawing.Bitmap($bounds.Width, $stripHeight)
$stripGraphics = [System.Drawing.Graphics]::FromImage($strip)
$stripGraphics.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, (New-Object System.Drawing.Size($bounds.Width, $stripHeight)))
$stripOut = Join-Path $env:TEMP "noctalia-bar-strip.png"
$strip.Save($stripOut, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host ("saved: " + $stripOut)

$stripGraphics.Dispose()
$strip.Dispose()
$g.Dispose()
$bmp.Dispose()

$procs = Get-Process NoctaliaWin -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host ("running pids: " + ($procs.Id -join ","))
} else {
    Write-Host "NoctaliaWin is not running"
}
