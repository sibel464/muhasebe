using System.Windows.Input;

namespace Masaustu.Yardimcilar;

// Zaman alan (async) komut çalışırken aynı komut yeniden çalıştırılamaz: "Kaydet"e çift tıklamak ya da
// ağ yavaşken tekrar basmak aynı faturayı/tahsilatı iki kez kaydetmesin. Düğme o süre pasif görünür.
// Barkod okutma gibi art arda hızlı gelmesi gereken komutlar tekrarGirisli: true ile açılır (okutma kaybolmasın).
public class KomutSarici : ICommand
{
    private readonly Func<object?, Task> _yurutme;
    private readonly Func<object?, bool>? _calistirabilir;
    private readonly bool _tekrarGirisli;
    private bool _calisiyor;

    public KomutSarici(Action<object?> yurutme, Func<object?, bool>? calistirabilir = null)
        : this(parametre =>
        {
            yurutme(parametre);
            return Task.CompletedTask;
        }, calistirabilir) { }

    public KomutSarici(Func<object?, Task> yurutme, Func<object?, bool>? calistirabilir = null, bool tekrarGirisli = false)
    {
        _yurutme = yurutme;
        _calistirabilir = calistirabilir;
        _tekrarGirisli = tekrarGirisli;
    }

    public KomutSarici(Action yurutme, Func<bool>? calistirabilir = null)
        : this(_ => yurutme(), calistirabilir == null ? null : _ => calistirabilir()) { }

    // "async () => ..." biçimi bu kurucuya gelir (eskiden Action kurucusuna düşüp async void oluyordu ve
    // çift tıklama korumasının dışında kalıyordu: 121 komut).
    public KomutSarici(Func<Task> yurutme, Func<bool>? calistirabilir = null, bool tekrarGirisli = false)
        : this(_ => yurutme(), calistirabilir == null ? null : _ => calistirabilir(), tekrarGirisli) { }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool Calisiyor => _calisiyor;

    public bool CanExecute(object? parametre) => (_tekrarGirisli || !_calisiyor) && (_calistirabilir?.Invoke(parametre) ?? true);

    public async void Execute(object? parametre)
    {
        if (!_tekrarGirisli && _calisiyor)
            return;
        var gorev = _yurutme(parametre);
        if (gorev.IsCompleted || _tekrarGirisli)
        {
            await gorev;
            return;
        }
        _calisiyor = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await gorev;
        }
        finally
        {
            _calisiyor = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public void BildirimGonder() => CommandManager.InvalidateRequerySuggested();
}
