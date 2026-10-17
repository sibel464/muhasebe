using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Kurumsal;

namespace Masaustu.Gorunumler.Giris;

// Birden fazla aktif firma varsa girişte hangi firmayla çalışılacağı seçilir; her firmanın kayıtları ayrıdır.
public sealed class FirmaSecimPenceresi : Window
{
    private readonly ComboBox _liste;
    public Sirket? SecilenFirma => _liste.SelectedItem as Sirket;

    public FirmaSecimPenceresi(IReadOnlyList<Sirket> firmalar, Guid onerilen)
    {
        Title = "Firma Seçimi";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = System.Windows.Media.Brushes.White;

        _liste = new ComboBox { ItemsSource = firmalar, DisplayMemberPath = nameof(Sirket.Unvan), Height = 32, Margin = new Thickness(0, 0, 0, 16) };
        _liste.SelectedItem = firmalar.FirstOrDefault(f => f.Id == onerilen) ?? firmalar.FirstOrDefault();

        var tamam = new Button { Content = "Devam", Width = 110, Height = 32, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        tamam.Click += (_, _) => { if (SecilenFirma != null) DialogResult = true; };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Hangi firmayla çalışmak istiyorsunuz?", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(new TextBlock
        {
            Text = "Her firmanın carileri, ürünleri, faturaları, kasa/banka ve muhasebe kayıtları ayrıdır. Firmayı değiştirmek için çıkış yapıp yeniden giriş yapın.",
            TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 0, 0, 14)
        });
        panel.Children.Add(_liste);
        panel.Children.Add(tamam);
        Content = panel;
    }
}
