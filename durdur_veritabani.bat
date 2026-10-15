@echo off
chcp 65001 >nul
echo PostgreSQL Veritabani Sunucusu Durduruluyor...

set "VERI=C:\MuhasebeVeri\db-data"

if not exist "C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" (
    echo PostgreSQL 16 bulunamadi.
    pause
    exit /b 1
)

rem Sunucu zorla oldurulmez; duzgun kapanis veriyi korur.
"C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" stop -D "%VERI%" -m fast -w -t 60

echo.
echo Islem tamamlandi.
pause
