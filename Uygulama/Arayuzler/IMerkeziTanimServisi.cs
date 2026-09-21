using EtkiAlani.Modeller.Tanimlar;

namespace Uygulama.Arayuzler;

public interface IMerkeziTanimServisi
{
    Task<List<MerkeziTanim>> ListeleAsync(string grup, bool sadeceAktif = true, int? dahilEdilecekId = null);
    Task<MerkeziTanim> KaydetAsync(MerkeziTanim tanim);
    Task AktiflikDegistirAsync(int id, bool aktifMi);
    Task<bool> KullanimdaMiAsync(int id);
}
