param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$ColorOutputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutlineOutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$size = 256
$source = [System.Drawing.Bitmap]::new($InputPath)
$color = [System.Drawing.Bitmap]::new(
    $size,
    $size,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($color)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$graphics.DrawImage(
    $source,
    [System.Drawing.Rectangle]::new(0, 0, $size, $size),
    0,
    0,
    $source.Width,
    $source.Height,
    [System.Drawing.GraphicsUnit]::Pixel)

$outline = [System.Drawing.Bitmap]::new(
    $size,
    $size,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $size; $y++)
{
    for ($x = 0; $x -lt $size; $x++)
    {
        $alpha = $color.GetPixel($x, $y).A
        if ($alpha -gt 8)
        {
            $outline.SetPixel(
                $x,
                $y,
                [System.Drawing.Color]::FromArgb($alpha, 255, 255, 255))
        }
    }
}

[System.IO.Directory]::CreateDirectory(
    [System.IO.Path]::GetDirectoryName($ColorOutputPath)) | Out-Null
$color.Save($ColorOutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$outline.Save($OutlineOutputPath, [System.Drawing.Imaging.ImageFormat]::Png)

$graphics.Dispose()
$outline.Dispose()
$color.Dispose()
$source.Dispose()
