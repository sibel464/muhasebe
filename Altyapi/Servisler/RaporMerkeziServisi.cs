using System.Text.Json;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Guvenlik;
using EtkiAlani.Modeller.Siparis;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Altyapi.Servisler;

public class RaporMerkeziServisi : IRaporMerkeziServisi
{
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly IAktifFirmaBaglami? _aktifFirma;
    private readonly MuhasebeBaglami _baglam;
    private readonly IAuditLogServisi? _auditLogServisi;

    public RaporMerkeziServisi(MuhasebeBaglami baglam, IAuditLogServisi? auditLogServisi = null, IKullaniciServisi? kullaniciServisi = null, IAktifFirmaBaglami? aktifFirma = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _aktifFirma = aktifFirma;
        _auditLogServisi = auditLogServisi;
    }

    private Guid RaporYetkiKontrol(SatisRaporFiltreDto? filtre = null)
    {
        if (_kullaniciServisi?.AktifKullanici == null || _aktifFirma == null || _aktifFirma.SirketId == Guid.Empty)
            throw new YetkisizErisimException("Rapor için aktif firma ve kullanıcı oturumu");
        _kullaniciServisi.YetkiKontrolEt(y => y.RaporGoruntule, "Rapor görüntüleme");
        if (filtre != null)
        {
            filtre.MaliyetGor = filtre.MaliyetGor && _kullaniciServisi.YetkiliMi(y => y.MaliyetHesaplariGoruntule);
            filtre.PageNumber = Math.Max(1, filtre.PageNumber);
            filtre.PageSize = Math.Clamp(filtre.PageSize, 1, 500);
        }
        return _aktifFirma.SirketId;
    }

    private static DateTime EnsureUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public async Task<YoneticiOzetDto> GetYoneticiOzetiAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var bugün = DateTime.Today;
        var bugünUtc = EnsureUtc(bugün);
        var ayBasiUtc = EnsureUtc(new DateTime(bugün.Year, bugün.Month, 1));
        var aySonuUtc = EnsureUtc(ayBasiUtc.AddMonths(1).AddTicks(-1));

        // Satışlar Query
        var satisQuery = _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi);

        if (filtre.SubeId.HasValue)
            satisQuery = satisQuery.Where(f => f.SubeId == filtre.SubeId.Value);

        var bugunkuSatis = await satisQuery
            .Where(f => f.Tarih >= bugünUtc && f.Tarih < bugünUtc.AddDays(1))
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        var buAySatis = await satisQuery
            .Where(f => f.Tarih >= ayBasiUtc && f.Tarih <= aySonuUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        // Satış Maliyet & Brüt Kâr (Server-Side)
        decimal? brutKar = null;
        if (filtre.MaliyetGor)
        {
            var buAySatilarId = await satisQuery
                .Where(f => f.Tarih >= ayBasiUtc && f.Tarih <= aySonuUtc)
                .Select(f => f.Id)
                .ToListAsync();

            var buAySatisKalemleri = await _baglam.FaturaKalemleri.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi]).AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId)
                .Where(fk => buAySatilarId.Contains(fk.FaturaId))
                .Select(fk => new { fk.FaturaId, fk.UrunId, FaturaTarihi = fk.Fatura!.Tarih, fk.SatirToplami, fk.KdvTutari, fk.Miktar })
                .ToListAsync();

            // Kâr KDV hariç ciro ile satış anındaki ortalama maliyet farkıdır.
            var maliyet = await HareketliOrtalamaMaliyet.HesaplaAsync(_baglam, sirketId, buAySatisKalemleri.Select(k => k.UrunId).Distinct().ToList());
            var buAyCiroNet = buAySatisKalemleri.Sum(k => k.SatirToplami - k.KdvTutari);
            var buAyMaliyet = buAySatisKalemleri.Sum(k => k.Miktar * maliyet.FaturaBirimMaliyeti(k.FaturaId, k.UrunId, k.FaturaTarihi));
            brutKar = buAyCiroNet - buAyMaliyet;
        }

        // Alış Query
        var alisQuery = _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Alis && f.Durum == BelgeDurumu.Onaylandi);
        if (filtre.SubeId.HasValue)
            alisQuery = alisQuery.Where(f => f.SubeId == filtre.SubeId.Value);

        var buAyAlis = await alisQuery
            .Where(f => f.Tarih >= ayBasiUtc && f.Tarih <= aySonuUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        // Kasa & Banka Hareketlerinden Tahsilat & Ödeme (İç transferler hariç). İptal edilen işlem ile ters kaydı
        // aynı fiş türünü taşıdığından ikisi de sayılmaz; yalnız tamamlanmış işlemler toplanır.
        var kasaGiris = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= ayBasiUtc && kh.Tarih <= aySonuUtc && kh.FisTuru == KasaFisTuru.Tahsilat && kh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var bankaGiris = await _baglam.BankaHareketleri.AsNoTracking().Where(x => x.BankaHesabi!.SirketId == sirketId)
            .Where(bh => bh.Tarih >= ayBasiUtc && bh.Tarih <= aySonuUtc && bh.FisTuru == BankaFisTuru.GelenHavale && bh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(bh => (decimal?)bh.Tutar) ?? 0m;

        var buAyTahsilat = kasaGiris + bankaGiris;

        var kasaCikis = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= ayBasiUtc && kh.Tarih <= aySonuUtc && kh.FisTuru == KasaFisTuru.Odeme && kh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var bankaCikis = await _baglam.BankaHareketleri.AsNoTracking().Where(x => x.BankaHesabi!.SirketId == sirketId)
            .Where(bh => bh.Tarih >= ayBasiUtc && bh.Tarih <= aySonuUtc && bh.FisTuru == BankaFisTuru.GidenHavale && bh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(bh => (decimal?)bh.Tutar) ?? 0m;

        var buAyOdeme = kasaCikis + bankaCikis;

        // Toplam Alacak & Borç (Cari Bakiyeleri)
        var cariBakiyeler = await _baglam.CariHareketler.AsNoTracking().Where(x => x.CariKart!.SirketId == sirketId)
            .GroupBy(ch => ch.CariKartId)
            .Select(g => new
            {
                CariId = g.Key,
                Borc = g.Sum(x => x.Borc),
                Alacak = g.Sum(x => x.Alacak)
            })
            .ToListAsync();

        var toplamAlacak = cariBakiyeler.Where(c => c.Borc > c.Alacak).Sum(c => c.Borc - c.Alacak);
        var toplamBorc = cariBakiyeler.Where(c => c.Alacak > c.Borc).Sum(c => c.Alacak - c.Borc);

        // Kasa Toplamı & Banka Toplamı
        // Bakiye hareketlerden hesaplanır (kart üzerindeki Bakiye alanı tutulmaz); iptal ile ters kaydı birbirini götürür.
        var kasaToplami = await _baglam.KasaHareketleri.AsNoTracking()
            .Where(h => h.Kasa!.SirketId == sirketId && h.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(h => (decimal?)(h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar)) ?? 0m;
        var bankaToplami = await _baglam.BankaHareketleri.AsNoTracking()
            .Where(h => h.BankaHesabi!.SirketId == sirketId && h.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(h => (decimal?)(h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar)) ?? 0m;

        // Vadesi Geçmiş Alacak
        // Ödenmiş faturalar düşülerek (yaşlandırmayla aynı hesap) yalnız vadesi geçmiş açık tutar.
        var vadesiGecmisAlacak = (await GetCariYaslandirmaAsync(filtre)).Sum(x => x.Gun1_30 + x.Gun31_60 + x.Gun61_90 + x.Gun90Plus);

        // Kritik Stok Sayısı
        var kritikStokSayisi = await _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(u => u.Aktif && u.StokTakibi && u.StokMiktari <= u.MinimumStok)
            .CountAsync();

        return new YoneticiOzetDto
        {
            BugunkuSatis = bugunkuSatis,
            BuAySatis = buAySatis,
            BuAyAlis = buAyAlis,
            BuAyTahsilat = buAyTahsilat,
            BuAyOdeme = buAyOdeme,
            BrutKar = brutKar,
            ToplamAlacak = toplamAlacak,
            ToplamBorc = toplamBorc,
            KasaToplami = kasaToplami,
            BankaToplami = bankaToplami,
            VadesiGecmisAlacak = vadesiGecmisAlacak,
            KritikStokSayisi = kritikStokSayisi
        };
    }

    public async Task<DonemKarsilastirmaResultDto> GetDonemKarsilastirmaAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var bugün = DateTime.Today;
        DateTime mBaslangic, mBitis, oBaslangic, oBitis;

        if (filtre.PresetKey == "BugunVsDun")
        {
            mBaslangic = bugün;
            mBitis = bugün.AddDays(1).AddTicks(-1);
            oBaslangic = bugün.AddDays(-1);
            oBitis = bugün.AddTicks(-1);
        }
        else if (filtre.PresetKey == "BuYilVsGecenYil")
        {
            mBaslangic = new DateTime(bugün.Year, 1, 1);
            mBitis = new DateTime(bugün.Year, 12, 31, 23, 59, 59);
            oBaslangic = new DateTime(bugün.Year - 1, 1, 1);
            oBitis = new DateTime(bugün.Year - 1, 12, 31, 23, 59, 59);
        }
        else // BuAyVsGecenAy
        {
            mBaslangic = new DateTime(bugün.Year, bugün.Month, 1);
            mBitis = mBaslangic.AddMonths(1).AddTicks(-1);
            oBaslangic = mBaslangic.AddMonths(-1);
            oBitis = mBaslangic.AddTicks(-1);
        }

        var mBasUtc = EnsureUtc(mBaslangic);
        var mBitUtc = EnsureUtc(mBitis);
        var oBasUtc = EnsureUtc(oBaslangic);
        var oBitUtc = EnsureUtc(oBitis);

        var mSatis = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi && f.Tarih >= mBasUtc && f.Tarih <= mBitUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        var oSatis = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi && f.Tarih >= oBasUtc && f.Tarih <= oBitUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        var mAlis = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Alis && f.Durum == BelgeDurumu.Onaylandi && f.Tarih >= mBasUtc && f.Tarih <= mBitUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        var oAlis = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Alis && f.Durum == BelgeDurumu.Onaylandi && f.Tarih >= oBasUtc && f.Tarih <= oBitUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        var mTahsilat = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= mBasUtc && kh.Tarih <= mBitUtc && kh.FisTuru == KasaFisTuru.Tahsilat && kh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var oTahsilat = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= oBasUtc && kh.Tarih <= oBitUtc && kh.FisTuru == KasaFisTuru.Tahsilat && kh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var mOdeme = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= mBasUtc && kh.Tarih <= mBitUtc && kh.FisTuru == KasaFisTuru.Odeme && kh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var oOdeme = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= oBasUtc && kh.Tarih <= oBitUtc && kh.FisTuru == KasaFisTuru.Odeme && kh.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var mIade = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.SatisIade && f.Durum == BelgeDurumu.Onaylandi && f.Tarih >= mBasUtc && f.Tarih <= mBitUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        var oIade = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.SatisIade && f.Durum == BelgeDurumu.Onaylandi && f.Tarih >= oBasUtc && f.Tarih <= oBitUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        var mSiparisCount = await _baglam.Siparisler.AsNoTracking().Where(x => x.CariKart!.SirketId == sirketId)
            .Where(s => s.SiparisTarihi >= mBasUtc && s.SiparisTarihi <= mBitUtc)
            .CountAsync();

        var oSiparisCount = await _baglam.Siparisler.AsNoTracking().Where(x => x.CariKart!.SirketId == sirketId)
            .Where(s => s.SiparisTarihi >= oBasUtc && s.SiparisTarihi <= oBitUtc)
            .CountAsync();

        return new DonemKarsilastirmaResultDto
        {
            PeriodType = filtre.PresetKey,
            Kalemler = new List<DonemKarsilastirmaKalemiDto>
            {
                new() { MetrikAd = "Satış Ciro", MevcutDeger = mSatis, ÖncekiDeger = oSatis },
                new() { MetrikAd = "Alış Ciro", MevcutDeger = mAlis, ÖncekiDeger = oAlis },
                new() { MetrikAd = "Tahsilat", MevcutDeger = mTahsilat, ÖncekiDeger = oTahsilat },
                new() { MetrikAd = "Ödeme", MevcutDeger = mOdeme, ÖncekiDeger = oOdeme },
                new() { MetrikAd = "Satış İadesi", MevcutDeger = mIade, ÖncekiDeger = oIade },
                new() { MetrikAd = "Sipariş Sayısı", MevcutDeger = mSiparisCount, ÖncekiDeger = oSiparisCount }
            }
        };
    }

    public async Task<PagedResult<SatisRaporuKalemiDto>> GetSatisRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var faturaQuery = _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Include(f => f.CariKart)
            .Include(f => f.Sube)
            .Where(f => f.Tur == FaturaTuru.Satis || f.Tur == FaturaTuru.SatisIade);

        if (!filtre.IptalleriDahilEt)
            faturaQuery = faturaQuery.Where(f => f.Durum == BelgeDurumu.Onaylandi);

        if (filtre.SubeId.HasValue)
            faturaQuery = faturaQuery.Where(f => f.SubeId == filtre.SubeId.Value);

        if (filtre.CariId.HasValue)
            faturaQuery = faturaQuery.Where(f => f.CariKartId == filtre.CariId.Value);

        if (filtre.BaslangicTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih >= RaporTarihAraligi.Olustur(filtre.BaslangicTarihi, null).Baslangic);

        if (filtre.BitisTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih < RaporTarihAraligi.Olustur(null, filtre.BitisTarihi).BitisSonrasi); // bitiş günü dahil

        var query = from f in faturaQuery
                    join fk in _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId) on f.Id equals fk.FaturaId
                    join u in _baglam.Urunler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi]).AsNoTracking().Where(x => x.SirketId == sirketId) on fk.UrunId equals u.Id
                    select new { Fatura = f, Kalem = fk, Urun = u };

        if (filtre.KategoriId.HasValue)
            query = query.Where(x => x.Urun.KategoriTanimiId == filtre.KategoriId.Value);

        if (filtre.MarkaId.HasValue)
            query = query.Where(x => x.Urun.MarkaTanimiId == filtre.MarkaId.Value);

        if (filtre.UrunId.HasValue)
            query = query.Where(x => x.Urun.Id == filtre.UrunId.Value);

        if (!string.IsNullOrWhiteSpace(filtre.AramaMetni))
        {
            var search = filtre.AramaMetni.Trim().ToLower();
            query = query.Where(x => x.Fatura.FaturaNo.ToLower().Contains(search) ||
                                     x.Fatura.CariKart!.Unvan.ToLower().Contains(search) ||
                                     x.Urun.Ad.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();

        var itemsRaw = await query
            .OrderByDescending(x => x.Fatura.Tarih)
            .ThenByDescending(x => x.Fatura.Id)
            .Skip((filtre.PageNumber - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .Select(x => new
            {
                FaturaId = x.Fatura.Id,
                Tarih = x.Fatura.Tarih,
                BelgeNo = x.Fatura.FaturaNo,
                CariKod = x.Fatura.CariKart != null ? x.Fatura.CariKart.Kod : "",
                CariUnvan = x.Fatura.CariKart != null ? x.Fatura.CariKart.Unvan : "",
                UrunKod = x.Urun.Kod,
                UrunAd = x.Urun.Ad,
                Kategori = x.Urun.Kategori ?? "",
                Marka = x.Urun.MarkaTanimi != null ? x.Urun.MarkaTanimi.Ad : "",
                SubeAd = x.Fatura.Sube != null ? x.Fatura.Sube.SubeAdi : "Merkez",
                Miktar = x.Fatura.Tur == FaturaTuru.SatisIade ? -x.Kalem.Miktar : x.Kalem.Miktar,
                BirimFiyat = x.Kalem.BirimFiyat,
                Iskonto = x.Kalem.IskontoTutari,
                KdvOrani = x.Kalem.KdvOrani,
                // SatirToplami KDV dahildir; net tutar ve kâr KDV hariç hesaplanır, iadeler eksi işaretlidir.
                NetTutar = (x.Fatura.Tur == FaturaTuru.SatisIade ? -1 : 1) * (x.Kalem.SatirToplami - x.Kalem.KdvTutari),
                KdvTutar = (x.Fatura.Tur == FaturaTuru.SatisIade ? -1 : 1) * x.Kalem.KdvTutari,
                ToplamTutar = (x.Fatura.Tur == FaturaTuru.SatisIade ? -1 : 1) * x.Kalem.SatirToplami,
                UrunId = x.Urun.Id,
                Durum = x.Fatura.Durum.ToString()
            })
            .ToListAsync();

        var maliyet = filtre.MaliyetGor
            ? await HareketliOrtalamaMaliyet.HesaplaAsync(_baglam, sirketId, itemsRaw.Select(x => x.UrunId).Distinct().ToList())
            : null;

        var items = itemsRaw.Select(x => new SatisRaporuKalemiDto
        {
            FaturaId = x.FaturaId,
            Tarih = x.Tarih,
            BelgeNo = x.BelgeNo,
            CariKod = x.CariKod,
            CariUnvan = x.CariUnvan,
            UrunKod = x.UrunKod,
            UrunAd = x.UrunAd,
            Kategori = x.Kategori,
            Marka = x.Marka,
            SubeAd = x.SubeAd,
            Miktar = x.Miktar,
            BirimFiyat = x.BirimFiyat,
            Iskonto = x.Iskonto,
            KdvOrani = x.KdvOrani,
            NetTutar = x.NetTutar,
            KdvTutar = x.KdvTutar,
            ToplamTutar = x.ToplamTutar,
            // Miktar iadelerde eksi işaretli olduğundan maliyet de eksi çıkar.
            Maliyet = maliyet == null ? null : x.Miktar * maliyet.FaturaBirimMaliyeti(x.FaturaId, x.UrunId, x.Tarih),
            BrutKar = maliyet == null ? null : x.NetTutar - x.Miktar * maliyet.FaturaBirimMaliyeti(x.FaturaId, x.UrunId, x.Tarih),
            Durum = x.Durum
        }).ToList();

        return new PagedResult<SatisRaporuKalemiDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = filtre.PageNumber,
            PageSize = filtre.PageSize
        };
    }

    public async Task<List<TopUrunRaporKalemiDto>> GetEnCokSatanUrunlerAsync(SatisRaporFiltreDto filtre, int topCount = 10)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var faturaQuery = _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId).Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi);

        if (filtre.BaslangicTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih >= RaporTarihAraligi.Olustur(filtre.BaslangicTarihi, null).Baslangic);

        if (filtre.BitisTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih < RaporTarihAraligi.Olustur(null, filtre.BitisTarihi).BitisSonrasi); // bitiş günü dahil

        var query = from f in faturaQuery
                    join fk in _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId) on f.Id equals fk.FaturaId
                    join u in _baglam.Urunler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi]).AsNoTracking().Where(x => x.SirketId == sirketId) on fk.UrunId equals u.Id
                    group new { fk, u } by new { u.Id, u.Kod, u.Ad, u.Kategori, u.StokMiktari } into g
                    select new TopUrunRaporKalemiDto
                    {
                        UrunId = g.Key.Id,
                        UrunKod = g.Key.Kod,
                        UrunAd = g.Key.Ad,
                        Kategori = g.Key.Kategori ?? "",
                        SatilanMiktar = g.Sum(x => x.fk.Miktar),
                        // Ciro ve kâr KDV hariç.
                        Ciro = g.Sum(x => x.fk.SatirToplami - x.fk.KdvTutari),
                        MevcutStok = g.Key.StokMiktari
                    };

        var sonuc = await query.OrderByDescending(x => x.SatilanMiktar).Take(topCount).ToListAsync();
        if (filtre.MaliyetGor && sonuc.Count > 0)
        {
            // Maliyet her satışın kendi tarihindeki ortalama maliyetiyle toplanır.
            var urunIds = sonuc.Select(x => x.UrunId).ToList();
            var kalemler = await (from f in faturaQuery
                                  join fk in _baglam.FaturaKalemleri.AsNoTracking() on f.Id equals fk.FaturaId
                                  where urunIds.Contains(fk.UrunId)
                                  select new { f.Id, f.Tarih, fk.UrunId, fk.Miktar }).ToListAsync();
            var maliyet = await HareketliOrtalamaMaliyet.HesaplaAsync(_baglam, sirketId, urunIds);
            var maliyetler = kalemler.GroupBy(k => k.UrunId)
                .ToDictionary(g => g.Key, g => g.Sum(k => k.Miktar * maliyet.FaturaBirimMaliyeti(k.Id, k.UrunId, k.Tarih)));
            foreach (var x in sonuc)
                x.BrutKar = x.Ciro - maliyetler.GetValueOrDefault(x.UrunId);
        }
        return sonuc;
    }

    public async Task<List<TopUrunRaporKalemiDto>> GetEnKarliUrunlerAsync(SatisRaporFiltreDto filtre, bool enDusukKar = false, int topCount = 10)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var topSatis = await GetEnCokSatanUrunlerAsync(filtre, 100);
        return enDusukKar
            ? topSatis.OrderBy(x => x.BrutKar ?? 0m).Take(topCount).ToList()
            : topSatis.OrderByDescending(x => x.BrutKar ?? 0m).Take(topCount).ToList();
    }

    public async Task<PagedResult<AlisRaporuKalemiDto>> GetAlisRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var faturaQuery = _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Include(f => f.CariKart)
            .Where(f => f.Tur == FaturaTuru.Alis && f.Durum == BelgeDurumu.Onaylandi);

        if (filtre.BaslangicTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih >= RaporTarihAraligi.Olustur(filtre.BaslangicTarihi, null).Baslangic);

        if (filtre.BitisTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih < RaporTarihAraligi.Olustur(null, filtre.BitisTarihi).BitisSonrasi); // bitiş günü dahil

        var query = from f in faturaQuery
                    join fk in _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId) on f.Id equals fk.FaturaId
                    join u in _baglam.Urunler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi]).AsNoTracking().Where(x => x.SirketId == sirketId) on fk.UrunId equals u.Id
                    select new { Fatura = f, Kalem = fk, Urun = u };

        var totalCount = await query.CountAsync();

        var satirlar = await query.OrderByDescending(x => x.Fatura.Tarih)
            .Skip((filtre.PageNumber - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .Select(x => new
            {
                UrunId = x.Urun.Id,
                Kalem = new AlisRaporuKalemiDto
                {
                    FaturaId = x.Fatura.Id,
                    Tarih = x.Fatura.Tarih,
                    BelgeNo = x.Fatura.FaturaNo,
                    TedarikciKod = x.Fatura.CariKart != null ? x.Fatura.CariKart.Kod : "",
                    TedarikciUnvan = x.Fatura.CariKart != null ? x.Fatura.CariKart.Unvan : "",
                    UrunKod = x.Urun.Kod,
                    UrunAd = x.Urun.Ad,
                    Kategori = x.Urun.Kategori ?? "",
                    Miktar = x.Kalem.Miktar,
                    ToplamTutar = x.Kalem.SatirToplami,
                    SonAlisFiyati = x.Kalem.BirimFiyat
                }
            }).ToListAsync();

        // Ortalama maliyet: alış tarihindeki (o alış dahil) hareketli ağırlıklı ortalama.
        var maliyet = await HareketliOrtalamaMaliyet.HesaplaAsync(_baglam, sirketId, satirlar.Select(x => x.UrunId).Distinct().ToList());
        foreach (var s in satirlar)
            s.Kalem.OrtalamaAlisMaliyeti = maliyet.TarihtekiBirimMaliyet(s.UrunId, s.Kalem.Tarih);
        var items = satirlar.Select(s => s.Kalem).ToList();

        return new PagedResult<AlisRaporuKalemiDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = filtre.PageNumber,
            PageSize = filtre.PageSize
        };
    }

    public async Task<PagedResult<CariRaporuKalemiDto>> GetCariRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var carilerQuery = _baglam.CariKartlar.AsNoTracking().Where(x => x.SirketId == sirketId).Where(c => c.Aktif);

        if (filtre.CariId.HasValue)
            carilerQuery = carilerQuery.Where(c => c.Id == filtre.CariId.Value);

        if (!string.IsNullOrWhiteSpace(filtre.AramaMetni))
        {
            var s = filtre.AramaMetni.Trim().ToLower();
            carilerQuery = carilerQuery.Where(c => c.Kod.ToLower().Contains(s) || c.Unvan.ToLower().Contains(s));
        }

        var totalCount = await carilerQuery.CountAsync();

        var cariler = await carilerQuery
            .OrderBy(c => c.Unvan)
            .Skip((filtre.PageNumber - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .ToListAsync();

        var cariIds = cariler.Select(c => c.Id).ToList();

        var hareketler = await _baglam.CariHareketler.AsNoTracking().Where(x => x.CariKart!.SirketId == sirketId)
            .Where(ch => cariIds.Contains(ch.CariKartId))
            .GroupBy(ch => ch.CariKartId)
            .Select(g => new
            {
                CariId = g.Key,
                Borc = g.Sum(x => x.Borc),
                Alacak = g.Sum(x => x.Alacak)
            })
            .ToListAsync();

        var faturaCountMap = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => cariIds.Contains(f.CariKartId) && f.Durum == BelgeDurumu.Onaylandi)
            .GroupBy(f => f.CariKartId)
            .Select(g => new { CariId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CariId, x => x.Count);

        var result = cariler.Select(c =>
        {
            var h = hareketler.FirstOrDefault(x => x.CariId == c.Id);
            var b = h?.Borc ?? 0m;
            var a = h?.Alacak ?? 0m;
            return new CariRaporuKalemiDto
            {
                CariId = c.Id,
                CariKod = c.Kod,
                CariUnvan = c.Unvan,
                CariTur = c.Tur.ToString(),
                ToplamBorc = b,
                ToplamAlacak = a,
                RiskLimiti = c.RiskLimiti ?? 0m,
                AcikFaturaAdedi = faturaCountMap.TryGetValue(c.Id, out var count) ? count : 0
            };
        }).ToList();

        return new PagedResult<CariRaporuKalemiDto>
        {
            Items = result,
            TotalCount = totalCount,
            PageNumber = filtre.PageNumber,
            PageSize = filtre.PageSize
        };
    }

    public async Task<List<CariYaslandirmaRaporuKalemiDto>> GetCariYaslandirmaAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var bugün = DateTime.Today;

        // Açık alacak: carinin tahsilat/iade gibi alacak kayıtları en eski borçtan başlayarak düşülür (FIFO);
        // kalan borçlar vadesine göre yaşlandırılır. Satış faturası borcunun vadesi faturadan, diğer borçlarınki kendi tarihinden alınır.
        var hareketler = await IptalsizCariHareketleriAsync(sirketId, null);
        var faturaVadeleri = await SatisFaturaVadeleriAsync(sirketId);
        var cariIdleri = hareketler.Select(h => h.CariKartId).Distinct().ToList();
        var cariler = await _baglam.CariKartlar.AsNoTracking().Where(c => cariIdleri.Contains(c.Id))
            .Select(c => new { c.Id, c.Kod, c.Unvan, Tur = c.Tur.ToString() }).ToDictionaryAsync(c => c.Id);

        var grouped = new List<CariYaslandirmaRaporuKalemiDto>();
        foreach (var g in hareketler.GroupBy(h => h.CariKartId))
        {
            var kalanAlacak = g.Sum(h => h.Alacak);
            decimal vadesiGelmemis = 0m, g1_30 = 0m, g31_60 = 0m, g61_90 = 0m, g90Plus = 0m;
            var borclar = g.Where(h => h.Borc > 0).Select(h => new
            {
                h.Borc,
                Vade = h.HareketTuru == CariHareketTuru.SatisFaturasi && h.BelgeNo != null && faturaVadeleri.TryGetValue((g.Key, h.BelgeNo), out var v) ? v : h.Tarih
            }).OrderBy(x => x.Vade);
            foreach (var borc in borclar)
            {
                var dusulen = Math.Min(borc.Borc, kalanAlacak);
                kalanAlacak -= dusulen;
                var acik = borc.Borc - dusulen;
                if (acik <= 0) continue;
                var gecikme = (bugün - borc.Vade.Date).Days;
                if (gecikme <= 0) vadesiGelmemis += acik;
                else if (gecikme <= 30) g1_30 += acik;
                else if (gecikme <= 60) g31_60 += acik;
                else if (gecikme <= 90) g61_90 += acik;
                else g90Plus += acik;
            }
            if (vadesiGelmemis + g1_30 + g31_60 + g61_90 + g90Plus <= 0 || !cariler.TryGetValue(g.Key, out var cari)) continue;

            grouped.Add(new CariYaslandirmaRaporuKalemiDto
            {
                CariId = g.Key,
                CariKod = cari.Kod,
                CariUnvan = cari.Unvan,
                CariTur = cari.Tur,
                VadesiGelmemis = vadesiGelmemis,
                Gun1_30 = g1_30,
                Gun31_60 = g31_60,
                Gun61_90 = g61_90,
                Gun90Plus = g90Plus
            });
        }

        return grouped;
    }

    // Seçilen dönemde kesilen satış faturalarının ne zaman tahsil edildiği: carinin alacak kayıtları tarih sırasıyla
    // en eski vadeli borçtan başlayarak düşülür (yaşlandırmayla aynı FIFO); borcu sıfırlayan kaydın tarihi tahsil tarihidir.
    public async Task<List<TahsilatPerformansiKalemiDto>> GetTahsilatPerformansiAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var faturaQuery = _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi);
        if (filtre.CariId.HasValue)
            faturaQuery = faturaQuery.Where(f => f.CariKartId == filtre.CariId.Value);
        if (filtre.BaslangicTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih >= RaporTarihAraligi.Olustur(filtre.BaslangicTarihi, null).Baslangic);
        if (filtre.BitisTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih < RaporTarihAraligi.Olustur(null, filtre.BitisTarihi).BitisSonrasi); // bitiş günü dahil

        var faturalar = await faturaQuery
            .OrderBy(f => f.Tarih).ThenBy(f => f.FaturaNo)
            .Select(f => new
            {
                f.CariKartId, f.FaturaNo, f.Tarih, Vade = f.VadeTarihi ?? f.Tarih, GenelToplam = f.GenelToplam - f.TevkifatTutari, // tahsil edilecek (tevkifat düşülmüş) tutar
                CariKod = f.CariKart != null ? f.CariKart.Kod : "", CariUnvan = f.CariKart != null ? f.CariKart.Unvan : ""
            })
            .ToListAsync();
        if (faturalar.Count == 0) return new();

        var cariIdleri = faturalar.Select(f => f.CariKartId).Distinct().ToList();
        var hareketler = await IptalsizCariHareketleriAsync(sirketId, cariIdleri);
        var faturaVadeleri = await SatisFaturaVadeleriAsync(sirketId);

        // (cari, fatura no) -> kapandığı tarih ve kalan tutar
        var kapanislar = new Dictionary<(int, string), (DateTime? Tarih, decimal Kalan)>();
        foreach (var g in hareketler.GroupBy(h => h.CariKartId))
        {
            var borclar = g.Where(h => h.Borc > 0).Select(h => new AcikBorc
            {
                FaturaNo = h.HareketTuru == CariHareketTuru.SatisFaturasi ? h.BelgeNo : null,
                Vade = h.HareketTuru == CariHareketTuru.SatisFaturasi && h.BelgeNo != null && faturaVadeleri.TryGetValue((g.Key, h.BelgeNo), out var v) ? v : h.Tarih,
                Kalan = h.Borc
            }).OrderBy(b => b.Vade).ToList();
            var sira = 0;
            foreach (var alacak in g.Where(h => h.Alacak > 0).OrderBy(h => h.Tarih))
            {
                var tutar = alacak.Alacak;
                while (tutar > 0 && sira < borclar.Count)
                {
                    var borc = borclar[sira];
                    var dusulen = Math.Min(tutar, borc.Kalan);
                    borc.Kalan -= dusulen;
                    tutar -= dusulen;
                    if (borc.Kalan > 0) break;
                    borc.KapanisTarihi = alacak.Tarih;
                    sira++;
                }
            }
            foreach (var borc in borclar.Where(b => b.FaturaNo != null))
                kapanislar[(g.Key, borc.FaturaNo!)] = (borc.KapanisTarihi, borc.Kalan);
        }

        var list = faturalar.Select(f =>
        {
            // Cari hareketi bulunmayan fatura cariye işlenmemiştir; tamamı açık sayılır.
            var kapanis = kapanislar.TryGetValue((f.CariKartId, f.FaturaNo), out var k) ? k : (Tarih: (DateTime?)null, Kalan: f.GenelToplam);
            return new TahsilatPerformansiKalemiDto
            {
                CariId = f.CariKartId,
                CariKod = f.CariKod,
                CariUnvan = f.CariUnvan,
                FaturaNo = f.FaturaNo,
                FaturaTarihi = f.Tarih,
                VadeTarihi = f.Vade,
                Tutar = f.GenelToplam,
                KalanTutar = kapanis.Kalan,
                TahsilTarihi = kapanis.Tarih
            };
        }).ToList();

        foreach (var cari in list.GroupBy(x => x.CariId))
        {
            var kapananlar = cari.Where(x => x.TahsilTarihi.HasValue).ToList();
            var ortalama = kapananlar.Count == 0 ? 0d
                : kapananlar.Average(x => Math.Max(0, (x.TahsilTarihi!.Value.Date - x.FaturaTarihi.Date).Days));
            foreach (var x in cari) x.OrtalamaTahsilSuresiGun = Math.Round(ortalama, 1);
        }

        return list;
    }

    private sealed class AcikBorc
    {
        public string? FaturaNo;
        public DateTime Vade;
        public decimal Kalan;
        public DateTime? KapanisTarihi;
    }

    private sealed record CariHareketOzeti(int CariKartId, DateTime Tarih, decimal Borc, decimal Alacak, string? BelgeNo, CariHareketTuru HareketTuru);

    // İptal edilen faturanın borç kaydı ile "-IPTAL" ters kaydı birbirini götürür; ikisi de FIFO eşleştirmesinden çıkarılır,
    // yoksa ters kayıt tahsilat gibi başka bir faturayı kapatırdı.
    private async Task<List<CariHareketOzeti>> IptalsizCariHareketleriAsync(Guid sirketId, List<int>? cariIdleri)
    {
        var sorgu = _baglam.CariHareketler.AsNoTracking().Where(x => x.CariKart!.SirketId == sirketId);
        if (cariIdleri != null)
            sorgu = sorgu.Where(h => cariIdleri.Contains(h.CariKartId));
        var hareketler = await sorgu
            .Select(h => new { h.CariKartId, h.Tarih, h.Borc, h.Alacak, h.BelgeNo, h.HareketTuru, h.IlgiliBelgeId })
            .ToListAsync();
        var iptaller = (await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId && x.Durum == BelgeDurumu.Iptal)
                .Select(f => new { f.Id, f.FaturaNo }).ToListAsync())
            .SelectMany(f => new[] { (f.Id, f.FaturaNo), (f.Id, f.FaturaNo + "-IPTAL") })
            .ToHashSet();
        return hareketler
            .Where(h => !(h.IlgiliBelgeId.HasValue && h.BelgeNo != null && iptaller.Contains((h.IlgiliBelgeId.Value, h.BelgeNo))))
            .Select(h => new CariHareketOzeti(h.CariKartId, h.Tarih, h.Borc, h.Alacak, h.BelgeNo, h.HareketTuru))
            .ToList();
    }

    private async Task<Dictionary<(int, string), DateTime>> SatisFaturaVadeleriAsync(Guid sirketId) =>
        (await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi)
            .Select(f => new { f.CariKartId, f.FaturaNo, Vade = f.VadeTarihi ?? f.Tarih })
            .ToListAsync())
        .GroupBy(f => (f.CariKartId, f.FaturaNo)).ToDictionary(g => g.Key, g => g.First().Vade);

    public async Task<PagedResult<StokRaporuKalemiDto>> GetStokRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var urunQuery = _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Include(u => u.Depo)
            .Include(u => u.KategoriTanimi)
            .Include(u => u.MarkaTanimi)
            .Where(u => u.Aktif && u.StokTakibi);

        if (filtre.KategoriId.HasValue)
            urunQuery = urunQuery.Where(u => u.KategoriTanimiId == filtre.KategoriId.Value);

        if (filtre.MarkaId.HasValue)
            urunQuery = urunQuery.Where(u => u.MarkaTanimiId == filtre.MarkaId.Value);

        if (!string.IsNullOrWhiteSpace(filtre.AramaMetni))
        {
            var s = filtre.AramaMetni.Trim().ToLower();
            urunQuery = urunQuery.Where(u => u.Kod.ToLower().Contains(s) || u.Ad.ToLower().Contains(s));
        }

        var totalCount = await urunQuery.CountAsync();

        var urunler = await urunQuery
            .OrderBy(u => u.Ad)
            .Skip((filtre.PageNumber - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .ToListAsync();

        var urunIds = urunler.Select(u => u.Id).ToList();

        // Rezerve: açık satış siparişlerinin henüz sevk edilmemiş miktarı (alış siparişi stok ayırmaz).
        var rezerveMap = await _baglam.SiparisKalemleri.AsNoTracking().Where(x => x.Siparis!.CariKart!.SirketId == sirketId)
            .Where(sk => urunIds.Contains(sk.UrunId) && sk.Siparis != null && sk.Siparis.SiparisTuru == SiparisTuru.Satis &&
                (sk.Siparis.Durum == SiparisDurumu.Beklemede || sk.Siparis.Durum == SiparisDurumu.Onaylandi || sk.Siparis.Durum == SiparisDurumu.KismenKarsilandi) &&
                sk.Miktar > sk.SevkEdilenMiktar)
            .GroupBy(sk => sk.UrunId)
            .Select(g => new { UrunId = g.Key, Rezerve = g.Sum(x => x.Miktar - x.SevkEdilenMiktar) })
            .ToDictionaryAsync(x => x.UrunId, x => x.Rezerve);

        // COGS & Stok Devir Hızı Formülü: COGS / Ortalama Stok Değeri
        var maliyet = await HareketliOrtalamaMaliyet.HesaplaAsync(_baglam, sirketId, urunIds);
        var satisKalemleri = await _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId)
            .Where(fk => urunIds.Contains(fk.UrunId) && fk.Fatura != null && fk.Fatura.Tur == FaturaTuru.Satis && fk.Fatura.Durum == BelgeDurumu.Onaylandi)
            .Select(fk => new { fk.FaturaId, fk.UrunId, fk.Miktar, fk.Fatura!.Tarih })
            .ToListAsync();
        var cogsMap = satisKalemleri.GroupBy(k => k.UrunId)
            .ToDictionary(g => g.Key, g => g.Sum(k => k.Miktar * maliyet.FaturaBirimMaliyeti(k.FaturaId, k.UrunId, k.Tarih)));

        var sonHareketMap = await SonStokHareketleriAsync(sirketId, urunIds);

        var result = urunler.Select(u =>
        {
            decimal birimMaliyet = maliyet.GuncelBirimMaliyet(u.Id);
            decimal stokDegeri = u.StokMiktari * birimMaliyet;
            decimal cogs = cogsMap.TryGetValue(u.Id, out var c) ? c : 0m;
            decimal devirHizi = stokDegeri > 0 ? Math.Round(cogs / stokDegeri, 2) : 0m;

            return new StokRaporuKalemiDto
            {
                UrunId = u.Id,
                UrunKod = u.Kod,
                UrunAd = u.Ad,
                Kategori = u.Kategori ?? "",
                Marka = u.MarkaTanimi != null ? u.MarkaTanimi.Ad : "",
                LokasyonAd = u.Depo != null ? u.Depo.DepoAdi : "Ana Depo",
                FizikselStok = u.StokMiktari,
                RezerveStok = rezerveMap.TryGetValue(u.Id, out var rez) ? rez : 0m,
                BirimMaliyet = filtre.MaliyetGor ? birimMaliyet : null,
                KritikStokSeviyesi = u.MinimumStok,
                SonHareketTarihi = sonHareketMap.TryGetValue(u.Id, out var son) ? son : null,
                StokDevirHizi = devirHizi
            };
        }).ToList();

        return new PagedResult<StokRaporuKalemiDto>
        {
            Items = result,
            TotalCount = totalCount,
            PageNumber = filtre.PageNumber,
            PageSize = filtre.PageSize
        };
    }

    public async Task<List<HareketsizUrunKalemiDto>> GetHareketsizUrunlerAsync(SatisRaporFiltreDto filtre, int gunFilter = 30)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var urunler = await _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(u => u.Aktif && u.StokMiktari > 0)
            .Select(u => new HareketsizUrunKalemiDto
            {
                UrunId = u.Id,
                UrunKod = u.Kod,
                UrunAd = u.Ad,
                Kategori = u.Kategori ?? "",
                StokMiktari = u.StokMiktari
            })
            .ToListAsync();

        var urunIds = urunler.Select(u => u.UrunId).ToList();
        var sonHareketMap = await SonStokHareketleriAsync(sirketId, urunIds);
        var maliyet = filtre.MaliyetGor ? await HareketliOrtalamaMaliyet.HesaplaAsync(_baglam, sirketId, urunIds) : null;
        foreach (var u in urunler)
        {
            u.SonHareketTarihi = sonHareketMap.TryGetValue(u.UrunId, out var son) ? son : null;
            u.StokDegeri = maliyet == null ? 0m : u.StokMiktari * maliyet.GuncelBirimMaliyet(u.UrunId);
        }
        return urunler.Where(x => x.HareketsizGunSayisi >= gunFilter).OrderByDescending(x => x.HareketsizGunSayisi).ToList();
    }

    // Ürünlerin iptal edilmemiş son stok hareketi tarihi.
    private Task<Dictionary<int, DateTime>> SonStokHareketleriAsync(Guid sirketId, List<int> urunIds) =>
        _baglam.StokHareketleri.AsNoTracking()
            .Where(h => h.Urun!.SirketId == sirketId && urunIds.Contains(h.UrunId) && !h.IptalEdildi)
            .GroupBy(h => h.UrunId)
            .Select(g => new { UrunId = g.Key, Son = g.Max(h => h.Tarih) })
            .ToDictionaryAsync(x => x.UrunId, x => x.Son);

    public async Task<List<StokDegerRaporuKalemiDto>> GetStokDegerRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var depos = await _baglam.Depolar.AsNoTracking().Where(x => x.Sube!.SirketId == sirketId).ToListAsync();
        var urunler = await _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId).Where(u => u.Aktif && u.StokTakibi).ToListAsync();

        var result = new List<StokDegerRaporuKalemiDto>();

        // Depo bazında gerçek lokasyon stokları (ürünün varsayılan deposu değil).
        var maliyet = await HareketliOrtalamaMaliyet.HesaplaAsync(_baglam, sirketId, urunler.Select(u => u.Id).ToList());
        var fiyatlar = urunler.ToDictionary(u => u.Id, u => maliyet.GuncelBirimMaliyet(u.Id));
        var depoIdleri = depos.Select(d => d.Id).ToList();
        var lokasyonlar = await _baglam.UrunLokasyonStoklari.AsNoTracking()
            .Where(l => depoIdleri.Contains(l.DepoId) && l.MevcutMiktar != 0)
            .Select(l => new { l.DepoId, l.UrunId, l.MevcutMiktar })
            .ToListAsync();
        foreach (var depo in depos)
        {
            var depoStoklari = lokasyonlar.Where(l => l.DepoId == depo.Id && fiyatlar.ContainsKey(l.UrunId)).ToList();
            result.Add(new StokDegerRaporuKalemiDto
            {
                LokasyonAd = depo.DepoAdi,
                UrunCesidiSayisi = depoStoklari.Select(l => l.UrunId).Distinct().Count(),
                ToplamMiktar = depoStoklari.Sum(l => l.MevcutMiktar),
                ToplamStokDegeri = filtre.MaliyetGor ? depoStoklari.Sum(l => l.MevcutMiktar * fiyatlar[l.UrunId]) : 0m
            });
        }

        if (result.Count == 0)
        {
            result.Add(new StokDegerRaporuKalemiDto
            {
                LokasyonAd = "Ana Depo",
                UrunCesidiSayisi = urunler.Count,
                ToplamMiktar = urunler.Sum(u => u.StokMiktari),
                ToplamStokDegeri = filtre.MaliyetGor ? urunler.Sum(u => u.StokMiktari * fiyatlar[u.Id]) : 0m
            });
        }

        return result;
    }

    public async Task<PagedResult<FinansRaporuKalemiDto>> GetFinansRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var kasaH = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Include(kh => kh.Kasa)
            .Include(kh => kh.CariKart)
            .OrderByDescending(kh => kh.Tarih)
            .Take(filtre.PageSize)
            .Select(kh => new FinansRaporuKalemiDto
            {
                IslemId = kh.Id,
                Tarih = kh.Tarih,
                IslemTuru = kh.FisTuru.ToString(),
                HesabAd = kh.Kasa != null ? kh.Kasa.Ad : "Kasa",
                CariUnvan = kh.CariKart != null ? kh.CariKart.Unvan : "",
                Giren = kh.Tur == HareketTuru.Gelir ? kh.Tutar : 0m,
                Cikan = kh.Tur == HareketTuru.Gider ? kh.Tutar : 0m,
                Bakiye = kh.Tutar,
                Aciklama = kh.Aciklama
            }).ToListAsync();

        return new PagedResult<FinansRaporuKalemiDto>
        {
            Items = kasaH,
            TotalCount = kasaH.Count,
            PageNumber = 1,
            PageSize = filtre.PageSize
        };
    }

    public async Task<NakitAkisRaporuDto> GetNakitAkisRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var aralik = RaporTarihAraligi.Olustur(filtre.BaslangicTarihi, filtre.BitisTarihi);

        // Kasa ve banka birlikte: cari tahsilat/ödemeleri ayrı, geri kalanı (gelir/gider, virman, POS geçişi, açılış) "diğer".
        // Dönem başı = başlangıçtan önceki bakiye, dönem sonu = kasa/banka raporundaki bakiyelerin toplamı.
        var kasa = await _baglam.KasaHareketleri.AsNoTracking()
            .Where(h => h.Kasa!.SirketId == sirketId && h.IslemDurumu == FinansIslemDurumu.Tamamlandi
                && h.Tarih >= aralik.Baslangic && h.Tarih < aralik.BitisSonrasi)
            .GroupBy(h => new { h.FisTuru, h.Tur })
            .Select(g => new { g.Key.FisTuru, g.Key.Tur, Tutar = g.Sum(h => h.Tutar) })
            .ToListAsync();
        var banka = await _baglam.BankaHareketleri.AsNoTracking()
            .Where(h => h.BankaHesabi!.SirketId == sirketId && h.IslemDurumu == FinansIslemDurumu.Tamamlandi
                && h.Tarih >= aralik.Baslangic && h.Tarih < aralik.BitisSonrasi)
            .GroupBy(h => new { h.FisTuru, h.Tur })
            .Select(g => new { g.Key.FisTuru, g.Key.Tur, Tutar = g.Sum(h => h.Tutar) })
            .ToListAsync();

        var tahsilatlar = kasa.Where(x => x.FisTuru == KasaFisTuru.Tahsilat && x.Tur == HareketTuru.Gelir).Sum(x => x.Tutar)
            + banka.Where(x => x.FisTuru == BankaFisTuru.GelenHavale && x.Tur == HareketTuru.Gelir).Sum(x => x.Tutar);
        var odemeler = kasa.Where(x => x.FisTuru == KasaFisTuru.Odeme && x.Tur == HareketTuru.Gider).Sum(x => x.Tutar)
            + banka.Where(x => x.FisTuru == BankaFisTuru.GidenHavale && x.Tur == HareketTuru.Gider).Sum(x => x.Tutar);
        var toplamGiris = kasa.Where(x => x.Tur == HareketTuru.Gelir).Sum(x => x.Tutar) + banka.Where(x => x.Tur == HareketTuru.Gelir).Sum(x => x.Tutar);
        var toplamCikis = kasa.Where(x => x.Tur == HareketTuru.Gider).Sum(x => x.Tutar) + banka.Where(x => x.Tur == HareketTuru.Gider).Sum(x => x.Tutar);

        var detaylar = await GetKasaBankaRaporuAsync(filtre);

        return new NakitAkisRaporuDto
        {
            DonemBasiNakit = detaylar.Sum(x => x.Devir),
            Tahsilatlar = tahsilatlar,
            DigerGirisler = toplamGiris - tahsilatlar,
            Odemeler = odemeler,
            DigerCikislar = toplamCikis - odemeler,
            KasaBankaDetaylari = detaylar
        };
    }

    public async Task<List<KasaBankaRaporKalemiDto>> GetKasaBankaRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var aralik = RaporTarihAraligi.Olustur(filtre.BaslangicTarihi, filtre.BitisTarihi);
        var kasalar = await _baglam.Kasalar.AsNoTracking().Where(x => x.SirketId == sirketId).ToListAsync();
        var bankalar = await _baglam.BankaHesaplari.AsNoTracking().Where(x => x.SirketId == sirketId).ToListAsync();

        // Devir: başlangıç gününden önceki hareketler; giriş/çıkış: aralıktaki hareketler; bitişten sonrası katılmaz.
        // İptal edilen işlem ile ters kaydı birbirini götürdüğünden yalnız tamamlananlar alınır.
        var kasaOzet = await _baglam.KasaHareketleri.AsNoTracking()
            .Where(h => h.Kasa!.SirketId == sirketId && h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih < aralik.BitisSonrasi)
            .GroupBy(h => h.KasaId)
            .Select(g => new
            {
                HesapId = g.Key,
                Devir = g.Where(h => h.Tarih < aralik.Baslangic).Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar),
                Giris = g.Where(h => h.Tarih >= aralik.Baslangic && h.Tur == HareketTuru.Gelir).Sum(h => h.Tutar),
                Cikis = g.Where(h => h.Tarih >= aralik.Baslangic && h.Tur == HareketTuru.Gider).Sum(h => h.Tutar)
            })
            .ToDictionaryAsync(x => x.HesapId);
        var bankaOzet = await _baglam.BankaHareketleri.AsNoTracking()
            .Where(h => h.BankaHesabi!.SirketId == sirketId && h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih < aralik.BitisSonrasi)
            .GroupBy(h => h.BankaHesabiId)
            .Select(g => new
            {
                HesapId = g.Key,
                Devir = g.Where(h => h.Tarih < aralik.Baslangic).Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar),
                Giris = g.Where(h => h.Tarih >= aralik.Baslangic && h.Tur == HareketTuru.Gelir).Sum(h => h.Tutar),
                Cikis = g.Where(h => h.Tarih >= aralik.Baslangic && h.Tur == HareketTuru.Gider).Sum(h => h.Tutar)
            })
            .ToDictionaryAsync(x => x.HesapId);

        var result = new List<KasaBankaRaporKalemiDto>();
        foreach (var k in kasalar)
        {
            kasaOzet.TryGetValue(k.Id, out var o);
            result.Add(new KasaBankaRaporKalemiDto
            {
                HesapId = k.Id,
                HesapAd = k.Ad,
                Hesapturu = "Kasa",
                Devir = o?.Devir ?? 0m,
                Giris = o?.Giris ?? 0m,
                Cikis = o?.Cikis ?? 0m,
                ParaBirimi = k.ParaBirimi
            });
        }

        foreach (var b in bankalar)
        {
            bankaOzet.TryGetValue(b.Id, out var o);
            result.Add(new KasaBankaRaporKalemiDto
            {
                HesapId = b.Id,
                HesapAd = b.Ad,
                Hesapturu = "Banka",
                Devir = o?.Devir ?? 0m,
                Giris = o?.Giris ?? 0m,
                Cikis = o?.Cikis ?? 0m,
                ParaBirimi = b.ParaBirimi
            });
        }

        return result;
    }

    public async Task<List<PosRaporuKalemiDto>> GetPosRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var poslar = await _baglam.PosIslemleri.AsNoTracking().Where(x => x.SirketId == sirketId).ToListAsync();

        return poslar.Select(p => new PosRaporuKalemiDto
        {
            PosId = p.Id,
            CihazAd = p.PosCihazAdi,
            BankaAd = p.BankaAdi,
            TaksitSayisi = p.TaksitSayisi,
            BrutTutar = p.BrutTutar,
            KomisyonOrani = p.KomisyonOrani,
            KomisyonTutar = p.KomisyonTutar,
            BekleyenTutar = p.Durum == PosIslemDurumu.Bekliyor ? p.NetTutar : 0m,
            HesabaGecenTutar = p.Durum == PosIslemDurumu.HesabaGecti ? p.NetTutar : 0m,
            IadeTutar = 0m
        }).ToList();
    }

    public async Task<List<CekSenetRaporuKalemiDto>> GetCekSenetRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var cekler = await _baglam.CekSenetler.AsNoTracking().Where(x => x.SirketId == sirketId).ToListAsync();

        return cekler.Select(c => new CekSenetRaporuKalemiDto
        {
            Id = c.Id,
            PortfoyNo = c.BelgeNo,
            Tur = c.Tur.ToString(),
            Direction = c.Tur == CekSenetTuru.AlinanCek || c.Tur == CekSenetTuru.AlinanSenet ? "Alınan" : "Verilen",
            VadeTarihi = c.VadeTarihi,
            Tutar = c.Tutar,
            Durum = c.Durum.ToString(),
            BorcluCari = c.Kesideci,
            BankaAd = c.BankaAdi ?? ""
        }).ToList();
    }

    public async Task<List<KdvRaporuKalemiDto>> GetKdvRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        // Seçilen dönemdeki onaylı faturalar; iadeler kendi tarafından düşülür (satış iadesi satıştan, alış iadesi alıştan).
        var sorgu = _baglam.FaturaKalemleri.AsNoTracking()
            .Where(fk => fk.Fatura!.SirketId == sirketId && fk.Fatura.Durum == BelgeDurumu.Onaylandi);
        if (filtre.BaslangicTarihi.HasValue)
            sorgu = sorgu.Where(fk => fk.Fatura!.Tarih >= RaporTarihAraligi.Olustur(filtre.BaslangicTarihi, null).Baslangic);
        if (filtre.BitisTarihi.HasValue)
            sorgu = sorgu.Where(fk => fk.Fatura!.Tarih < RaporTarihAraligi.Olustur(null, filtre.BitisTarihi).BitisSonrasi);
        var kalemler = await sorgu
            .Select(fk => new { fk.Fatura!.Tur, fk.KdvOrani, Matrah = fk.SatirToplami - fk.KdvTutari, Kdv = fk.KdvTutari })
            .ToListAsync();

        static decimal Isaret(FaturaTuru tur, bool satis) => (satis, tur) switch
        {
            (true, FaturaTuru.Satis) or (false, FaturaTuru.Alis) => 1m,
            (true, FaturaTuru.SatisIade) or (false, FaturaTuru.AlisIade) => -1m,
            _ => 0m
        };

        // Standart oranlar her zaman listelenir; kayıtlarda geçen başka oranlar (eski %8, %18 gibi) da eklenir.
        var list = new List<KdvRaporuKalemiDto>();
        foreach (var r in new[] { 0m, 1m, 10m, 20m }.Union(kalemler.Select(k => k.KdvOrani)).Distinct().OrderBy(x => x))
        {
            var oranKalemleri = kalemler.Where(x => x.KdvOrani == r).ToList();
            list.Add(new KdvRaporuKalemiDto
            {
                KdvOrani = r,
                SatisMatrah = oranKalemleri.Sum(x => Isaret(x.Tur, true) * x.Matrah),
                SatisKdv = oranKalemleri.Sum(x => Isaret(x.Tur, true) * x.Kdv),
                AlisMatrah = oranKalemleri.Sum(x => Isaret(x.Tur, false) * x.Matrah),
                AlisKdv = oranKalemleri.Sum(x => Isaret(x.Tur, false) * x.Kdv)
            });
        }

        return list;
    }

    public async Task<List<RaporFavoriDto>> GetFavoriFiltrelerAsync(int sirketId)
    {
        return await Task.FromResult(new List<RaporFavoriDto>
        {
            new RaporFavoriDto
            {
                Id = 1,
                RaporAd = "Bu Ay Mağaza Satışları",
                FiltreDataJson = JsonSerializer.Serialize(new SatisRaporFiltreDto { PresetKey = "BuAy" }),
                OlusturmaTarihi = DateTime.Now,
                KullaniciAd = "Yönetici"
            }
        });
    }

    public async Task FavoriFiltreKaydetAsync(int sirketId, string raporAd, SatisRaporFiltreDto filtre, string kullaniciAd)
    {
        if (_auditLogServisi != null)
        {
            await _auditLogServisi.LogEkleAsync(new AuditLog
            {
                IslemTuru = AuditIslemTuru.Guncelleme,
                TabloAdi = "RaporMerkezi",
                IslemDetayi = $"Favori filtre kaydedildi: {raporAd}"
            });
        }
    }

    public async Task BenchmarkPerformanceAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _ = await GetYoneticiOzetiAsync(filtre);
        _ = await GetSatisRaporuAsync(filtre);
        _ = await GetStokRaporuAsync(filtre);
        _ = await GetCariRaporuAsync(filtre);
        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"[BENCHMARK] Rapor sorguları süresi: {sw.ElapsedMilliseconds} ms");
    }
}
