#!/bin/bash
set -e
echo "==================================================="
echo "  Building Single-File RaytolfasLauncher for Linux "
echo "==================================================="
dotnet publish RaytolfasLauncher.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
chmod +x publish/linux-x64/RaytolfasLauncher
echo ""
echo "==================================================="
echo "  SUCCESS!"
echo "  Single standalone file: publish/linux-x64/RaytolfasLauncher"
echo "  (Contains all DLLs, native libraries, and .NET runtime)"
echo "==================================================="
