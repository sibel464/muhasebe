using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EtkiAlani.Modeller.Fatura;
using IrsaliyeVarligi = EtkiAlani.Modeller.Irsaliye.Irsaliye;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

// İrsaliye listesindeki "e-İrsaliye": taşıma bilgisini (plaka, şoför, şoför T.C. no) aldırır, eksikleri gösterir,
// UBL-TR e-İrsaliye XML'ini hazırlar (dosya modunda klasöre yazar) ve portala yüklendiğini işaretletir.
public static class EIrsaliyePenceresi
{
    // Kayıt değiştiyse true döner (liste yenilenir).
    public static async Task<bool> GosterAsync(IEBelgeServisi servis, IrsaliyeVarligi irsaliye)
    {
        var degisti = false;
        string? sonDosya = null;

        var bilgi = new TextBlock { FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
            Text = $"{irsaliye.IrsaliyeNo}  •  {irsaliye.CariKart?.Unvan}  •  Sevk: {irsaliye.SevkTarihiMetni}" };
        var plaka = new TextBox { Width = 160, Text = irsaliye.AracPlaka };
        var sofor = new TextBox { Width = 260, Text = irsaliye.SoforAdi };
        var tckn = new TextBox { Width = 160, Text = irsaliye.SoforTckn, MaxLength = 11 };
        var hataListesi = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        var durumMetni = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), Foreground = Brushes.DarkSlateBlue };
        var ipucu = new TextBlock
        {
            Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), FontSize = 12,
            Text = "Oluşturulan XML dosyasını özel entegratörünüzün portalına yükleyin, sonra \"Portala yüklendi\" ile işaretleyin. " +
                   "Firma bilgileri Ayarlar'dan, alıcının vergi no ve il/ilçe bilgisi cari karttan gelir."
        };

        var hazirla = Dugme(servis.DogrudanGonderir ? "📨 Hazırla ve Gönder" : "📄 e-İrsaliye XML'i Oluştur", true);
        var klasor = Dugme("Klasörü Aç");
        var gonderildi = Dugme("Portala yüklendi (Gönderildi)");
        var iptal = Dugme("Portalda iptal edildi");
        var kapat = Dugme("Kapat");

        var kok = new DockPanel { Margin = new Thickness(20) };
        var ust = new StackPanel
        {
            Children =
            {
                bilgi,
                Satir("Araç plakası:", plaka),
                Satir("Şoför adı soyadı:", sofor),
                Satir("Şoför T.C. kimlik no:", tckn),
                hataListesi, durumMetni, ipucu
            }
        };
        DockPanel.SetDock(ust, Dock.Top);
        kok.Children.Add(ust);
        var dugmeler = new WrapPanel { Margin = new Thickness(0, 16, 0, 0), Children = { hazirla, klasor, gonderildi, iptal, kapat } };
        DockPanel.SetDock(dugmeler, Dock.Bottom);
        kok.Children.Add(dugmeler);
        kok.Children.Add(new Border());

        var pencere = new Window
        {
            Title = $"e-İrsaliye - {irsaliye.IrsaliyeNo}", Width = 720, Height = 480, MinWidth = 600, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = Brushes.White,
            Content = new ScrollViewer { Content = kok, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };

        void DurumuGoster()
        {
            var gonderilmis = irsaliye.EBelgeDurumu is EBelgeDurumu.Gonderildi or EBelgeDurumu.KabulEdildi or EBelgeDurumu.IptalEdildi;
            durumMetni.Text = irsaliye.EBelgeDurumu == EBelgeDurumu.Yok ? "Henüz e-İrsaliye hazırlanmadı."
                : $"Durum: {irsaliye.EBelgeDurumu}  •  e-İrsaliye No: {irsaliye.EBelgeNo}\n{irsaliye.EBelgeMesaji}";
            hazirla.IsEnabled = !gonderilmis;
            plaka.IsEnabled = sofor.IsEnabled = tckn.IsEnabled = !gonderilmis;
            gonderildi.IsEnabled = irsaliye.EBelgeDurumu is EBelgeDurumu.Hazirlandi or EBelgeDurumu.Hata;
            iptal.IsEnabled = irsaliye.EBelgeDurumu is EBelgeDurumu.Gonderildi or EBelgeDurumu.KabulEdildi;
            klasor.IsEnabled = sonDosya != null;
        }

        async Task EksikleriGosterAsync()
        {
            var eksikler = await servis.IrsaliyeOnKontrolAsync(irsaliye.Id);
            hataListesi.Text = eksikler.Count == 0 ? string.Empty : "Hazırlamadan önce düzeltilmesi gerekenler:\n• " + string.Join("\n• ", eksikler);
        }

        hazirla.Click += async (_, _) =>
        {
            try
            {
                hazirla.IsEnabled = false;
                await servis.IrsaliyeTasimaBilgisiKaydetAsync(irsaliye.Id, plaka.Text, sofor.Text, tckn.Text);
                (irsaliye.AracPlaka, irsaliye.SoforAdi, irsaliye.SoforTckn) = (plaka.Text, sofor.Text, tckn.Text);
                degisti = true;
                await EksikleriGosterAsync();
                if (hataListesi.Text.Length > 0) { DurumuGoster(); return; }
                var sonuc = await servis.IrsaliyeHazirlaAsync(irsaliye.Id);
                sonDosya = sonuc.DosyaYolu;
                irsaliye.EBelgeDurumu = sonuc.Durum;
                irsaliye.EBelgeMesaji = sonuc.Mesaj;
            }
            catch (Exception ex)
            {
                hataListesi.Text = ex.Message;
            }
            DurumuGoster();
        };
        klasor.Click += (_, _) =>
        {
            if (sonDosya != null && File.Exists(sonDosya))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{sonDosya}\"") { UseShellExecute = true });
        };
        async Task IsaretleAsync(EBelgeDurumu durum)
        {
            try
            {
                await servis.IrsaliyeDurumIsaretleAsync(irsaliye.Id, durum);
                irsaliye.EBelgeDurumu = durum;
                irsaliye.EBelgeMesaji = $"'{durum}' olarak işaretlendi.";
                degisti = true;
            }
            catch (Exception ex) { hataListesi.Text = ex.Message; }
            DurumuGoster();
        }
        gonderildi.Click += async (_, _) => await IsaretleAsync(EBelgeDurumu.Gonderildi);
        iptal.Click += async (_, _) =>
        {
            if (MessageBox.Show("e-İrsaliye portalda iptal edildi olarak işaretlensin mi?", "e-İrsaliye", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await IsaretleAsync(EBelgeDurumu.IptalEdildi);
        };
        kapat.Click += (_, _) => pencere.Close();

        DurumuGoster();
        try { await EksikleriGosterAsync(); } catch (Exception ex) { hataListesi.Text = ex.Message; }
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
        Children = { new TextBlock { Text = etiket, Width = 180, VerticalAlignment = VerticalAlignment.Center }, icerik }
    };
}
