@echo off
chcp 65001 >nul
echo PostgreSQL Veritabani Sunucusu Baslatiliyor...

rem Veri klasoru OneDrive disinda tutulur (Masaustu\appsettings.json -> VeritabaniAyarlari:VeriKlasoru).
set "VERI=C:\MuhasebeVeri\db-data"

if not exist "C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" (
    echo PostgreSQL 16 bulunamadi.
    pause
    exit /b 1
)

rem postmaster.pid elle silinmez: pg_ctl eski kilit dosyasini kendisi tanir, calisan sunucunun kilidini silmek veriyi bozabilir.
"C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" start -D "%VERI%" -w -t 120 -o "-c logging_collector=on"

echo.
echo Islem tamamlandi.
pause
