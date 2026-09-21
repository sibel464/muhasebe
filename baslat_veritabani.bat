@echo off
chcp 65001 >nul
echo PostgreSQL Veritabani Sunucusu Baslatiliyor...

if exist "%~dp0db-data\postmaster.pid" (
    del /f /q "%~dp0db-data\postmaster.pid" 2>nul
)

if exist "C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" (
    "C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" start -D "%~dp0db-data" -l "%TEMP%\pg_server.log"
) else (
    echo PostgreSQL 16 bulunamadi. postgres.exe calistirilmaya calisiliyor...
    start "" postgres.exe -D "%~dp0db-data"
)

echo.
echo Islem tamamlandi.
pause
