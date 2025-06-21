# ChronoGuard Portable Builder
# Creates a portable ZIP package of ChronoGuard

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
$OutputDir = Join-Path $PSScriptRoot "output"
$PublishDir = Join-Path $SrcPath "ChronoGuard.App\bin\$Configuration\net8.0-windows10.0.22621.0\win-$Platform\publish"
$PortableDir = Join-Path $OutputDir "ChronoGuard-Portable"

Write-Host "[PACKAGE] ChronoGuard Portable Builder" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
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

# Create output directories
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
New-Item -ItemType Directory -Path $PortableDir -Force | Out-Null

# Step 1: Build and publish the application
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

# Step 2: Copy application files
Write-Host "[COPY] Copying application files..." -ForegroundColor Yellow
try {
    Copy-Item -Path "$PublishDir\*" -Destination $PortableDir -Recurse -Force
    Write-Host "[OK] Files copied successfully" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Failed to copy files: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: Create portable configuration
Write-Host "[CONFIG] Creating portable configuration..." -ForegroundColor Yellow

# Create portable.txt marker file
$portableMarker = Join-Path $PortableDir "portable.txt"
@"
ChronoGuard Portable Mode
========================

This file indicates that ChronoGuard should run in portable mode.

In portable mode:
- All configuration files are stored in the application directory
- No registry entries are created
- No auto-start configuration is applied
- Settings are saved to 'config' subfolder

Version: $Version
Built: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
"@ | Out-File -FilePath $portableMarker -Encoding utf8

# Create config directory
$configDir = Join-Path $PortableDir "config"
New-Item -ItemType Directory -Path $configDir -Force | Out-Null

# Create default portable configuration
$portableConfig = Join-Path $configDir "appsettings.json"
@"
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ChronoGuard": {
    "DataPath": "./config",
    "LogPath": "./logs",
    "PortableMode": true,
    "AutoStart": false,
    "CheckForUpdates": false
  }
}
"@ | Out-File -FilePath $portableConfig -Encoding utf8

Write-Host "[OK] Portable configuration created" -ForegroundColor Green

# Step 4: Create documentation
Write-Host "[DOCS] Creating documentation..." -ForegroundColor Yellow

$readmeContent = @"
# ChronoGuard Portable v$Version

## Quick Start

1. **Run ChronoGuard**: Double-click 'ChronoGuard.App.exe'
2. **Configure**: Right-click system tray icon -> Settings
3. **Enjoy**: Your screen temperature will adjust automatically

## Portable Mode Features

[+] **No Installation Required** - Just extract and run
[+] **Portable Settings** - All configuration stored locally
[+] **Leave No Traces** - No registry entries or system changes
[+] **USB Friendly** - Run from external drives

## System Requirements

- Windows 10 version 1903+ or Windows 11
- .NET 8.0 Runtime (included in this package)
- DirectX 11 compatible graphics card

## Directory Structure

```
ChronoGuard-Portable/
|-- ChronoGuard.App.exe     # Main application
|-- portable.txt            # Portable mode marker
|-- config/                 # Configuration files
|-- logs/                   # Application logs (created on first run)
|-- profiles/               # Color profiles (created on first run)
\`-- README.txt              # This file
```

## Configuration

All settings are stored in the 'config/' directory:
- appsettings.json - Application configuration
- user-settings.json - User preferences (created on first run)

## Troubleshooting

**ChronoGuard won't start:**
- Ensure your graphics drivers are up to date
- Run as Administrator if needed
- Check 'logs/' directory for error details

**Color changes not working:**
- Update graphics drivers
- Disable other color management software
- Check monitor compatibility in Settings

## Support

- **Documentation**: Installation Guide at GitHub
- **Issues**: GitHub Issues section
- **Community**: GitHub Discussions

## Legal

ChronoGuard is open source software licensed under the MIT License.
See LICENSE file for full details.

Built on $(Get-Date -Format 'yyyy-MM-dd') | Version $Version
"@

$readmePath = Join-Path $PortableDir "README.txt"
$readmeContent | Out-File -FilePath $readmePath -Encoding utf8

Write-Host "[OK] Documentation created" -ForegroundColor Green

# Step 5: Create ZIP package
Write-Host "[ZIP] Creating ZIP package..." -ForegroundColor Yellow
try {
    $zipName = "ChronoGuard-Portable-v$Version.zip"
    $zipPath = Join-Path $OutputDir $zipName
    
    # Remove existing ZIP if present
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    # Create ZIP (requires PowerShell 5.0+)
    Compress-Archive -Path "$PortableDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    # Clean up temporary directory
    Remove-Item $PortableDir -Recurse -Force
    
    $fileSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    $fileCount = (Get-ChildItem -Path $PublishDir -Recurse -File).Count
    
    Write-Host "[OK] ZIP package created successfully" -ForegroundColor Green
    Write-Host ""
    Write-Host "[SUCCESS] Portable package created!" -ForegroundColor Green
    Write-Host "[INFO] Location: $zipPath" -ForegroundColor Cyan
    Write-Host "[INFO] Size: $fileSize MB" -ForegroundColor Cyan
    Write-Host "[INFO] Files: $fileCount" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "[READY] Ready for distribution!" -ForegroundColor Green
    
} catch {
    Write-Host "[ERROR] Failed to create ZIP: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 6: Create checksum
$checksumFile = $zipPath + ".sha256"
$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
$hash.Hash.ToLower() + "  " + $zipName | Out-File -FilePath $checksumFile -Encoding utf8
Write-Host "[INFO] Checksum created: $checksumFile" -ForegroundColor Cyan

Write-Host ""
Write-Host "[SUCCESS] Portable build completed successfully!" -ForegroundColor Green

