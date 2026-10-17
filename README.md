# Ön Muhasebe ve Finans Yönetim Sistemi (Muhasebe)

.NET ve WPF tabanlı, PostgreSQL veritabanı mimarisine sahip modern ve kapsamlı kurumsal ön muhasebe ve finans yönetim sistemi.

## 🚀 Özellikler

- **Cari Yönetimi (Cari 360):** Müşteri ve tedarikçi kartları, cari hareketler, ekstreler, bakiye ve risk takibi.
- **Stok ve Depo Yönetimi:** Çoklu depo takibi, stok giriş/çıkış, sayım ve transfer fişleri, stok operasyon merkezi.
- **Fatura ve İrsaliye:** Satış, alış faturaları ve irsaliye entegrasyonu, stok ve cari entegre hareketleri.
- **Finans ve Kasa/Banka:**
  - Kasa 360 ve Banka 360 modülleri
  - Çek / Senet takibi
  - Tahsilat ve ödeme makbuzları
  - POS işlemleri ve banka mutabakatı
  - Belge kapama ve borç/alacak eşleştirme
- **Muhasebe ve Raporlama:**
  - Genel muhasebe entegrasyonu, hesap planı ve yevmiye fişleri
  - TCMB otomatik döviz kuru entegrasyonu ve revalüasyon
  - Rapor Merkezi (PDF ve Excel dışa aktarım desteği)
- **Çoklu Firma ve Güvenlik:** Çoklu şirket/şube mimarisi, kullanıcı rol ve detaylı yetkilendirme altyapısı.

## 🛠️ Teknolojiler

- **C# / .NET** (WPF masaüstü kullanıcı arayüzü)
- **PostgreSQL** (İlişkisel veritabanı)
- **Entity Framework Core / Npgsql** veri erişim katmanı

## Kurulum ve çalıştırma

Windows üzerinde .NET 10 SDK ve PostgreSQL 16 gereklidir. Veritabanı bağlantısı
`Masaustu/appsettings.json` içindeki `VeritabaniAyarlari:BaglantiDizesi` alanından okunur.
Yerel parola veya farklı sunucu ayarları için Git tarafından izlenmeyen
`Masaustu/appsettings.local.json` dosyasını kullanın. Bu dosya `appsettings.json`
ile aynı JSON yapısında olmalıdır.

```powershell
dotnet build Muhasebe.slnx
dotnet run --project Masaustu/Masaustu.csproj
```

Uygulama açılışta bekleyen EF Core migration dosyalarını uygular. Kurulum (Release)
sürümünde, içinde veri olan bir veritabanı güncellenmeden önce otomatik yedek alınır;
yedek alınamazsa güncelleme yapılmaz ve program açılmaz.

## Müşteri kurulum paketi (MSI)

```powershell
dotnet tool restore
powershell -ExecutionPolicy Bypass -File kurulum\Paketle.ps1 -Surum 1.0.0
```

Çıktı: `kurulum\cikti\MuhasebeYonetim-1.0.0.msi`. Paket tek başına çalışır: .NET çalışma
zamanı ve PostgreSQL 16 (bu bilgisayardaki `C:\Program Files\PostgreSQL` kurulumundan
yalnız sunucu, kurulum ve yedek araçları) pakete gömülüdür; müşteride ayrıca bir şey kurulmaz.

Müşteri bilgisayarında:

- Program `C:\Program Files\Muhasebe Yönetim Sistemi` klasörüne kurulur; masaüstü ve Başlat menüsü kısayolu açılır.
- İlk açılışta `C:\ProgramData\MuhasebeYonetim\db-data` klasörüne Türkçe sıralamalı (ICU tr-TR) yeni
  bir veritabanı kurulur. Gömülü sunucu ayrı bir portta (5544) çalışır, bilgisayardaki başka bir
  PostgreSQL ile çakışmaz ve program kapanınca durur.
- Veritabanı şifresi rastgele üretilir, Windows DPAPI (makine kapsamı) ile şifrelenip
  `C:\ProgramData\MuhasebeYonetimppsettings.local.json` dosyasında saklanır. Düz metin şifre yoktur;
  dosya başka bilgisayara kopyalanırsa çözülemez.
- Loglar `C:\ProgramData\MuhasebeYonetim\Loglar`, otomatik yedekler `Belgeler\Muhasebe Yedekleri` altındadır.
- İlk giriş: kullanıcı `admin`, şifre `admin123`. Program ilk girişte şifrenin değiştirilmesini zorunlu tutar.
- Programı kaldırmak verileri silmez (`C:\ProgramData\MuhasebeYonetim` yerinde kalır). Yeni sürüm
  eski sürümün üzerine kurulur; veritabanı açılışta yedeklenip güncellenir.

`kurulum\Lisans.rtf` bir **taslaktır**; satıştan önce hukuki olarak son haline getirilmelidir.

## Oturum zaman aşımı

Program `UygulamaAyarlari:OturumZamanAsimiDakika` (varsayılan 15) dakika boyunca klavye
veya fare hareketi görmezse açık pencereleri bulanıklaştırıp kilitler. Devam etmek için
oturum sahibinin şifresi gerekir; "Çıkış Yap" ile giriş ekranına dönülür. Hatalı şifre
denemeleri giriş ekranındaki kilitleme kuralına tabidir. Değeri `0` yapmak özelliği kapatır.

## Otomatik yedekleme ve geri yükleme

Uygulama her açılışta son yedek 24 saatten eskiyse arka planda `pg_dump` ile tam yedek
alır. Yedekler varsayılan olarak `Belgeler\Muhasebe Yedekleri` klasörüne
`Muhasebe_YYYY-AA-GG_SSDDss_mmm.dump` adıyla yazılır ve son 30 yedek saklanır.
Klasör, yedek sayısı ve otomatik yedeğin açık/kapalı olması `appsettings.json`
içindeki `YedekAyarlari` bölümünden değiştirilebilir. Elle yedek almak ve yedekleri
görmek için **Ayarlar → Yedekleme** sekmesini kullanın (Ayarları Yönet yetkisi gerekir).

Bir yedeğe geri dönmek için uygulamayı kapatın ve aşağıdaki komutu kendi dosya
adınızla çalıştırın. Bu işlem veritabanındaki mevcut verinin yerine yedeği yazar;
önce güncel durumun da yedeğini alın.

```powershell
& 'C:\Program Files\PostgreSQL\16\bin\pg_restore.exe' -h 127.0.0.1 -p 5432 -U postgres -d Muhasebe --clean --if-exists "$env:USERPROFILE\Documents\Muhasebe Yedekleri\Muhasebe_2026-10-14_143600_000.dump"
```

## Üretim veritabanını güncelleme

İşlemden önce uygulamayı kapatın ve hedef veritabanının tam yedeğini alın.
Aşağıdaki komutlarda bağlantı bilgilerini kendi ortamınıza göre değiştirin.
`--connection` parametresini atlamayın: tasarım zamanı fabrikasının varsayılanı
`Muhasebe_Test` veritabanıdır.

```powershell
& 'C:\Program Files\PostgreSQL\16\bin\pg_dump.exe' -h 127.0.0.1 -p 5432 -U postgres -d Muhasebe -Fc -f 'Muhasebe-yedek.dump'
& 'C:\Program Files\PostgreSQL\16\bin\pg_restore.exe' -l 'Muhasebe-yedek.dump'
dotnet ef database update --project Altyapi/Altyapi.csproj --startup-project Masaustu/Masaustu.csproj --context MuhasebeBaglami --connection 'Host=127.0.0.1;Port=5432;Database=Muhasebe;Username=postgres;'
```

`20260921000000_RedactAuditPasswordHash` migration'ı, eski kullanıcı denetim
kayıtlarının JSON içindeki `SifreHash` alanını temizler. Bu temizlik geri
alınamaz; gerektiğinde yukarıdaki yedekten dönülmelidir. Güncelleme sonrası
`__EFMigrationsHistory` tablosunda bu migration'ın bulunduğunu ve uygulamanın
açıldığını doğrulayın. Entegrasyon testleri üretim veritabanını kullanmaz;
adı `_Test` ile biten ayrı bir veritabanı kullanır:

```powershell
dotnet test Altyapi.EntegrasyonTestleri/Altyapi.EntegrasyonTestleri.csproj
```
- **Material / Modern UI** bileşenleri
