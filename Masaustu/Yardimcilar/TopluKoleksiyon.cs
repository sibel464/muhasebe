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
        // Önce kopyalanır: yeni satırlar bu listenin kendisinden süzülüyorsa (X.Where(...)) temizlik onları silmesin.
        var liste = yeniler.ToList();
        Items.Clear();
        foreach (var x in liste) Items.Add(x);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
