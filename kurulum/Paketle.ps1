<#
  Muhasebe Yönetim Sistemi kurulum paketini (MSI) üretir.
  Kullanım (proje kökünden):  powershell -ExecutionPolicy Bypass -File kurulum\Paketle.ps1 [-Surum 1.0.0] [-PostgresKaynak "C:\Program Files\PostgreSQL\16"]
  Çıktı: kurulum\cikti\MuhasebeYonetim-<sürüm>.msi
  Gerekenler: .NET SDK, proje içi WiX aracı (dotnet tool restore) ve bu bilgisayarda kurulu PostgreSQL 16 (yalnız dosyaları kopyalanır).
  -YayinAdresi verilirse (ör. https://site.com/guncelleme) lisans anahtarıyla imzalı kurulum\cikti\guncelleme.json da üretilir;
  MSI ve guncelleme.json o adrese yüklenir, programdaki GuncellemeAdresi = <YayinAdresi>/guncelleme.json olur.
  Her yeni sürümde -Surum artırılmalıdır (ör. 1.0.1); aynı sürüm üzerine kurulmaz.
  Kod imzalama: -ImzaSertifikasi ile sertifikanın parmak izi (Windows sertifika deposunda, ör. USB token'lı EV sertifikası)
  ya da .pfx dosyası (-ImzaSifresi ile) verilirse Masaustu.exe ve MSI imzalanır, zaman damgası eklenir. İmzasız paket
  müşteride Windows SmartScreen "tanınmayan uygulama" uyarısı verir.
#>
param(
    [string]$Surum = "1.0.0",
    [string]$PostgresKaynak = "C:\Program Files\PostgreSQL\16",
    [string]$YayinAdresi = "",
    [string]$Notlar = "",
    [string]$ImzaSertifikasi = "",
    [string]$ImzaSifresi = "",
    [string]$ZamanDamgasi = "http://timestamp.digicert.com"
)
$ErrorActionPreference = "Stop"

$sertifika = $null
if ($ImzaSertifikasi) {
    if (Test-Path $ImzaSertifikasi) {
        $sertifika = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($ImzaSertifikasi, $ImzaSifresi)
    } else {
        $parmakIzi = ($ImzaSertifikasi -replace '\s', '').ToUpperInvariant()
        $sertifika = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $parmakIzi } | Select-Object -First 1
    }
    if (-not $sertifika -or -not $sertifika.HasPrivateKey) { throw "İmza sertifikası bulunamadı ya da özel anahtarı yok: $ImzaSertifikasi" }
    Write-Host "Kod imzalama sertifikası: $($sertifika.Subject) (geçerlilik $($sertifika.NotAfter.ToString('dd.MM.yyyy')))"
}
function Imzala([string]$dosya) {
    if (-not $sertifika) { return }
    $sonuc = Set-AuthenticodeSignature -FilePath $dosya -Certificate $sertifika -HashAlgorithm SHA256 -TimestampServer $ZamanDamgasi
    if (-not $sonuc.SignerCertificate) { throw "İmzalanamadı ($dosya): $($sonuc.StatusMessage)" }
    if ($sonuc.Status -ne "Valid") { Write-Host "  UYARI ($(Split-Path -Leaf $dosya)): imza eklendi ama Windows sertifikaya güvenmiyor: $($sonuc.StatusMessage)" -ForegroundColor Yellow }
    if (-not $sonuc.TimeStamperCertificate) { Write-Host "  UYARI: zaman damgası eklenemedi (internet?). Sertifika süresi bitince imza geçersiz sayılır." -ForegroundColor Yellow }
    Write-Host "  imzalandı: $(Split-Path -Leaf $dosya)"
}
$kok = Split-Path -Parent $PSScriptRoot
$cikti = Join-Path $PSScriptRoot "cikti"
$uygulama = Join-Path $cikti "uygulama"

if (-not (Test-Path (Join-Path $PostgresKaynak "bin\postgres.exe"))) { throw "PostgreSQL bulunamadı: $PostgresKaynak" }
if (Test-Path $cikti) { Remove-Item -Recurse -Force $cikti }
New-Item -ItemType Directory -Force $uygulama | Out-Null

Write-Host "1/4 Uygulama yayınlanıyor (tek başına çalışır, .NET kurulumu gerekmez)..."
dotnet publish (Join-Path $kok "Masaustu\Masaustu.csproj") -c Release -r win-x64 --self-contained true `
    -p:Version=$Surum -p:DebugType=None -p:GenerateDocumentationFile=false -o $uygulama --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız." }

# Geliştirme ayarları yerine kurulum ayarları (gömülü PostgreSQL, ayrı port, veri ProgramData'da)
Copy-Item (Join-Path $PSScriptRoot "appsettings.Kurulum.json") (Join-Path $uygulama "appsettings.json") -Force
Get-ChildItem $uygulama -Filter "appsettings.local.json" | Remove-Item -Force

Imzala (Join-Path $uygulama "Masaustu.exe")

Write-Host "2/4 PostgreSQL dosyaları ekleniyor..."
$pg = Join-Path $uygulama "pgsql"
New-Item -ItemType Directory -Force (Join-Path $pg "bin"), (Join-Path $pg "LISANSLAR") | Out-Null
# Yalnız sunucu, kurulum, yedek/geri yükleme ve bakım araçları; StackBuilder, pgAdmin ve test araçları alınmaz.
$araclar = "postgres.exe","pg_ctl.exe","initdb.exe","pg_dump.exe","pg_restore.exe","psql.exe","pg_isready.exe","pg_controldata.exe","pg_resetwal.exe","pg_upgrade.exe"
foreach ($a in $araclar) { Copy-Item (Join-Path $PostgresKaynak "bin\$a") (Join-Path $pg "bin") }
Get-ChildItem (Join-Path $PostgresKaynak "bin") -Filter *.dll | Where-Object { $_.Name -notlike "wx*" } | Copy-Item -Destination (Join-Path $pg "bin")
Copy-Item (Join-Path $PostgresKaynak "lib") $pg -Recurse
Copy-Item (Join-Path $PostgresKaynak "share") $pg -Recurse
foreach ($l in "server_license.txt","commandlinetools_3rd_party_licenses.txt") {
    if (Test-Path (Join-Path $PostgresKaynak $l)) { Copy-Item (Join-Path $PostgresKaynak $l) (Join-Path $pg "LISANSLAR") }
}
# Geliştirme dosyaları (.lib, .pdb) kuruluma gerekmez
Get-ChildItem $pg -Recurse -Include *.lib,*.pdb | Remove-Item -Force

Write-Host "3/4 Paket denetleniyor..."
foreach ($gerekli in "Masaustu.exe","appsettings.json","pgsql\bin\postgres.exe","pgsql\bin\initdb.exe","pgsql\bin\pg_ctl.exe","pgsql\share\postgres.bki") {
    if (-not (Test-Path (Join-Path $uygulama $gerekli))) { throw "Pakette eksik dosya: $gerekli" }
}

Write-Host "4/4 MSI oluşturuluyor..."
Push-Location $kok
try {
    dotnet tool restore | Out-Null
    $msi = Join-Path $cikti "MuhasebeYonetim-$Surum.msi"
    dotnet wix build (Join-Path $PSScriptRoot "Paket.wxs") -arch x64 `
        -ext WixToolset.Util.wixext -ext WixToolset.UI.wixext `
        -d "Kaynak=$uygulama" -d "Surum=$Surum" -culture tr-TR -loc (Join-Path $PSScriptRoot "tr-TR.wxl") -o $msi
    if ($LASTEXITCODE -ne 0) { throw "MSI oluşturulamadı." }
}
finally { Pop-Location }

Imzala $msi
if (-not $sertifika) { Write-Host "UYARI: paket imzasız (müşteride SmartScreen uyarısı çıkar). İmzalamak için -ImzaSertifikasi verin." -ForegroundColor Yellow }

$boyut = [math]::Round((Get-Item $msi).Length / 1MB, 1)
Write-Host "Hazır: $msi ($boyut MB)"

if ($YayinAdresi) {
    # Lisans özel anahtarıyla imzalanır; anahtar yoksa bildirim üretilmez.
    $adres = $YayinAdresi.TrimEnd('/') + "/MuhasebeYonetim-$Surum.msi"
    dotnet run --project (Join-Path $kok "LisansUretici") -v q -- guncelleme --msi $msi --surum $Surum --adres $adres --notlar $Notlar --cikti (Join-Path $cikti "guncelleme.json")
    if ($LASTEXITCODE -ne 0) { throw "Güncelleme bildirimi üretilemedi." }
}
