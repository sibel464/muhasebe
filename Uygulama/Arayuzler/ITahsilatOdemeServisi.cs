using EtkiAlani.Modeller.Finans;

namespace Uygulama.Arayuzler;

public interface ITahsilatOdemeServisi
{
    Task<TahsilatOdemeKaydi> KaydetAsync(TahsilatOdemeTalebi talep);
    Task<IReadOnlyList<TahsilatOdemeKaydi>> GecmisiGetirAsync(FinansIslemTuru? tur = null, string? arama = null);
    Task IptalEtAsync(string kaynak, int hareketId, string neden);
    Task<IReadOnlyList<Kasa>> AktifKasalariGetirAsync();
    Task<IReadOnlyList<BankaHesabi>> AktifBankaHesaplariniGetirAsync();
}
