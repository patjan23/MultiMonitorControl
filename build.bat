@echo off
echo Multi-Monitor Control Panel - Build Script
echo ==========================================

REM Check if .NET 6 is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET 6 SDK is not installed or not in PATH
    echo Please download and install .NET 6 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/6.0
    pause
    exit /b 1
)

echo .NET SDK Version:
dotnet --version
echo.

REM Clean previous builds
echo Cleaning previous builds...
if exist "bin" rmdir /s /q bin
if exist "obj" rmdir /s /q obj

REM Restore dependencies
echo Restoring NuGet packages...
dotnet restore
if errorlevel 1 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)

REM Build Debug version
echo.
echo Building Debug version...
dotnet build --configuration Debug
if errorlevel 1 (
    echo ERROR: Debug build failed
    pause
    exit /b 1
)

REM Build Release version
echo.
echo Building Release version...
dotnet build --configuration Release
if errorlevel 1 (
    echo ERROR: Release build failed
    pause
    exit /b 1
)

REM Publish self-contained executable
echo.
echo Publishing self-contained executable...
dotnet publish --configuration Release --self-contained true --runtime win-x64 --output "publish"
if errorlevel 1 (
    echo ERROR: Publish failed
    pause
    exit /b 1
)

echo.
echo ==========================================
echo Build completed successfully!
echo.
echo Debug build: bin\Debug\net6.0-windows\
echo Release build: bin\Release\net6.0-windows\
echo Self-contained: publish\
echo.
echo You can run the application with:
echo   dotnet run
echo or execute: publish\MultiMonitorControl.exe
echo ==========================================

pause