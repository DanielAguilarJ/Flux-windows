@echo off
echo ========================================
echo   VERIFICANDO CHRONOGUARD TESTAPP
echo ========================================
echo.

cd /d "c:\Users\junco\OneDrive\JUNCOKEVIN\Documents\GitHub\Flux-windows\ChronoGuard"

echo 1. Compilando el proyecto...
dotnet build src\ChronoGuard.TestApp\ChronoGuard.TestApp.csproj --configuration Release --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo ERROR: Fallo en la compilacion del TestApp
    pause
    exit /b 1
)
echo    ✓ Compilacion exitosa

echo.
echo 2. Ejecutando TestApp...
dotnet run --project src\ChronoGuard.TestApp\ChronoGuard.TestApp.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Fallo en la ejecucion del TestApp
    pause
    exit /b 1
)

echo.
echo 3. Compilando tests...
dotnet build src\ChronoGuard.Tests\ChronoGuard.Tests.csproj --configuration Release --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo ERROR: Fallo en la compilacion de tests
    pause
    exit /b 1
)
echo    ✓ Tests compilados exitosamente

echo.
echo 4. Ejecutando tests especificos...
dotnet test src\ChronoGuard.Tests\ChronoGuard.Tests.csproj --filter "ChronoGuardBackgroundServiceTests" --configuration Release --no-build --verbosity normal
if %ERRORLEVEL% neq 0 (
    echo ERROR: Algunos tests fallaron
    pause
    exit /b 1
)

echo.
echo ========================================
echo   ✓ CHRONOGUARD ESTA LISTO PARA USAR
echo ========================================
pause
