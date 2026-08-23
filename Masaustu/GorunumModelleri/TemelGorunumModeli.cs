using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Masaustu.GorunumModelleri;

public abstract class TemelGorunumModeli : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void BildirimGonder([CallerMemberName] string? ozellikAdi = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ozellikAdi));
    }

    protected bool OzellikAyarla<T>(ref T alan, T deger, [CallerMemberName] string? ozellikAdi = null)
    {
        if (EqualityComparer<T>.Default.Equals(alan, deger)) return false;
        alan = deger;
        BildirimGonder(ozellikAdi);
        return true;
    }
}
