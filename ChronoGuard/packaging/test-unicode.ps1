# Test Unicode Character Detection
# Scans PowerShell files for Unicode emoji characters

$ScriptDir = $PSScriptRoot
$PowerShellFiles = Get-ChildItem -Path $ScriptDir -Filter "*.ps1"

Write-Host "[TEST] Unicode Character Detection Report" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

$UnicodeFound = $false

foreach ($file in $PowerShellFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    
    # Common Unicode emoji patterns
    $unicodePatterns = @(
        '🎉', '📱', '🧹', '🔧', '✅', '❌', '📦', '🏗️', '🔨', '📁', '📏', '🚀', '🔐', '✨',
        '📋', '⚙️', '📄', '🗜️', '🎯', '⚡', '🔑', '🌟', '💡', '🔍', '📈', '⭐'
    )
    
    $fileHasUnicode = $false
    
    foreach ($pattern in $unicodePatterns) {
        if ($content -match [regex]::Escape($pattern)) {
            if (-not $fileHasUnicode) {
                Write-Host "[UNICODE] $($file.Name):" -ForegroundColor Yellow
                $fileHasUnicode = $true
                $UnicodeFound = $true
            }
            Write-Host "  Found: $pattern" -ForegroundColor Red
        }
    }
    
    if (-not $fileHasUnicode) {
        Write-Host "[OK] $($file.Name) - No Unicode characters" -ForegroundColor Green
    }
}

Write-Host ""
if ($UnicodeFound) {
    Write-Host "[RESULT] Unicode characters found - Scripts may have encoding issues" -ForegroundColor Red
    exit 1
} else {
    Write-Host "[RESULT] All scripts clean - No Unicode characters detected" -ForegroundColor Green
    exit 0
}
