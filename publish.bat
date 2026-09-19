@echo off
echo ===================================================
echo   PC Launcher Server - Windows Tek Dosya Derleme
echo ===================================================
echo.

echo 1. Projeler temizleniyor ve derleniyor...
dotnet build PCLauncher.sln -c Release

if %ERRORLEVEL% NEQ 0 (
    echo [HATA] Derleme basarisiz oldu!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo 2. Self-contained Single-File .exe uretiliyor...
dotnet publish PCLauncher.App\PCLauncher.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin\publish-singlefile

if %ERRORLEVEL% NEQ 0 (
    echo [HATA] Publish islemi basarisiz oldu!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ===================================================
echo   Basariyla tamamlandi!
echo   Uretilen dosya konumu: bin\publish-singlefile\
echo ===================================================
pause
