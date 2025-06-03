# ChronoGuard Production Verification Script
# Tests all packaging components for production readiness

param(
    [string]$Version = "1.0.2-production"
)

$ErrorActionPreference = "Stop"

Write-Host "[VERIFY] ChronoGuard Production Readiness Test" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Core Application Build
Write-Host "[TEST 1] Building Release Configuration..." -ForegroundColor Yellow
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $ProjectRoot "src\ChronoGuard.App\ChronoGuard.App.csproj"

try {
    dotnet build $AppProject -c Release --verbosity quiet
    Write-Host "[OK] Release build successful" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Release build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Portable Package Creation
Write-Host "[TEST 2] Creating Portable Package..." -ForegroundColor Yellow
$OutputDir = Join-Path $PSScriptRoot "output"

# Clean output directory
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

try {
    # Publish the application
    dotnet publish $AppProject -c Release -r win-x64 --self-contained true -o "$OutputDir\ChronoGuard-Portable" --verbosity quiet
    
    # Create ZIP package
    $ZipPath = Join-Path $OutputDir "ChronoGuard-Portable-$Version.zip"
    Compress-Archive -Path "$OutputDir\ChronoGuard-Portable\*" -DestinationPath $ZipPath -Force
    
    $ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "[OK] Portable package created: $ZipSize MB" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Portable package creation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 3: Test Application Execution
Write-Host "[TEST 3] Testing Published Application..." -ForegroundColor Yellow
try {
    $TestAppProject = Join-Path $ProjectRoot "src\ChronoGuard.TestApp\ChronoGuard.TestApp.csproj"
    $TestOutput = dotnet run --project $TestAppProject 2>&1
    
    if ($TestOutput -match "TODAS LAS PRUEBAS EXITOSAS") {
        Write-Host "[OK] All core tests passed" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] Core tests failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "[ERROR] Test execution failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 4: Package Integrity Check
Write-Host "[TEST 4] Verifying Package Integrity..." -ForegroundColor Yellow
try {
    $ExtractDir = Join-Path $OutputDir "integrity-test"
    Expand-Archive -Path $ZipPath -DestinationPath $ExtractDir -Force
    
    $RequiredFiles = @(
        "ChronoGuard.App.exe",
        "ChronoGuard.Application.dll",
        "ChronoGuard.Domain.dll",
        "ChronoGuard.Infrastructure.dll"
    )
    
    $MissingFiles = @()
    foreach ($file in $RequiredFiles) {
        if (-not (Test-Path (Join-Path $ExtractDir $file))) {
            $MissingFiles += $file
        }
    }
    
    if ($MissingFiles.Count -eq 0) {
        Write-Host "[OK] All required files present in package" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] Missing files: $($MissingFiles -join ', ')" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "[ERROR] Package integrity check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host ""
Write-Host "[SUCCESS] ChronoGuard Production Verification Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor White
Write-Host "Package Size: $ZipSize MB" -ForegroundColor White
Write-Host "Location: $ZipPath" -ForegroundColor White
Write-Host ""
Write-Host "[READY] ChronoGuard is production-ready for distribution!" -ForegroundColor Green
