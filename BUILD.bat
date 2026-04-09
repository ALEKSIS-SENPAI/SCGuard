@echo off
title SCGuard Builder
echo.
echo  ================================================
echo   SCGuard - One-Click Builder
echo  ================================================
echo.

:: Check for .NET SDK
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo  [!] .NET SDK not found.
    echo.
    echo  Please install it from:
    echo  https://dotnet.microsoft.com/download/dotnet/8.0
    echo  ^(Download the SDK - x64 Windows Installer^)
    echo.
    pause
    exit /b 1
)

echo  [OK] .NET SDK found
dotnet --version
echo.

echo  [..] Building SCGuard...
dotnet publish SCGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if %errorlevel% neq 0 (
    echo.
    echo  [FAIL] Build failed. See errors above.
    pause
    exit /b 1
)

echo.
echo  [OK] Build complete!
echo.
echo  Output: bin\Release\net8.0-windows\win-x64\publish\SCGuard.exe
echo.

:: Ask to copy to Desktop
set /p COPY="  Copy SCGuard.exe to your Desktop? (y/n): "
if /i "%COPY%"=="y" (
    copy "bin\Release\net8.0-windows\win-x64\publish\SCGuard.exe" "%USERPROFILE%\Desktop\SCGuard.exe"
    echo  [OK] Copied to Desktop!
)

echo.
echo  SETUP COMPLETE
echo  ================================================
echo  - Double-click SCGuard.exe to start
echo  - Right-click the tray icon to add to Startup:
echo    Copy the .exe to:
echo    shell:startup  (Win+R, type shell:startup, Enter)
echo.
pause
