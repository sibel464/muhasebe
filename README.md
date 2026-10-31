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
- **Material / Modern UI** bileşenleri

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
  PostgreSQL ile çakışmaz ve program kapanınca durur (ağ sunucusu modunda Windows hizmeti olarak sürekli çalışır).
- Veritabanı şifresi rastgele üretilir, Windows DPAPI (makine kapsamı) ile şifrelenip
  `C:\ProgramData\MuhasebeYonetim\appsettings.local.json` dosyasında saklanır. Düz metin şifre yoktur;
  dosya başka bilgisayara kopyalanırsa çözülemez.
- Loglar `C:\ProgramData\MuhasebeYonetim\Loglar`, otomatik yedekler `Belgeler\Muhasebe Yedekleri` altındadır.
- İlk giriş: kullanıcı `admin`, şifre `admin123`. Program ilk girişte şifrenin değiştirilmesini zorunlu tutar.
- Programı kaldırmak verileri silmez (`C:\ProgramData\MuhasebeYonetim` yerinde kalır). Yeni sürüm
  eski sürümün üzerine kurulur; veritabanı açılışta yedeklenip güncellenir.

`kurulum\Lisans.rtf` bir **taslaktır**; satıştan önce hukuki olarak son haline getirilmelidir.

## Lisans ve deneme sürümü

Kurulum sürümü ilk açılıştan itibaren 30 gün deneme olarak çalışır. Süre bitince program
yalnız lisans penceresini açar (veriler silinmez). Lisans; müşterinin bilgisayarına bağlı,
ECDSA ile imzalı bir dosyadır (`C:\ProgramData\MuhasebeYonetim\lisans.lic`), internet gerekmez.
Geliştirme ortamında lisans denetimi yapılmaz.

1. Müşteri **Ayarlar → Lisans** (veya deneme bitince açılan pencere) ekranındaki makine kodunu iletir.
2. Satıcı lisans dosyasını üretir ve gönderir:

```powershell
dotnet run --project LisansUretici -- uret --musteri "ABC Ltd." --vkn 1234567890 --makine 3XZ5-AK45-SXCF-DK98 --bitis 2027-12-31
```

   `--bitis` verilmezse lisans süresizdir. `dogrula --dosya ... --makine ...` ile kontrol edilebilir.
3. Müşteri dosyayı "Lisans Dosyası Yükle" ile seçer.

Lisans üretmek için kullanılan **özel anahtar** `%USERPROFILE%\MuhasebeLisansAnahtari\ozel-anahtar.pem`
dosyasıdır ve depoda yoktur. Güvenli bir yere yedeklenmelidir: kaybolursa yeni lisans
üretilemez (programdaki açık anahtar değiştirilip yeni sürüm dağıtmak gerekir), başkasının
eline geçerse herkes lisans üretebilir.

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

Bir yedeğe geri dönmek için **Ayarlar → Yedekleme** sekmesinde yedeği seçip
"Seçili Yedeği Geri Yükle" (ya da başka bir klasördeki/USB'deki dosya için "Dosyadan
Geri Yükle") düğmesini kullanın ve onay için `GERİ YÜKLE` yazın. Program önce güncel
verinin yedeğini alır, yedeği ayrı bir veritabanına açıp bu programa ait olduğunu
doğrular, sonra kullanılan veritabanıyla yer değiştirir ve yeniden başlar. Önceki
veritabanı silinmez, `Muhasebe_eski_<tarih>` adıyla saklanır.

Elle (komut satırından) geri yükleme gerekirse uygulamayı kapatıp aşağıdaki komutu
kendi dosya adınızla çalıştırın; bu yol mevcut verinin yerine yedeği doğrudan yazar.

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

## Otomatik güncelleme

Kurulum sürümü, ana pencere açılınca `UygulamaAyarlari:GuncellemeAdresi` adresindeki
`guncelleme.json` dosyasına bakar (adres boşsa özellik kapalıdır). Daha yeni sürüm varsa
kullanıcıya sorar, MSI'ı arka planda indirir ve program kapanırken (veritabanı durduktan
sonra) kurar; kurulumdan sonra program kendiliğinden açılır. Bildirim lisans özel
anahtarıyla imzalanır ve paketin SHA-256 özetini taşır; imzası ya da özeti tutmayan
güncelleme kurulmaz. Adres `https` olmalıdır.

Yeni sürüm yayınlamak için (sürüm numarası her seferinde artırılmalıdır):

```powershell
powershell -ExecutionPolicy Bypass -File kurulum\Paketle.ps1 -Surum 1.0.2 -YayinAdresi https://ornek.com/guncelleme -Notlar "Hata düzeltmeleri"
```

`kurulum\cikti` içindeki `MuhasebeYonetim-1.0.2.msi` ve `guncelleme.json` dosyaları
`https://ornek.com/guncelleme/` altına yüklenir. Programdaki adres
`https://ornek.com/guncelleme/guncelleme.json` olur (`kurulum\appsettings.Kurulum.json`).
Bildirim elle de üretilebilir: `dotnet run --project LisansUretici -- guncelleme --msi ... --surum ... --adres ...`.

## Örnek verilerle tanıtım

Giriş ekranındaki **Örnek verilerle tanıyın** programı `--ornek` ile yeniden açar: aynı PostgreSQL sunucusunda ayrı
`Muhasebe_Ornek` veritabanı oluşturulur ve ilk açılışta `Altyapi/Servisler/OrnekVeriServisi.cs` ile (programın kendi
servisleriyle) örnek firma, 11 cari, 12 ürün ve son 3 ayın alış/satış/tahsilat/ödemeleri yazılır. Gerçek veritabanına
dokunulmaz; servis yalnız `Muhasebe_Ornek` adlı ve boş veritabanına yazar. Ekranda kırmızı ÖRNEK VERİ şeridi görünür,
otomatik yedek alınmaz, giriş `admin / admin123`. Ağ istemcisinde kapalıdır.

## e-Fatura entegratörü

**Ayarlar → Firma & Parametreler → Gönderim yolu**:

- **XML dosyası** (varsayılan): UBL-TR XML klasöre yazılır, müşteri entegratör portalına yükler.
- **Uyumsoft**: web servis kullanıcı adı/şifresiyle doğrudan gönderim (`Altyapi/EBelge/UyumsoftEBelgeEntegratoru.cs`).
  Her gönderimde alıcının mükellefiyeti GİB listesinden sorgulanıp cari kart düzeltilir; durum sorgulama,
  bekleyen durumları toplu güncelleme ve gelen kutusundan fatura aktarma vardır. Şifre veritabanında şifreli durur.
  Test ortamı herkese açıktır (kullanıcı/şifre `Uyumsoft`); `UyumsoftEntegratorTests` ve
  `EBelgeTests.Uyumsoft_test_ortaminda_...` bu ortama gerçekten gönderir (internet yoksa atlanır).
- Uyumsoft bağlıyken ayrıca: resmî PDF, e-Arşiv iptali, e-Arşivde alıcıya e-posta bildirimi (cari e-postası),
  cari kartta vergi no'dan GİB kaydı (ünvan/adres/mükellefiyet) getirme ve e-İrsaliye gönderimi
  (`/Services/DespatchIntegration`) + durum sorgulama. Bağlı değilken e-İrsaliye XML dosyası olarak üretilir.
- Başka entegratör eklemek için `IEBelgeCevrimiciEntegrator` uygulanır ve `EBelgeServisi.AyardanEntegrator`'a eklenir.

## Ağ (çok kullanıcılı) kullanım

**Ayarlar → 🌐 Ağ** (Ayarları Yönet yetkisi gerekir):

- **Sunucu:** verilerin duracağı bilgisayarda "Bu Bilgisayarı Sunucu Yap". Windows yönetici
  onayı istenir. Veritabanı `MuhasebeYonetimVeritabani` adlı, otomatik başlayan Windows hizmeti
  olur (NetworkService hesabıyla), yalnız aynı yerel ağdan (`samenet`) şifreli bağlantı kabul
  eder; güvenlik duvarında 5544 portu yalnız özel/etki alanı ağları için açılır. Ekranda
  `MYS1-` ile başlayan bağlantı kodu gösterilir. Adımlar `C:\ProgramData\MuhasebeYonetim\Loglar\ag-kurulum.log`
  dosyasına yazılır.
- **İstemci:** diğer bilgisayarlarda "Sunucuya Bağlan" ile kod girilir. İstemci kendi
  veritabanını kullanmaz (silmez), günlük yedek almaz ve geri yükleme yapamaz.
- Bağlantı kodu veritabanına tam erişim verir; yalnız kendi bilgisayarlarınıza girin.
- Ağdaki bir bilgisayar daha yeni sürümle veritabanını güncellemişse eski sürümlü program
  açılmaz ve güncellenmesini ister. Tüm bilgisayarlar aynı sürümde tutulmalıdır.
- Sunucu kapalıyken istemciler çalışamaz. Programı tamamen kaldırmak hizmeti ve güvenlik
  duvarı kuralını siler, verileri silmez.

## KVKK

**Cari Sil** ekranında seçili cari için:

- **KVKK: Kişisel Veri Dökümü** — kişinin hakkında tutulan verilerin (kimlik, iletişim,
  belge özetleri, teslimat bilgileri, saklama süresi) metin dökümü (KVKK md. 11 bilgi talebi).
- **KVKK: Anonimleştir** — yalnız hiç işlemi olmayan ya da son kaydının yılından itibaren
  10 yıllık yasal saklama süresi (VUK md. 253, TTK md. 82) dolmuş caride çalışır. Ad, kimlik ve
  vergi no, iletişim, adres, IBAN ve fotoğraf kalıcı olarak silinir; sipariş/irsaliye teslimat
  bilgileri ve değişiklik günlüğündeki eski değerler de maskelenir. Tutarlar ve belgeler kalır.
  Süre dolmamışsa program gerekçeli ret metnini gösterir.

Aydınlatma metni, VERBİS ve müşteriyle yapılacak veri işleme sözleşmesi hukuki konulardır;
program bunları üretmez.

## Satış öncesi kontrol listesi

- [ ] Lisans özel anahtarı (`%USERPROFILE%\MuhasebeLisansAnahtari\ozel-anahtar.pem`) şifreli ve
      bilgisayar dışında en az bir yerde yedekli. Hem lisanslar hem güncellemeler bu anahtara bağlıdır.
- [ ] `kurulum\Lisans.rtf` hukuken gözden geçirildi.
- [ ] Ürün/üretici adı (`Paket.wxs`, `appsettings.Kurulum.json`) son haline getirildi.
- [ ] Güncelleme adresi belirlendi ve `GuncellemeAdresi` dolduruldu.
- [ ] Kod imzalama sertifikası alındı (yoksa müşteride Windows SmartScreen uyarısı çıkar).
      Paketlerken: `kurulum\Paketle.ps1 -Surum x -ImzaSertifikasi <parmak izi | dosya.pfx> [-ImzaSifresi ...]`
      (Masaustu.exe ve MSI imzalanır, DigiCert zaman damgası eklenir).
- [ ] MSI temiz bir bilgisayarda kurulup ilk açılış, lisans, yedek/geri yükleme, güncelleme ve
      iki bilgisayarlı ağ kurulumu denendi.
