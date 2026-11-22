using System.Windows;

namespace Masaustu.Yardimcilar;

// Kod arkası "async void ..._Click" işleyicilerinde çift tıklamayı engeller: işlem bitene kadar düğme pasif olur,
// ikinci tıklama yok sayılır. Kullanım (metodun ilk satırı):  using var kilit = TiklamaKilidi.Al(sender); if (kilit == null) return;
public static class TiklamaKilidi
{
    private static readonly HashSet<object> Mesguller = new(ReferenceEqualityComparer.Instance);

    public static IDisposable? Al(object? sender)
    {
        if (sender == null) return new Kilit(null, false);
        if (!Mesguller.Add(sender)) return null;
        var oge = sender as UIElement;
        var oncekiDurum = oge?.IsEnabled ?? false;
        if (oge != null) oge.IsEnabled = false;
        return new Kilit(sender, oncekiDurum);
    }

    private sealed class Kilit(object? sender, bool oncekiDurum) : IDisposable
    {
        private bool _birakildi;
        public void Dispose()
        {
            if (_birakildi || sender == null) return;
            _birakildi = true;
            Mesguller.Remove(sender);
            if (sender is UIElement oge) oge.IsEnabled = oncekiDurum;
        }
    }
}
