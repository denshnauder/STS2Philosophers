param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Canvas
{
    return [System.Drawing.Bitmap]::new(
        256,
        256,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function New-Points([float[][]]$Coordinates)
{
    return [System.Drawing.PointF[]]($Coordinates | ForEach-Object {
        [System.Drawing.PointF]::new($_[0], $_[1])
    })
}

function SaveKongziMuduoIcon([string]$Path, [bool]$OutlineOnly)
{
    $bitmap = New-Canvas
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $bell = New-Points @(@(82, 58), @(174, 58), @(201, 174), @(55, 174))
    $clapper = New-Points @(@(114, 199), @(142, 199), @(136, 227), @(120, 227))

    if ($OutlineOnly)
    {
        $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        $whitePen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 18)
        $whitePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $graphics.FillRectangle($whiteBrush, 97, 22, 62, 45)
        $graphics.FillPolygon($whiteBrush, $bell)
        $graphics.FillRectangle($whiteBrush, 37, 165, 182, 43)
        $graphics.FillPolygon($whiteBrush, $clapper)
        $graphics.DrawPolygon($whitePen, $bell)
        $whitePen.Dispose()
        $whiteBrush.Dispose()
    }
    else
    {
        $darkPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 50, 27, 19), 10)
        $darkPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $woodBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 154, 90, 44))
        $handleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 118, 80, 45))
        $rimBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 184, 121, 61))
        $clapperBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 215, 160, 79))
        $highlightPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 232, 188, 113), 7)
        $highlightPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $highlightPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $grainPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 107, 56, 31), 6)

        $graphics.FillRectangle($handleBrush, 106, 31, 44, 27)
        $graphics.DrawRectangle($darkPen, 106, 31, 44, 27)
        $graphics.FillPolygon($woodBrush, $bell)
        $graphics.DrawPolygon($darkPen, $bell)
        $graphics.FillRectangle($rimBrush, 46, 174, 164, 25)
        $graphics.DrawRectangle($darkPen, 46, 174, 164, 25)
        $graphics.FillPolygon($clapperBrush, $clapper)
        $graphics.DrawPolygon($darkPen, $clapper)
        $graphics.DrawArc($highlightPen, 90, 67, 76, 36, 20, 140)
        $graphics.DrawArc($highlightPen, 73, 105, 110, 48, 15, 150)
        $graphics.DrawLine($grainPen, 107, 71, 98, 159)
        $graphics.DrawLine($grainPen, 143, 70, 154, 159)

        $grainPen.Dispose()
        $highlightPen.Dispose()
        $clapperBrush.Dispose()
        $rimBrush.Dispose()
        $handleBrush.Dispose()
        $woodBrush.Dispose()
        $darkPen.Dispose()
    }

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
SaveKongziMuduoIcon (Join-Path $OutputDirectory 'kongzi_muduo.png') $false
SaveKongziMuduoIcon (Join-Path $OutputDirectory 'kongzi_muduo_outline.png') $true
