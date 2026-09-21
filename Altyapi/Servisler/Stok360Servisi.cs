using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Siparis;
using Uygulama.Arayuzler;
using Uygulama.Dto.Stok360;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class Stok360Servisi : IStok360Servisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public Stok360Servisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private void YetkiKontrol(Func<EtkiAlani.Modeller.Kullanici.KullaniciYetkileri, bool> yetki, string islem)
    {
        if (_kullaniciServisi?.AktifKullanici == null)
            throw new UnauthorizedAccessException("Bu işlem için geçerli kullanıcı oturumu gerekiyor.");
        _kullaniciServisi.YetkiKontrolEt(yetki, islem);
    }

    public async Task<UrunStokOzetDto?> GetUrunOzetAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.StokGoruntule, "Stok 360° Görüntüleme");

        var urun = await _baglam.Urunler.AsNoTracking()
            .Include(u => u.KategoriTanimi)
            .Include(u => u.MarkaTanimi)
            .Include(u => u.BirimTanimi)
            .Include(u => u.LokasyonStoklari)
            .FirstOrDefaultAsync(u => u.Id == urunId && (u.SirketId == null || u.SirketId == sirketId), cancellationToken);

        if (urun == null) return null;

        var lokasyonlar = urun.LokasyonStoklari.ToList();
        decimal fiziksel = lokasyonlar.Sum(l => l.MevcutMiktar);
        decimal rezerve = lokasyonlar.Sum(l => l.RezerveMiktar);
        decimal kullanilabilir = Math.Max(0, fiziksel - rezerve);

        string durum = fiziksel <= 0 ? "StokYok" : (fiziksel <= urun.MinimumStok ? "Kritik" : "Normal");

        var sonAlis = await _baglam.FaturaKalemleri.AsNoTracking()
            .Where(fk => fk.UrunId == urunId && fk.Fatura != null && fk.Fatura.SirketId == sirketId && fk.Fatura.Tur == FaturaTuru.Alis && fk.Fatura.Durum == BelgeDurumu.Onaylandi)
            .MaxAsync(fk => (DateTime?)fk.Fatura!.Tarih, cancellationToken);

        var sonSatis = await _baglam.FaturaKalemleri.AsNoTracking()
            .Where(fk => fk.UrunId == urunId && fk.Fatura != null && fk.Fatura.SirketId == sirketId && fk.Fatura.Tur == FaturaTuru.Satis && fk.Fatura.Durum == BelgeDurumu.Onaylandi)
            .MaxAsync(fk => (DateTime?)fk.Fatura!.Tarih, cancellationToken);

        return new UrunStokOzetDto(
            urun.Id,
            urun.Kod,
            urun.Ad,
            urun.Barkod,
            urun.KategoriTanimi?.Ad ?? urun.Kategori,
            urun.MarkaTanimi?.Ad,
            urun.BirimTanimi?.Ad ?? urun.Birim,
            urun.AlisFiyati,
            urun.PerakendeSatisFiyati,
            urun.ToptanSatisFiyati,
            urun.KdvOrani,
            fiziksel,
            rezerve,
            kullanilabilir,
            urun.MinimumStok,
            urun.Aktif,
            durum,
            sonAlis,
            sonSatis
        );
    }

    public async Task<List<LokasyonStokOzetDto>> GetLokasyonStoklariAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.StokGoruntule, "Stok Lokasyonları Görüntüleme");

        var depolar = await _baglam.Depolar.AsNoTracking()
            .Where(d => d.AktifMi)
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var lokasyonStoklari = await _baglam.UrunLokasyonStoklari.AsNoTracking()
            .Where(l => l.UrunId == urunId)
            .ToListAsync(cancellationToken);

        var urun = await _baglam.Urunler.AsNoTracking().FirstOrDefaultAsync(u => u.Id == urunId, cancellationToken);
        decimal minStok = urun?.MinimumStok ?? 0;

        var result = new List<LokasyonStokOzetDto>();

        foreach (var depo in depolar)
        {
            var l = lokasyonStoklari.FirstOrDefault(x => x.DepoId == depo.Id);
            decimal fiziksel = l?.MevcutMiktar ?? 0;
            decimal rezerve = l?.RezerveMiktar ?? 0;
            decimal kullanilabilir = Math.Max(0, fiziksel - rezerve);

            var sonHareket = await _baglam.StokHareketleri.AsNoTracking()
                .Where(sh => sh.UrunId == urunId && (sh.DepoId == depo.Id || sh.HedefDepoId == depo.Id))
                .MaxAsync(sh => (DateTime?)sh.Tarih, cancellationToken);

            string durum = fiziksel <= 0 ? "Stok Yok" : (fiziksel <= minStok ? "Kritik" : "Normal");

            result.Add(new LokasyonStokOzetDto(
                depo.Id,
                depo.DepoKodu,
                depo.DepoAdi,
                depo.DepoTuru.ToString(),
                fiziksel,
                rezerve,
                kullanilabilir,
                minStok,
                durum,
                sonHareket
            ));
        }

        return result;
    }

    public async Task<List<YuruyenStokSatirDto>> GetYuruyenStokHareketleriAsync(int urunId, Guid sirketId, UrunStok360Sorgu sorgu, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.StokGoruntule, "Stok Hareketleri Görüntüleme");

        var q = _baglam.StokHareketleri.AsNoTracking()
            .Include(sh => sh.Depo)
            .Include(sh => sh.HedefDepo)
            .Include(sh => sh.CariKart)
            .Where(sh => sh.UrunId == urunId && (sh.SirketId == null || sh.SirketId == sirketId));

        if (!sorgu.IptalleriGoster)
            q = q.Where(sh => !sh.IptalEdildi);

        if (sorgu.DepoId.HasValue && sorgu.DepoId > 0)
            q = q.Where(sh => sh.DepoId == sorgu.DepoId || sh.HedefDepoId == sorgu.DepoId);

        if (sorgu.HareketTuru.HasValue)
            q = q.Where(sh => sh.HareketTuru == sorgu.HareketTuru.Value);

        if (sorgu.IslemTuru.HasValue)
            q = q.Where(sh => sh.IslemTuru == sorgu.IslemTuru.Value);

        if (sorgu.CariKartId.HasValue)
            q = q.Where(sh => sh.CariKartId == sorgu.CariKartId.Value);

        if (!string.IsNullOrWhiteSpace(sorgu.BelgeNo))
            q = q.Where(sh => (sh.BelgeNo != null && EF.Functions.ILike(sh.BelgeNo, $"%{sorgu.BelgeNo}%")) || (sh.FisNo != null && EF.Functions.ILike(sh.FisNo, $"%{sorgu.BelgeNo}%")));

        // Calculate Devir Stok if Baslangic date is specified
        decimal devirStok = 0;
        if (sorgu.Baslangic.HasValue)
        {
            var d = DateTime.SpecifyKind(sorgu.Baslangic.Value, DateTimeKind.Utc);
            var devirQ = _baglam.StokHareketleri.AsNoTracking()
                .Where(sh => sh.UrunId == urunId && !sh.IptalEdildi && sh.Tarih < d && (sh.SirketId == null || sh.SirketId == sirketId));
            if (sorgu.DepoId.HasValue && sorgu.DepoId > 0)
                devirQ = devirQ.Where(sh => sh.DepoId == sorgu.DepoId);

            var devirGiris = await devirQ.Where(sh => sh.HareketTuru == StokHareketTuru.Giris).SumAsync(sh => sh.Miktar, cancellationToken);
            var devirCikis = await devirQ.Where(sh => sh.HareketTuru == StokHareketTuru.Cikis).SumAsync(sh => sh.Miktar, cancellationToken);
            devirStok = devirGiris - devirCikis;

            q = q.Where(sh => sh.Tarih >= d);
        }

        if (sorgu.Bitis.HasValue)
        {
            var d = DateTime.SpecifyKind(sorgu.Bitis.Value, DateTimeKind.Utc).AddDays(1);
            q = q.Where(sh => sh.Tarih < d);
        }

        var tumHareketler = await q.OrderBy(sh => sh.Tarih).ThenBy(sh => sh.Id).ToListAsync(cancellationToken);

        // Yürüyen bakiye hesapla
        decimal bakiye = devirStok;
        var list = new List<YuruyenStokSatirDto>();

        foreach (var sh in tumHareketler)
        {
            decimal giris = sh.HareketTuru == StokHareketTuru.Giris ? sh.Miktar : 0;
            decimal cikis = sh.HareketTuru == StokHareketTuru.Cikis ? sh.Miktar : 0;

            if (!sh.IptalEdildi)
                bakiye += (giris - cikis);

            list.Add(new YuruyenStokSatirDto(
                sh.Id,
                sh.Tarih,
                sh.FisNo ?? $"STK-{sh.Id:D5}",
                sh.BelgeNo ?? "",
                sh.HareketTuru == StokHareketTuru.Giris ? "Giriş" : "Çıkış",
                sh.HareketTuru,
                sh.IslemTuru,
                IslemTuruMetni(sh.IslemTuru),
                sh.Depo?.DepoAdi ?? "Genel Depo",
                sh.HedefDepo?.DepoAdi ?? "-",
                sh.CariKart?.Unvan ?? "-",
                giris,
                cikis,
                bakiye,
                sh.BirimFiyat,
                sh.Miktar * sh.BirimFiyat,
                sh.Aciklama,
                sh.IptalEdildi,
                "Sistem",
                sh.IlgiliBelgeId
            ));
        }

        // Apply pagination in reverse (newest first)
        return list.OrderByDescending(x => x.Tarih).ThenByDescending(x => x.Id)
            .Skip((sorgu.Sayfa - 1) * sorgu.SayfaBoyutu)
            .Take(sorgu.SayfaBoyutu)
            .ToList();
    }

    private static string IslemTuruMetni(StokIslemTuru tur) => tur switch
    {
        StokIslemTuru.AlisFaturasi => "Alış Faturası",
        StokIslemTuru.SatisFaturasi => "Satış Faturası",
        StokIslemTuru.AlisIrsaliyesi => "Alış İrsaliyesi",
        StokIslemTuru.SatisIrsaliyesi => "Satış İrsaliyesi",
        StokIslemTuru.DepoTransferi => "Depo Transferi",
        StokIslemTuru.SayimFarki => "Sayım Farkı",
        StokIslemTuru.SarfFire => "Sarf / Fire",
        StokIslemTuru.DevirGiris => "Devir Giriş",
        _ => "Manuel İşlem"
    };

    public async Task<List<UrunSatisGecmisDto>> GetSatisGecmisiAsync(int urunId, Guid sirketId, UrunStok360Sorgu sorgu, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.SatisGecmisiGoruntule || y.FaturaGoruntule, "Satış Geçmişi Görüntüleme");

        var q = _baglam.FaturaKalemleri.AsNoTracking()
            .Include(fk => fk.Fatura)
            .ThenInclude(f => f!.CariKart)
            .Where(fk => fk.UrunId == urunId && fk.Fatura != null && fk.Fatura.SirketId == sirketId && fk.Fatura.Tur == FaturaTuru.Satis && fk.Fatura.Durum == BelgeDurumu.Onaylandi);

        if (sorgu.Baslangic.HasValue)
        {
            var d = DateTime.SpecifyKind(sorgu.Baslangic.Value, DateTimeKind.Utc);
            q = q.Where(fk => fk.Fatura!.Tarih >= d);
        }
        if (sorgu.Bitis.HasValue)
        {
            var d = DateTime.SpecifyKind(sorgu.Bitis.Value, DateTimeKind.Utc).AddDays(1);
            q = q.Where(fk => fk.Fatura!.Tarih < d);
        }

        return await q.OrderByDescending(fk => fk.Fatura!.Tarih)
            .Skip((sorgu.Sayfa - 1) * sorgu.SayfaBoyutu)
            .Take(sorgu.SayfaBoyutu)
            .Select(fk => new UrunSatisGecmisDto(
                fk.FaturaId,
                fk.Fatura!.Tarih,
                fk.Fatura.FaturaNo,
                fk.Fatura.CariKart != null ? fk.Fatura.CariKart.Unvan : "-",
                fk.Miktar,
                fk.BirimFiyat,
                fk.IskontoOrani,
                fk.BirimFiyat * (1 - (fk.IskontoOrani / 100)),
                fk.KdvTutari,
                fk.SatirToplami,
                "Mağaza"
            )).ToListAsync(cancellationToken);
    }

    public async Task<List<UrunAlisGecmisDto>> GetAlisGecmisiAsync(int urunId, Guid sirketId, UrunStok360Sorgu sorgu, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.FaturaGoruntule, "Alış Geçmişi Görüntüleme");

        var q = _baglam.FaturaKalemleri.AsNoTracking()
            .Include(fk => fk.Fatura)
            .ThenInclude(f => f!.CariKart)
            .Where(fk => fk.UrunId == urunId && fk.Fatura != null && fk.Fatura.SirketId == sirketId && fk.Fatura.Tur == FaturaTuru.Alis && fk.Fatura.Durum == BelgeDurumu.Onaylandi);

        if (sorgu.Baslangic.HasValue)
        {
            var d = DateTime.SpecifyKind(sorgu.Baslangic.Value, DateTimeKind.Utc);
            q = q.Where(fk => fk.Fatura!.Tarih >= d);
        }
        if (sorgu.Bitis.HasValue)
        {
            var d = DateTime.SpecifyKind(sorgu.Bitis.Value, DateTimeKind.Utc).AddDays(1);
            q = q.Where(fk => fk.Fatura!.Tarih < d);
        }

        return await q.OrderByDescending(fk => fk.Fatura!.Tarih)
            .Skip((sorgu.Sayfa - 1) * sorgu.SayfaBoyutu)
            .Take(sorgu.SayfaBoyutu)
            .Select(fk => new UrunAlisGecmisDto(
                fk.FaturaId,
                fk.Fatura!.Tarih,
                fk.Fatura.FaturaNo,
                fk.Fatura.CariKart != null ? fk.Fatura.CariKart.Unvan : "-",
                fk.Miktar,
                fk.BirimFiyat,
                fk.IskontoOrani,
                fk.BirimFiyat * (1 - (fk.IskontoOrani / 100)),
                fk.KdvTutari,
                fk.SatirToplami,
                "Depo 1"
            )).ToListAsync(cancellationToken);
    }

    public async Task<List<UrunSiparisRezervasyonDto>> GetSiparisRezervasyonlariAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.SatisGecmisiGoruntule, "Sipariş Rezervasyonları Görüntüleme");

        var q = _baglam.SiparisKalemleri.AsNoTracking()
            .Include(sk => sk.Siparis)
            .ThenInclude(s => s!.CariKart)
            .Where(sk => sk.UrunId == urunId && sk.Siparis != null && sk.Siparis.CariKart != null && sk.Siparis.CariKart.SirketId == sirketId && (sk.Siparis.Durum == SiparisDurumu.Onaylandi || sk.Siparis.Durum == SiparisDurumu.KismenKarsilandi));

        return await q.OrderByDescending(sk => sk.Siparis!.SiparisTarihi)
            .Select(sk => new UrunSiparisRezervasyonDto(
                sk.SiparisId,
                sk.Siparis!.SiparisTarihi,
                sk.Siparis.SiparisNo,
                sk.Siparis.CariKart != null ? sk.Siparis.CariKart.Unvan : "-",
                sk.Miktar,
                sk.Miktar - sk.SevkEdilenMiktar,
                sk.Miktar - sk.SevkEdilenMiktar,
                sk.Siparis.SiparisTarihi,
                sk.Siparis.Durum.ToString(),
                "Mağaza"
            )).ToListAsync(cancellationToken);
    }

    public async Task<List<YuruyenStokSatirDto>> GetTransferGecmisiAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.StokGoruntule, "Transfer Geçmişi Görüntüleme");

        return await GetYuruyenStokHareketleriAsync(urunId, sirketId, new UrunStok360Sorgu { IslemTuru = StokIslemTuru.DepoTransferi }, cancellationToken);
    }

    public async Task<List<YuruyenStokSatirDto>> GetSayimGecmisiAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.StokGoruntule, "Sayım Geçmişi Görüntüleme");

        return await GetYuruyenStokHareketleriAsync(urunId, sirketId, new UrunStok360Sorgu { IslemTuru = StokIslemTuru.SayimFarki }, cancellationToken);
    }

    public async Task<UrunMaliyetKarlilikDto> GetMaliyetKarlilikAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.StokGoruntule, "Maliyet & Kârlılık Analizi");

        var urun = await _baglam.Urunler.AsNoTracking().FirstOrDefaultAsync(u => u.Id == urunId, cancellationToken);
        decimal sonAlisMaliyeti = urun?.AlisFiyati ?? 0;

        var tumHareketler = await _baglam.StokHareketleri.AsNoTracking()
            .Where(sh => sh.UrunId == urunId && (sh.SirketId == null || sh.SirketId == sirketId) && !sh.IptalEdildi)
            .OrderBy(sh => sh.Tarih)
            .ThenBy(sh => sh.Id)
            .ToListAsync(cancellationToken);

        decimal cariStokMiktari = 0;
        decimal cariStokDegeri = 0;
        decimal birimMaliyet = urun?.AlisFiyati ?? 0;
        decimal toplamSatilanMiktar = 0;
        decimal toplamSatisCirosu = 0;
        decimal toplamSatilanMalMaliyeti = 0;

        foreach (var h in tumHareketler)
        {
            if (h.IslemTuru == StokIslemTuru.DepoTransferi)
            {
                // Transfer moves quantity between locations; company total stock value & average cost remain unchanged
                continue;
            }

            if (h.HareketTuru == StokHareketTuru.Giris)
            {
                decimal girenFiyat = h.BirimFiyat > 0 ? h.BirimFiyat : (birimMaliyet > 0 ? birimMaliyet : urun?.AlisFiyati ?? 0);
                decimal girenTutar = h.Miktar * girenFiyat;

                cariStokMiktari += h.Miktar;
                cariStokDegeri += girenTutar;

                if (cariStokMiktari > 0)
                    birimMaliyet = cariStokDegeri / cariStokMiktari;

                if (h.BirimFiyat > 0 && (h.IslemTuru == StokIslemTuru.AlisFaturasi || h.IslemTuru == StokIslemTuru.DevirGiris || h.IslemTuru == StokIslemTuru.AlisIrsaliyesi))
                    sonAlisMaliyeti = h.BirimFiyat;
            }
            else if (h.HareketTuru == StokHareketTuru.Cikis)
            {
                decimal cikanMiktar = Math.Min(cariStokMiktari, h.Miktar);
                decimal cikanMaliyet = (cikanMiktar > 0 ? cikanMiktar : h.Miktar) * birimMaliyet;

                cariStokMiktari = Math.Max(0, cariStokMiktari - h.Miktar);
                cariStokDegeri = Math.Max(0, cariStokDegeri - cikanMaliyet);

                if (h.IslemTuru == StokIslemTuru.SatisFaturasi || h.IslemTuru == StokIslemTuru.SatisIrsaliyesi || h.IslemTuru == StokIslemTuru.SarfFire || h.IslemTuru == StokIslemTuru.Diger)
                {
                    toplamSatilanMiktar += h.Miktar;
                    toplamSatisCirosu += h.Miktar * (h.BirimFiyat > 0 ? h.BirimFiyat : urun?.PerakendeSatisFiyati ?? 0);
                    toplamSatilanMalMaliyeti += cikanMaliyet;
                }
            }
        }

        // Also fallback to FaturaKalemleri if no StokHareketleri exist yet
        if (tumHareketler.Count == 0)
        {
            var satisKalemleri = await _baglam.FaturaKalemleri.AsNoTracking()
                .Include(fk => fk.Fatura)
                .Where(fk => fk.UrunId == urunId && fk.Fatura != null && fk.Fatura.SirketId == sirketId && fk.Fatura.Tur == FaturaTuru.Satis && fk.Fatura.Durum == BelgeDurumu.Onaylandi)
                .ToListAsync(cancellationToken);

            toplamSatilanMiktar = satisKalemleri.Sum(k => k.Miktar);
            toplamSatisCirosu = satisKalemleri.Sum(k => k.SatirToplami);
            toplamSatilanMalMaliyeti = toplamSatilanMiktar * birimMaliyet;
        }

        decimal brutKar = toplamSatisCirosu - toplamSatilanMalMaliyeti;
        decimal brutKarYuzdesi = toplamSatisCirosu > 0 ? (brutKar / toplamSatisCirosu) * 100 : 0;

        return new UrunMaliyetKarlilikDto(
            sonAlisMaliyeti,
            birimMaliyet,
            toplamSatilanMiktar,
            toplamSatisCirosu,
            toplamSatilanMalMaliyeti,
            brutKar,
            brutKarYuzdesi
        );
    }
}
