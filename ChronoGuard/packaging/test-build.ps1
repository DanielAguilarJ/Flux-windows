# Simple test script for portable packaging
param(
    [string]$Version = "1.0.0-test"
)

Write-Host "Starting simple portable build test..."
Write-Host "Version: $Version"

# Create output directory
$OutputDir = ".\output"
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force

# Check if the published app exists
$PublishDir = "..\src\ChronoGuard.App\bin\Release\net8.0-windows10.0.22621.0\win-x64\publish"
if (Test-Path $PublishDir) {
    Write-Host "Found published app at: $PublishDir"
    
    # Create portable directory
    $PortableDir = Join-Path $OutputDir "ChronoGuard-Portable"
    New-Item -ItemType Directory -Path $PortableDir -Force
    
    # Copy application files
    Copy-Item -Path "$PublishDir\*" -Destination $PortableDir -Recurse -Force
    
    # Create portable marker
    $portableMarker = Join-Path $PortableDir "portable.txt"
    "ChronoGuard Portable Mode - Version $Version" | Out-File -FilePath $portableMarker -Encoding utf8
    
    # Create ZIP
    $zipPath = Join-Path $OutputDir "ChronoGuard-Portable-v$Version.zip"
    Compress-Archive -Path "$PortableDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    # Clean up
    Remove-Item $PortableDir -Recurse -Force
    
    $fileSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "SUCCESS: Created portable ZIP: $zipPath ($fileSize MB)"
} else {
    Write-Host "ERROR: Published app not found at $PublishDir"
    Write-Host "Please run: dotnet publish --configuration Release --runtime win-x64 --self-contained true"
}

Write-Host "Test completed."
