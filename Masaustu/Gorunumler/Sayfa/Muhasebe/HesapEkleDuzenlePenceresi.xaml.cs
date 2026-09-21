using System.Windows;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class HesapEkleDuzenlePenceresi : Window
{
    public bool Kaydedildi { get; private set; }

    public HesapEkleDuzenlePenceresi(HesapEkleDuzenleGorunumModeli gm)
    {
        InitializeComponent();
        DataContext = gm;

        gm.PencereKapat += () =>
        {
            Dispatcher.Invoke(() =>
            {
                Kaydedildi = true;
                try
                {
                    DialogResult = true;
                }
                catch
                {
                    Close();
                }
            });
        };

        gm.IptalEt += () =>
        {
            Dispatcher.Invoke(() =>
            {
                Kaydedildi = false;
                try
                {
                    DialogResult = false;
                }
                catch
                {
                    Close();
                }
            });
        };
    }
}
