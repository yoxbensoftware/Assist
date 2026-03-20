# PowerShell script to create Assist app icon
# Creates a Matrix-style terminal icon with emoji

Add-Type -AssemblyName System.Drawing

$outputPath = "C:\Users\ozgen\source\repos\Assist\Assist\Resources"
$pngFile = Join-Path $outputPath "assist_icon.png"
$icoFile = Join-Path $outputPath "assist_icon.ico"

Write-Host "Creating icon at: $outputPath" -ForegroundColor Green

# Create 128x128 bitmap
$bmp = New-Object System.Drawing.Bitmap(128, 128)
$graphics = [System.Drawing.Graphics]::FromImage($bmp)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

# Black background
$graphics.Clear([System.Drawing.Color]::Black)

# Green border (Matrix style)
$greenPen = New-Object System.Drawing.Pen([System.Drawing.Color]::Lime, 5)
$graphics.DrawRectangle($greenPen, 5, 5, 118, 118)

# Terminal header (dark gray)
$headerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 30, 30))
$graphics.FillRectangle($headerBrush, 10, 10, 108, 18)

# Terminal dots (red, yellow, green)
$redBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Red)
$yellowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Yellow)
$greenBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Lime)

$graphics.FillEllipse($redBrush, 15, 13, 10, 10)
$graphics.FillEllipse($yellowBrush, 30, 13, 10, 10)
$graphics.FillEllipse($greenBrush, 45, 13, 10, 10)

# Matrix code
$codeFont = New-Object System.Drawing.Font('Consolas', 9, [System.Drawing.FontStyle]::Bold)
$darkGreen = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 180, 0))
$graphics.DrawString('> 01010101', $codeFont, $greenBrush, 12, 35)
$graphics.DrawString('> 10101010', $codeFont, $darkGreen, 12, 50)

# Cool hacker/robot emoji or symbol
$emojiFont = New-Object System.Drawing.Font('Segoe UI Emoji', 42, [System.Drawing.FontStyle]::Bold)
# Using a simple text-based face since emoji rendering can be tricky
$graphics.DrawString(';-)', $emojiFont, $greenBrush, 35, 58)

# "ASSIST" text
$titleFont = New-Object System.Drawing.Font('Consolas', 11, [System.Drawing.FontStyle]::Bold)
$title = 'ASSIST'
$titleSize = $graphics.MeasureString($title, $titleFont)
$titleX = (128 - $titleSize.Width) / 2
$graphics.DrawString($title, $titleFont, $greenBrush, $titleX, 107)

# Save PNG
$bmp.Save($pngFile, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "✓ PNG created: $pngFile" -ForegroundColor Green

# Convert to ICO
try {
    $icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
    $fileStream = [System.IO.File]::Create($icoFile)
    $icon.Save($fileStream)
    $fileStream.Close()
    $icon.Dispose()
    Write-Host "✓ ICO created: $icoFile" -ForegroundColor Green
}
catch {
    Write-Host "Warning: ICO creation failed, but PNG is available" -ForegroundColor Yellow
    Write-Host "You can use online converter: https://convertio.co/png-ico/" -ForegroundColor Yellow
}

# Cleanup
$graphics.Dispose()
$greenPen.Dispose()
$headerBrush.Dispose()
$redBrush.Dispose()
$yellowBrush.Dispose()
$greenBrush.Dispose()
$darkGreen.Dispose()
$codeFont.Dispose()
$emojiFont.Dispose()
$titleFont.Dispose()
$bmp.Dispose()

Write-Host "`nIcon creation completed!" -ForegroundColor Cyan
Write-Host "Add to Assist.csproj: <ApplicationIcon>Resources\assist_icon.ico</ApplicationIcon>" -ForegroundColor Yellow
