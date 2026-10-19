#!/bin/bash
set -e
echo "==================================================="
echo "  Building Single-File RaytolfasLauncher for macOS "
echo "==================================================="
echo "1. Building for macOS Apple Silicon (ARM64)..."
dotnet publish RaytolfasLauncher.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o publish/osx-arm64
chmod +x publish/osx-arm64/RaytolfasLauncher 2>/dev/null || true

echo ""
echo "2. Building for macOS Intel (x64)..."
dotnet publish RaytolfasLauncher.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o publish/osx-x64
chmod +x publish/osx-x64/RaytolfasLauncher 2>/dev/null || true

echo ""
echo "==================================================="
echo "  SUCCESS!"
echo "  - macOS ARM64: publish/osx-arm64/RaytolfasLauncher"
echo "  - macOS Intel: publish/osx-x64/RaytolfasLauncher"
echo "  (Contains all DLLs, native libraries, and .NET runtime)"
echo "==================================================="
