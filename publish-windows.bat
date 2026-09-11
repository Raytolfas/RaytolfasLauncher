@echo off
echo ===================================================
echo   Building Single-File RaytolfasLauncher for Windows
echo ===================================================
dotnet publish RaytolfasLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\win-x64
echo.
echo ===================================================
echo   SUCCESS!
echo   Single standalone file: publish\win-x64\RaytolfasLauncher.exe
echo   (Contains all DLLs, native libraries, and .NET runtime)
echo ===================================================
pause
