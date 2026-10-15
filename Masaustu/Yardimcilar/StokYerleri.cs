using EtkiAlani.Modeller.Kurumsal;
using Uygulama.Arayuzler;

namespace Masaustu.Yardimcilar;

// Satış/fatura ekranlarının kullandığı aktif stok yerleri (mağaza ve depolar), veritabanından okunur.
// Sıra servisle aynıdır: önce mağazalar, sonra depolar; ilk kayıt varsayılan stok yeridir.
public static class StokYerleri
{
    public sealed record StokYeri(int Id, string Ad, DepoTuru Tur);

    public static IReadOnlyList<StokYeri> Liste { get; private set; } = [];

    // 0: henüz yüklenmedi veya yetki yok; bu durumda dağılım boş bırakılır ve sunucu varsayılan mağazayı kullanır.
    public static int VarsayilanId => Liste.Count > 0 ? Liste[0].Id : 0;

    public static string Ad(int depoId) => Liste.FirstOrDefault(x => x.Id == depoId)?.Ad ?? $"Depo #{depoId}";

    public static async Task YukleAsync(IUrunServisi urunServisi)
    {
        try
        {
            Liste = (await urunServisi.DepolariGetirAsync())
                .OrderBy(d => d.DepoTuru == DepoTuru.Magaza ? 0 : 1).ThenBy(d => d.Id)
                .Select(d => new StokYeri(d.Id, d.DepoAdi, d.DepoTuru))
                .ToList();
        }
        catch
        {
            // Depo görüntüleme yetkisi olmayan kullanıcıda liste boş kalır; satış yine de sunucu varsayılanıyla yapılır.
        }
    }
}
