Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap(128, 128)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Black)

$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Lime, 6)
$g.DrawRectangle($pen, 6, 6, 116, 116)

$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Lime)
$font = New-Object System.Drawing.Font('Consolas', 48, [System.Drawing.FontStyle]::Bold)
$g.DrawString('A', $font, $brush, 32, 30)

$smallFont = New-Object System.Drawing.Font('Consolas', 11, [System.Drawing.FontStyle]::Bold)
$g.DrawString('ASSIST', $smallFont, $brush, 32, 100)

$bmp.Save('C:\Users\ozgen\source\repos\Assist\Assist\Resources\assist_icon.png')

Write-Host 'PNG icon created!' -ForegroundColor Green

try {
    $icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
    $fs = [System.IO.File]::Create('C:\Users\ozgen\source\repos\Assist\Assist\Resources\assist_icon.ico')
    $icon.Save($fs)
    $fs.Close()
    Write-Host 'ICO icon created!' -ForegroundColor Green
} catch {
    Write-Host 'ICO creation failed, use PNG or convert online' -ForegroundColor Yellow
}

$g.Dispose()
$bmp.Dispose()
