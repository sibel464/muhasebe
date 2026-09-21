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

            var buAySatisKalemleri = await _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId)
                .Where(fk => buAySatilarId.Contains(fk.FaturaId))
                .Select(fk => new
                {
                    fk.SatirToplami,
                    fk.Miktar,
                    AlisFiyati = fk.Urun != null ? fk.Urun.AlisFiyati : 0m
                })
                .ToListAsync();

            var buAyCiroNet = buAySatisKalemleri.Sum(k => k.SatirToplami);
            var buAyMaliyet = buAySatisKalemleri.Sum(k => k.Miktar * k.AlisFiyati);
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

        // Kasa & Banka Hareketlerinden Tahsilat & Ödeme (İç transferler Hariç)
        var kasaGiris = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= ayBasiUtc && kh.Tarih <= aySonuUtc && kh.FisTuru == KasaFisTuru.Tahsilat)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var bankaGiris = await _baglam.BankaHareketleri.AsNoTracking().Where(x => x.BankaHesabi!.SirketId == sirketId)
            .Where(bh => bh.Tarih >= ayBasiUtc && bh.Tarih <= aySonuUtc && bh.FisTuru == BankaFisTuru.GelenHavale)
            .SumAsync(bh => (decimal?)bh.Tutar) ?? 0m;

        var buAyTahsilat = kasaGiris + bankaGiris;

        var kasaCikis = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= ayBasiUtc && kh.Tarih <= aySonuUtc && kh.FisTuru == KasaFisTuru.Odeme)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var bankaCikis = await _baglam.BankaHareketleri.AsNoTracking().Where(x => x.BankaHesabi!.SirketId == sirketId)
            .Where(bh => bh.Tarih >= ayBasiUtc && bh.Tarih <= aySonuUtc && bh.FisTuru == BankaFisTuru.GidenHavale)
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
        var kasaToplami = await _baglam.Kasalar.AsNoTracking().Where(x => x.SirketId == sirketId).SumAsync(k => (decimal?)k.Bakiye) ?? 0m;
        var bankaToplami = await _baglam.BankaHesaplari.AsNoTracking().Where(x => x.SirketId == sirketId).SumAsync(b => (decimal?)b.Bakiye) ?? 0m;

        // Vadesi Geçmiş Alacak
        var vadesiGecmisAlacak = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi && f.VadeTarihi.HasValue && f.VadeTarihi.Value < bugünUtc)
            .SumAsync(f => (decimal?)f.GenelToplam) ?? 0m;

        // Kritik Stok Sayısı
        var kritikStokSayisi = await _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Where(u => u.Aktif && u.StokMiktari <= u.MinimumStok)
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
            .Where(kh => kh.Tarih >= mBasUtc && kh.Tarih <= mBitUtc && kh.FisTuru == KasaFisTuru.Tahsilat)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var oTahsilat = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= oBasUtc && kh.Tarih <= oBitUtc && kh.FisTuru == KasaFisTuru.Tahsilat)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var mOdeme = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= mBasUtc && kh.Tarih <= mBitUtc && kh.FisTuru == KasaFisTuru.Odeme)
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var oOdeme = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= oBasUtc && kh.Tarih <= oBitUtc && kh.FisTuru == KasaFisTuru.Odeme)
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
            faturaQuery = faturaQuery.Where(f => f.Tarih >= EnsureUtc(filtre.BaslangicTarihi.Value));

        if (filtre.BitisTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih <= EnsureUtc(filtre.BitisTarihi.Value));

        var query = from f in faturaQuery
                    join fk in _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId) on f.Id equals fk.FaturaId
                    join u in _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId) on fk.UrunId equals u.Id
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
                NetTutar = x.Fatura.Tur == FaturaTuru.SatisIade ? -x.Kalem.SatirToplami : x.Kalem.SatirToplami,
                KdvTutar = x.Kalem.KdvTutari,
                ToplamTutar = x.Fatura.Tur == FaturaTuru.SatisIade ? -x.Kalem.SatirToplami : x.Kalem.SatirToplami,
                Maliyet = x.Kalem.Miktar * x.Urun.AlisFiyati,
                BrutKar = (x.Fatura.Tur == FaturaTuru.SatisIade ? -x.Kalem.SatirToplami : x.Kalem.SatirToplami) - (x.Kalem.Miktar * x.Urun.AlisFiyati),
                Durum = x.Fatura.Durum.ToString()
            })
            .ToListAsync();

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
            Maliyet = filtre.MaliyetGor ? x.Maliyet : null,
            BrutKar = filtre.MaliyetGor ? x.BrutKar : null,
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
            faturaQuery = faturaQuery.Where(f => f.Tarih >= EnsureUtc(filtre.BaslangicTarihi.Value));

        if (filtre.BitisTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih <= EnsureUtc(filtre.BitisTarihi.Value));

        var query = from f in faturaQuery
                    join fk in _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId) on f.Id equals fk.FaturaId
                    join u in _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId) on fk.UrunId equals u.Id
                    group new { fk, u } by new { u.Id, u.Kod, u.Ad, u.Kategori, u.AlisFiyati, u.StokMiktari } into g
                    select new TopUrunRaporKalemiDto
                    {
                        UrunId = g.Key.Id,
                        UrunKod = g.Key.Kod,
                        UrunAd = g.Key.Ad,
                        Kategori = g.Key.Kategori ?? "",
                        SatilanMiktar = g.Sum(x => x.fk.Miktar),
                        Ciro = g.Sum(x => x.fk.SatirToplami),
                        BrutKar = filtre.MaliyetGor ? (g.Sum(x => x.fk.SatirToplami) - g.Sum(x => x.fk.Miktar * g.Key.AlisFiyati)) : null,
                        MevcutStok = g.Key.StokMiktari
                    };

        return await query.OrderByDescending(x => x.SatilanMiktar).Take(topCount).ToListAsync();
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
            faturaQuery = faturaQuery.Where(f => f.Tarih >= EnsureUtc(filtre.BaslangicTarihi.Value));

        if (filtre.BitisTarihi.HasValue)
            faturaQuery = faturaQuery.Where(f => f.Tarih <= EnsureUtc(filtre.BitisTarihi.Value));

        var query = from f in faturaQuery
                    join fk in _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId) on f.Id equals fk.FaturaId
                    join u in _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId) on fk.UrunId equals u.Id
                    select new { Fatura = f, Kalem = fk, Urun = u };

        var totalCount = await query.CountAsync();

        var items = await query.OrderByDescending(x => x.Fatura.Tarih)
            .Skip((filtre.PageNumber - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .Select(x => new AlisRaporuKalemiDto
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
                SonAlisFiyati = x.Kalem.BirimFiyat,
                OrtalamaAlisMaliyeti = x.Urun.AlisFiyati
            }).ToListAsync();

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

        var acikFaturalar = await _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Include(f => f.CariKart)
            .Where(f => f.Durum == BelgeDurumu.Onaylandi && f.Tur == FaturaTuru.Satis)
            .Select(f => new
            {
                f.CariKartId,
                CariKod = f.CariKart != null ? f.CariKart.Kod : "",
                CariUnvan = f.CariKart != null ? f.CariKart.Unvan : "",
                CariTur = f.CariKart != null ? f.CariKart.Tur.ToString() : "",
                f.GenelToplam,
                VadeTarihi = f.VadeTarihi ?? f.Tarih
            })
            .ToListAsync();

        var grouped = acikFaturalar.GroupBy(x => x.CariKartId).Select(g =>
        {
            var first = g.First();
            decimal vadesiGelmemis = 0m, g1_30 = 0m, g31_60 = 0m, g61_90 = 0m, g90Plus = 0m;

            foreach (var item in g)
            {
                var gecikme = (bugün - item.VadeTarihi.Date).Days;
                if (gecikme <= 0) vadesiGelmemis += item.GenelToplam;
                else if (gecikme <= 30) g1_30 += item.GenelToplam;
                else if (gecikme <= 60) g31_60 += item.GenelToplam;
                else if (gecikme <= 90) g61_90 += item.GenelToplam;
                else g90Plus += item.GenelToplam;
            }

            return new CariYaslandirmaRaporuKalemiDto
            {
                CariId = g.Key,
                CariKod = first.CariKod,
                CariUnvan = first.CariUnvan,
                CariTur = first.CariTur,
                VadesiGelmemis = vadesiGelmemis,
                Gun1_30 = g1_30,
                Gun31_60 = g31_60,
                Gun61_90 = g61_90,
                Gun90Plus = g90Plus
            };
        }).ToList();

        return grouped;
    }

    public async Task<List<TahsilatPerformansiKalemiDto>> GetTahsilatPerformansiAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var faturaQuery = _baglam.Faturalar.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Include(f => f.CariKart)
            .Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi);

        var list = await faturaQuery.Take(100).Select(f => new TahsilatPerformansiKalemiDto
        {
            CariId = f.CariKartId,
            CariKod = f.CariKart != null ? f.CariKart.Kod : "",
            CariUnvan = f.CariKart != null ? f.CariKart.Unvan : "",
            FaturaNo = f.FaturaNo,
            FaturaTarihi = f.Tarih,
            VadeTarihi = f.VadeTarihi ?? f.Tarih,
            TahsilTarihi = null
        }).ToListAsync();

        return list;
    }

    public async Task<PagedResult<StokRaporuKalemiDto>> GetStokRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var urunQuery = _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId)
            .Include(u => u.Depo)
            .Include(u => u.KategoriTanimi)
            .Include(u => u.MarkaTanimi)
            .Where(u => u.Aktif);

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

        var rezerveMap = await _baglam.SiparisKalemleri.AsNoTracking().Where(x => x.Siparis!.CariKart!.SirketId == sirketId)
            .Where(sk => urunIds.Contains(sk.UrunId) && sk.Siparis != null && sk.Siparis.Durum == SiparisDurumu.Onaylandi)
            .GroupBy(sk => sk.UrunId)
            .Select(g => new { UrunId = g.Key, Rezerve = g.Sum(x => x.Miktar) })
            .ToDictionaryAsync(x => x.UrunId, x => x.Rezerve);

        // COGS & Stok Devir Hızı Formülü: COGS / Ortalama Stok Değeri
        var cogsMap = await _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId)
            .Where(fk => urunIds.Contains(fk.UrunId) && fk.Fatura != null && fk.Fatura.Tur == FaturaTuru.Satis && fk.Fatura.Durum == BelgeDurumu.Onaylandi)
            .GroupBy(fk => fk.UrunId)
            .Select(g => new { UrunId = g.Key, Cogs = g.Sum(x => x.Miktar * (x.Urun != null ? x.Urun.AlisFiyati : 0m)) })
            .ToDictionaryAsync(x => x.UrunId, x => x.Cogs);

        var result = urunler.Select(u =>
        {
            decimal stokDegeri = u.StokMiktari * u.AlisFiyati;
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
                BirimMaliyet = filtre.MaliyetGor ? u.AlisFiyati : null,
                KritikStokSeviyesi = u.MinimumStok,
                SonHareketTarihi = null,
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
                StokMiktari = u.StokMiktari,
                SonHareketTarihi = null,
                StokDegeri = filtre.MaliyetGor ? (u.StokMiktari * u.AlisFiyati) : 0m
            })
            .ToListAsync();

        return urunler.Where(x => x.HareketsizGunSayisi >= gunFilter).ToList();
    }

    public async Task<List<StokDegerRaporuKalemiDto>> GetStokDegerRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var depos = await _baglam.Depolar.AsNoTracking().Where(x => x.Sube!.SirketId == sirketId).ToListAsync();
        var urunler = await _baglam.Urunler.AsNoTracking().Where(x => x.SirketId == sirketId).Where(u => u.Aktif).ToListAsync();

        var result = new List<StokDegerRaporuKalemiDto>();

        foreach (var depo in depos)
        {
            var depoUrunleri = urunler.Where(u => u.DepoId == depo.Id).ToList();
            result.Add(new StokDegerRaporuKalemiDto
            {
                LokasyonAd = depo.DepoAdi,
                UrunCesidiSayisi = depoUrunleri.Count,
                ToplamMiktar = depoUrunleri.Sum(u => u.StokMiktari),
                ToplamStokDegeri = filtre.MaliyetGor ? depoUrunleri.Sum(u => u.StokMiktari * u.AlisFiyati) : 0m
            });
        }

        if (result.Count == 0)
        {
            result.Add(new StokDegerRaporuKalemiDto
            {
                LokasyonAd = "Ana Depo",
                UrunCesidiSayisi = urunler.Count,
                ToplamMiktar = urunler.Sum(u => u.StokMiktari),
                ToplamStokDegeri = filtre.MaliyetGor ? urunler.Sum(u => u.StokMiktari * u.AlisFiyati) : 0m
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
        var bugün = DateTime.Today;
        var ayBasiiUtc = EnsureUtc(new DateTime(bugün.Year, bugün.Month, 1));

        var kasaQuery = _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= ayBasiiUtc && kh.FisTuru == KasaFisTuru.Tahsilat);

        if (filtre.CariId.HasValue)
            kasaQuery = kasaQuery.Where(kh => kh.KasaId == filtre.CariId.Value);

        var tahsilatlar = await kasaQuery
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var odemeQuery = _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId)
            .Where(kh => kh.Tarih >= ayBasiiUtc && kh.FisTuru == KasaFisTuru.Odeme);

        if (filtre.CariId.HasValue)
            odemeQuery = odemeQuery.Where(kh => kh.KasaId == filtre.CariId.Value);

        var odemeler = await odemeQuery
            .SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;

        var detaylar = await GetKasaBankaRaporuAsync(filtre);

        return new NakitAkisRaporuDto
        {
            DonemBasiNakit = 0m,
            Tahsilatlar = tahsilatlar,
            DigerGirisler = 0m,
            Odemeler = odemeler,
            DigerCikislar = 0m,
            KasaBankaDetaylari = detaylar
        };
    }

    public async Task<List<KasaBankaRaporKalemiDto>> GetKasaBankaRaporuAsync(SatisRaporFiltreDto filtre)
    {
        var sirketId = RaporYetkiKontrol(filtre);
        var kasalar = await _baglam.Kasalar.AsNoTracking().Where(x => x.SirketId == sirketId).ToListAsync();
        var bankalar = await _baglam.BankaHesaplari.AsNoTracking().Where(x => x.SirketId == sirketId).ToListAsync();

        var result = new List<KasaBankaRaporKalemiDto>();

        foreach (var k in kasalar)
        {
            var giris = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId).Where(kh => kh.KasaId == k.Id && kh.Tur == HareketTuru.Gelir).SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;
            var cikis = await _baglam.KasaHareketleri.AsNoTracking().Where(x => x.Kasa!.SirketId == sirketId).Where(kh => kh.KasaId == k.Id && kh.Tur == HareketTuru.Gider).SumAsync(kh => (decimal?)kh.Tutar) ?? 0m;
            result.Add(new KasaBankaRaporKalemiDto
            {
                HesapId = k.Id,
                HesapAd = k.Ad,
                Hesapturu = "Kasa",
                Devir = 0m,
                Giris = giris,
                Cikis = cikis,
                ParaBirimi = k.ParaBirimi
            });
        }

        foreach (var b in bankalar)
        {
            var giris = await _baglam.BankaHareketleri.AsNoTracking().Where(x => x.BankaHesabi!.SirketId == sirketId).Where(bh => bh.BankaHesabiId == b.Id && bh.Tur == HareketTuru.Gelir).SumAsync(bh => (decimal?)bh.Tutar) ?? 0m;
            var cikis = await _baglam.BankaHareketleri.AsNoTracking().Where(x => x.BankaHesabi!.SirketId == sirketId).Where(bh => bh.BankaHesabiId == b.Id && bh.Tur == HareketTuru.Gider).SumAsync(bh => (decimal?)bh.Tutar) ?? 0m;
            result.Add(new KasaBankaRaporKalemiDto
            {
                HesapId = b.Id,
                HesapAd = b.Ad,
                Hesapturu = "Banka",
                Devir = 0m,
                Giris = giris,
                Cikis = cikis,
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
        var satisKalemler = await _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId)
            .Include(fk => fk.Fatura)
            .Where(fk => fk.Fatura != null && fk.Fatura.Tur == FaturaTuru.Satis && fk.Fatura.Durum == BelgeDurumu.Onaylandi)
            .ToListAsync();

        var alisKalemler = await _baglam.FaturaKalemleri.AsNoTracking().Where(x => x.Fatura!.SirketId == sirketId)
            .Include(fk => fk.Fatura)
            .Where(fk => fk.Fatura != null && fk.Fatura.Tur == FaturaTuru.Alis && fk.Fatura.Durum == BelgeDurumu.Onaylandi)
            .ToListAsync();

        var rates = new[] { 0m, 1m, 10m, 20m };

        var list = new List<KdvRaporuKalemiDto>();

        foreach (var r in rates)
        {
            var sMatrah = satisKalemler.Where(x => x.KdvOrani == r).Sum(x => x.SatirToplami - x.KdvTutari);
            var sKdv = satisKalemler.Where(x => x.KdvOrani == r).Sum(x => x.KdvTutari);

            var aMatrah = alisKalemler.Where(x => x.KdvOrani == r).Sum(x => x.SatirToplami - x.KdvTutari);
            var aKdv = alisKalemler.Where(x => x.KdvOrani == r).Sum(x => x.KdvTutari);

            list.Add(new KdvRaporuKalemiDto
            {
                KdvOrani = r,
                SatisMatrah = sMatrah,
                SatisKdv = sKdv,
                AlisMatrah = aMatrah,
                AlisKdv = aKdv
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
