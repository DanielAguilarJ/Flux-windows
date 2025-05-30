# PowerShell script to verify the ChronoGuard implementation
Write-Host "Verifying ChronoGuard Implementation..." -ForegroundColor Green

# Build the test project
Write-Host "Building ChronoGuard.Tests..." -ForegroundColor Yellow
dotnet build "src\ChronoGuard.Tests\ChronoGuard.Tests.csproj" --configuration Release --verbosity minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Run the tests
    Write-Host "Running unit tests..." -ForegroundColor Yellow
    dotnet test "src\ChronoGuard.Tests\ChronoGuard.Tests.csproj" --configuration Release --verbosity normal --logger "console;verbosity=detailed"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "All tests passed! Implementation is working correctly." -ForegroundColor Green
    } else {
        Write-Host "Some tests failed. Check the output above for details." -ForegroundColor Red
    }
} else {
    Write-Host "Build failed. Check the output above for compilation errors." -ForegroundColor Red
}

# Also try to build and run the test app
Write-Host "`nBuilding ChronoGuard.TestApp..." -ForegroundColor Yellow
dotnet build "src\ChronoGuard.TestApp\ChronoGuard.TestApp.csproj" --configuration Release --verbosity minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "TestApp build successful! Running test application..." -ForegroundColor Green
    dotnet run --project "src\ChronoGuard.TestApp\ChronoGuard.TestApp.csproj" --configuration Release
} else {
    Write-Host "TestApp build failed." -ForegroundColor Red
}

Write-Host "`nVerification complete." -ForegroundColor Green
