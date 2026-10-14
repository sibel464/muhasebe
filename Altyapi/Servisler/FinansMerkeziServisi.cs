using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;

namespace Altyapi.Servisler;

public sealed class FinansMerkeziServisi : IFinansMerkeziServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi _kullanici;

    public FinansMerkeziServisi(MuhasebeBaglami baglam, IKullaniciServisi kullanici)
    {
        _baglam = baglam;
        _kullanici = kullanici;
    }

    public async Task<FinansDashboardOzetDto> GetDashboardOzetAsync(Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.FinansGoruntule, "Finans Görüntüleme");

        var bugun = DateTime.UtcNow.Date;
        var yarinki = bugun.AddDays(1);

        var kasalar = await GetKasalarAsync(sirketId, cancellationToken);
        var bankalar = await GetBankaHesaplariAsync(sirketId, cancellationToken);

        var toplamKasa = kasalar.Sum(x => x.MevcutBakiye);
        var toplamBanka = bankalar.Sum(x => x.Bakiye);

        // Bugünkü Tahsilat ve Ödeme (Kasa + Banka)
        var bugunkiKasaGelir = await _baglam.KasaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.Tur == HareketTuru.Gelir && x.Tarih >= bugun && x.Tarih < yarinki)
            .SumAsync(x => (decimal?)x.Tutar, cancellationToken) ?? 0m;

        var bugunkiKasaGider = await _baglam.KasaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.Tur == HareketTuru.Gider && x.Tarih >= bugun && x.Tarih < yarinki)
            .SumAsync(x => (decimal?)x.Tutar, cancellationToken) ?? 0m;

        var bugunkiBankaGelir = await _baglam.BankaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.Tur == HareketTuru.Gelir && x.Tarih >= bugun && x.Tarih < yarinki)
            .SumAsync(x => (decimal?)x.Tutar, cancellationToken) ?? 0m;

        var bugunkiBankaGider = await _baglam.BankaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.Tur == HareketTuru.Gider && x.Tarih >= bugun && x.Tarih < yarinki)
            .SumAsync(x => (decimal?)x.Tutar, cancellationToken) ?? 0m;

        var bugunkiTahsilat = bugunkiKasaGelir + bugunkiBankaGelir;
        var bugunkiOdeme = bugunkiKasaGider + bugunkiBankaGider;

        // Vade Merkezi Kalemleri
        var vadeler = await GetVadeMerkeziAsync(sirketId, cancellationToken);
        var beklenenTahsilat = vadeler.Where(x => x.YonetimTuru == "Alacak" && x.VadeTarihi >= bugun).Sum(x => x.KalanTutar);
        var beklenenOdeme = vadeler.Where(x => x.YonetimTuru == "Borc" && x.VadeTarihi >= bugun).Sum(x => x.KalanTutar);
        var vadesiGecenAlacak = vadeler.Where(x => x.YonetimTuru == "Alacak" && x.VadeTarihi < bugun).Sum(x => x.KalanTutar);
        var vadesiGecenBorc = vadeler.Where(x => x.YonetimTuru == "Borc" && x.VadeTarihi < bugun).Sum(x => x.KalanTutar);

        // Son 10 Finansal Hareket
        var globalHareketler = await GetGlobalFinansHareketleriAsync(sirketId, new GlobalFinansFiltreDto { SayfaBoyutu = 10 }, cancellationToken);

        // Çek/Senet Durumu
        var cekSenetler = await _baglam.CekSenetler
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId)
            .ToListAsync(cancellationToken);

        var alinanCek = cekSenetler.Where(x => x.Tur == CekSenetTuru.AlinanCek && x.Durum == CekSenetDurumu.Portfoyde).ToList();
        var verilenCek = cekSenetler.Where(x => x.Tur == CekSenetTuru.VerilenCek && x.Durum == CekSenetDurumu.Portfoyde).ToList();
        var alinanSenet = cekSenetler.Where(x => x.Tur == CekSenetTuru.AlinanSenet && x.Durum == CekSenetDurumu.Portfoyde).ToList();
        var verilenSenet = cekSenetler.Where(x => x.Tur == CekSenetTuru.VerilenSenet && x.Durum == CekSenetDurumu.Portfoyde).ToList();

        var cekSenetOzet = new CekSenetOzetDto(
            PortfoydekiAlinanCekSayisi: alinanCek.Count,
            PortfoydekiAlinanCekTutar: alinanCek.Sum(x => x.Tutar),
            PortfoydekiVerilenCekSayisi: verilenCek.Count,
            PortfoydekiVerilenCekTutar: verilenCek.Sum(x => x.Tutar),
            PortfoydekiAlinanSenetSayisi: alinanSenet.Count,
            PortfoydekiAlinanSenetTutar: alinanSenet.Sum(x => x.Tutar),
            PortfoydekiVerilenSenetSayisi: verilenSenet.Count,
            PortfoydekiVerilenSenetTutar: verilenSenet.Sum(x => x.Tutar)
        );

        return new FinansDashboardOzetDto
        {
            ToplamKasa = toplamKasa,
            ToplamBanka = toplamBanka,
            BugunkiTahsilat = bugunkiTahsilat,
            BugunkiOdeme = bugunkiOdeme,
            BeklenenTahsilat = beklenenTahsilat,
            BeklenenOdeme = beklenenOdeme,
            VadesiGecenAlacak = vadesiGecenAlacak,
            VadesiGecenBorc = vadesiGecenBorc,
            KasaDagilimi = kasalar,
            BankaHesapBakiyeleri = bankalar,
            SonFinansalHareketler = globalHareketler.Items,
            YaklasanVadeler = vadeler.Where(x => x.VadeTarihi >= bugun).OrderBy(x => x.VadeTarihi).Take(5).ToList(),
            VadesiGecenler = vadeler.Where(x => x.VadeTarihi < bugun).OrderBy(x => x.VadeTarihi).Take(5).ToList(),
            CekSenetDurumu = cekSenetOzet
        };
    }

    public async Task<List<KasaOzetDto>> GetKasalarAsync(Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.KasaGoruntule, "Kasa Görüntüleme");

        var bugun = DateTime.UtcNow.Date;
        var yarinki = bugun.AddDays(1);

        var kasalar = await _baglam.Kasalar
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId)
            .OrderBy(x => x.Kod)
            .ToListAsync(cancellationToken);

        var kasaIds = kasalar.Select(k => k.Id).ToList();

        var hareketler = await _baglam.KasaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.KasaId.HasValue && kasaIds.Contains(x.KasaId.Value) && x.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .Select(h => new { h.KasaId, h.Tur, h.Tutar, h.Tarih })
            .ToListAsync(cancellationToken);

        var liste = new List<KasaOzetDto>();
        foreach (var k in kasalar)
        {
            var kHareketler = hareketler.Where(h => h.KasaId == k.Id).ToList();
            var bakiye = kHareketler.Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar);
            var bugunkiGiris = kHareketler.Where(h => h.Tur == HareketTuru.Gelir && h.Tarih >= bugun && h.Tarih < yarinki).Sum(h => h.Tutar);
            var bugunkiCikis = kHareketler.Where(h => h.Tur == HareketTuru.Gider && h.Tarih >= bugun && h.Tarih < yarinki).Sum(h => h.Tutar);
            var sonIslem = kHareketler.OrderByDescending(h => h.Tarih).Select(h => (DateTime?)h.Tarih).FirstOrDefault();

            liste.Add(new KasaOzetDto(
                Id: k.Id,
                KasaKodu: k.Kod,
                KasaAdi: k.Ad,
                ParaBirimi: k.ParaBirimi,
                SubeAdi: "Merkez",
                MevcutBakiye: bakiye,
                BugunkiGiris: bugunkiGiris,
                BugunkiCikis: bugunkiCikis,
                SonIslemTarihi: sonIslem,
                Aktif: k.AktifMi
            ));
        }

        return liste;
    }

    public async Task<List<BankaHesapOzetDto>> GetBankaHesaplariAsync(Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.BankaGoruntule, "Banka Görüntüleme");

        var hesaplar = await _baglam.BankaHesaplari
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId)
            .OrderBy(x => x.Ad)
            .ToListAsync(cancellationToken);

        var hesapIds = hesaplar.Select(h => h.Id).ToList();

        var hareketler = await _baglam.BankaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.BankaHesabiId.HasValue && hesapIds.Contains(x.BankaHesabiId.Value) && x.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .Select(h => new { h.BankaHesabiId, h.Tur, h.Tutar, h.Tarih })
            .ToListAsync(cancellationToken);

        var liste = new List<BankaHesapOzetDto>();
        foreach (var h in hesaplar)
        {
            var hHareketler = hareketler.Where(x => x.BankaHesabiId == h.Id).ToList();
            var bakiye = hHareketler.Sum(x => x.Tur == HareketTuru.Gelir ? x.Tutar : -x.Tutar);
            var sonIslem = hHareketler.OrderByDescending(x => x.Tarih).Select(x => (DateTime?)x.Tarih).FirstOrDefault();

            liste.Add(new BankaHesapOzetDto(
                Id: h.Id,
                BankaAdi: h.Ad,
                HesapKodu: h.Kod,
                HesapAdi: h.Ad,
                Iban: h.Iban,
                HesapNo: h.HesapNo,
                Sube: string.IsNullOrWhiteSpace(h.Sube) ? "Merkez" : h.Sube,
                ParaBirimi: h.ParaBirimi,
                Bakiye: bakiye,
                KullanilabilirBakiye: bakiye - h.BlokeBakiye,
                Aciklama: h.MuhasebeKodu ?? string.Empty,
                Aktif: h.AktifMi
            ));
        }

        return liste;
    }

    public async Task<List<YuruyenFinansSatirDto>> GetKasaHareketleri360Async(int kasaId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.KasaGoruntule, "Kasa Görüntüleme");

        var hareketler = await _baglam.KasaHareketleri
            .AsNoTracking()
            .Include(x => x.CariKart)
            .Where(x => x.SirketId == sirketId && x.KasaId == kasaId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .OrderBy(x => x.Tarih).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        decimal yuruyenBakiye = 0m;
        var sonuc = new List<YuruyenFinansSatirDto>();

        foreach (var h in hareketler)
        {
            var giris = h.Tur == HareketTuru.Gelir ? h.Tutar : 0m;
            var cikis = h.Tur == HareketTuru.Gider ? h.Tutar : 0m;
            yuruyenBakiye += (giris - cikis);

            sonuc.Add(new YuruyenFinansSatirDto(
                Id: h.Id,
                Kaynak: "Kasa",
                Tarih: h.Tarih,
                Saat: h.Tarih.ToString("HH:mm"),
                IslemNo: $"KSH-{h.Id:D6}",
                IslemTuru: h.Tur == HareketTuru.Gelir ? "Kasa Giriş" : "Kasa Çıkış",
                CariKartId: h.CariKartId,
                CariKod: h.CariKart?.Kod ?? "-",
                CariUnvan: h.CariKart?.Unvan ?? "-",
                KaynakAdi: "Kasa",
                HedefAdi: "-",
                BelgeNo: h.Belge ?? "-",
                Aciklama: h.Aciklama ?? string.Empty,
                Giris: giris,
                Cikis: cikis,
                Bakiye: yuruyenBakiye,
                Tutar: h.Tutar,
                ParaBirimi: "TRY",
                KullaniciAdi: "Sistem",
                Durum: h.IslemDurumu.ToString()
            ));
        }

        sonuc.Reverse(); // En yeni tarih en üstte görünüm için
        return sonuc;
    }

    public async Task<List<YuruyenFinansSatirDto>> GetBankaHareketleri360Async(int bankaHesapId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.BankaGoruntule, "Banka Görüntüleme");

        var hareketler = await _baglam.BankaHareketleri
            .AsNoTracking()
            .Include(x => x.CariKart)
            .Where(x => x.SirketId == sirketId && x.BankaHesabiId == bankaHesapId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .OrderBy(x => x.Tarih).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        decimal yuruyenBakiye = 0m;
        var sonuc = new List<YuruyenFinansSatirDto>();

        foreach (var h in hareketler)
        {
            var giris = h.Tur == HareketTuru.Gelir ? h.Tutar : 0m;
            var cikis = h.Tur == HareketTuru.Gider ? h.Tutar : 0m;
            yuruyenBakiye += (giris - cikis);

            sonuc.Add(new YuruyenFinansSatirDto(
                Id: h.Id,
                Kaynak: "Banka",
                Tarih: h.Tarih,
                Saat: h.Tarih.ToString("HH:mm"),
                IslemNo: $"BNK-{h.Id:D6}",
                IslemTuru: h.Tur == HareketTuru.Gelir ? "Banka Giriş" : "Banka Çıkış",
                CariKartId: h.CariKartId,
                CariKod: h.CariKart?.Kod ?? "-",
                CariUnvan: h.CariKart?.Unvan ?? "-",
                KaynakAdi: h.BankaAdi ?? "Banka",
                HedefAdi: h.HesapNo ?? "-",
                BelgeNo: "-",
                Aciklama: h.Aciklama ?? string.Empty,
                Giris: giris,
                Cikis: cikis,
                Bakiye: yuruyenBakiye,
                Tutar: h.Tutar,
                ParaBirimi: "TRY",
                KullaniciAdi: "Sistem",
                Durum: h.IslemDurumu.ToString()
            ));
        }

        sonuc.Reverse();
        return sonuc;
    }

    public async Task<List<PosIslemOzetDto>> GetPosIslemleriAsync(Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.POSYonet || x.FinansGoruntule, "POS İşlemleri");

        var poslar = await _baglam.PosIslemleri
            .AsNoTracking()
            .Include(x => x.BankaHesabi)
            .Where(x => x.SirketId == sirketId)
            .OrderByDescending(x => x.IslemTarihi)
            .ToListAsync(cancellationToken);

        return poslar.Select(p => new PosIslemOzetDto(
            Id: p.Id,
            BankaAdi: p.BankaHesabi?.Ad ?? p.BankaAdi ?? "Banka",
            PosCihazi: p.PosCihazAdi,
            KartTuru: p.KartTuru,
            TaksitSayisi: p.TaksitSayisi,
            BrutTutar: p.BrutTutar,
            KomisyonOrani: p.KomisyonOrani,
            KomisyonTutari: p.KomisyonTutar,
            NetTutar: p.NetTutar,
            IslemTarihi: p.IslemTarihi,
            BeklenenVadeTarihi: p.BeklenenHesabaGecisTarihi,
            AktarimTarihi: p.HesabaGecisTarihi,
            Durum: p.Durum
        )).ToList();
    }

    public async Task PosIslemiHesabaGecirAsync(int posIslemId, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.POSYonet, "POS Yönetimi");

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var pos = await _baglam.PosIslemleri
                .Include(p => p.BankaHesabi)
                .SingleOrDefaultAsync(p => p.Id == posIslemId && p.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("POS işlemi bulunamadı.");

            if (pos.Durum == PosIslemDurumu.HesabaGecti)
                throw new InvalidOperationException("Bu POS işlemi zaten hesaba geçirilmiştir.");

            if (pos.Durum == PosIslemDurumu.Iptal)
                throw new InvalidOperationException("İptal edilen POS işlemi hesaba geçirilemez.");

            pos.Durum = PosIslemDurumu.HesabaGecti;
            pos.HesabaGecisTarihi = DateTime.UtcNow;

            // Banka Hareketi Gelir ekle (Net Tutar)
            var bh = new BankaHareketi
            {
                SirketId = sirketId,
                BankaHesabiId = pos.BankaHesabiId,
                BankaAdi = pos.BankaHesabi?.Ad ?? pos.BankaAdi,
                HesapNo = pos.BankaHesabi?.HesapNo,
                Tarih = DateTime.UtcNow,
                Tur = HareketTuru.Gelir,
                OdemeYontemi = OdemeYontemi.KrediKarti,
                Tutar = pos.NetTutar,
                Aciklama = $"POS Tahsilat Aktarımı ({pos.PosCihazAdi} - {pos.KartTuru})",
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            _baglam.BankaHareketleri.Add(bh);
            await _baglam.SaveChangesAsync(cancellationToken);

            // Yevmiye Fişi (102 Banka Borç / 108 POS Alacak)
            var bankaHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(x => x.SirketId == sirketId && x.HesapKodu == "102", cancellationToken);
            var posHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(x => x.SirketId == sirketId && x.HesapKodu == "108", cancellationToken);

            if (bankaHesap != null && posHesap != null)
            {
                _baglam.YevmiyeFisleri.Add(new YevmiyeFis
                {
                    SirketId = sirketId,
                    FisNo = $"POS-{pos.Id:D6}",
                    Tarih = DateTime.UtcNow,
                    BelgeTipi = "POSAktarim",
                    BelgeId = pos.Id,
                    Aciklama = $"POS Aktarımı - {pos.PosCihazAdi}",
                    Durum = YevmiyeDurumu.Onaylandi,
                    Satirlar = new List<YevmiyeSatiri>
                    {
                        new() { HesapId = bankaHesap.Id, Borc = pos.NetTutar, Aciklama = "Banka Hesabına Geçen POS Net Tutar" },
                        new() { HesapId = posHesap.Id, Alacak = pos.NetTutar, Aciklama = "POS Çözülme Alacağı" }
                    }
                });
                await _baglam.SaveChangesAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<VadeKalemOzetDto>> GetVadeMerkeziAsync(Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.FinansGoruntule, "Finans Görüntüleme");

        var bugun = DateTime.UtcNow.Date;
        var sonuc = new List<VadeKalemOzetDto>();

        // 1. Açık Faturalar
        var faturalar = await _baglam.Faturalar
            .AsNoTracking()
            .Include(f => f.CariKart)
            .Where(f => f.SirketId == sirketId && f.Durum == BelgeDurumu.Onaylandi)
            .ToListAsync(cancellationToken);

        foreach (var f in faturalar)
        {
            var vade = (f.VadeTarihi ?? f.Tarih).Date;
            var gecikme = (bugun - vade).Days;
            var yonetimTuru = f.Tur == FaturaTuru.Satis ? "Alacak" : "Borc";

            sonuc.Add(new VadeKalemOzetDto(
                Id: f.Id,
                BelgeTuru: f.Tur == FaturaTuru.Satis ? "Satış Faturası" : "Alış Faturası",
                YonetimTuru: yonetimTuru,
                CariKartId: f.CariKartId,
                CariKod: f.CariKart?.Kod ?? "-",
                CariUnvan: f.CariKart?.Unvan ?? "-",
                BelgeNo: f.FaturaNo,
                VadeTarihi: vade,
                Tutar: f.GenelToplam,
                KalanTutar: f.GenelToplam,
                GecikmeGunSayisi: gecikme > 0 ? gecikme : 0,
                Durum: gecikme > 0 ? "Vadesi Geçmiş" : (gecikme == 0 ? "Bugün" : "Gelecek Vade")
            ));
        }

        // 2. Portföydeki Çek / Senetler
        var cekSenetler = await _baglam.CekSenetler
            .AsNoTracking()
            .Include(c => c.CariKart)
            .Where(c => c.SirketId == sirketId && c.Durum == CekSenetDurumu.Portfoyde)
            .ToListAsync(cancellationToken);

        foreach (var c in cekSenetler)
        {
            var vade = c.VadeTarihi.Date;
            var gecikme = (bugun - vade).Days;
            var isAlacak = c.Tur == CekSenetTuru.AlinanCek || c.Tur == CekSenetTuru.AlinanSenet;
            var yonetimTuru = isAlacak ? "Alacak" : "Borc";

            sonuc.Add(new VadeKalemOzetDto(
                Id: c.Id,
                BelgeTuru: c.Tur.ToString(),
                YonetimTuru: yonetimTuru,
                CariKartId: c.CariKartId,
                CariKod: c.CariKart?.Kod ?? "-",
                CariUnvan: c.CariKart?.Unvan ?? "-",
                BelgeNo: c.BelgeNo,
                VadeTarihi: vade,
                Tutar: c.Tutar,
                KalanTutar: c.Tutar,
                GecikmeGunSayisi: gecikme > 0 ? gecikme : 0,
                Durum: gecikme > 0 ? "Vadesi Geçmiş" : (gecikme == 0 ? "Bugün" : "Gelecek Vade")
            ));
        }

        return sonuc.OrderBy(x => x.VadeTarihi).ToList();
    }

    public async Task KasaMutabakatKaydetAsync(KasaMutabakatKayitDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.Mutabakat, "Mutabakat İşlemleri");

        if (dto.KasaId <= 0) throw new ArgumentException("Geçerli bir kasa seçiniz.");
        if (dto.SayimMiktari < 0) throw new ArgumentException("Sayım tutarı negatif olamaz.");
        var aciklama = dto.Aciklama?.Trim() ?? string.Empty;
        var tarih = DateTime.UtcNow;

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await FinansKurallari.KasaKilitleAsync(_baglam, dto.KasaId);
            var kasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == dto.KasaId && k.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Kasa bulunamadı.");
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);

            var sistemBakiyesi = await FinansKurallari.KasaBakiyesiAsync(_baglam, kasa.Id);
            var fark = dto.SayimMiktari - sistemBakiyesi;

            var mutabakat = new KasaSayimMutabakat
            {
                SirketId = sirketId,
                KasaId = dto.KasaId,
                Tarih = tarih,
                SistemBakiyesi = sistemBakiyesi,
                BozukParaToplam = dto.SayimMiktari,
                Aciklama = aciklama,
                OlusturanKullaniciId = kullaniciId
            };
            _baglam.KasaSayimMutabakatlari.Add(mutabakat);
            await _baglam.SaveChangesAsync(cancellationToken);

            if (dto.DuzeltmeHareketOlustur && fark != 0)
            {
                _baglam.KasaHareketleri.Add(new KasaHareketi
                {
                    SirketId = sirketId,
                    KasaId = dto.KasaId,
                    Tarih = tarih,
                    Tur = fark > 0 ? HareketTuru.Gelir : HareketTuru.Gider,
                    FisTuru = KasaFisTuru.SayimFarki,
                    FisNo = FinansKurallari.FisNoUret("SYM"),
                    Tutar = Math.Abs(fark),
                    Aciklama = $"Kasa Mutabakat Düzeltmesi ({aciklama})",
                    Belge = $"MUT-{mutabakat.Id}",
                    IlgiliBelgeId = mutabakat.Id,
                    IslemDurumu = FinansIslemDurumu.Tamamlandi
                });
                await _baglam.SaveChangesAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task KasaBankaTransferiYapAsync(KasaBankaTransferIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.VirmanYap, "Virman Yapma");
        FinansKurallari.TutarDogrula(dto.Tutar, "Transfer");

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await FinansKurallari.KasaKilitleAsync(_baglam, dto.KasaId);
            await FinansKurallari.BankaHesabiKilitleAsync(_baglam, dto.BankaHesapId);
            var kasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == dto.KasaId && k.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Kaynak kasa bulunamadı.");
            var banka = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == dto.BankaHesapId && b.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Hedef banka hesabı bulunamadı.");
            TransferHesaplariniDogrula(kasa.AktifMi, kasa.Ad, banka.AktifMi, banka.Ad, kasa.ParaBirimi, banka.ParaBirimi);

            var tarih = DateTime.UtcNow;
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
            await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, dto.Tutar);

            var islemNo = FinansKurallari.FisNoUret("KBT");
            var aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? $"Kasa ↔ Banka Transferi ({islemNo})" : FinansKurallari.AciklamaDogrula(dto.Aciklama);

            var kh = new KasaHareketi
            {
                SirketId = sirketId,
                KasaId = dto.KasaId,
                Tarih = tarih,
                Tur = HareketTuru.Gider,
                FisTuru = KasaFisTuru.BankayaYatirma,
                FisNo = islemNo,
                Tutar = dto.Tutar,
                Aciklama = aciklama,
                Belge = islemNo,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            var bh = new BankaHareketi
            {
                SirketId = sirketId,
                BankaHesabiId = dto.BankaHesapId,
                BankaAdi = banka.Ad,
                HesapNo = banka.HesapNo,
                Tarih = tarih,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.BankayaYatirma,
                FisNo = islemNo,
                OdemeYontemi = OdemeYontemi.Banka,
                Tutar = dto.Tutar,
                Aciklama = aciklama,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            _baglam.KasaHareketleri.Add(kh);
            _baglam.BankaHareketleri.Add(bh);
            await _baglam.SaveChangesAsync(cancellationToken);
            kh.TersKayitId = bh.Id;
            bh.TersKayitId = kh.Id;
            await _baglam.SaveChangesAsync(cancellationToken);

            // Yevmiye (102 Banka Borç / 100 Kasa Alacak)
            await TransferYevmiyesiEkleAsync(sirketId, islemNo, tarih, "KasaBankaTransfer", kh.Id, aciklama, "102", "100", dto.Tutar, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task BankaKasaTransferiYapAsync(BankaKasaTransferIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.VirmanYap, "Virman Yapma");
        FinansKurallari.TutarDogrula(dto.Tutar, "Transfer");

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await FinansKurallari.KasaKilitleAsync(_baglam, dto.KasaId);
            await FinansKurallari.BankaHesabiKilitleAsync(_baglam, dto.BankaHesapId);
            var banka = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == dto.BankaHesapId && b.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Kaynak banka hesabı bulunamadı.");
            var kasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == dto.KasaId && k.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Hedef kasa bulunamadı.");
            TransferHesaplariniDogrula(kasa.AktifMi, kasa.Ad, banka.AktifMi, banka.Ad, kasa.ParaBirimi, banka.ParaBirimi);

            var tarih = DateTime.UtcNow;
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, banka, dto.Tutar);

            var islemNo = FinansKurallari.FisNoUret("BKT");
            var aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? $"Banka ↔ Kasa Transferi ({islemNo})" : FinansKurallari.AciklamaDogrula(dto.Aciklama);

            var bh = new BankaHareketi
            {
                SirketId = sirketId,
                BankaHesabiId = dto.BankaHesapId,
                BankaAdi = banka.Ad,
                HesapNo = banka.HesapNo,
                Tarih = tarih,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.KasadanCekme,
                FisNo = islemNo,
                OdemeYontemi = OdemeYontemi.Banka,
                Tutar = dto.Tutar,
                Aciklama = aciklama,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            var kh = new KasaHareketi
            {
                SirketId = sirketId,
                KasaId = dto.KasaId,
                Tarih = tarih,
                Tur = HareketTuru.Gelir,
                FisTuru = KasaFisTuru.BankadanCekme,
                FisNo = islemNo,
                Tutar = dto.Tutar,
                Aciklama = aciklama,
                Belge = islemNo,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            _baglam.BankaHareketleri.Add(bh);
            _baglam.KasaHareketleri.Add(kh);
            await _baglam.SaveChangesAsync(cancellationToken);
            kh.TersKayitId = bh.Id;
            bh.TersKayitId = kh.Id;
            await _baglam.SaveChangesAsync(cancellationToken);

            // Yevmiye (100 Kasa Borç / 102 Banka Alacak)
            await TransferYevmiyesiEkleAsync(sirketId, islemNo, tarih, "BankaKasaTransfer", bh.Id, aciklama, "100", "102", dto.Tutar, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task BankaVirmaniYapAsync(BankaVirmanIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.VirmanYap, "Virman Yapma");

        if (dto.KaynakBankaHesapId == dto.HedefBankaHesapId)
            throw new InvalidOperationException("Kaynak ve hedef banka hesabı aynı olamaz.");
        FinansKurallari.TutarDogrula(dto.Tutar, "Transfer");
        if (dto.Masraf < 0) throw new ArgumentException("Virman masrafı negatif olamaz.");
        if (dto.Masraf > 0) FinansKurallari.TutarDogrula(dto.Masraf, "Virman masrafı");

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var id in new[] { dto.KaynakBankaHesapId, dto.HedefBankaHesapId }.Order())
                await FinansKurallari.BankaHesabiKilitleAsync(_baglam, id);
            var kaynak = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == dto.KaynakBankaHesapId && b.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Kaynak banka hesabı bulunamadı.");
            var hedef = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == dto.HedefBankaHesapId && b.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Hedef banka hesabı bulunamadı.");
            TransferHesaplariniDogrula(kaynak.AktifMi, kaynak.Ad, hedef.AktifMi, hedef.Ad, kaynak.ParaBirimi, hedef.ParaBirimi);

            var tarih = DateTime.UtcNow;
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, kaynak, dto.Tutar + dto.Masraf);

            var islemNo = FinansKurallari.FisNoUret("BVR");
            var aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? $"Banka Virmanı ({kaynak.Ad} -> {hedef.Ad})" : FinansKurallari.AciklamaDogrula(dto.Aciklama);

            var bhKaynak = new BankaHareketi
            {
                SirketId = sirketId,
                BankaHesabiId = dto.KaynakBankaHesapId,
                BankaAdi = kaynak.Ad,
                HesapNo = kaynak.HesapNo,
                Tarih = tarih,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.BankaVirman,
                FisNo = islemNo,
                OdemeYontemi = OdemeYontemi.Virman,
                Tutar = dto.Tutar,
                Aciklama = aciklama,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            var bhHedef = new BankaHareketi
            {
                SirketId = sirketId,
                BankaHesabiId = dto.HedefBankaHesapId,
                BankaAdi = hedef.Ad,
                HesapNo = hedef.HesapNo,
                Tarih = tarih,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.BankaVirman,
                FisNo = islemNo,
                OdemeYontemi = OdemeYontemi.Virman,
                Tutar = dto.Tutar,
                Aciklama = aciklama,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            _baglam.BankaHareketleri.Add(bhKaynak);
            _baglam.BankaHareketleri.Add(bhHedef);

            if (dto.Masraf > 0)
            {
                _baglam.BankaHareketleri.Add(new BankaHareketi
                {
                    SirketId = sirketId,
                    BankaHesabiId = dto.KaynakBankaHesapId,
                    BankaAdi = kaynak.Ad,
                    HesapNo = kaynak.HesapNo,
                    Tarih = tarih,
                    Tur = HareketTuru.Gider,
                    FisTuru = BankaFisTuru.BankaGiderMasraf,
                    FisNo = islemNo,
                    OdemeYontemi = OdemeYontemi.Banka,
                    Tutar = dto.Masraf,
                    MasrafTutari = dto.Masraf,
                    Aciklama = $"Banka Virman Masrafı ({islemNo})",
                    IslemDurumu = FinansIslemDurumu.Tamamlandi
                });
            }

            await _baglam.SaveChangesAsync(cancellationToken);
            bhKaynak.TersKayitId = bhHedef.Id;
            bhHedef.TersKayitId = bhKaynak.Id;
            await _baglam.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void TransferHesaplariniDogrula(bool birinciAktif, string birinciAd, bool ikinciAktif, string ikinciAd, string birinciParaBirimi, string ikinciParaBirimi)
    {
        if (!birinciAktif) throw new InvalidOperationException($"'{birinciAd}' pasif durumda. Pasif hesaba işlem yapılamaz.");
        if (!ikinciAktif) throw new InvalidOperationException($"'{ikinciAd}' pasif durumda. Pasif hesaba işlem yapılamaz.");
        if (!string.Equals(birinciParaBirimi, ikinciParaBirimi, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Farklı para birimindeki hesaplar arasında transfer yapılamaz.");
    }

    private async Task TransferYevmiyesiEkleAsync(Guid sirketId, string islemNo, DateTime tarih, string belgeTipi, int belgeId, string aciklama,
        string borcHesapKodu, string alacakHesapKodu, decimal tutar, CancellationToken cancellationToken)
    {
        var borcHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(x => x.SirketId == sirketId && x.HesapKodu == borcHesapKodu, cancellationToken);
        var alacakHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(x => x.SirketId == sirketId && x.HesapKodu == alacakHesapKodu, cancellationToken);
        if (borcHesap == null || alacakHesap == null)
            return;

        _baglam.YevmiyeFisleri.Add(new YevmiyeFis
        {
            SirketId = sirketId,
            FisNo = islemNo,
            Tarih = tarih,
            BelgeTipi = belgeTipi,
            BelgeId = belgeId,
            Aciklama = aciklama,
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiri>
            {
                new() { HesapId = borcHesap.Id, Borc = tutar, Aciklama = aciklama },
                new() { HesapId = alacakHesap.Id, Alacak = tutar, Aciklama = aciklama }
            }
        });
        await _baglam.SaveChangesAsync(cancellationToken);
    }

    public async Task FinansIslemIptalEtAsync(int hareketId, string kaynak, Guid sirketId, int kullaniciId, string neden, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.FinansIptal, "Finans İptal İşlemi");

        if (string.IsNullOrWhiteSpace(neden)) throw new ArgumentException("İptal nedeni zorunludur.");
        var temizNeden = FinansKurallari.AciklamaDogrula(neden);
        if (kaynak is not ("Kasa" or "Banka")) throw new ArgumentException("Geçersiz finans kaynağı.");

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var tarih = DateTime.UtcNow;
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
            int? cariKartId;
            int? cariHareketId;
            HareketTuru asilTur;
            decimal tutar;
            string? belge;

            if (kaynak == "Kasa")
            {
                var asil = await _baglam.KasaHareketleri.SingleOrDefaultAsync(x => x.Id == hareketId && x.SirketId == sirketId, cancellationToken)
                    ?? throw new KeyNotFoundException("Kasa hareketi bulunamadı.");
                IptalEdilebilirMi(asil.IslemDurumu, asil.FisTuru is KasaFisTuru.KasaVirman or KasaFisTuru.BankayaYatirma or KasaFisTuru.BankadanCekme or KasaFisTuru.SayimFarki);

                if (asil.KasaId.HasValue)
                {
                    await FinansKurallari.KasaKilitleAsync(_baglam, asil.KasaId.Value);
                    var kasa = await _baglam.Kasalar.SingleAsync(k => k.Id == asil.KasaId.Value, cancellationToken);
                    // Gelirin iptali kasadan çıkış demektir; bakiye yetmeli.
                    if (asil.Tur == HareketTuru.Gelir)
                        await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, asil.Tutar);
                }

                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                var ters = new KasaHareketi
                {
                    SirketId = sirketId,
                    KasaId = asil.KasaId,
                    Tarih = tarih,
                    Tur = asil.Tur == HareketTuru.Gelir ? HareketTuru.Gider : HareketTuru.Gelir,
                    FisTuru = asil.FisTuru,
                    FisNo = FinansKurallari.FisNoUret("IPT"),
                    Tutar = asil.Tutar,
                    CariKartId = asil.CariKartId,
                    Aciklama = $"Ters Kayıt: İptal ({temizNeden})",
                    Belge = asil.Belge,
                    IslemDurumu = FinansIslemDurumu.TersKayit,
                    TersKayitId = asil.Id
                };
                _baglam.KasaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync(cancellationToken);
                asil.TersKayitId = ters.Id;
                (cariKartId, cariHareketId, asilTur, tutar, belge) = (asil.CariKartId, asil.IlgiliBelgeId, asil.Tur, asil.Tutar, asil.Belge);
            }
            else
            {
                var asil = await _baglam.BankaHareketleri.SingleOrDefaultAsync(x => x.Id == hareketId && x.SirketId == sirketId, cancellationToken)
                    ?? throw new KeyNotFoundException("Banka hareketi bulunamadı.");
                IptalEdilebilirMi(asil.IslemDurumu, asil.FisTuru is BankaFisTuru.BankaVirman or BankaFisTuru.BankayaYatirma or BankaFisTuru.KasadanCekme or BankaFisTuru.PosHesabaGecis);

                if (asil.BankaHesabiId.HasValue)
                {
                    await FinansKurallari.BankaHesabiKilitleAsync(_baglam, asil.BankaHesabiId.Value);
                    var hesap = await _baglam.BankaHesaplari.SingleAsync(b => b.Id == asil.BankaHesabiId.Value, cancellationToken);
                    if (asil.Tur == HareketTuru.Gelir)
                        await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, hesap, asil.Tutar);
                }

                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                var ters = new BankaHareketi
                {
                    SirketId = sirketId,
                    BankaHesabiId = asil.BankaHesabiId,
                    BankaAdi = asil.BankaAdi,
                    HesapNo = asil.HesapNo,
                    Tarih = tarih,
                    Tur = asil.Tur == HareketTuru.Gelir ? HareketTuru.Gider : HareketTuru.Gelir,
                    FisTuru = asil.FisTuru,
                    FisNo = FinansKurallari.FisNoUret("IPT"),
                    OdemeYontemi = asil.OdemeYontemi,
                    Tutar = asil.Tutar,
                    CariKartId = asil.CariKartId,
                    Aciklama = $"Ters Kayıt: İptal ({temizNeden})",
                    IslemDurumu = FinansIslemDurumu.TersKayit,
                    TersKayitId = asil.Id
                };
                _baglam.BankaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync(cancellationToken);
                asil.TersKayitId = ters.Id;
                (cariKartId, cariHareketId, asilTur, tutar, belge) = (asil.CariKartId, asil.IlgiliBelgeId, asil.Tur, asil.Tutar, asil.DekontNo);
            }

            // Cari tahsilat/ödeme iptal edildiğinde cari bakiye de ters kayıtla düzeltilir.
            if (cariKartId.HasValue)
            {
                var cariHareket = cariHareketId.HasValue
                    ? await _baglam.CariHareketler.SingleOrDefaultAsync(c => c.Id == cariHareketId.Value && c.CariKartId == cariKartId.Value, cancellationToken)
                    : null;
                if (cariHareket?.KapaliTutar > 0)
                    throw new InvalidOperationException("Bu işlem bir faturayla kapatılmış. Önce belge kapama eşleştirmesini kaldırın.");

                _baglam.CariHareketler.Add(new CariHareket
                {
                    CariKartId = cariKartId.Value,
                    Tarih = tarih,
                    HareketTuru = CariHareketTuru.Diger,
                    Aciklama = $"İptal: {temizNeden}",
                    BelgeNo = belge,
                    // Tahsilat cariyi alacaklandırmıştı, iptali borçlandırır; ödemede tersi.
                    Borc = asilTur == HareketTuru.Gelir ? tutar : 0,
                    Alacak = asilTur == HareketTuru.Gider ? tutar : 0,
                    IlgiliBelgeId = hareketId
                });
            }

            await _baglam.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void IptalEdilebilirMi(FinansIslemDurumu durum, bool transferFisi)
    {
        if (durum == FinansIslemDurumu.IptalEdildi)
            throw new InvalidOperationException("Bu işlem zaten iptal edilmiştir.");
        if (durum == FinansIslemDurumu.TersKayit)
            throw new InvalidOperationException("Ters kayıtlar iptal edilemez.");
        if (transferFisi)
            throw new InvalidOperationException("Transfer ve sayım fişleri iptal edilemez; düzeltme için ters yönde yeni bir transfer girin.");
    }

    public async Task<SayfaliFinansHareketDto> GetGlobalFinansHareketleriAsync(Guid sirketId, GlobalFinansFiltreDto filtre, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.FinansGoruntule, "Finans Görüntüleme");

        filtre ??= new GlobalFinansFiltreDto();

        var kasaSorgu = _baglam.KasaHareketleri
            .AsNoTracking()
            .Include(x => x.CariKart)
            .Include(x => x.Kasa)
            .Where(x => x.SirketId == sirketId);

        var bankaSorgu = _baglam.BankaHareketleri
            .AsNoTracking()
            .Include(x => x.CariKart)
            .Include(x => x.BankaHesabi)
            .Where(x => x.SirketId == sirketId);

        if (filtre.BaslangicTarihi.HasValue)
        {
            var bas = filtre.BaslangicTarihi.Value.Date;
            kasaSorgu = kasaSorgu.Where(x => x.Tarih >= bas);
            bankaSorgu = bankaSorgu.Where(x => x.Tarih >= bas);
        }

        if (filtre.BitisTarihi.HasValue)
        {
            var bit = filtre.BitisTarihi.Value.Date.AddDays(1);
            kasaSorgu = kasaSorgu.Where(x => x.Tarih < bit);
            bankaSorgu = bankaSorgu.Where(x => x.Tarih < bit);
        }

        if (filtre.CariKartId.HasValue)
        {
            kasaSorgu = kasaSorgu.Where(x => x.CariKartId == filtre.CariKartId);
            bankaSorgu = bankaSorgu.Where(x => x.CariKartId == filtre.CariKartId);
        }

        if (filtre.KasaId.HasValue)
        {
            kasaSorgu = kasaSorgu.Where(x => x.KasaId == filtre.KasaId);
            bankaSorgu = bankaSorgu.Where(x => false);
        }

        if (filtre.BankaHesapId.HasValue)
        {
            bankaSorgu = bankaSorgu.Where(x => x.BankaHesabiId == filtre.BankaHesapId);
            kasaSorgu = kasaSorgu.Where(x => false);
        }

        var kasaList = await kasaSorgu.ToListAsync(cancellationToken);
        var bankaList = await bankaSorgu.ToListAsync(cancellationToken);

        var birlesik = kasaList.Select(k => new YuruyenFinansSatirDto(
            Id: k.Id,
            Kaynak: "Kasa",
            Tarih: k.Tarih,
            Saat: k.Tarih.ToString("HH:mm"),
            IslemNo: $"KSH-{k.Id:D6}",
            IslemTuru: k.Tur == HareketTuru.Gelir ? "Kasa Giriş" : "Kasa Çıkış",
            CariKartId: k.CariKartId,
            CariKod: k.CariKart?.Kod ?? "-",
            CariUnvan: k.CariKart?.Unvan ?? "-",
            KaynakAdi: k.Kasa?.Ad ?? "Kasa",
            HedefAdi: "-",
            BelgeNo: k.Belge ?? "-",
            Aciklama: k.Aciklama ?? string.Empty,
            Giris: k.Tur == HareketTuru.Gelir ? k.Tutar : 0m,
            Cikis: k.Tur == HareketTuru.Gider ? k.Tutar : 0m,
            Bakiye: 0m,
            Tutar: k.Tutar,
            ParaBirimi: "TRY",
            KullaniciAdi: "Sistem",
            Durum: k.IslemDurumu.ToString()
        )).Concat(bankaList.Select(b => new YuruyenFinansSatirDto(
            Id: b.Id,
            Kaynak: "Banka",
            Tarih: b.Tarih,
            Saat: b.Tarih.ToString("HH:mm"),
            IslemNo: $"BNK-{b.Id:D6}",
            IslemTuru: b.Tur == HareketTuru.Gelir ? "Banka Giriş" : "Banka Çıkış",
            CariKartId: b.CariKartId,
            CariKod: b.CariKart?.Kod ?? "-",
            CariUnvan: b.CariKart?.Unvan ?? "-",
            KaynakAdi: b.BankaHesabi?.Ad ?? b.BankaAdi ?? "Banka",
            HedefAdi: b.BankaHesabi?.HesapNo ?? b.HesapNo ?? "-",
            BelgeNo: "-",
            Aciklama: b.Aciklama ?? string.Empty,
            Giris: b.Tur == HareketTuru.Gelir ? b.Tutar : 0m,
            Cikis: b.Tur == HareketTuru.Gider ? b.Tutar : 0m,
            Bakiye: 0m,
            Tutar: b.Tutar,
            ParaBirimi: "TRY",
            KullaniciAdi: "Sistem",
            Durum: b.IslemDurumu.ToString()
        )));

        if (!string.IsNullOrWhiteSpace(filtre.IslemTuru))
            birlesik = birlesik.Where(x => x.IslemTuru.Equals(filtre.IslemTuru, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filtre.Durum))
            birlesik = birlesik.Where(x => x.Durum.Equals(filtre.Durum, StringComparison.OrdinalIgnoreCase));

        var sirali = birlesik.OrderByDescending(x => x.Tarih).ThenByDescending(x => x.Id).ToList();
        var toplamAdet = sirali.Count;

        var sayfa = Math.Max(1, filtre.SayfaNo);
        var sayfaBoyutu = Math.Max(1, filtre.SayfaBoyutu);
        var pagedItems = sirali.Skip((sayfa - 1) * sayfaBoyutu).Take(sayfaBoyutu).ToList();

        return new SayfaliFinansHareketDto(pagedItems, toplamAdet, sayfa, sayfaBoyutu);
    }
}
