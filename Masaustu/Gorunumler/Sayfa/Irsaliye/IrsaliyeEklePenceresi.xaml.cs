using System.Windows;
using System.Windows.Input;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Irsaliye;

public partial class IrsaliyeEklePenceresi : Window
{
    public IrsaliyeEklePenceresi(IrsaliyeEkleGorunumModeli viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.KapatIstendi = sonuc =>
        {
            DialogResult = sonuc;
            Close();
        };

        MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };
    }
}
