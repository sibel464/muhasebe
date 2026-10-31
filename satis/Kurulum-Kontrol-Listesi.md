# Müşteride Kurulum Günü Kontrol Listesi

Bir müşteriye program kurarken sırayla işaretleyin. Ortalama süre: 1–2 saat (veri aktarımı hariç).

## Önce (satıcının bilgisayarında)
- [ ] Son sürüm MSI (`kurulum\cikti\MuhasebeYonetim-<sürüm>.msi`) USB'de. İmzalıysa sağ tık → Özellikler → Dijital İmzalar'da görünür.
- [ ] Müşterinin firma bilgileri alındı: ünvan, vergi dairesi/no, MERSİS, adres, il/ilçe, telefon, e-posta.
- [ ] e-Fatura mükellefi mi? Entegratörü kim? (Uyumsoft ise web servis kullanıcı adı/şifresi müşteriden alınacak.)
- [ ] Eski programdan aktarılacaklar: cari listesi ve ürün listesi Excel olarak istendi.

## Kurulum
- [ ] Bilgisayar: Windows 10/11 64 bit, en az 4 GB RAM, 2 GB boş disk.
- [ ] MSI çift tıklanarak kuruldu (SmartScreen çıkarsa "Ek bilgi → Yine de çalıştır" — imzasız paket).
- [ ] Program açıldı, ilk kurulum tamamlandı; `admin / admin123` ile girilip şifre değiştirildi. **Yeni şifre müşteriye teslim edildi, satıcıda kalmadı.**
- [ ] **Ayarlar → Lisans**: makine kodu alındı → lisans üretildi (`LisansUretici uret ...`) → dosya yüklendi.
- [ ] **Ayarlar → Firma & Parametreler**: künye eksiksiz dolduruldu.
- [ ] e-Fatura: Gönderim yolu seçildi; Uyumsoft ise **Bağlantıyı Dene** başarılı ve VKN uyarısı yok.
- [ ] **Ayarlar → Yedekleme**: ikinci yedek konumu (harici disk / OneDrive / ağ klasörü) seçildi, **Şimdi Yedek Al** denendi.
- [ ] E-posta (isteğe bağlı): SMTP girildi, deneme iletisi gitti.
- [ ] Kullanıcılar açıldı, yetkileri verildi (muhasebe, satış, depo...).
- [ ] Ana sayfadaki **Başlarken** listesinin tamamı yeşil.

## Veri aktarımı
- [ ] Cari ve ürün listeleri Excel'den aktarıldı (Cari / Ürün sayfası → Excel'den Aktar); açılış bakiyeleri kontrol edildi.
- [ ] Kasa ve banka açılış bakiyeleri girildi.

## Ağ (birden fazla bilgisayar)
- [ ] Sunucu bilgisayarda **Ayarlar → Ağ → Bu Bilgisayarı Sunucu Yap**; bağlantı kodu not edildi.
- [ ] Diğer bilgisayarlara aynı sürüm kuruldu, **Sunucuya Bağlan** ile kod girildi; her birinden giriş denendi.

## Teslim
- [ ] Deneme faturası kesildi → e-Belge gönderildi (ya da XML oluştu) → iptal/ters kayıt gösterildi.
- [ ] Müşteriye gösterildi: F1 kullanım kılavuzu, **Destek Dosyası** düğmesi, yedeğin nerede olduğu.
- [ ] Lisans sözleşmesi ve (uzaktan destek verilecekse) Veri İşleme Eki imzalandı.
- [ ] Müşteri kaydı tutuldu: ünvan, makine kodu(ları), lisans no, kurulum tarihi, sürüm, bakım bitişi.
