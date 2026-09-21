using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public sealed class BelgeKapamaServisi : IBelgeKapamaServisi
{
    private readonly MuhasebeBaglami _baglam;

    public BelgeKapamaServisi(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
    }

    public async Task<IReadOnlyList<AcikBelge>> AcikBelgeleriGetirAsync(int cariKartId)
    {
        var hareketler = await _baglam.CariHareketler
            .Where(c => c.CariKartId == cariKartId && !c.Silindi)
            .ToListAsync();

        var acikBelgeler = new List<AcikBelge>();

        foreach (var c in hareketler)
        {
            var toplamTutar = c.Borc > 0 ? c.Borc : c.Alacak;
            var acikTutar = toplamTutar - c.KapaliTutar;
            var dovizAcik = (c.DovizBorc > 0 ? c.DovizBorc : c.DovizAlacak) - c.KapaliDovizTutar;

            if (acikTutar > 0)
            {
                acikBelgeler.Add(new AcikBelge(c.Id, c.Tarih, c.Aciklama, c.BelgeNo, toplamTutar, acikTutar, dovizAcik, c.Borc > 0));
            }
        }

        return acikBelgeler.OrderBy(x => x.Tarih).ToList();
    }

    public async Task<BelgeKapama> KapatAsync(int odenenCariHareketId, int faturaCariHareketId, decimal tutar, decimal dovizTutar = 0)
    {
        if (tutar <= 0) throw new ArgumentException("Kapama tutarı 0'dan büyük olmalıdır.");

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var odenen = await _baglam.CariHareketler.SingleOrDefaultAsync(x => x.Id == odenenCariHareketId)
                ?? throw new KeyNotFoundException("Ödenen cari hareket bulunamadı.");

            var fatura = await _baglam.CariHareketler.SingleOrDefaultAsync(x => x.Id == faturaCariHareketId)
                ?? throw new KeyNotFoundException("Kapatılacak belge bulunamadı.");

            var odenenToplam = odenen.Borc > 0 ? odenen.Borc : odenen.Alacak;
            var odenenAcik = odenenToplam - odenen.KapaliTutar;

            var faturaToplam = fatura.Borc > 0 ? fatura.Borc : fatura.Alacak;
            var faturaAcik = faturaToplam - fatura.KapaliTutar;

            if (tutar > odenenAcik) throw new InvalidOperationException($"Kapama tutarı ödeme bakiyesinden büyük olamaz. Kalan: {odenenAcik}");
            if (tutar > faturaAcik) throw new InvalidOperationException($"Kapama tutarı belge bakiyesinden büyük olamaz. Kalan: {faturaAcik}");

            var kapama = new BelgeKapama
            {
                OdenenCariHareketId = odenenCariHareketId,
                FaturaCariHareketId = faturaCariHareketId,
                KapananTutar = tutar,
                KapananDovizTutar = dovizTutar,
                KapamaTarihi = DateTime.UtcNow
            };

            odenen.KapaliTutar += tutar;
            odenen.KapaliDovizTutar += dovizTutar;
            odenen.GuncellemeTarihi = DateTime.UtcNow;

            fatura.KapaliTutar += tutar;
            fatura.KapaliDovizTutar += dovizTutar;
            fatura.GuncellemeTarihi = DateTime.UtcNow;

            _baglam.BelgeKapamalar.Add(kapama);
            await _baglam.SaveChangesAsync();
            await tx.CommitAsync();

            return kapama;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task IptalEtAsync(int kapamaId)
    {
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var kapama = await _baglam.BelgeKapamalar.SingleOrDefaultAsync(x => x.Id == kapamaId)
                ?? throw new KeyNotFoundException("Kapama kaydı bulunamadı.");

            if (kapama.Silindi) throw new InvalidOperationException("Bu kayıt zaten iptal edilmiş.");

            var odenen = await _baglam.CariHareketler.SingleOrDefaultAsync(x => x.Id == kapama.OdenenCariHareketId);
            var fatura = await _baglam.CariHareketler.SingleOrDefaultAsync(x => x.Id == kapama.FaturaCariHareketId);

            if (odenen != null)
            {
                odenen.KapaliTutar -= kapama.KapananTutar;
                odenen.KapaliDovizTutar -= kapama.KapananDovizTutar;
                odenen.GuncellemeTarihi = DateTime.UtcNow;
            }

            if (fatura != null)
            {
                fatura.KapaliTutar -= kapama.KapananTutar;
                fatura.KapaliDovizTutar -= kapama.KapananDovizTutar;
                fatura.GuncellemeTarihi = DateTime.UtcNow;
            }

            kapama.Silindi = true;
            kapama.SilinmeTarihi = DateTime.UtcNow;
            
            await _baglam.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
