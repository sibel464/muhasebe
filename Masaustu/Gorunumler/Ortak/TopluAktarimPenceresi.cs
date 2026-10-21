using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Ortak;

// Başka programdan geçiş: cari/ürün listesini Excel ya da CSV'den toplu aktarma.
// Adımlar: şablonu kaydet → Excel'de doldur → dosyayı seç (önizleme, hatalı satırlar işaretlenir) → aktar.
public static class TopluAktarimPenceresi
{
    // Aktarılan kayıt sayısını döner (liste yenilensin diye).
    public static async Task<int> GosterAsync(AktarimTuru tur)
    {
        var servis = App.HizmetSaglayici.GetRequiredService<ITopluAktarimServisi>();
        var cari = tur == AktarimTuru.Cari;
        string? dosya = null;
        var aktarilan = 0;

        var aciklama = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
            Text = (cari
                ? "Müşteri ve tedarikçi listenizi Excel'den aktarın. Açılış bakiyesi sütunundaki tutar: artı = cari size borçlu, eksi = siz cariye borçlusunuz; açılış tarihiyle devir fişi olarak işlenir (karşı hesap 500 Sermaye)."
                : "Ürün listenizi Excel'den aktarın. Açılış stoğu varsayılan mağazaya, alış fiyatıyla girilir. KDV boşsa Ayarlar'daki varsayılan KDV kullanılır; kod boşsa otomatik verilir.")
                + "\n\n1) Şablonu kaydedip Excel'de doldurun (sütun sırası önemli değil, başlıklar tanınır)  2) Dosyayı seçin  3) Önizlemeyi kontrol edip aktarın."
        };
        var sablon = Dugme("1) Şablonu Kaydet");
        var sec = Dugme("2) Dosya Seç (.xlsx / .csv)", true);
        var tarihEtiket = new TextBlock { Text = "Açılış tarihi:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 8), Visibility = cari ? Visibility.Visible : Visibility.Collapsed };
        var tarih = new DatePicker { SelectedDate = new DateTime(DateTime.Today.Year, 1, 1), Width = 130, Margin = new Thickness(0, 0, 8, 8), Visibility = cari ? Visibility.Visible : Visibility.Collapsed };
        var aktar = Dugme("3) Aktar", true);
        aktar.IsEnabled = false;
        var ozet = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8), TextWrapping = TextWrapping.Wrap };

        var tablo = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = false, Background = Brushes.White, RowHeight = 26 };
        void Kolonlar(params (string Baslik, string Yol, double Genislik)[] kolonlar)
        {
            tablo.Columns.Clear();
            foreach (var (baslik, yol, genislik) in kolonlar)
                tablo.Columns.Add(new DataGridTextColumn
                {
                    Header = baslik, Binding = new Binding(yol),
                    Width = genislik > 0 ? new DataGridLength(genislik) : new DataGridLength(1, DataGridLengthUnitType.Star)
                });
        }
        Kolonlar(("Satır", nameof(AktarimSatiri.SatirNo), 50), ("Kod", nameof(AktarimSatiri.Kod), 110), (cari ? "Ünvan" : "Ürün Adı", nameof(AktarimSatiri.Ad), 220),
            ("Bilgi", nameof(AktarimSatiri.Ozet), 260), ("Durum", nameof(AktarimSatiri.Durum), 0));

        var ust = new StackPanel
        {
            Children =
            {
                aciklama,
                new WrapPanel { Children = { sablon, sec, tarihEtiket, tarih, aktar } },
                ozet
            }
        };
        var kok = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(ust, Dock.Top);
        kok.Children.Add(ust);
        kok.Children.Add(tablo);

        var pencere = new Window
        {
            Title = cari ? "Excel'den Cari Aktarımı" : "Excel'den Ürün Aktarımı", Width = 1050, Height = 640, MinWidth = 760, MinHeight = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = Brushes.White, Content = kok,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };

        sablon.Click += (_, _) =>
        {
            var kayit = new Microsoft.Win32.SaveFileDialog
            {
                FileName = cari ? "Cari_Aktarim_Sablonu.csv" : "Urun_Aktarim_Sablonu.csv", Filter = "CSV (Excel'de açılır) (*.csv)|*.csv"
            };
            if (kayit.ShowDialog(pencere) != true) return;
            try
            {
                File.WriteAllText(kayit.FileName, servis.SablonCsv(tur), new System.Text.UTF8Encoding(false));
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(kayit.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(pencere, "Şablon kaydedilemedi: " + ex.Message, pencere.Title); }
        };

        sec.Click += (_, _) =>
        {
            var ac = new Microsoft.Win32.OpenFileDialog { Filter = "Excel veya CSV (*.xlsx;*.csv)|*.xlsx;*.csv|Tüm dosyalar (*.*)|*.*" };
            if (ac.ShowDialog(pencere) != true) return;
            try
            {
                var satirlar = servis.Onizle(tur, ac.FileName);
                dosya = ac.FileName;
                Kolonlar(("Satır", nameof(AktarimSatiri.SatirNo), 50), ("Kod", nameof(AktarimSatiri.Kod), 110), (cari ? "Ünvan" : "Ürün Adı", nameof(AktarimSatiri.Ad), 220),
                    ("Bilgi", nameof(AktarimSatiri.Ozet), 260), ("Durum", nameof(AktarimSatiri.Durum), 0));
                tablo.ItemsSource = satirlar;
                var gecerli = satirlar.Count(s => s.Gecerli);
                ozet.Text = $"{Path.GetFileName(dosya)}: {satirlar.Count} satır, {gecerli} tanesi aktarılabilir"
                            + (gecerli < satirlar.Count ? $", {satirlar.Count - gecerli} tanesi hatalı (✖) ve atlanacak. Hataları Excel'de düzeltip dosyayı yeniden seçebilirsiniz." : ".");
                aktar.IsEnabled = gecerli > 0;
            }
            catch (Exception ex)
            {
                dosya = null;
                aktar.IsEnabled = false;
                tablo.ItemsSource = null;
                ozet.Text = "Dosya okunamadı: " + ex.Message;
            }
        };

        aktar.Click += async (_, _) =>
        {
            if (dosya == null) return;
            var gecerli = (tablo.ItemsSource as IEnumerable<AktarimSatiri>)?.Count(s => s.Gecerli) ?? 0;
            if (MessageBox.Show(pencere, $"{gecerli} kayıt aktarılacak. Devam edilsin mi?", pencere.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            aktar.IsEnabled = sec.IsEnabled = false;
            pencere.Cursor = System.Windows.Input.Cursors.Wait;
            ozet.Text = "Aktarılıyor...";
            try
            {
                var sonuc = await servis.AktarAsync(tur, dosya, tarih.SelectedDate ?? DateTime.Today);
                aktarilan += sonuc.Count(s => s.Basarili);
                Kolonlar(("Satır", nameof(AktarimSonucu.SatirNo), 50), (cari ? "Ünvan" : "Ürün Adı", nameof(AktarimSonucu.Ad), 260), ("Sonuç", nameof(AktarimSonucu.Basarili), 70),
                    ("Açıklama", nameof(AktarimSonucu.Mesaj), 0));
                tablo.ItemsSource = sonuc;
                ozet.Text = $"{sonuc.Count(s => s.Basarili)} kayıt aktarıldı" + (sonuc.Any(s => !s.Basarili) ? $", {sonuc.Count(s => !s.Basarili)} satır aktarılamadı (nedenleri aşağıda)." : ".");
                dosya = null;
            }
            catch (Exception ex)
            {
                ozet.Text = "Aktarım yapılamadı: " + ex.Message;
            }
            finally
            {
                sec.IsEnabled = true;
                pencere.Cursor = null;
            }
        };

        await Task.Yield();
        pencere.ShowDialog();
        return aktarilan;
    }

    private static Button Dugme(string metin, bool birincil = false) => new()
    {
        Content = metin, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 8), MinHeight = 32,
        Background = birincil ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
        Foreground = birincil ? Brushes.White : Brushes.Black, BorderThickness = new Thickness(0)
    };
}
