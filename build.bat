@echo off
cd /d %~dp0
dotnet publish src -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish || goto :err
echo Build OK: publish\AudioReceiver.exe
pause
exit /b 0
:err
echo Build FAILED
pause
exit /b 1
