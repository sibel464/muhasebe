using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Masaustu.GorunumModelleri;

public abstract class TemelGorunumModeli : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _ozellikHatalari = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _ozellikHatalari.Count > 0;

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

    // INotifyDataErrorInfo Implementasyonu
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _ozellikHatalari.Values;

        return _ozellikHatalari.TryGetValue(propertyName, out var errors) 
            ? errors 
            : Enumerable.Empty<string>();
    }

    protected void EkleHata(string hataMesaji, [CallerMemberName] string? ozellikAdi = null)
    {
        if (string.IsNullOrEmpty(ozellikAdi)) return;

        if (!_ozellikHatalari.ContainsKey(ozellikAdi))
            _ozellikHatalari[ozellikAdi] = new List<string>();

        if (!_ozellikHatalari[ozellikAdi].Contains(hataMesaji))
        {
            _ozellikHatalari[ozellikAdi].Add(hataMesaji);
            HataGeriBildirimiGonder(ozellikAdi);
        }
    }

    protected void TemizleHata([CallerMemberName] string? ozellikAdi = null)
    {
        if (string.IsNullOrEmpty(ozellikAdi)) return;

        if (_ozellikHatalari.ContainsKey(ozellikAdi))
        {
            _ozellikHatalari.Remove(ozellikAdi);
            HataGeriBildirimiGonder(ozellikAdi);
        }
    }

    protected void HataGeriBildirimiGonder(string ozellikAdi)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(ozellikAdi));
        BildirimGonder(nameof(HasErrors));
    }
}
