#!/bin/bash
set -e
cd "$(dirname "$0")"
echo "Building Soundboard for Windows x64..."
dotnet publish -c Debug -r win-x64 --self-contained false -o bin/publish
echo ""
echo "To run under Wine:"
echo "  WINEPREFIX=~/.wine DISPLAY=:1 wine bin/publish/Soundboard.exe"
echo ""
echo "To build on Windows (without EnableWindowsTargeting):"
echo "  Remove <EnableWindowsTargeting>true</EnableWindowsTargeting> from .csproj"
echo "  dotnet build"