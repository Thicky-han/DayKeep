$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    # Render at 4x before downsampling so the 16 px tray mark keeps smooth edges.
    $renderSize = $size * 4
    $bitmap = [System.Drawing.Bitmap]::new($renderSize, $renderSize)
    $graphic = [System.Drawing.Graphics]::FromImage($bitmap)
    $green = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.PointF]::new(0,0), [System.Drawing.PointF]::new(32,32), [System.Drawing.ColorTranslator]::FromHtml('#286F64'), [System.Drawing.ColorTranslator]::FromHtml('#174C47'))
    $cream = [System.Drawing.ColorTranslator]::FromHtml('#F8F3DF')
    $gold = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#E9B56D'))
    $pen = [System.Drawing.Pen]::new($cream, 3.1)
    $hand = [System.Drawing.Pen]::new($cream, 2.1)
    $shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $clockHand = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hand.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hand.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hand.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $stream = [System.IO.MemoryStream]::new()
    try {
        $graphic.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphic.Clear([System.Drawing.Color]::Transparent)
        $graphic.ScaleTransform(($renderSize / 32.0), ($renderSize / 32.0))
        $shape.AddArc(1,1,18,18,180,90); $shape.AddArc(13,1,18,18,270,90)
        $shape.AddArc(13,13,18,18,0,90); $shape.AddArc(1,13,18,18,90,90)
        $shape.CloseFigure()
        $graphic.FillPath($green, $shape)
        $graphic.DrawArc($pen, 8,8,16,16,0,270)
        $clockHand.AddLine([single]16,[single]12.5,[single]16,[single]16)
        $clockHand.AddLine([single]16,[single]16,[single]19.2,[single]17.8)
        $graphic.DrawPath($hand, $clockHand)
        $graphic.FillEllipse($gold, [single]20.9,[single]4.9,[single]6.2,[single]6.2)
        $output = [System.Drawing.Bitmap]::new($size,$size)
        $scaled = [System.Drawing.Graphics]::FromImage($output)
        try {
            $scaled.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $scaled.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scaled.DrawImage($bitmap,0,0,$size,$size)
            $output.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            if ($size -eq 256) { $output.Save((Join-Path $projectRoot 'assets\brand\daykeep-mark.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
        }
        finally { $scaled.Dispose(); $output.Dispose() }
        $images.Add($stream.ToArray())
    }
    finally { $stream.Dispose(); $clockHand.Dispose(); $shape.Dispose(); $hand.Dispose(); $pen.Dispose(); $gold.Dispose(); $green.Dispose(); $graphic.Dispose(); $bitmap.Dispose() }
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
