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
