using EtkiAlani.Modeller.Kullanici;
using Uygulama.Arayuzler;

namespace Altyapi.EntegrasyonTestleri;

internal static class StabilizasyonTestOturumu
{
    public static Altyapi.Servisler.RaporMerkeziServisi Rapor(Altyapi.Veri.MuhasebeBaglami db, Guid sirketId)
    {
        var firma = new AktifFirmaBaglami();
        firma.Degistir(sirketId, null, null, 1);
        return new Altyapi.Servisler.RaporMerkeziServisi(db, kullaniciServisi: Olustur(), aktifFirma: firma);
    }

    // İş kuralı testleri artık yetki kontrolünü atlamak yerine açık test oturumu kullanır.
    public static IKullaniciServisi Olustur() => new Cari360TestKullanici
    {
        AktifYetkiler = new KullaniciYetkileri
        {
            RaporGoruntule = true, MaliyetHesaplariGoruntule = true, CariEkle = true, CariDuzenle = true, CariGoruntule = true,
            StokEkle = true, StokDuzenle = true, StokGoruntule = true,
            KasaYonet = true, BankaEkle = true, BankaDuzenle = true,
            TahsilatYap = true, OdemeYap = true,
            CekSenetGoruntule = true, CekSenetIslemYap = true
        }
    };
}
