using EtkiAlani.Modeller.Ayar;

namespace Uygulama.Arayuzler;

public interface ISistemAyarlariServisi
{
    Task<SistemAyarlari> AyarlariGetirAsync();
    Task<SistemAyarlari> AyarlariKaydetAsync(SistemAyarlari ayarlar);
}
