@echo off
chcp 65001 >nul
echo PostgreSQL Veritabani Sunucusu Durduruluyor...

if exist "C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" (
    "C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" stop -D "%~dp0db-data" -m fast
) else (
    taskkill /f /im postgres.exe
)

echo.
echo Islem tamamlandi.
pause
