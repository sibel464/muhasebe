using System.Windows.Input;

namespace Masaustu.Yardimcilar;

public class KomutSarici : ICommand
{
    private readonly Action<object?> _yurutme;
    private readonly Func<object?, bool>? _calistirabilir;

    public KomutSarici(Action<object?> yurutme, Func<object?, bool>? calistirabilir = null)
    {
        _yurutme = yurutme;
        _calistirabilir = calistirabilir;
    }

    public KomutSarici(Action yurutme, Func<bool>? calistirabilir = null)
        : this(_ => yurutme(), calistirabilir == null ? null : _ => calistirabilir()) { }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parametre) => _calistirabilir?.Invoke(parametre) ?? true;
    public void Execute(object? parametre) => _yurutme(parametre);

    public void BildirimGonder() => CommandManager.InvalidateRequerySuggested();
}
