#!/bin/bash
set -e
echo "==================================================="
echo "  Building Single-File RaytolfasLauncher for Linux "
echo "==================================================="
echo "1. Building for Linux x64..."
dotnet publish RaytolfasLauncher.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
chmod +x publish/linux-x64/RaytolfasLauncher 2>/dev/null || true

echo ""
echo "2. Building for Linux ARM64 (aarch64)..."
dotnet publish RaytolfasLauncher.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -o publish/linux-arm64
chmod +x publish/linux-arm64/RaytolfasLauncher 2>/dev/null || true

echo ""
echo "==================================================="
echo "  SUCCESS!"
echo "  - Linux x64:   publish/linux-x64/RaytolfasLauncher"
echo "  - Linux ARM64: publish/linux-arm64/RaytolfasLauncher"
echo "  (Contains all DLLs, native libraries, and .NET runtime)"
echo "==================================================="
