using System.Windows;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Ana;
using Microsoft.Extensions.DependencyInjection;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Giris;

public partial class GirisEkrani : Window
{
    private readonly GirisGorunumModeli _gorunumModeli;
    private bool _isSyncing = false;
    private bool _sifreGosteriliyor = false;
    private bool _anaPencereAcildi;

    public GirisEkrani(GirisGorunumModeli gorunumModeli)
    {
        InitializeComponent();
        OrnekPaneliniHazirla();
        _gorunumModeli = gorunumModeli;
        DataContext = _gorunumModeli;

        // Şifreyi açık/gizli duruma göre al
        _gorunumModeli.SifreAl = () => _sifreGosteriliyor ? AcikSifreKutusu.Text : SifreKutusu.Password;

        _gorunumModeli.SifreDegistirmeIste = () =>
        {
            var pencere = new Ortak.SifreDegistirPenceresi(App.HizmetSaglayici.GetRequiredService<IKullaniciServisi>(), zorunlu: true) { Owner = this };
            return pencere.ShowDialog() == true;
        };

        _gorunumModeli.GirisBasarili = async () =>
        {
            try
            {
            await LoginAsamasiniLoglaAsync("Ana pencere çözümleme başladı");
                await AktifFirmaContextiniBaslatAsync();
                var kapsam = App.HizmetSaglayici.CreateScope();
                var anaPencere = kapsam.ServiceProvider.GetRequiredService<AnaPencere>();
                anaPencere.Closed += (_, _) => kapsam.Dispose();
                
                Application.Current.MainWindow = anaPencere;
                anaPencere.Show();
                _anaPencereAcildi = true;
                await LoginAsamasiniLoglaAsync("Ana pencere gösterildi");
                this.Close();
            }
            catch (Exception ex)
            {
                var kilitAlindi = false;
                try
                {
                    var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] GirisBasarili Hatasi: {ex.Message}{Environment.NewLine}";
                    await LogKilidi.WaitAsync();
                    kilitAlindi = true;
                    await System.IO.File.AppendAllTextAsync(Altyapi.Veri.UygulamaKlasorleri.LogYolu("uygulama-hatalari.log"), log);
                }
                catch { }
                finally
                {
                    if (kilitAlindi)
                        LogKilidi.Release();
                }

                System.Windows.MessageBox.Show(
                    $"Ana pencere açılamadı:\n{ex.Message}\n\nDetay: {ex.InnerException?.Message}",
                    "Başlatma Hatası",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        };

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        
        // Başlangıçta CapsLock kontrolü
        CapsLockKontrolu();
    }

    private static async Task AktifFirmaContextiniBaslatAsync()
    {
        var context = App.HizmetSaglayici.GetRequiredService<IAktifFirmaBaglami>();
        var sirketServisi = App.HizmetSaglayici.GetRequiredService<ISirketServisi>();
        var kullanici = App.HizmetSaglayici.GetRequiredService<IKullaniciServisi>().AktifKullanici;
        // Firma sırası ada göre değil: tek aktif firma varsa o, birden fazlaysa kullanıcıya sorulur (öneri: varsayılan şirket).
        var firmalar = await sirketServisi.ListeleAsync(aktif: true);
        var sirket = firmalar.Count <= 1
            ? firmalar.FirstOrDefault()
            : FirmaSor(firmalar);
        if (sirket == null)
        {
            context.Degistir(context.SirketId, context.SubeId, context.MaliDonemId, kullanici?.Id, kullanici?.KullaniciAdi);
            return;
        }

        var sube = (await sirketServisi.SubeleriGetirAsync(sirket.Id)).FirstOrDefault();
        var donem = (await sirketServisi.DonemleriGetirAsync(sirket.Id)).FirstOrDefault(x => !x.KilitliMi);
        context.SirketAdi = sirket.Unvan;
        context.Degistir(sirket.Id, sube?.Id, donem?.Id, kullanici?.Id, kullanici?.KullaniciAdi);
    }

    private static EtkiAlani.Modeller.Kurumsal.Sirket FirmaSor(IReadOnlyList<EtkiAlani.Modeller.Kurumsal.Sirket> firmalar)
    {
        var pencere = new FirmaSecimPenceresi(firmalar, Altyapi.Veri.MuhasebeBaglami.VarsayilanSirketId);
        pencere.ShowDialog();
        return pencere.SecilenFirma
            ?? firmalar.FirstOrDefault(f => f.Id == Altyapi.Veri.MuhasebeBaglami.VarsayilanSirketId)
            ?? firmalar.OrderBy(f => f.OlusturmaTarihi).First();
    }

    private static readonly SemaphoreSlim LogKilidi = new(1, 1);

    private static async Task LoginAsamasiniLoglaAsync(string asama)
    {
        await LogKilidi.WaitAsync();
        try
        {
            var yol = Altyapi.Veri.UygulamaKlasorleri.LogYolu("login-akis.log");
            await System.IO.File.AppendAllTextAsync(yol, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {asama}{Environment.NewLine}");
        }
        catch
        {
        }
        finally
        {
            LogKilidi.Release();
        }
    }

    private void KapatDugmeTiklandi(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    // Uygulama OnExplicitShutdown modunda; giriş ekranı Alt+F4 veya görev çubuğundan kapatılınca da uygulama
    // kapanmalı. Aksi halde görünmez süreç ve veritabanı açık kalır, Windows kapanırken veritabanı düzgün kapanamaz.
    // Başarılı girişte ana pencere açık kaldığı için o durumda kapanmaz.
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_anaPencereAcildi)
            Application.Current.Shutdown();
    }

    private void SifreDegisti(object sender, RoutedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        AcikSifreKutusu.Text = SifreKutusu.Password;
        _isSyncing = false;
    }

    private void AcikSifreDegisti(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        SifreKutusu.Password = AcikSifreKutusu.Text;
        _isSyncing = false;
    }

    private void SifreGosterGizleTiklandi(object sender, RoutedEventArgs e)
    {
        _sifreGosteriliyor = !_sifreGosteriliyor;
        if (_sifreGosteriliyor)
        {
            SifreKutusu.Visibility = Visibility.Collapsed;
            AcikSifreKutusu.Visibility = Visibility.Visible;
            SifreGosterDugmesi.Content = "🙈";
            SifreGosterDugmesi.ToolTip = "Şifreyi Gizle";
            AcikSifreKutusu.Focus();
        }
        else
        {
            SifreKutusu.Visibility = Visibility.Visible;
            AcikSifreKutusu.Visibility = Visibility.Collapsed;
            SifreGosterDugmesi.Content = "👁️";
            SifreGosterDugmesi.ToolTip = "Şifreyi Göster";
            SifreKutusu.Focus();
        }
    }

    private void SifreTemizleTiklandi(object sender, RoutedEventArgs e)
    {
        _isSyncing = true;
        SifreKutusu.Password = string.Empty;
        AcikSifreKutusu.Text = string.Empty;
        _isSyncing = false;
        
        if (_sifreGosteriliyor)
            AcikSifreKutusu.Focus();
        else
            SifreKutusu.Focus();
    }

    private void KlavyeTusuBasildi(object sender, System.Windows.Input.KeyEventArgs e)
    {
        CapsLockKontrolu();
    }

    private void CapsLockKontrolu()
    {
        if (System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.CapsLock))
        {
            CapsLockUyarisi.Visibility = Visibility.Visible;
        }
        else
        {
            CapsLockUyarisi.Visibility = Visibility.Collapsed;
        }
    }

    private void NumpadTusaBasildi(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string islem)
        {
            string mevcutSifre = _sifreGosteriliyor ? AcikSifreKutusu.Text : SifreKutusu.Password;

            if (islem == "CLEAR")
            {
                mevcutSifre = string.Empty;
            }
            else if (islem == "BACK")
            {
                if (mevcutSifre.Length > 0)
                    mevcutSifre = mevcutSifre.Substring(0, mevcutSifre.Length - 1);
            }
            else
            {
                // Sayısal tuş (0-9)
                mevcutSifre += islem;
            }

            // Değişikliği aktif alana yansıt
            _isSyncing = true;
            SifreKutusu.Password = mevcutSifre;
            AcikSifreKutusu.Text = mevcutSifre;
            _isSyncing = false;

            if (_sifreGosteriliyor)
            {
                AcikSifreKutusu.Focus();
                AcikSifreKutusu.CaretIndex = AcikSifreKutusu.Text.Length;
            }
            else
            {
                SifreKutusu.Focus();
                // PasswordBox has no CaretIndex property directly accessible in standard WPF without extensions,
                // but setting focus typically brings cursor to end in modern UI flows.
            }
        }
    }

    private void OrnekPaneliniHazirla()
    {
        if (App.AgModu == Altyapi.Veri.AgModu.Istemci) return;
        var bag = new System.Windows.Controls.Button
        {
            Content = App.OrnekMod ? "↩ Gerçek verilerime dön" : "🎓 Örnek verilerle tanıyın (gerçek kayıtlarınıza dokunmaz)",
            Cursor = System.Windows.Input.Cursors.Hand, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)), HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(4)
        };
        bag.Click += (_, _) => App.OrnekModaGec(!App.OrnekMod);
        if (App.OrnekMod)
            OrnekPaneli.Children.Add(new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226)), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 6),
                Child = new System.Windows.Controls.TextBlock
                {
                    TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 27, 27)),
                    Text = "ÖRNEK VERİ (tanıtım) — Kullanıcı: admin, Şifre: admin123. Burada girilen kayıtlar gerçek defterlerinize yazılmaz."
                }
            });
        OrnekPaneli.Children.Add(bag);
    }
}
