using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Masaustu.Yardimcilar;

// Büyük listeyi tek bildirimle doldurur. ObservableCollection'a satırları tek tek eklemek her satırda ekranı
// uyarır; yüz binlerce kayıtta liste dolarken program donmuş gibi görünür.
public class TopluKoleksiyon<T> : ObservableCollection<T>
{
    public void HepsiniDegistir(IEnumerable<T> yeniler)
    {
        Items.Clear();
        foreach (var x in yeniler) Items.Add(x);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
