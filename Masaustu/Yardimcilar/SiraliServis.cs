using System.Collections.Concurrent;
using System.Reflection;

namespace Masaustu.Yardimcilar;

// Bir sayfanın servisleri tek veritabanı bağlamını paylaşır. Seçim değişince arka planda başlayan yükleme, süren başka bir
// yüklemeyle çakışınca "A second operation was started on this context" hatası verip listeleri boş bırakıyordu.
// Sarmalanan servisin her async çağrısı aynı kilit altında sırayla çalışır; aynı kilidi paylaşan servisler de birbirini bekler.
public class SiraliServis<T> : DispatchProxy where T : class
{
    private T _hedef = null!;
    private SemaphoreSlim _kilit = null!;

    private static readonly ConcurrentDictionary<Type, MethodInfo> SonucluSarmalar = new();
    private static readonly MethodInfo SonucluSarmaYontemi =
        typeof(SiraliServis<T>).GetMethod(nameof(SonucluSarAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public static T Sar(T hedef, SemaphoreSlim? kilit = null)
    {
        var vekil = Create<T, SiraliServis<T>>();
        var s = (SiraliServis<T>)(object)vekil;
        s._hedef = hedef;
        s._kilit = kilit ?? new SemaphoreSlim(1, 1);
        return vekil;
    }

    protected override object? Invoke(MethodInfo? yontem, object?[]? argumanlar)
    {
        ArgumentNullException.ThrowIfNull(yontem);
        var donus = yontem.ReturnType;
        if (donus == typeof(Task))
            return SarAsync(() => (Task)Cagir(yontem, argumanlar)!);
        if (donus.IsGenericType && donus.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var sar = SonucluSarmalar.GetOrAdd(donus.GetGenericArguments()[0], t => SonucluSarmaYontemi.MakeGenericMethod(t));
            return sar.Invoke(this, [new Func<Task>(() => (Task)Cagir(yontem, argumanlar)!)]);
        }
        return Cagir(yontem, argumanlar);
    }

    private object? Cagir(MethodInfo yontem, object?[]? argumanlar)
    {
        try { return yontem.Invoke(_hedef, argumanlar); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private async Task SarAsync(Func<Task> is_)
    {
        await _kilit.WaitAsync();
        try { await is_(); }
        finally { _kilit.Release(); }
    }

    private async Task<TSonuc> SonucluSarAsync<TSonuc>(Func<Task> is_)
    {
        await _kilit.WaitAsync();
        try
        {
            var gorev = (Task<TSonuc>)is_();
            return await gorev;
        }
        finally { _kilit.Release(); }
    }
}
