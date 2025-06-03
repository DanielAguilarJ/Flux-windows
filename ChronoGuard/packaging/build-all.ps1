# ChronoGuard Master Builder
# Builds all distribution packages (MSI, MSIX, Portable)

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0", 
    [string]$Platform = "x64",
    [switch]$Clean = $false,
    [switch]$SkipMsi = $false,
    [switch]$SkipMsix = $false,
    [switch]$SkipPortable = $false
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# ASCII Art
Write-Host ""
Write-Host "  ██████╗██╗  ██╗██████╗  ██████╗ ███╗   ██╗ ██████╗  ██████╗ ██╗   ██╗ █████╗ ██████╗ ██████╗ " -ForegroundColor Cyan
Write-Host " ██╔════╝██║  ██║██╔══██╗██╔═══██╗████╗  ██║██╔═══██╗██╔════╝ ██║   ██║██╔══██╗██╔══██╗██╔══██╗" -ForegroundColor Cyan
Write-Host " ██║     ███████║██████╔╝██║   ██║██╔██╗ ██║██║   ██║██║  ███╗██║   ██║███████║██████╔╝██║  ██║" -ForegroundColor Cyan
Write-Host " ██║     ██╔══██║██╔══██╗██║   ██║██║╚██╗██║██║   ██║██║   ██║██║   ██║██╔══██║██╔══██╗██║  ██║" -ForegroundColor Cyan
Write-Host " ╚██████╗██║  ██║██║  ██║╚██████╔╝██║ ╚████║╚██████╔╝╚██████╔╝╚██████╔╝██║  ██║██║  ██║██████╔╝" -ForegroundColor Cyan
Write-Host "  ╚═════╝╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═══╝ ╚═════╝  ╚═════╝  ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═════╝ " -ForegroundColor Cyan
Write-Host ""
Write-Host "                           Advanced Display Color Temperature Manager" -ForegroundColor Yellow
Write-Host ""
Write-Host "[PRODUCTION] Build System" -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Version: $Version" -ForegroundColor White
Write-Host "Platform: $Platform" -ForegroundColor White
Write-Host "Clean Build: $Clean" -ForegroundColor White
Write-Host ""

# Timing
$startTime = Get-Date

# Build summary
$buildResults = @{
    MSI = @{ Built = $false; Success = $false; Error = $null; Size = 0 }
    MSIX = @{ Built = $false; Success = $false; Error = $null; Size = 0 }
    Portable = @{ Built = $false; Success = $false; Error = $null; Size = 0 }
}

# Output directory
$outputDir = Join-Path $PSScriptRoot "output"
if ($Clean -and (Test-Path $outputDir)) {
    Write-Host "[CLEAN] Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Build MSI
if (-not $SkipMsi) {
    Write-Host "[MSI] Building MSI Installer..." -ForegroundColor Blue
    Write-Host "==============================" -ForegroundColor Blue
    try {
        $buildResults.MSI.Built = $true
        & "$PSScriptRoot\build-msi.ps1" -Configuration $Configuration -Version $Version -Platform $Platform -Clean:$Clean
        
        if ($LASTEXITCODE -eq 0) {
            $msiFile = Get-ChildItem -Path $outputDir -Filter "*Setup*.msi" | Select-Object -First 1
            if ($msiFile) {
                $buildResults.MSI.Success = $true
                $buildResults.MSI.Size = [math]::Round($msiFile.Length / 1MB, 2)
            }
        }
    } catch {
        $buildResults.MSI.Error = $_.Exception.Message
        Write-Host "[WARNING] MSI build failed, continuing with other packages..." -ForegroundColor Yellow
    }
    Write-Host ""
}

# Build MSIX
if (-not $SkipMsix) {
    Write-Host "[MSIX] Building MSIX Package..." -ForegroundColor Blue
    Write-Host "============================" -ForegroundColor Blue
    try {
        $buildResults.MSIX.Built = $true
        & "$PSScriptRoot\build-msix.ps1" -Configuration $Configuration -Version $Version -Platform $Platform -Clean:$false
        
        if ($LASTEXITCODE -eq 0) {
            $msixFile = Get-ChildItem -Path $outputDir -Filter "*.msix" | Select-Object -First 1
            if ($msixFile) {
                $buildResults.MSIX.Success = $true
                $buildResults.MSIX.Size = [math]::Round($msixFile.Length / 1MB, 2)
            }
        }
    } catch {
        $buildResults.MSIX.Error = $_.Exception.Message
        Write-Host "[WARNING] MSIX build failed, continuing with other packages..." -ForegroundColor Yellow
    }
    Write-Host ""
}

# Build Portable
if (-not $SkipPortable) {
    Write-Host "[PORTABLE] Building Portable Package..." -ForegroundColor Blue
    Write-Host "================================" -ForegroundColor Blue
    try {
        $buildResults.Portable.Built = $true
        & "$PSScriptRoot\build-portable.ps1" -Configuration $Configuration -Version $Version -Platform $Platform -Clean:$false
        
        if ($LASTEXITCODE -eq 0) {
            $zipFile = Get-ChildItem -Path $outputDir -Filter "*Portable*.zip" | Select-Object -First 1
            if ($zipFile) {
                $buildResults.Portable.Success = $true
                $buildResults.Portable.Size = [math]::Round($zipFile.Length / 1MB, 2)
            }
        }
    } catch {
        $buildResults.Portable.Error = $_.Exception.Message
        Write-Host "[WARNING] Portable build failed..." -ForegroundColor Yellow
    }
    Write-Host ""
}

# Build Summary
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host "[SUMMARY] BUILD SUMMARY" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan
Write-Host "Build Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor White
Write-Host ""

$successCount = 0
$totalBuilt = 0

foreach ($package in $buildResults.Keys) {
    $result = $buildResults[$package]
    if ($result.Built) {
        $totalBuilt++        if ($result.Success) {
            $successCount++
            $status = "[OK] SUCCESS"
            $color = "Green"
            $sizeInfo = " ($($result.Size) MB)"
        } else {
            $status = "[FAIL] FAILED"
            $color = "Red"
            $sizeInfo = ""
            if ($result.Error) {
                Write-Host "   Error: $($result.Error)" -ForegroundColor Red
            }
        }
    } else {
        $status = "[SKIP] SKIPPED"
        $color = "Yellow"
        $sizeInfo = ""
    }
    
    Write-Host "[PACKAGE] $package`: $status$sizeInfo" -ForegroundColor $color
}

Write-Host ""

# Final status
if ($successCount -eq $totalBuilt -and $totalBuilt -gt 0) {
    Write-Host "[SUCCESS] ALL BUILDS SUCCESSFUL!" -ForegroundColor Green
    Write-Host "[INFO] Output Directory: $outputDir" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "[READY] ChronoGuard v$Version is ready for release!" -ForegroundColor Green
    Write-Host ""
    Write-Host "[NEXT] Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Test packages on clean Windows machines" -ForegroundColor White
    Write-Host "   2. Sign packages with production certificates" -ForegroundColor White
    Write-Host "   3. Upload to GitHub Releases" -ForegroundColor White
    Write-Host "   4. Update documentation and changelogs" -ForegroundColor White
    $exitCode = 0
} elseif ($successCount -gt 0) {
    Write-Host "[PARTIAL] PARTIAL SUCCESS" -ForegroundColor Yellow
    Write-Host "[INFO] Output Directory: $outputDir" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Some packages failed to build. Check errors above." -ForegroundColor Yellow
    $exitCode = 1
} else {
    Write-Host "[FAILED] ALL BUILDS FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "No packages were successfully created. Check errors above." -ForegroundColor Red
    $exitCode = 2
}

# List output files
Write-Host ""
Write-Host "[FILES] Generated Files:" -ForegroundColor Cyan
$outputFiles = Get-ChildItem -Path $outputDir -File | Sort-Object Name
foreach ($file in $outputFiles) {
    $size = [math]::Round($file.Length / 1MB, 2)
    Write-Host "   [FILE] $($file.Name) ($size MB)" -ForegroundColor White
}

Write-Host ""
exit $exitCode
