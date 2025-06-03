# ChronoGuard MSI Builder
# Builds ChronoGuard MSI installer using WiX Toolset

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
$MsiProject = Join-Path $PSScriptRoot "msi\ChronoGuard.Installer.wixproj"
$OutputDir = Join-Path $PSScriptRoot "output"
$PublishDir = Join-Path $SrcPath "ChronoGuard.App\bin\$Configuration\net8.0-windows10.0.22621.0\win-$Platform\publish"

Write-Host "[MSI] ChronoGuard MSI Builder" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
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

# Step 1: Build and publish the main application
Write-Host "[BUILD] Building ChronoGuard application..." -ForegroundColor Yellow
try {
    $buildArgs = @(
        "publish",
        $AppProject,
        "-c", $Configuration,
        "-r", "win-$Platform",
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

# Step 2: Verify WiX Toolset is available
Write-Host "[CHECK] Checking WiX Toolset..." -ForegroundColor Yellow
try {
    $wixVersion = & wix --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "WiX Toolset not found"
    }
    Write-Host "[OK] WiX Toolset found: $wixVersion" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] WiX Toolset not found. Please install WiX Toolset v4+" -ForegroundColor Red
    Write-Host "   Download from: https://wixtoolset.org/releases/" -ForegroundColor Yellow
    exit 1
}

# Step 3: Build MSI installer
Write-Host "[MSI] Building MSI installer..." -ForegroundColor Yellow
try {
    $msiArgs = @(
        "build",
        $MsiProject,
        "-c", $Configuration,
        "-p:Version=$Version",
        "-p:SourceDir=$PublishDir",
        "-o", $OutputDir,
        "--verbosity", "minimal"
    )
    
    & dotnet @msiArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MSI build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "[OK] MSI installer built successfully" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] MSI build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Rename output file
$msiFile = Get-ChildItem -Path $OutputDir -Filter "*.msi" | Select-Object -First 1
if ($msiFile) {
    $newName = "ChronoGuard-Setup-v$Version.msi"
    $newPath = Join-Path $OutputDir $newName
    Move-Item $msiFile.FullName $newPath -Force
    
    $fileSize = [math]::Round((Get-Item $newPath).Length / 1MB, 2)
    
    Write-Host ""
    Write-Host "[SUCCESS] MSI installer created successfully!" -ForegroundColor Green
    Write-Host "[INFO] Location: $newPath" -ForegroundColor Cyan
    Write-Host "[INFO] Size: $fileSize MB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "[READY] Ready for distribution!" -ForegroundColor Green
} else {
    Write-Host "[ERROR] MSI file not found in output directory" -ForegroundColor Red
    exit 1
}

# Step 5: Optional - Create checksum
$checksumFile = $newPath + ".sha256"
$hash = Get-FileHash -Path $newPath -Algorithm SHA256
$hash.Hash.ToLower() + "  " + $newName | Out-File -FilePath $checksumFile -Encoding utf8
Write-Host "[INFO] Checksum created: $checksumFile" -ForegroundColor Cyan

Write-Host ""
Write-Host "[SUCCESS] Build completed successfully!" -ForegroundColor Green
