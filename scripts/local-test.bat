@echo off
setlocal enabledelayedexpansion

rem Local Development Testing Script for Windows
rem Mirrors the CI pipeline for local development

echo 🚀 Starting ERP Inventory Module Local Test Pipeline
echo ==================================================

rem Step 1: Clean previous builds
echo [INFO] Cleaning previous builds...
dotnet clean
if exist TestResults rmdir /s /q TestResults
if exist coverage rmdir /s /q coverage

rem Step 2: Restore dependencies
echo [INFO] Restoring dependencies...
dotnet restore
if !errorlevel! neq 0 (
    echo [ERROR] Failed to restore dependencies
    exit /b 1
)

rem Step 3: Build solution
echo [INFO] Building solution...
dotnet build --no-restore --configuration Release
if !errorlevel! neq 0 (
    echo [ERROR] Build failed
    exit /b 1
)
echo [SUCCESS] Build completed successfully

rem Step 4: Run unit tests (excluding integration tests)
echo [INFO] Running unit tests...
dotnet test --no-build --configuration Release ^
    --collect:"XPlat Code Coverage" ^
    --results-directory ./TestResults ^
    --logger trx ^
    --verbosity normal ^
    --filter "Category!=Integration" ^
    --settings tests.runsettings
if !errorlevel! equ 0 (
    echo [SUCCESS] Unit tests passed
) else (
    echo [WARNING] Some unit tests failed
)

rem Step 5: Check Docker availability
echo [INFO] Checking Docker availability...
docker info >nul 2>&1
if !errorlevel! neq 0 (
    echo [ERROR] Docker is not running or not installed
    echo Please start Docker Desktop and try again
    exit /b 1
)
echo [SUCCESS] Docker is available

rem Step 6: Run integration tests
echo [INFO] Running integration tests...
dotnet test --no-build --configuration Release ^
    --collect:"XPlat Code Coverage" ^
    --results-directory ./TestResults ^
    --logger trx ^
    --verbosity normal ^
    --filter "Category=Integration" ^
    --settings tests.runsettings
if !errorlevel! equ 0 (
    echo [SUCCESS] Integration tests passed
) else (
    echo [WARNING] Some integration tests failed
)

rem Step 7: Run end-to-end tests
echo [INFO] Running end-to-end tests...
dotnet test --no-build --configuration Release ^
    --collect:"XPlat Code Coverage" ^
    --results-directory ./TestResults ^
    --logger trx ^
    --verbosity normal ^
    tests/EndToEnd.Integration.Tests/ ^
    --settings tests.runsettings
if !errorlevel! equ 0 (
    echo [SUCCESS] End-to-end tests passed
) else (
    echo [WARNING] Some end-to-end tests failed
)

rem Step 8: Security scan
echo [INFO] Running security audit...
dotnet list package --vulnerable --include-transitive
if !errorlevel! neq 0 (
    echo [WARNING] Security audit completed with warnings
)

rem Step 9: Check for outdated packages
echo [INFO] Checking for outdated packages...
dotnet list package --outdated --include-transitive
if !errorlevel! neq 0 (
    echo [WARNING] Some packages are outdated
)

rem Step 10: Generate test report
echo [INFO] Generating test report...
if exist TestResults (
    echo Test results generated in ./TestResults
    dir /s /b TestResults\*.trx
    dir /s /b TestResults\*.xml
) else (
    echo [WARNING] No test results found
)

echo [SUCCESS] Local test pipeline completed!
echo [INFO] Check ./TestResults for detailed test reports and coverage data

rem Optional: Open test results in VS Code
where code >nul 2>&1
if !errorlevel! equ 0 (
    echo [INFO] Opening test results in VS Code...
    code ./TestResults
)

pause
