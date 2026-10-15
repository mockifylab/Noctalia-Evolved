# Crops and enlarges a region of the primary screen, for checking shell windows by eye.
#   powershell -File tools/crop-screen.ps1 -X 1400 -Y 0 -Width 520 -Height 80 -Scale 3
param(
    [int]$X = 0,
    [int]$Y = 0,
    [int]$Width = 640,
    [int]$Height = 120,
    [int]$Scale = 2,
    [string]$Out = ""
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screen = [System.Windows.Forms.Screen]::PrimaryScreen
if (-not $screen) { Write-Error "no primary screen"; exit 1 }

$bounds = $screen.Bounds
if ($X -lt 0 -or $Y -lt 0 -or $X + $Width -gt $bounds.Width -or $Y + $Height -gt $bounds.Height) {
    Write-Error "region $X,$Y $($Width)x$Height is outside $($bounds.Width)x$($bounds.Height)"
    exit 1
}

$source = New-Object System.Drawing.Bitmap($Width, $Height)
$sourceGraphics = [System.Drawing.Graphics]::FromImage($source)
$copySize = New-Object System.Drawing.Size($Width, $Height)
$sourceGraphics.CopyFromScreen($bounds.X + $X, $bounds.Y + $Y, 0, 0, $copySize)

# Multiplications go in variables: `New-Object Type(a * b, c * d)` makes PowerShell build an
# array from the comma before multiplying, which then fails with op_Multiply.
$ZoomedWidth = $Width * $Scale
$ZoomedHeight = $Height * $Scale
$target = New-Object System.Drawing.Bitmap($ZoomedWidth, $ZoomedHeight)
$targetGraphics = [System.Drawing.Graphics]::FromImage($target)
$targetGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$targetGraphics.DrawImage($source, 0, 0, $ZoomedWidth, $ZoomedHeight)

if (-not $Out) { $Out = Join-Path $env:TEMP "noctalia-crop.png" }
$target.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)

$targetGraphics.Dispose()
$target.Dispose()
$sourceGraphics.Dispose()
$source.Dispose()

Write-Host ("saved: " + $Out)
