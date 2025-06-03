# ChronoGuard MSIX Builder
# Creates MSIX package for modern Windows distribution

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$Platform = "x64",
    [switch]$Clean = $false
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SrcPath = Join-Path $ProjectRoot "src"
$AppProject = Join-Path $SrcPath "ChronoGuard.App\ChronoGuard.App.csproj"
$MsixProject = Join-Path $PSScriptRoot "msix\ChronoGuard.Msix.csproj"
$OutputDir = Join-Path $PSScriptRoot "output"
$PublishDir = Join-Path $SrcPath "ChronoGuard.App\bin\$Configuration\net8.0-windows10.0.22621.0\win-$Platform\publish"

Write-Host "[MSIX] ChronoGuard MSIX Builder" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Green
Write-Host "Platform: $Platform" -ForegroundColor Green
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "[CLEAN] Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }
}

# Create output directory
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Step 1: Check Windows SDK
Write-Host "[CHECK] Checking Windows SDK..." -ForegroundColor Yellow
try {
    $sdkPath = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*" -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $sdkPath) {
        throw "Windows SDK not found"
    }
    
    $makeappxPath = Join-Path $sdkPath "x64\makeappx.exe"
    $signtoolPath = Join-Path $sdkPath "x64\signtool.exe"
    
    if (-not (Test-Path $makeappxPath)) {
        throw "makeappx.exe not found in Windows SDK"
    }
      Write-Host "[OK] Windows SDK found: $($sdkPath.Name)" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Windows SDK not found. Please install Windows 10/11 SDK" -ForegroundColor Red
    Write-Host "   Download from: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/" -ForegroundColor Yellow
    exit 1
}

# Step 2: Build and publish the main application
Write-Host "[BUILD] Building ChronoGuard application..." -ForegroundColor Yellow
try {
    $buildArgs = @(
        "publish",
        $AppProject,
        "-c", $Configuration,
        "-r", "win10-$Platform",
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "--verbosity", "minimal"
    )
    
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Application build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "[OK] Application built successfully" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Application build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: Update MSIX manifest version
Write-Host "[MANIFEST] Updating MSIX manifest..." -ForegroundColor Yellow
try {
    $manifestPath = Join-Path $PSScriptRoot "msix\Package.appxmanifest"
    $manifest = Get-Content $manifestPath -Raw
    $versionPattern = 'Version="[\d\.]+"'
    $newVersion = 'Version="' + $Version + '.0"'
    $manifest = $manifest -replace $versionPattern, $newVersion
    $manifest | Set-Content $manifestPath -Encoding UTF8
    
    Write-Host "[OK] Manifest updated to version $Version" -ForegroundColor Green
} catch {    Write-Host "[ERROR] Failed to update manifest: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Create MSIX package structure
Write-Host "[PACKAGE] Creating MSIX package structure..." -ForegroundColor Yellow
try {
    $msixStaging = Join-Path $OutputDir "msix-staging"
    if (Test-Path $msixStaging) {
        Remove-Item $msixStaging -Recurse -Force
    }
    New-Item -ItemType Directory -Path $msixStaging -Force | Out-Null
    
    # Copy application files
    Copy-Item -Path "$PublishDir\*" -Destination $msixStaging -Recurse -Force
    
    # Copy manifest
    Copy-Item -Path "$PSScriptRoot\msix\Package.appxmanifest" -Destination $msixStaging -Force
    
    # Copy assets (if they exist)
    $assetsPath = Join-Path $PSScriptRoot "msix\Assets"
    if (Test-Path $assetsPath) {
        $msixAssets = Join-Path $msixStaging "Assets"
        New-Item -ItemType Directory -Path $msixAssets -Force | Out-Null
        Copy-Item -Path "$assetsPath\*" -Destination $msixAssets -Recurse -Force
    } else {
        Write-Host "[WARNING] Warning: No assets found. Using default icons." -ForegroundColor Yellow
    }
    
    Write-Host "[OK] Package structure created" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Failed to create package structure: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 5: Create MSIX package
Write-Host "[MSIX] Creating MSIX package..." -ForegroundColor Yellow
try {
    $msixFile = Join-Path $OutputDir "ChronoGuard-v$Version.msix"
    
    $makeappxArgs = @(
        "pack",
        "/d", $msixStaging,
        "/p", $msixFile,
        "/l"
    )
    
    & $makeappxPath @makeappxArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MSIX packaging failed with exit code $LASTEXITCODE"
    }
    
    # Clean up staging directory
    Remove-Item $msixStaging -Recurse -Force
    
    $fileSize = [math]::Round((Get-Item $msixFile).Length / 1MB, 2)
    
    Write-Host "[OK] MSIX package created successfully" -ForegroundColor Green
    Write-Host ""
    Write-Host "[SUCCESS] MSIX package created!" -ForegroundColor Green
    Write-Host "[INFO] Location: $msixFile" -ForegroundColor Cyan
    Write-Host "[INFO] Size: $fileSize MB" -ForegroundColor Cyan
    
} catch {
    Write-Host "[ERROR] MSIX packaging failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 6: Optional - Create test certificate for signing (development only)
Write-Host ""
Write-Host "[CERT] Certificate Information" -ForegroundColor Yellow
Write-Host "=========================" -ForegroundColor Yellow
Write-Host "[WARNING] The MSIX package is currently unsigned." -ForegroundColor Yellow
Write-Host ""
Write-Host "For testing, you can create a self-signed certificate:" -ForegroundColor Gray
Write-Host "1. Run: New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=ChronoGuard Team' -KeyUsage DigitalSignature -FriendlyName 'ChronoGuard Test Certificate' -CertStoreLocation 'Cert:\CurrentUser\My'" -ForegroundColor Gray
Write-Host "2. Export and install the certificate" -ForegroundColor Gray
Write-Host "3. Sign the package with: signtool sign /a /fd SHA256 '$msixFile'" -ForegroundColor Gray
Write-Host ""
Write-Host "For production, use a trusted code signing certificate." -ForegroundColor Gray

# Step 7: Create checksum
$checksumFile = $msixFile + ".sha256"
$hash = Get-FileHash -Path $msixFile -Algorithm SHA256
$hash.Hash.ToLower() + "  " + (Split-Path $msixFile -Leaf) | Out-File -FilePath $checksumFile -Encoding utf8
Write-Host "[INFO] Checksum created: $checksumFile" -ForegroundColor Cyan

Write-Host ""
Write-Host "[SUCCESS] MSIX build completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "[NEXT] Next Steps:" -ForegroundColor Cyan
Write-Host "   1. Sign the MSIX package for distribution" -ForegroundColor White
Write-Host "   2. Test installation on clean Windows machine" -ForegroundColor White
Write-Host "   3. Submit to Microsoft Store (optional)" -ForegroundColor White
