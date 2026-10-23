<#
    Rasterizes the vector brand (src/Kiwindow.Core/Theme/Brand.xaml) into:
      assets/brand/kiwindow.ico          multi-size icon: app exe, installer exe, uninstaller, window
      assets/brand/png/logo-<size>.png   PNG exports (logo-256.png is the SwissTechKnife catalog icon)
      assets/brand/png/mark-512.png      mark on a transparent background
      assets/brand/installer/*.bmp       Inno Setup wizard art
    Run with Windows PowerShell (STA, WPF available):  powershell -File scripts/render-brand.ps1
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

$root = Split-Path -Parent $PSScriptRoot
$brandDir = Join-Path $root 'assets/brand'
$pngDir = Join-Path $brandDir 'png'
$installerDir = Join-Path $brandDir 'installer'
New-Item -ItemType Directory -Force $pngDir, $installerDir | Out-Null

$xaml = [IO.File]::ReadAllText((Join-Path $root 'src/Kiwindow.Core/Theme/Brand.xaml'), [Text.Encoding]::UTF8)
$dict = [Windows.Markup.XamlReader]::Parse($xaml)
$logo = $dict['Brand.LogoDrawing']
$small = $dict['Brand.LogoSmallDrawing']
$mark = $dict['Brand.MarkDrawing']

function Render-Drawing($drawing, [int]$width, [int]$height, $background, [double]$drawSize, [double]$x, [double]$y) {
    $visual = New-Object Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    if ($background) { $dc.DrawRectangle($background, $null, (New-Object Windows.Rect 0, 0, $width, $height)) }
    $dc.PushTransform((New-Object Windows.Media.TranslateTransform $x, $y))
    $dc.PushTransform((New-Object Windows.Media.ScaleTransform ($drawSize / 256), ($drawSize / 256)))
    $dc.DrawDrawing($drawing)
    $dc.Pop(); $dc.Pop()
    $dc.Close()
    $bmp = New-Object Windows.Media.Imaging.RenderTargetBitmap $width, $height, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($visual)
    return $bmp
}

function Save-Png($bitmap, [string]$path) {
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

function Save-Bmp($bitmap, [string]$path) {
    # Inno Setup needs 24-bit BMPs without alpha.
    $converted = New-Object Windows.Media.Imaging.FormatConvertedBitmap $bitmap, ([Windows.Media.PixelFormats]::Bgr24), $null, 0
    $encoder = New-Object Windows.Media.Imaging.BmpBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($converted))
    $stream = [IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

# Icon frames: 16-32 px use the bolder small drawing so the panes stay legible.
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$frames = @()
foreach ($size in $sizes) {
    $drawing = if ($size -le 32) { $small } else { $logo }
    $path = Join-Path $pngDir "logo-$size.png"
    Save-Png (Render-Drawing $drawing $size $size $null $size 0 0) $path
    $frames += , ([IO.File]::ReadAllBytes($path))
}
Save-Png (Render-Drawing $logo 512 512 $null 512 0 0) (Join-Path $pngDir 'logo-512.png')
Save-Png (Render-Drawing $mark 512 512 $null 512 0 0) (Join-Path $pngDir 'mark-512.png')

# ICO container with PNG-compressed frames (supported since Windows Vista).
$ms = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $ms
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $data = $frames[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($data in $frames) { $w.Write($data) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $brandDir 'kiwindow.ico'), $ms.ToArray())

# Installer wizard art; the canvas color matches the app background.
$canvas = New-Object Windows.Media.SolidColorBrush ([Windows.Media.Color]::FromRgb(0x14, 0x14, 0x13))
foreach ($scale in 1, 2) {
    $w1 = 164 * $scale; $h1 = 314 * $scale; $logoSize = 96 * $scale
    Save-Bmp (Render-Drawing $logo $w1 $h1 $canvas $logoSize (($w1 - $logoSize) / 2) (($h1 - $logoSize) / 2)) (Join-Path $installerDir "wizard-$($w1)x$($h1).bmp")
    $w2 = 55 * $scale; $h2 = 58 * $scale; $s2 = 44 * $scale
    Save-Bmp (Render-Drawing $logo $w2 $h2 $canvas $s2 (($w2 - $s2) / 2) (($h2 - $s2) / 2)) (Join-Path $installerDir "wizard-small-$($w2)x$($h2).bmp")
}

Write-Host "Brand assets written to $brandDir" -ForegroundColor Green
