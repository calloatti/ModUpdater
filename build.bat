@echo off
echo ========================================
echo Compiling ModUpdater for Windows (Single File)...
echo ========================================
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/modupdater-win
echo.
echo ========================================
echo Compiling ModUpdater for Linux (Single File)...
echo ========================================
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/modupdater-linux

echo.
echo ========================================
echo Build Complete! Check the "Publish" folder.
echo ========================================
pause