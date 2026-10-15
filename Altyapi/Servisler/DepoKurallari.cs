using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Kurumsal;
using Altyapi.Veri;

namespace Altyapi.Servisler;

// Stok hareketi yalnızca aktif depolara yazılır. Depo numaraları sabit kabul edilmez (1-2-3 gibi):
// kurulumda depolar yeniden tanımlanabildiği için her işlem gerçek, aktif depoyu veritabanından okur.
public static class DepoKurallari
{
    // Sıra: önce mağazalar, sonra depolar; her grupta tanım sırası (Id).
    public static Task<List<Depo>> AktifDepolarAsync(MuhasebeBaglami baglam, CancellationToken ct = default) =>
        baglam.Depolar.AsNoTracking()
            .Where(d => d.AktifMi)
            .OrderBy(d => d.DepoTuru == DepoTuru.Magaza ? 0 : 1).ThenBy(d => d.Id)
            .ToListAsync(ct);

    // Depo seçilmemiş satış/alış kalemleri için varsayılan stok yeri: ilk aktif mağaza (yoksa ilk aktif depo).
    public static async Task<Depo> VarsayilanDepoAsync(MuhasebeBaglami baglam, CancellationToken ct = default) =>
        (await AktifDepolarAsync(baglam, ct)).FirstOrDefault()
        ?? throw new InvalidOperationException("Tanımlı aktif depo/mağaza yok. Önce Depo ekranından bir mağaza veya depo tanımlayın.");

    public static async Task<Depo> AktifDepoGetirAsync(MuhasebeBaglami baglam, int depoId, CancellationToken ct = default) =>
        await baglam.Depolar.AsNoTracking().FirstOrDefaultAsync(d => d.Id == depoId && d.AktifMi, ct)
        ?? throw new InvalidOperationException($"Seçilen stok yeri ({depoId}) bulunamadı veya pasif. Aktif bir mağaza/depo seçin.");
}
