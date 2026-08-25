param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$outputDirectory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$bitmap = [System.Drawing.Bitmap]::new(
    1600,
    900,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

$background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.Rectangle]::new(0, 0, 1600, 900),
    [System.Drawing.Color]::FromArgb(255, 18, 26, 38),
    [System.Drawing.Color]::FromArgb(255, 91, 70, 47),
    90)
$graphics.FillRectangle($background, 0, 0, 1600, 900)

$moonBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(205, 232, 215, 160))
$mistBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(55, 215, 225, 214))
$mountainBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225, 32, 42, 48))
$figureBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230, 12, 18, 24))
$goldPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(180, 202, 163, 86), 8)

$graphics.FillEllipse($moonBrush, 1040, 115, 315, 315)
$graphics.FillEllipse($mistBrush, 120, 340, 1360, 230)
$mountains = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(0, 660),
    [System.Drawing.PointF]::new(360, 330),
    [System.Drawing.PointF]::new(710, 650),
    [System.Drawing.PointF]::new(1020, 390),
    [System.Drawing.PointF]::new(1600, 700),
    [System.Drawing.PointF]::new(1600, 900),
    [System.Drawing.PointF]::new(0, 900))
$graphics.FillPolygon($mountainBrush, $mountains)

foreach ($centerX in @(570, 800, 1030))
{
    $graphics.FillEllipse($figureBrush, $centerX - 42, 390, 84, 84)
    $graphics.FillPie($figureBrush, $centerX - 105, 455, 210, 330, 205, 130)
}

$graphics.DrawArc($goldPen, 425, 250, 750, 470, 200, 140)
$graphics.DrawArc($goldPen, 505, 295, 590, 390, 200, 140)

$goldPen.Dispose()
$figureBrush.Dispose()
$mountainBrush.Dispose()
$mistBrush.Dispose()
$moonBrush.Dispose()
$background.Dispose()
$graphics.Dispose()

$bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
