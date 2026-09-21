using EtkiAlani.Modeller.Cari;

namespace Uygulama.Arayuzler;

public interface ICariTipiTanimiServisi
{
    Task<List<CariTipiTanimi>> ListeleAsync(bool sadeceAktif = true, int? dahilEdilecekId = null);
    Task<CariTipiTanimi> KaydetAsync(CariTipiTanimi tanim);
    Task AktiflikDegistirAsync(int id, bool aktifMi);
    Task<bool> KullanimdaMiAsync(int id);
}
