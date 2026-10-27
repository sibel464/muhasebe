using Altyapi.Veri;
using EtkiAlani.Modeller.Fatura;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

// Tekrarlayan (periyodik) faturalar. Her dönemin faturası kendi tarihiyle ayrı kesilir; program birkaç ay açılmadıysa
// kaçırılan dönemlerin her biri için ayrı fatura çıkar. Bir dönem kesilemezse (ör. kilitli mali dönem, pasif cari)
// o plan orada durur, hata planda saklanır ve sonraki dönemlere geçilmez: dönemler sırayla kesilmelidir.
public class TekrarlayanFaturaServisi : ITekrarlayanFaturaServisi
{
    private const int TekCalismadaEnFazla = 120;

    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly FaturaServisi _faturaServisi;

    public TekrarlayanFaturaServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null, IFinancialTransactionService? finansServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _faturaServisi = new FaturaServisi(baglam, kullaniciServisi, finansServisi);
    }

    private void Goruntule() => _kullaniciServisi?.YetkiKontrolEt(y => y.FaturaGoruntule || y.FaturaOlustur, "Tekrarlayan fatura görüntüleme");
    private void Duzenle(string islem) => (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOlustur, islem);

    private static int BekleyenAdet(TekrarlayanFatura p, DateTime bugun)
    {
        if (!p.Aktif) return 0;
        var adet = 0;
        for (var k = p.OlusturulanAdet; adet < 1000; k++)
        {
            var tarih = p.BaslangicTarihi.AddMonths(k * Math.Max(1, p.PeriyotAy)).Date;
            if (tarih > bugun.Date || (p.BitisTarihi is DateTime b && tarih > b.Date)) break;
            adet++;
        }
        return adet;
    }

    public async Task<List<TekrarlayanFaturaListeDto>> ListeAsync(DateTime? bugun = null)
    {
        Goruntule();
        var gun = bugun ?? DateTime.Today;
        var planlar = await _baglam.TekrarlayanFaturalar.AsNoTracking()
            .Include(p => p.OrnekFatura).ThenInclude(f => f!.CariKart)
            .OrderBy(p => p.Ad).ToListAsync();
        var sonIdler = planlar.Where(p => p.SonFaturaId.HasValue).Select(p => p.SonFaturaId!.Value).ToList();
        var sonNolar = await _baglam.Faturalar.AsNoTracking().IgnoreQueryFilters([MuhasebeBaglami.SilinmisFiltresi])
            .Where(f => sonIdler.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FaturaNo);
        return planlar.Select(p => new TekrarlayanFaturaListeDto
        {
            Id = p.Id, Ad = p.Ad, OrnekFaturaNo = p.OrnekFatura?.FaturaNo ?? "", CariUnvan = p.OrnekFatura?.CariKart?.Unvan ?? "",
            Tur = p.OrnekFatura?.Tur ?? FaturaTuru.Satis, Tutar = p.OrnekFatura?.GenelToplam ?? 0m, PeriyotAy = p.PeriyotAy,
            BaslangicTarihi = p.BaslangicTarihi, BitisTarihi = p.BitisTarihi, SiradakiTarih = p.SiradakiTarih, VadeGunu = p.VadeGunu,
            Onayla = p.Onayla, Aktif = p.Aktif, OlusturulanAdet = p.OlusturulanAdet, BekleyenAdet = BekleyenAdet(p, gun),
            SonFaturaNo = p.SonFaturaId is int id && sonNolar.TryGetValue(id, out var no) ? no : null,
            SonHata = p.SonHata, BittiMi = p.BittiMi
        }).ToList();
    }

    public async Task<int> BekleyenSayisiAsync(DateTime? bugun = null)
    {
        // Ana sayfa uyarısı için: fatura yetkisi olmayan kullanıcıya sayı gösterilmez.
        try { Goruntule(); }
        catch (YetkisizErisimException) { return 0; }
        var gun = bugun ?? DateTime.Today;
        var planlar = await _baglam.TekrarlayanFaturalar.AsNoTracking().Where(p => p.Aktif).ToListAsync();
        return planlar.Sum(p => BekleyenAdet(p, gun));
    }

    public async Task<TekrarlayanFatura?> GetirAsync(int id)
    {
        Goruntule();
        return await _baglam.TekrarlayanFaturalar.AsNoTracking().Include(p => p.OrnekFatura).FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<TekrarlayanFatura> KaydetAsync(TekrarlayanFatura plan)
    {
        Duzenle("Tekrarlayan fatura kaydetme");
        if (plan.PeriyotAy < 1 || plan.PeriyotAy > 24) throw new ArgumentException("Tekrar aralığı 1 ile 24 ay arasında olmalıdır.");
        if (plan.VadeGunu is < 0 or > 3650) throw new ArgumentException("Vade günü 0 ile 3650 arasında olmalıdır.");
        if (plan.BitisTarihi is DateTime b && b.Date < plan.BaslangicTarihi.Date) throw new ArgumentException("Bitiş tarihi ilk fatura tarihinden önce olamaz.");

        var ornek = await _baglam.Faturalar.AsNoTracking().Include(f => f.Kalemler).Include(f => f.CariKart)
            .FirstOrDefaultAsync(f => f.Id == plan.OrnekFaturaId)
            ?? throw new KeyNotFoundException("Örnek fatura bulunamadı.");
        if (ornek.Tur is not (FaturaTuru.Satis or FaturaTuru.Alis)) throw new InvalidOperationException("Yalnız satış ve alış faturası tekrarlanabilir.");
        if (ornek.KaynakTuru == FaturaKaynakTuru.Pos) throw new InvalidOperationException("Hızlı satış (POS) fişi tekrarlanamaz.");
        if (ornek.Kalemler.Count == 0) throw new InvalidOperationException("Örnek faturada kalem yok.");
        if (plan.Id == 0 && ornek.Durum is BelgeDurumu.Iptal or BelgeDurumu.Beklemede) throw new InvalidOperationException("İptal edilmiş ya da bekletilen fatura örnek alınamaz.");

        var ad = string.IsNullOrWhiteSpace(plan.Ad) ? (ornek.CariKart?.Unvan ?? ornek.FaturaNo) : plan.Ad.Trim();
        if (ad.Length > 200) ad = ad[..200];
        var baslangic = DateTime.SpecifyKind(plan.BaslangicTarihi.Date, DateTimeKind.Utc);
        DateTime? bitis = plan.BitisTarihi is DateTime bt ? DateTime.SpecifyKind(bt.Date, DateTimeKind.Utc) : null;

        if (plan.Id == 0)
        {
            var yeni = new TekrarlayanFatura
            {
                SirketId = ornek.SirketId, Ad = ad, OrnekFaturaId = ornek.Id, PeriyotAy = plan.PeriyotAy, BaslangicTarihi = baslangic,
                BitisTarihi = bitis, VadeGunu = plan.VadeGunu, Onayla = plan.Onayla, Aktif = plan.Aktif
            };
            _baglam.TekrarlayanFaturalar.Add(yeni);
            await _baglam.SaveChangesAsync();
            return yeni;
        }

        var kayitli = await _baglam.TekrarlayanFaturalar.FirstOrDefaultAsync(p => p.Id == plan.Id)
            ?? throw new KeyNotFoundException("Tekrarlayan fatura bulunamadı.");
        if (kayitli.OlusturulanAdet > 0 && (kayitli.BaslangicTarihi.Date != baslangic.Date || kayitli.PeriyotAy != plan.PeriyotAy))
            throw new InvalidOperationException("Fatura kesilmeye başlanmış bir planın ilk tarihi ve tekrar aralığı değiştirilemez. Planı durdurup yenisini oluşturun.");
        kayitli.Ad = ad;
        kayitli.OrnekFaturaId = ornek.Id;
        kayitli.PeriyotAy = plan.PeriyotAy;
        kayitli.BaslangicTarihi = baslangic;
        kayitli.BitisTarihi = bitis;
        kayitli.VadeGunu = plan.VadeGunu;
        kayitli.Onayla = plan.Onayla;
        kayitli.Aktif = plan.Aktif;
        kayitli.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
        return kayitli;
    }

    public async Task SilAsync(int id)
    {
        Duzenle("Tekrarlayan fatura silme");
        var plan = await _baglam.TekrarlayanFaturalar.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Tekrarlayan fatura bulunamadı.");
        // Kesilmiş faturalar yerinde kalır; yalnız plan kaldırılır.
        plan.Silindi = true;
        plan.SilinmeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
    }

    public async Task<List<TekrarlayanFaturaSonucu>> BekleyenleriKesAsync(DateTime? bugun = null, int? planId = null)
    {
        Duzenle("Tekrarlayan fatura kesme");
        var gun = (bugun ?? DateTime.Today).Date;
        var sonuclar = new List<TekrarlayanFaturaSonucu>();
        var planIdleri = await _baglam.TekrarlayanFaturalar.AsNoTracking()
            .Where(p => p.Aktif && (planId == null || p.Id == planId)).OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();

        foreach (var id in planIdleri)
        {
            while (sonuclar.Count < TekCalismadaEnFazla)
            {
                var sonuc = await BirDonemKesAsync(id, gun);
                if (sonuc == null) break;
                sonuclar.Add(sonuc);
                if (sonuc.Hata != null) break;
            }
        }
        return sonuclar;
    }

    // Planın sıradaki dönemini keser; kesilecek dönem yoksa null. Fatura, onay ve planın sayacı tek işlemde yazılır;
    // aynı plan iki bilgisayardan aynı anda kesilmeye çalışılırsa danışma kilidi ikincisini bekletir ve sayaç tazelenir.
    private async Task<TekrarlayanFaturaSonucu?> BirDonemKesAsync(int planId, DateTime gun)
    {
        _baglam.ChangeTracker.Clear();
        TekrarlayanFatura? plan = null;
        DateTime donem = default;
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(5452464, {0})", planId);
            plan = await _baglam.TekrarlayanFaturalar.FirstOrDefaultAsync(p => p.Id == planId);
            if (plan == null || !plan.Aktif || plan.BittiMi || plan.SiradakiTarih.Date > gun) return null;
            donem = plan.SiradakiTarih.Date;

            var ornek = await _baglam.Faturalar.AsNoTracking().IgnoreQueryFilters([MuhasebeBaglami.SilinmisFiltresi])
                .Include(f => f.Kalemler).FirstOrDefaultAsync(f => f.Id == plan.OrnekFaturaId)
                ?? throw new InvalidOperationException("Örnek fatura bulunamadı.");
            var tarih = DateTime.SpecifyKind(donem, DateTimeKind.Utc);
            var fatura = new Fatura
            {
                Tur = ornek.Tur,
                CariKartId = ornek.CariKartId,
                SirketId = ornek.SirketId,
                SubeId = ornek.SubeId,
                ParaBirimiId = ornek.ParaBirimiId,
                Tarih = tarih,
                VadeTarihi = plan.VadeGunu is int v ? tarih.AddDays(v) : null,
                OdemeSekli = FaturaOdemeSekli.AcikHesap,
                KaynakTuru = FaturaKaynakTuru.Manuel,
                GenelIskontoOrani = ornek.GenelIskontoOrani,
                TevkifatKodu = ornek.TevkifatKodu,
                TevkifatOrani = ornek.TevkifatOrani,
                Aciklama = DonemAciklamasi(ornek.Aciklama, plan, donem),
                Kalemler = ornek.Kalemler.OrderBy(k => k.Id).Select(k => new FaturaKalemi
                {
                    UrunId = k.UrunId, Miktar = k.Miktar, BirimFiyat = k.BirimFiyat, IskontoOrani = k.IskontoOrani, KdvOrani = k.KdvOrani
                }).ToList()
            };
            await _faturaServisi.EkleAsync(fatura);
            if (plan.Onayla)
                await _faturaServisi.OnaylaAsync(fatura.Id);

            plan.OlusturulanAdet++;
            plan.SonFaturaId = fatura.Id;
            plan.SonHata = null;
            plan.GuncellemeTarihi = DateTime.UtcNow;
            await _baglam.SaveChangesAsync();
            await tx.CommitAsync();
            return new TekrarlayanFaturaSonucu(planId, plan.Ad, donem, fatura.FaturaNo, null);
        }
        catch (Exception ex) when (plan != null)
        {
            await tx.RollbackAsync();
            var hata = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
            _baglam.ChangeTracker.Clear();
            var kayitli = await _baglam.TekrarlayanFaturalar.FirstOrDefaultAsync(p => p.Id == planId);
            if (kayitli != null)
            {
                kayitli.SonHata = $"{donem:dd.MM.yyyy} dönemi kesilemedi: {hata}";
                await _baglam.SaveChangesAsync();
            }
            return new TekrarlayanFaturaSonucu(planId, plan.Ad, donem, null, hata);
        }
    }

    // Açıklamaya dönem eklenir (ör. "Dükkan kirası - 10/2026"); muhasebe fişinde ve faturada hangi ayın kirası olduğu görünsün.
    private static string DonemAciklamasi(string? ornekAciklama, TekrarlayanFatura plan, DateTime donem)
    {
        var donemMetni = plan.PeriyotAy == 12 ? donem.ToString("yyyy") : donem.ToString("MM'/'yyyy");
        var metin = $"{plan.Ad} - {donemMetni}";
        if (!string.IsNullOrWhiteSpace(ornekAciklama) && !ornekAciklama.Contains(plan.Ad))
            metin = $"{ornekAciklama.Trim()} ({metin})";
        return metin.Length > 500 ? metin[..500] : metin;
    }
}
