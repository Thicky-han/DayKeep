$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphic = [System.Drawing.Graphics]::FromImage($bitmap)
    $green = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#347568'))
    $cream = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F9F4D9'))
    $pen = [System.Drawing.Pen]::new($cream.Color, 2.3)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $stream = [System.IO.MemoryStream]::new()
    try {
        $graphic.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphic.Clear([System.Drawing.Color]::Transparent)
        $graphic.ScaleTransform(($size / 32.0), ($size / 32.0))
        $graphic.FillEllipse($green, 1, 1, 30, 30)
        $graphic.FillEllipse($cream, 10, 10, 12, 12)
        $graphic.FillRectangle($green, 7, 18, 19, 7)
        $graphic.DrawLine($pen, 7, 20, 25, 20)
        $graphic.DrawLine($pen, 16, 5, 16, 8)
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $images.Add($stream.ToArray())
    }
    finally { $stream.Dispose(); $pen.Dispose(); $cream.Dispose(); $green.Dispose(); $graphic.Dispose(); $bitmap.Dispose() }
}
$outputPath = Join-Path $projectRoot 'assets\brand\daykeep.ico'
$file = [System.IO.File]::Create($outputPath)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write($bytes) }
}
finally { $writer.Dispose(); $file.Dispose() }
Write-Output 'Generated original DayKeep icon.'
