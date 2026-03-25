# ============================================================
# Assist - Publish and Obfuscate Script
# Kullanim: .\publish.ps1
# ============================================================

$ErrorActionPreference = "Stop"
$ProjectDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile  = Join-Path $ProjectDir "Assist.csproj"
$PublishDir   = Join-Path $ProjectDir "bin\Publish"
$ObfuscatedDir = Join-Path $PublishDir "Obfuscated"
$FinalDir     = Join-Path $ProjectDir "bin\Release-Ready"

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Assist - Publish and Obfuscate" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Step 1: Clean previous outputs
Write-Host "[1/5] Temizleniyor..." -ForegroundColor Yellow
if (Test-Path $PublishDir)  { Remove-Item $PublishDir  -Recurse -Force }
if (Test-Path $FinalDir)    { Remove-Item $FinalDir    -Recurse -Force }

# Step 2: Publish
Write-Host "[2/5] Publish ediliyor..." -ForegroundColor Yellow
dotnet publish $ProjectFile -c Release -o $PublishDir -r win-x64 --self-contained true -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { Write-Host "Publish basarisiz!" -ForegroundColor Red; exit 1 }

# Step 3: Find Obfuscar tool
Write-Host "[3/5] Obfuscar araniyor..." -ForegroundColor Yellow
$obfuscarExe = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\obfuscar" -Filter "Obfuscar.Console.exe" -Recurse | Select-Object -First 1
if (-not $obfuscarExe) {
    Write-Host "Obfuscar bulunamadi! dotnet restore calistirin." -ForegroundColor Red
    exit 1
}
Write-Host "  Obfuscar: $($obfuscarExe.FullName)" -ForegroundColor DarkGray

# Step 4: Copy config and run obfuscation
Write-Host "[4/5] Obfuscation uygulanıyor..." -ForegroundColor Yellow
Copy-Item (Join-Path $ProjectDir "obfuscar.xml") -Destination $PublishDir -Force

Push-Location $PublishDir
$obfProcess = Start-Process -FilePath $obfuscarExe.FullName -ArgumentList "obfuscar.xml" -NoNewWindow -Wait -PassThru
Pop-Location

if ($obfProcess.ExitCode -ne 0) { Write-Host "Obfuscation basarisiz!" -ForegroundColor Red; exit 1 }

# Step 5: Prepare final output
Write-Host "[5/5] Son paket hazirlaniyor..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $FinalDir -Force | Out-Null

# Copy obfuscated files over original publish
Copy-Item "$ObfuscatedDir\*" -Destination $PublishDir -Force -Recurse
Remove-Item $ObfuscatedDir -Recurse -Force
Remove-Item (Join-Path $PublishDir "obfuscar.xml") -Force -ErrorAction SilentlyContinue

# Copy everything to Release-Ready
Copy-Item "$PublishDir\*" -Destination $FinalDir -Recurse -Force

# Remove unnecessary files
Get-ChildItem $FinalDir -Filter "*.pdb" -Recurse | Remove-Item -Force
Get-ChildItem $FinalDir -Filter "*.xml" -Recurse | Where-Object { $_.Name -ne "assist_icon.ico" } | Remove-Item -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Tamamlandi!" -ForegroundColor Green
Write-Host "  Konum: $FinalDir" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Bu klasoru zipleyip GitHub Release olarak yayinlayabilirsiniz." -ForegroundColor Cyan
