using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EtkiAlani.Modeller.Fatura;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

// Fatura listesindeki "e-Belge": belgenin e-Fatura mı e-Arşiv mi olacağını, eksik bilgileri ve durumu gösterir;
// UBL-TR XML'i hazırlar (dosya modunda klasöre yazar) ve portaldaki sonucu (gönderildi/kabul/red/iptal) işaretletir.
public static class EBelgePenceresi
{
    private static readonly FaturaMetinDonusturucu Metin = new();
    private static string M(object deger) => (string)Metin.Convert(deger, typeof(string), null!, System.Globalization.CultureInfo.CurrentCulture);

    // Durum değiştiyse true döner (liste yenilenir).
    public static async Task<bool> GosterAsync(IEBelgeServisi servis, Fatura fatura)
    {
        var degisti = false;
        string? sonDosya = null;

        var bilgi = new TextBlock { FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        var hataBaslik = new TextBlock { FontWeight = FontWeights.Bold, Foreground = Brushes.Firebrick, Margin = new Thickness(0, 6, 0, 4), Text = "Hazırlamadan önce düzeltilmesi gerekenler:" };
        var hataListesi = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
        var ipucu = new TextBlock
        {
            Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontSize = 12,
            Text = "Firma bilgileri: Ayarlar > Firma & Parametreler (vergi no, vergi dairesi, il, ilçe). Alıcı bilgileri: cari kartı (vergi/T.C. no, il, ilçe, e-Fatura mükellefi)."
        };
        var hataPaneli = new StackPanel { Children = { hataBaslik, hataListesi, ipucu } };

        var senaryo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, ItemsSource = new[] { "Temel fatura", "Ticari fatura (alıcı kabul/red verebilir)" } };
        var senaryoPaneli = Satir("Senaryo:", senaryo);
        var iadeNo = new TextBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        var iadeTarih = new DatePicker { Width = 140, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 0, 0) };
        var iadePaneli = Satir("İade edilen fatura (no / tarih):", new StackPanel { Orientation = Orientation.Horizontal, Children = { iadeNo, iadeTarih } });

        var durumMetni = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), Foreground = Brushes.DarkSlateBlue };

        var hazirla = Dugme(servis.DogrudanGonderir ? "📨 Hazırla ve Gönder" : "📄 XML Dosyası Oluştur", true);
        var goster = Dugme("XML'i Görüntüle");
        var klasor = Dugme("Klasörü Aç");
        var gonderildi = Dugme("Portala yüklendi (Gönderildi)");
        var kabul = Dugme("Kabul edildi");
        var red = Dugme("Reddedildi");
        var iptal = Dugme("Portalda iptal edildi");
        var kapat = Dugme("Kapat");

        var kok = new DockPanel { Margin = new Thickness(20) };
        var ust = new StackPanel { Children = { bilgi, senaryoPaneli, iadePaneli, hataPaneli, durumMetni } };
        DockPanel.SetDock(ust, Dock.Top);
        kok.Children.Add(ust);
        var dugmeler = new WrapPanel { Margin = new Thickness(0, 16, 0, 0), Children = { hazirla, goster, klasor, gonderildi, kabul, red, iptal, kapat } };
        DockPanel.SetDock(dugmeler, Dock.Bottom);
        kok.Children.Add(dugmeler);
        kok.Children.Add(new Border());

        var pencere = new Window
        {
            Title = $"e-Belge - {fatura.FaturaNo}", Width = 820, Height = 520, MinWidth = 640, MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = Brushes.White,
            Content = new ScrollViewer { Content = kok, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };

        EBelgeOnKontrol? kontrol = null;
        EBelgeHazirlamaIstegi Istek() => new(fatura.Id,
            senaryo.SelectedIndex == 1 ? EFaturaSenaryosu.Ticari : EFaturaSenaryosu.Temel,
            string.IsNullOrWhiteSpace(iadeNo.Text) ? null : iadeNo.Text, iadeTarih.SelectedDate);

        async Task YenileAsync(bool ilk = false)
        {
            try
            {
                kontrol = await servis.OnKontrolAsync(ilk ? new EBelgeHazirlamaIstegi(fatura.Id) : Istek());
                if (ilk)
                {
                    senaryo.SelectedIndex = kontrol.Senaryo == EFaturaSenaryosu.Ticari ? 1 : 0;
                    iadeNo.Text = kontrol.IadeEdilenFaturaNo ?? string.Empty;
                    iadeTarih.SelectedDate = kontrol.IadeEdilenFaturaTarihi;
                }
                var turAciklama = kontrol.Tur == EBelgeTuru.EFatura
                    ? "e-Fatura (alıcı GİB e-Fatura sistemine kayıtlı)"
                    : "e-Arşiv fatura (alıcı e-Fatura mükellefi değil" + (kontrol.AliciVknTckn == "11111111111" ? "; kimlik no yok, nihai tüketici olarak kesilir)" : ")");
                bilgi.Text = $"{fatura.FaturaNo}  •  {M(fatura.Tur)}  •  {fatura.GenelToplam:N2} ₺\n" +
                             $"Alıcı: {kontrol.AliciUnvan} ({kontrol.AliciVknTckn})\n" +
                             $"Belge türü: {turAciklama}\n" +
                             $"Durum: {M(kontrol.MevcutDurum)}   •   Gönderim yolu: {servis.EntegratorAdi}";
                senaryoPaneli.Visibility = kontrol.Tur == EBelgeTuru.EFatura ? Visibility.Visible : Visibility.Collapsed;
                iadePaneli.Visibility = kontrol.IadeBilgisiGerekli ? Visibility.Visible : Visibility.Collapsed;
                hataListesi.Text = string.Join("\n", kontrol.Hatalar.Select(h => "• " + h));
                hataPaneli.Visibility = kontrol.Hatalar.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                if (!string.IsNullOrWhiteSpace(kontrol.SonMesaj) && string.IsNullOrEmpty(durumMetni.Text))
                    durumMetni.Text = kontrol.SonMesaj;

                var d = kontrol.MevcutDurum;
                var hazirlanmis = d != EBelgeDurumu.Yok;
                hazirla.IsEnabled = kontrol.Hazirlanabilir;
                goster.IsEnabled = hazirlanmis;
                klasor.IsEnabled = !servis.DogrudanGonderir;
                gonderildi.IsEnabled = !servis.DogrudanGonderir && d is EBelgeDurumu.Hazirlandi or EBelgeDurumu.Hata;
                kabul.IsEnabled = d is EBelgeDurumu.Gonderildi;
                red.IsEnabled = d is EBelgeDurumu.Gonderildi && kontrol.Tur == EBelgeTuru.EFatura && kontrol.Senaryo == EFaturaSenaryosu.Ticari;
                iptal.IsEnabled = d is EBelgeDurumu.Gonderildi or EBelgeDurumu.KabulEdildi;
            }
            catch (Exception ex)
            {
                hataListesi.Text = "• " + ex.Message;
                hataPaneli.Visibility = Visibility.Visible;
                hazirla.IsEnabled = false;
            }
        }

        async Task IslemAsync(Func<Task> islem)
        {
            pencere.Cursor = System.Windows.Input.Cursors.Wait;
            try
            {
                await islem();
                degisti = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(pencere, ex.Message, "e-Belge", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                pencere.Cursor = null;
                await YenileAsync();
            }
        }

        async Task IsaretleAsync(EBelgeDurumu durum, string soru)
        {
            if (MessageBox.Show(pencere, soru, "e-Belge durumu", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            await IslemAsync(async () =>
            {
                await servis.DurumIsaretleAsync(fatura.Id, durum);
                durumMetni.Text = $"Durum '{M(durum)}' olarak işaretlendi.";
            });
        }

        senaryo.SelectionChanged += async (_, _) => { if (kontrol != null) await YenileAsync(); };
        iadeNo.LostFocus += async (_, _) => await YenileAsync();
        iadeTarih.SelectedDateChanged += async (_, _) => { if (kontrol != null) await YenileAsync(); };

        hazirla.Click += async (_, _) => await IslemAsync(async () =>
        {
            var sonuc = await servis.HazirlaVeGonderAsync(Istek());
            sonDosya = sonuc.DosyaYolu ?? sonDosya;
            durumMetni.Text = (sonuc.Basarili ? "✔ " : "✖ ") + sonuc.Mesaj + (sonuc.DosyaYolu != null ? $"\nDosya: {sonuc.DosyaYolu}" : string.Empty);
        });
        goster.Click += async (_, _) => await IslemAsync(async () =>
        {
            var xml = await servis.SonXmlGetirAsync(fatura.Id) ?? throw new InvalidOperationException("Kayıtlı XML bulunamadı.");
            var yol = Path.Combine(Path.GetTempPath(), $"{fatura.FaturaNo}.xml");
            await File.WriteAllTextAsync(yol, xml);
            Process.Start(new ProcessStartInfo(yol) { UseShellExecute = true });
        });
        klasor.Click += (_, _) =>
        {
            var hedef = sonDosya != null ? Path.GetDirectoryName(sonDosya) : Altyapi.EBelge.DosyaEBelgeEntegratoru.VarsayilanKlasor;
            if (hedef != null && Directory.Exists(hedef))
                Process.Start(new ProcessStartInfo("explorer.exe", sonDosya != null ? $"/select,\"{sonDosya}\"" : $"\"{hedef}\"") { UseShellExecute = true });
            else
                MessageBox.Show(pencere, "Henüz dosya oluşturulmadı.", "e-Belge");
        };
        gonderildi.Click += async (_, _) => await IsaretleAsync(EBelgeDurumu.Gonderildi,
            "XML dosyasını entegratör portalına yükleyip gönderdiniz mi?\n\nGönderildi olarak işaretlenen fatura programdan doğrudan iptal edilemez.");
        kabul.Click += async (_, _) => await IsaretleAsync(EBelgeDurumu.KabulEdildi, "Belge alıcı tarafından kabul edildi / GİB'e raporlandı olarak işaretlensin mi?");
        red.Click += async (_, _) => await IsaretleAsync(EBelgeDurumu.Reddedildi,
            "Alıcı faturayı reddetti mi?\n\nReddedilen faturayı programda da iptal etmeniz gerekir (Fatura listesinde İptal).");
        iptal.Click += async (_, _) => await IsaretleAsync(EBelgeDurumu.IptalEdildi,
            "Belge entegratör/GİB portalında iptal edildi mi?\n\nSonrasında faturayı programda da iptal etmeniz gerekir (Fatura listesinde İptal).");
        kapat.Click += (_, _) => pencere.Close();

        await YenileAsync(ilk: true);
        pencere.ShowDialog();
        return degisti;
    }

    // Tarih aralığındaki bekleyen (hazırlanmamış/hatalı) e-belgeleri toplu hazırlar ve sonucu satır satır gösterir.
    public static async Task<bool> TopluGosterAsync(IEBelgeServisi servis)
    {
        var bugun = DateTime.Today;
        var bas = new DatePicker { SelectedDate = new DateTime(bugun.Year, bugun.Month, 1), Width = 140 };
        var bit = new DatePicker { SelectedDate = bugun, Width = 140, Margin = new Thickness(8, 0, 0, 0) };
        var calistir = Dugme(servis.DogrudanGonderir ? "Hazırla ve Gönder" : "XML Dosyalarını Oluştur", true);
        calistir.Margin = new Thickness(12, 0, 0, 0);
        var ozet = new TextBlock { Margin = new Thickness(0, 12, 0, 8), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
        var tablo = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = false, Background = Brushes.White };
        tablo.Columns.Add(new DataGridTextColumn { Header = "Fatura No", Binding = new System.Windows.Data.Binding(nameof(EBelgeTopluSonuc.FaturaNo)), Width = 150 });
        tablo.Columns.Add(new DataGridTextColumn { Header = "Cari", Binding = new System.Windows.Data.Binding(nameof(EBelgeTopluSonuc.CariUnvan)), Width = 200 });
        tablo.Columns.Add(new DataGridTextColumn { Header = "Sonuç", Binding = new System.Windows.Data.Binding(nameof(EBelgeTopluSonuc.Basarili)) { Converter = new BasariliMetni() }, Width = 70 });
        tablo.Columns.Add(new DataGridTextColumn
        {
            Header = "Açıklama", Binding = new System.Windows.Data.Binding(nameof(EBelgeTopluSonuc.Mesaj)), Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) } }
        });

        var ust = new StackPanel
        {
            Children =
            {
                new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), Text = "Seçilen tarihler arasındaki onaylı satış faturaları ve alış iadelerinden e-belgesi henüz hazırlanmamış ya da hata almış olanlar hazırlanır. Eksik bilgisi olan fatura atlanır ve nedeni listede yazar." },
                new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "Tarih aralığı:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, bas, bit, calistir } },
                ozet
            }
        };
        var kok = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(ust, Dock.Top);
        kok.Children.Add(ust);
        kok.Children.Add(tablo);

        var pencere = new Window
        {
            Title = "Toplu e-Belge Hazırlama", Width = 900, Height = 560, MinWidth = 640, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = Brushes.White, Content = kok,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };
        var degisti = false;
        calistir.Click += async (_, _) =>
        {
            if (bas.SelectedDate is not DateTime b || bit.SelectedDate is not DateTime s || b > s)
            {
                MessageBox.Show(pencere, "Geçerli bir tarih aralığı seçin.", "Toplu e-Belge");
                return;
            }
            calistir.IsEnabled = false;
            pencere.Cursor = System.Windows.Input.Cursors.Wait;
            ozet.Text = "Hazırlanıyor...";
            try
            {
                var sonuclar = await servis.TopluHazirlaAsync(b, s);
                tablo.ItemsSource = sonuclar;
                var basarili = sonuclar.Count(x => x.Basarili);
                degisti |= basarili > 0;
                ozet.Text = sonuclar.Count == 0
                    ? "Bu aralıkta hazırlanacak fatura yok."
                    : $"{sonuclar.Count} faturadan {basarili} tanesi hazırlandı" + (basarili < sonuclar.Count ? $", {sonuclar.Count - basarili} tanesi atlandı (nedenleri aşağıda)." : ".");
            }
            catch (Exception ex)
            {
                ozet.Text = "Hazırlanamadı: " + ex.Message;
            }
            finally
            {
                calistir.IsEnabled = true;
                pencere.Cursor = null;
            }
        };
        pencere.ShowDialog();
        return degisti;
    }

    // Tedarikçiden gelen e-Fatura XML dosyalarını seçtirir; her biri için önizleme gösterip onaylananı taslak fatura yapar.
    // Aktarılan fatura sayısını döner.
    public static async Task<int> GelenAktarAsync(IEBelgeServisi servis)
    {
        var secim = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Gelen e-Fatura XML dosyalarını seçin", Filter = "e-Fatura XML (*.xml)|*.xml|Tüm dosyalar (*.*)|*.*", Multiselect = true
        };
        if (secim.ShowDialog() != true)
            return 0;

        var aktarilan = 0;
        foreach (var dosya in secim.FileNames)
        {
            string xml;
            GelenFaturaOnizleme onizleme;
            try
            {
                xml = await File.ReadAllTextAsync(dosya);
                onizleme = await servis.GelenFaturaOnizleAsync(xml);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Path.GetFileName(dosya)} okunamadı:\n\n{ex.Message}", "Gelen e-Fatura", MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }

            var sonuc = GelenOnizlemeGoster(Path.GetFileName(dosya), onizleme);
            if (sonuc == null) break;      // vazgeç: kalan dosyalar da bırakılır
            if (sonuc == false) continue;  // bu dosyayı atla
            try
            {
                var fatura = await servis.GelenFaturaAktarAsync(xml);
                aktarilan++;
                MessageBox.Show($"{fatura.FaturaNo} taslak {M(fatura.Tur)} faturası olarak kaydedildi.\n\nFatura listesinden kontrol edip onaylayın; onaylanınca stok ve cari hesap işlenir.",
                    "Gelen e-Fatura", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Path.GetFileName(dosya)} aktarılamadı:\n\n{ex.Message}", "Gelen e-Fatura", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        return aktarilan;
    }

    // true: aktar, false: bu dosyayı atla, null: vazgeç.
    private static bool? GelenOnizlemeGoster(string dosyaAdi, GelenFaturaOnizleme o)
    {
        var bilgi = new TextBlock
        {
            FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
            Text = $"{o.FaturaNo}  •  {o.Tarih:dd.MM.yyyy}  •  {o.TurAciklamasi}\n" +
                   $"Gönderen: {o.SaticiUnvan} ({o.SaticiVknTckn})\n" +
                   $"Cari kart: {o.EslesenCari ?? "yok, yeni cari açılacak"}\n" +
                   $"Ödenecek tutar: {o.OdenecekTutar:N2} ₺   •   ETTN: {o.Ettn.ToString().ToUpperInvariant()}"
        };
        var hatalar = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Text = string.Join("\n", o.Hatalar.Select(h => "✖ " + h)), Visibility = o.Hatalar.Count > 0 ? Visibility.Visible : Visibility.Collapsed };
        var uyarilar = new TextBlock { Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8), Text = string.Join("\n", o.Uyarilar.Select(h => "• " + h)), Visibility = o.Uyarilar.Count > 0 ? Visibility.Visible : Visibility.Collapsed };

        var tablo = new DataGrid { ItemsSource = o.Satirlar, AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = false, Background = Brushes.White };
        void Kolon(string ad, string yol, string? bicim = null, double genislik = 90) =>
            tablo.Columns.Add(new DataGridTextColumn { Header = ad, Binding = new System.Windows.Data.Binding(yol) { StringFormat = bicim }, Width = genislik });
        Kolon("#", nameof(GelenFaturaSatirOnizleme.SiraNo), genislik: 35);
        tablo.Columns.Add(new DataGridTextColumn { Header = "Ürün (faturada)", Binding = new System.Windows.Data.Binding(nameof(GelenFaturaSatirOnizleme.UrunAdi)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Kolon("Miktar", nameof(GelenFaturaSatirOnizleme.Miktar), "N2", 70);
        Kolon("Birim", nameof(GelenFaturaSatirOnizleme.Birim), genislik: 60);
        Kolon("Birim Fiyat", nameof(GelenFaturaSatirOnizleme.BirimFiyat), "N2");
        Kolon("İsk. %", nameof(GelenFaturaSatirOnizleme.IskontoOrani), "N2", 60);
        Kolon("KDV %", nameof(GelenFaturaSatirOnizleme.KdvOrani), "N0", 55);
        Kolon("Tutar", nameof(GelenFaturaSatirOnizleme.Tutar), "N2");
        tablo.Columns.Add(new DataGridTextColumn { Header = "Programdaki ürün", Binding = new System.Windows.Data.Binding(nameof(GelenFaturaSatirOnizleme.EslesenUrun)), Width = 220 });

        var aktar = Dugme("Taslak Fatura Olarak Aktar", true);
        aktar.IsEnabled = o.Aktarilabilir;
        var atla = Dugme("Bu Dosyayı Atla");
        var vazgec = Dugme("Vazgeç");
        var ust = new StackPanel { Children = { bilgi, hatalar, uyarilar } };
        var alt = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), Children = { aktar, atla, vazgec } };
        var kok = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(ust, Dock.Top);
        DockPanel.SetDock(alt, Dock.Bottom);
        kok.Children.Add(ust);
        kok.Children.Add(alt);
        kok.Children.Add(tablo);

        bool? sonuc = null;
        var pencere = new Window
        {
            Title = $"Gelen e-Fatura - {dosyaAdi}", Width = 1000, Height = 600, MinWidth = 700, MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = Brushes.White, Content = kok,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };
        aktar.Click += (_, _) => { sonuc = true; pencere.Close(); };
        atla.Click += (_, _) => { sonuc = false; pencere.Close(); };
        vazgec.Click += (_, _) => { sonuc = null; pencere.Close(); };
        pencere.ShowDialog();
        return sonuc;
    }

    private sealed class BasariliMetni : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value is true ? "✔ Tamam" : "✖ Atlandı";
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
    }

    private static Button Dugme(string metin, bool birincil = false) => new()
    {
        Content = metin, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 8), MinHeight = 32,
        Background = birincil ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
        Foreground = birincil ? Brushes.White : Brushes.Black, BorderThickness = new Thickness(0)
    };

    private static StackPanel Satir(string etiket, UIElement icerik) => new()
    {
        Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4),
        Children = { new TextBlock { Text = etiket, Width = 200, VerticalAlignment = VerticalAlignment.Center }, icerik }
    };
}
