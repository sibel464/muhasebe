using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Uygulama.Dto.Depo;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class DepoServisi : IDepoServisi
{
    private readonly IDbContextFactory<MuhasebeBaglami> _contextFactory;
    private readonly IKullaniciServisi _kullaniciServisi;

    public DepoServisi(IDbContextFactory<MuhasebeBaglami> contextFactory, IKullaniciServisi kullaniciServisi)
    {
        _contextFactory = contextFactory;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<Depo>> DepolariGetirAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Depolar.Where(d => d.AktifMi).OrderBy(d => d.Id).ToListAsync();
    }

    public async Task<Depo?> DepoGetirByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Depolar.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Depo> DepoEkleAsync(Depo depo)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet, "Depo Ekleme");
        ArgumentNullException.ThrowIfNull(depo);
        await using var context = await _contextFactory.CreateDbContextAsync();
        depo.DepoKodu = depo.DepoKodu?.Trim().ToUpperInvariant() ?? string.Empty;
        depo.DepoAdi = depo.DepoAdi?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(depo.DepoKodu) || string.IsNullOrWhiteSpace(depo.DepoAdi))
            throw new ArgumentException("Depo kodu ve adı zorunludur.");
        if (!await context.Subeler.AnyAsync(s => s.Id == depo.SubeId && s.AktifMi))
            throw new InvalidOperationException("Depo için geçerli ve aktif bir şube seçilmelidir.");
        if (await context.Depolar.AnyAsync(d => d.SubeId == depo.SubeId && d.DepoKodu.ToUpper() == depo.DepoKodu))
            throw new InvalidOperationException($"'{depo.DepoKodu}' kodlu depo bu şubede zaten mevcut.");
        depo.AktifMi = true;
        depo.OlusturmaTarihi = DateTime.UtcNow;
        context.Depolar.Add(depo);
        await context.SaveChangesAsync();
        return depo;
    }

    public async Task DepoGuncelleAsync(Depo depo)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet, "Depo Güncelleme");
        ArgumentNullException.ThrowIfNull(depo);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var mevcut = await context.Depolar.FirstOrDefaultAsync(d => d.Id == depo.Id)
            ?? throw new KeyNotFoundException("Güncellenecek depo bulunamadı.");
        var kod = depo.DepoKodu?.Trim().ToUpperInvariant() ?? string.Empty;
        var ad = depo.DepoAdi?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(kod) || string.IsNullOrWhiteSpace(ad))
            throw new ArgumentException("Depo kodu ve adı zorunludur.");
        if (await context.Depolar.AnyAsync(d => d.Id != depo.Id && d.SubeId == depo.SubeId && d.DepoKodu.ToUpper() == kod))
            throw new InvalidOperationException($"'{kod}' kodlu depo bu şubede zaten mevcut.");
        mevcut.SubeId = depo.SubeId;
        mevcut.DepoKodu = kod;
        mevcut.DepoAdi = ad;
        mevcut.DepoTuru = depo.DepoTuru;
        mevcut.Sorumlu = depo.Sorumlu?.Trim();
        mevcut.Telefon = depo.Telefon?.Trim();
        mevcut.Adres = depo.Adres?.Trim();
        mevcut.MuhasebeKodu = depo.MuhasebeKodu?.Trim();
        mevcut.AktifMi = depo.AktifMi;
        mevcut.GuncellemeTarihi = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task<bool> DepoSilAsync(int id)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet, "Depo Silme");
        await using var context = await _contextFactory.CreateDbContextAsync();
        var depo = await context.Depolar.FirstOrDefaultAsync(d => d.Id == id);
        if (depo == null) return false;

        var stokVar = await context.UrunLokasyonStoklari
            .AnyAsync(s => s.DepoId == id && (s.MevcutMiktar != 0 || s.RezerveMiktar != 0));
        if (stokVar)
            throw new InvalidOperationException("Stoğu veya rezervasyonu bulunan depo pasife alınamaz. Önce stokları başka depoya aktarın.");

        depo.AktifMi = false;
        context.Depolar.Update(depo);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<DepoStokMatrisDto>> DepoStokMatrisiGetirAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var depolar = await DepolariGetirAsync();
        var magazaId = depolar.Where(d => d.DepoTuru == DepoTuru.Magaza).OrderBy(d => d.Id).Select(d => (int?)d.Id).FirstOrDefault();
        var normalDepolar = depolar.Where(d => d.DepoTuru == DepoTuru.Depo).OrderBy(d => d.Id).ToList();
        var depo1Id = normalDepolar.ElementAtOrDefault(0)?.Id;
        var depo2Id = normalDepolar.ElementAtOrDefault(1)?.Id;

        var urunler = await context.Urunler.Where(u => u.Aktif).ToListAsync();
        var lokasyonStoklari = await context.UrunLokasyonStoklari.ToListAsync();

        var matris = new List<DepoStokMatrisDto>();

        foreach (var u in urunler)
        {
            var uStoklar = lokasyonStoklari.Where(ls => ls.UrunId == u.Id).ToList();

            decimal magazaMiktar = 0;
            decimal depo1Miktar = 0;
            decimal depo2Miktar = 0;
            decimal digerMiktar = 0;

            if (uStoklar.Any())
            {
                foreach (var ls in uStoklar)
                {
                    if (ls.DepoId == magazaId) magazaMiktar += ls.MevcutMiktar;
                    else if (ls.DepoId == depo1Id) depo1Miktar += ls.MevcutMiktar;
                    else if (ls.DepoId == depo2Id) depo2Miktar += ls.MevcutMiktar;
                    else digerMiktar += ls.MevcutMiktar;
                }
            }
            else
            {
                // Lokasyon kaydı henüz oluşmamışsa tüm stok varsayılan olarak Depo 1'e veya Mağazaya atanır
                depo1Miktar = 0;
            }

            matris.Add(new DepoStokMatrisDto
            {
                UrunId = u.Id,
                UrunKodu = u.Kod,
                UrunAdi = u.Ad,
                Barkod = u.Barkod,
                Birim = u.Birim,
                MagazaMiktar = magazaMiktar,
                Depo1Miktar = depo1Miktar,
                Depo2Miktar = depo2Miktar,
                DigerDepolarMiktar = digerMiktar,
                MinStok = u.MinimumStok,
                AlisFiyati = u.AlisFiyati,
                SatisFiyati = u.PerakendeSatisFiyati
            });
        }

        return matris;
    }

    public async Task<List<DepoHareketDetayDto>> DepoHareketleriGetirAsync(int? depoId = null, int? urunId = null, DateTime? baslangic = null, DateTime? bitis = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Silinen ürünlerin geçmiş hareketleri de listelenir.
        var query = context.StokHareketleri
            .IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi])
            .Include(sh => sh.Urun)
            .Include(sh => sh.Depo)
            .Include(sh => sh.HedefDepo)
            .Include(sh => sh.CariKart)
            .AsNoTracking()
            .AsQueryable();

        if (depoId.HasValue && depoId.Value > 0)
        {
            query = query.Where(sh => sh.DepoId == depoId.Value || sh.HedefDepoId == depoId.Value);
        }

        if (urunId.HasValue && urunId.Value > 0)
        {
            query = query.Where(sh => sh.UrunId == urunId.Value);
        }

        // Gün bazında: bitiş gününün tüm hareketleri dahil.
        var aralik = RaporTarihAraligi.Olustur(baslangic, bitis);
        if (baslangic.HasValue)
        {
            query = query.Where(sh => sh.Tarih >= aralik.Baslangic);
        }

        if (bitis.HasValue)
        {
            query = query.Where(sh => sh.Tarih < aralik.BitisSonrasi);
        }

        var list = await query.OrderByDescending(sh => sh.Tarih).Take(300).ToListAsync();

        return list.Select(sh => new DepoHareketDetayDto
        {
            Id = sh.Id,
            Tarih = sh.Tarih,
            FisNo = sh.FisNo ?? $"STK-{sh.Id:D5}",
            BelgeNo = sh.BelgeNo,
            IslemTuru = sh.IslemTuru,
            HareketTuru = sh.HareketTuru,
            UrunId = sh.UrunId,
            UrunKodu = sh.Urun?.Kod ?? string.Empty,
            UrunAdi = sh.Urun?.Ad ?? "Bilinmeyen Ürün",
            Barkod = sh.Urun?.Barkod,
            DepoId = sh.DepoId,
            DepoAdi = sh.Depo?.DepoAdi ?? "Genel Depo",
            HedefDepoId = sh.HedefDepoId,
            HedefDepoAdi = sh.HedefDepo?.DepoAdi ?? "-",
            CariKartId = sh.CariKartId,
            CariUnvan = sh.CariKart?.Unvan ?? "-",
            Miktar = sh.Miktar,
            BirimFiyat = sh.BirimFiyat,
            Aciklama = sh.Aciklama
        }).ToList();
    }

    public async Task<DepoTransferFisi> TransferFisiKaydetAsync(DepoTransferFisi transferFisi)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.DepoTransferYap, "Depo Transferi");
        ArgumentNullException.ThrowIfNull(transferFisi);
        if (transferFisi.KaynakDepoId <= 0 || transferFisi.HedefDepoId <= 0)
            throw new ArgumentException("Kaynak ve hedef depo seçilmelidir.");
        if (transferFisi.KaynakDepoId == transferFisi.HedefDepoId)
            throw new InvalidOperationException("Kaynak ve hedef depo aynı olamaz.");
        if (transferFisi.Kalemler == null || transferFisi.Kalemler.Count == 0)
            throw new ArgumentException("Transfer fişinde en az bir ürün bulunmalıdır.");
        if (transferFisi.Kalemler.Any(k => k.UrunId <= 0 || k.Miktar <= 0 || k.BirimFiyat < 0))
            throw new ArgumentException("Transfer kalemlerinde ürün, pozitif miktar ve negatif olmayan fiyat zorunludur.");
        if (transferFisi.Kalemler.GroupBy(k => k.UrunId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Aynı ürün transfer fişine birden fazla kez eklenemez.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Aynı depodaki ürünler için eşzamanlı transferlerin aynı stoğu harcamasını önler.
        foreach (var urunId in transferFisi.Kalemler.Select(k => k.UrunId).OrderBy(id => id))
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({transferFisi.KaynakDepoId}, {urunId})");

        var kaynakDepo = await context.Depolar.FirstOrDefaultAsync(d => d.Id == transferFisi.KaynakDepoId && d.AktifMi)
            ?? throw new InvalidOperationException("Kaynak depo bulunamadı veya pasif.");
        var hedefDepo = await context.Depolar.FirstOrDefaultAsync(d => d.Id == transferFisi.HedefDepoId && d.AktifMi)
            ?? throw new InvalidOperationException("Hedef depo bulunamadı veya pasif.");

        var urunIdleri = transferFisi.Kalemler.Select(k => k.UrunId).ToList();
        var urunler = await context.Urunler.Where(u => urunIdleri.Contains(u.Id) && u.Aktif).ToDictionaryAsync(u => u.Id);
        if (urunler.Count != urunIdleri.Count)
            throw new InvalidOperationException("Transfer kalemlerinden en az birinin ürünü bulunamadı veya pasif.");

        transferFisi.FisNo = transferFisi.FisNo?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(transferFisi.FisNo) && await context.DepoTransferFisleri.AnyAsync(f => f.FisNo == transferFisi.FisNo))
            throw new InvalidOperationException($"'{transferFisi.FisNo}' numaralı transfer fişi zaten mevcut.");

        if (string.IsNullOrWhiteSpace(transferFisi.FisNo))
        {
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(44504649)");
            var count = await context.DepoTransferFisleri.IgnoreQueryFilters().CountAsync();
            transferFisi.FisNo = $"TRF-{DateTime.Today.Year}-{(count + 1):D5}";
        }

        transferFisi.Tarih = transferFisi.Tarih == default ? DateTime.UtcNow : DateTime.SpecifyKind(transferFisi.Tarih, DateTimeKind.Utc);
        transferFisi.Durum = DepoTransferDurumu.Tamamlandi;
        transferFisi.OlusturmaTarihi = DateTime.UtcNow;
        var kAdi = kaynakDepo.DepoAdi;
        var hAdi = hedefDepo.DepoAdi;

        decimal toplamMiktar = 0;
        decimal toplamTutar = 0;

        foreach (var k in transferFisi.Kalemler)
        {
            toplamMiktar += k.Miktar;
            toplamTutar += k.ToplamTutar;

            // Kaynak Depo Stoğunu Düş
            var kLokasyon = await context.UrunLokasyonStoklari
                .FirstOrDefaultAsync(ls => ls.UrunId == k.UrunId && ls.DepoId == transferFisi.KaynakDepoId);
            var kullanilabilir = (kLokasyon?.MevcutMiktar ?? 0) - (kLokasyon?.RezerveMiktar ?? 0);
            if (kLokasyon == null || kullanilabilir < k.Miktar)
                throw new InvalidOperationException($"'{urunler[k.UrunId].Ad}' için kaynak depoda yeterli kullanılabilir stok yok. Mevcut: {kullanilabilir:N2}, istenen: {k.Miktar:N2}.");
            kLokasyon.MevcutMiktar -= k.Miktar;

            // Hedef Depo Stoğunu Arttır
            var hLokasyon = await context.UrunLokasyonStoklari
                .FirstOrDefaultAsync(ls => ls.UrunId == k.UrunId && ls.DepoId == transferFisi.HedefDepoId);
            if (hLokasyon == null)
            {
                hLokasyon = new UrunLokasyonStogu { UrunId = k.UrunId, DepoId = transferFisi.HedefDepoId, MevcutMiktar = 0 };
                context.UrunLokasyonStoklari.Add(hLokasyon);
            }
            hLokasyon.MevcutMiktar += k.Miktar;

            // Çift Yönlü Stok Hareketi Yaz
            var cikisHareketi = new StokHareketi
            {
                UrunId = k.UrunId,
                DepoId = transferFisi.KaynakDepoId,
                HedefDepoId = transferFisi.HedefDepoId,
                Tarih = transferFisi.Tarih,
                HareketTuru = StokHareketTuru.Cikis,
                IslemTuru = StokIslemTuru.DepoTransferi,
                FisNo = transferFisi.FisNo,
                BelgeNo = transferFisi.BelgeNo,
                Miktar = k.Miktar,
                BirimFiyat = k.BirimFiyat,
                Aciklama = $"{kAdi} ➔ {hAdi} Transfer Çıkışı ({transferFisi.Aciklama})"
            };

            var girisHareketi = new StokHareketi
            {
                UrunId = k.UrunId,
                DepoId = transferFisi.HedefDepoId,
                HedefDepoId = transferFisi.KaynakDepoId,
                Tarih = transferFisi.Tarih,
                HareketTuru = StokHareketTuru.Giris,
                IslemTuru = StokIslemTuru.DepoTransferi,
                FisNo = transferFisi.FisNo,
                BelgeNo = transferFisi.BelgeNo,
                Miktar = k.Miktar,
                BirimFiyat = k.BirimFiyat,
                Aciklama = $"{kAdi} ➔ {hAdi} Transfer Girişi ({transferFisi.Aciklama})"
            };

            context.StokHareketleri.AddRange(cikisHareketi, girisHareketi);
        }

        transferFisi.ToplamMiktar = toplamMiktar;
        transferFisi.ToplamTutar = toplamTutar;

        context.DepoTransferFisleri.Add(transferFisi);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return transferFisi;
    }

    public async Task<List<DepoTransferFisi>> TransferFisleriGetirAsync(int? depoId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.DepoTransferFisleri
            .IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi])
            .Include(f => f.KaynakDepo)
            .Include(f => f.HedefDepo)
            .Include(f => f.Kalemler).ThenInclude(k => k.Urun)
            .AsNoTracking()
            .AsQueryable();

        if (depoId.HasValue && depoId.Value > 0)
        {
            query = query.Where(f => f.KaynakDepoId == depoId.Value || f.HedefDepoId == depoId.Value);
        }

        return await query.OrderByDescending(f => f.Tarih).ToListAsync();
    }

    public async Task DepoSayimFarkiKaydetAsync(int depoId, int urunId, decimal sayilanMiktar, string aciklama)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.StokDuzenle, "Depo Sayım Farkı Kaydetme");
        if (depoId <= 0 || urunId <= 0)
            throw new ArgumentException("Depo ve ürün seçilmelidir.");
        if (sayilanMiktar < 0)
            throw new ArgumentOutOfRangeException(nameof(sayilanMiktar), "Sayılan miktar negatif olamaz.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({depoId}, {urunId})");

        var depo = await context.Depolar.FirstOrDefaultAsync(d => d.Id == depoId && d.AktifMi)
            ?? throw new InvalidOperationException("Sayım yapılacak depo bulunamadı veya pasif.");
        var urun = await context.Urunler.FirstOrDefaultAsync(u => u.Id == urunId && u.Aktif)
            ?? throw new InvalidOperationException("Sayımı yapılacak ürün bulunamadı veya pasif.");

        var lokasyon = await context.UrunLokasyonStoklari
            .FirstOrDefaultAsync(ls => ls.UrunId == urunId && ls.DepoId == depoId);

        var mevcutMiktar = lokasyon?.MevcutMiktar ?? 0;
        var fark = sayilanMiktar - mevcutMiktar;

        if (fark == 0) return;

        if (lokasyon == null)
        {
            lokasyon = new UrunLokasyonStogu { UrunId = urunId, DepoId = depoId, MevcutMiktar = sayilanMiktar };
            context.UrunLokasyonStoklari.Add(lokasyon);
        }
        else
        {
            lokasyon.MevcutMiktar = sayilanMiktar;
        }

        // Ana ürün toplam stoğu sayım farkı hareketinden otomatik güncellenir (MuhasebeBaglami).
        var depoAdi = depo.DepoAdi;

        var sh = new StokHareketi
        {
            UrunId = urunId,
            DepoId = depoId,
            Tarih = DateTime.UtcNow,
            HareketTuru = fark > 0 ? StokHareketTuru.Giris : StokHareketTuru.Cikis,
            IslemTuru = StokIslemTuru.SayimFarki,
            Miktar = Math.Abs(fark),
            BirimFiyat = urun.AlisFiyati,
            Aciklama = $"{depoAdi} Sayım Düzeltmesi (Sayılan: {sayilanMiktar:N0}, Fark: {(fark > 0 ? "+" : "")}{fark:N0}) {aciklama}"
        };

        context.StokHareketleri.Add(sh);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<decimal> DepoToplamStokDegeriHesaplaAsync(int? depoId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sorgu = context.UrunLokasyonStoklari
            .Where(s => s.Urun != null && s.Urun.Aktif && s.Depo != null && s.Depo.AktifMi);
        if (depoId.HasValue)
            sorgu = sorgu.Where(s => s.DepoId == depoId.Value);

        return await sorgu.SumAsync(s => s.MevcutMiktar * s.Urun!.AlisFiyati);
    }
}
