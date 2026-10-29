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

        var bugun = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc); // belge tarihleri yerel saat "UTC" etiketli
        var yarinki = bugun.AddDays(1);

        var kasalar = await GetKasalarAsync(sirketId, cancellationToken);
        var bankalar = await GetBankaHesaplariAsync(sirketId, cancellationToken);

        // Listeler dövizli hesabı kendi para biriminde gösterir; toplamlar TL (defter) tutarlarından.
        var toplamKasa = await _baglam.KasaHareketleri.AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.Kasa != null)
            .SumAsync(x => (decimal?)(x.Tur == HareketTuru.Gelir ? x.Tutar : -x.Tutar), cancellationToken) ?? 0m;
        var toplamBanka = await _baglam.BankaHareketleri.AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.BankaHesabi != null)
            .SumAsync(x => (decimal?)(x.Tur == HareketTuru.Gelir ? x.Tutar : -x.Tutar), cancellationToken) ?? 0m;

        // Bugünkü tahsilat ve ödeme (kasa + banka): yalnız cari tahsilat/ödemeleri; açılış, virman, gelir/gider fişleri değil.
        var bugunkiKasaGelir = await _baglam.KasaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.FisTuru == KasaFisTuru.Tahsilat && x.Tur == HareketTuru.Gelir && x.Tarih >= bugun && x.Tarih < yarinki)
            .SumAsync(x => (decimal?)x.Tutar, cancellationToken) ?? 0m;

        var bugunkiKasaGider = await _baglam.KasaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.FisTuru == KasaFisTuru.Odeme && x.Tur == HareketTuru.Gider && x.Tarih >= bugun && x.Tarih < yarinki)
            .SumAsync(x => (decimal?)x.Tutar, cancellationToken) ?? 0m;

        var bugunkiBankaGelir = await _baglam.BankaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.FisTuru == BankaFisTuru.GelenHavale && x.Tur == HareketTuru.Gelir && x.Tarih >= bugun && x.Tarih < yarinki)
            .SumAsync(x => (decimal?)x.Tutar, cancellationToken) ?? 0m;

        var bugunkiBankaGider = await _baglam.BankaHareketleri
            .AsNoTracking()
            .Where(x => x.SirketId == sirketId && x.IslemDurumu == FinansIslemDurumu.Tamamlandi && x.FisTuru == BankaFisTuru.GidenHavale && x.Tur == HareketTuru.Gider && x.Tarih >= bugun && x.Tarih < yarinki)
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

        var bugun = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc); // belge tarihleri yerel saat "UTC" etiketli
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
            .Select(h => new { h.KasaId, h.Tur, h.Tutar, h.DovizTutar, h.Tarih })
            .ToListAsync(cancellationToken);

        var liste = new List<KasaOzetDto>();
        foreach (var k in kasalar)
        {
            var kHareketler = hareketler.Where(h => h.KasaId == k.Id).ToList();
            // Dövizli kasa kendi para biriminde.
            var dovizli = FinansKurallari.DovizliMi(k.ParaBirimi);
            decimal Tutar(decimal tl, decimal doviz) => dovizli ? doviz : tl;
            var bakiye = kHareketler.Sum(h => h.Tur == HareketTuru.Gelir ? Tutar(h.Tutar, h.DovizTutar) : -Tutar(h.Tutar, h.DovizTutar));
            var bugunkiGiris = kHareketler.Where(h => h.Tur == HareketTuru.Gelir && h.Tarih >= bugun && h.Tarih < yarinki).Sum(h => Tutar(h.Tutar, h.DovizTutar));
            var bugunkiCikis = kHareketler.Where(h => h.Tur == HareketTuru.Gider && h.Tarih >= bugun && h.Tarih < yarinki).Sum(h => Tutar(h.Tutar, h.DovizTutar));
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
            .Select(h => new { h.BankaHesabiId, h.Tur, h.Tutar, h.DovizTutar, h.Tarih })
            .ToListAsync(cancellationToken);

        var liste = new List<BankaHesapOzetDto>();
        foreach (var h in hesaplar)
        {
            var hHareketler = hareketler.Where(x => x.BankaHesabiId == h.Id).ToList();
            // Dövizli hesap kendi para biriminde.
            var dovizli = FinansKurallari.DovizliMi(h.ParaBirimi);
            var bakiye = hHareketler.Sum(x => (x.Tur == HareketTuru.Gelir ? 1 : -1) * (dovizli ? x.DovizTutar : x.Tutar));
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
        // Dövizli kasada tutarlar ve yürüyen bakiye kasanın para birimindedir.
        var paraBirimi = await _baglam.Kasalar.Where(k => k.Id == kasaId).Select(k => k.ParaBirimi).FirstOrDefaultAsync(cancellationToken) ?? "TRY";
        var dovizli = FinansKurallari.DovizliMi(paraBirimi);

        decimal yuruyenBakiye = 0m;
        var sonuc = new List<YuruyenFinansSatirDto>();

        foreach (var h in hareketler)
        {
            var tutar = dovizli ? h.DovizTutar : h.Tutar;
            var giris = h.Tur == HareketTuru.Gelir ? tutar : 0m;
            var cikis = h.Tur == HareketTuru.Gider ? tutar : 0m;
            yuruyenBakiye += (giris - cikis);

            sonuc.Add(new YuruyenFinansSatirDto(
                Id: h.Id,
                Kaynak: "Kasa",
                Tarih: h.Tarih,
                Saat: h.Tarih.ToString("HH:mm"),
                IslemNo: $"KSH-{h.Id:D6}",
                IslemTuru: h.FisTuru switch
                {
                    KasaFisTuru.Tahsilat => "Tahsilat",
                    KasaFisTuru.Odeme => "Ödeme",
                    KasaFisTuru.Gelir => "Gelir",
                    KasaFisTuru.Gider => "Gider",
                    KasaFisTuru.KasaVirman => "Virman",
                    KasaFisTuru.BankayaYatirma => "Transfer (bankaya)",
                    KasaFisTuru.BankadanCekme => "Transfer (bankadan)",
                    KasaFisTuru.AcilisDevir => "Açılış / Devir",
                    _ => "Sayım Farkı"
                },
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
                Tutar: tutar,
                ParaBirimi: paraBirimi,
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
        // Dövizli hesapta tutarlar ve yürüyen bakiye hesabın para birimindedir.
        var paraBirimi = await _baglam.BankaHesaplari.Where(b => b.Id == bankaHesapId).Select(b => b.ParaBirimi).FirstOrDefaultAsync(cancellationToken) ?? "TRY";
        var dovizli = FinansKurallari.DovizliMi(paraBirimi);

        decimal yuruyenBakiye = 0m;
        var sonuc = new List<YuruyenFinansSatirDto>();

        foreach (var h in hareketler)
        {
            var tutar = dovizli ? h.DovizTutar : h.Tutar;
            var giris = h.Tur == HareketTuru.Gelir ? tutar : 0m;
            var cikis = h.Tur == HareketTuru.Gider ? tutar : 0m;
            yuruyenBakiye += (giris - cikis);

            sonuc.Add(new YuruyenFinansSatirDto(
                Id: h.Id,
                Kaynak: "Banka",
                Tarih: h.Tarih,
                Saat: h.Tarih.ToString("HH:mm"),
                IslemNo: $"BNK-{h.Id:D6}",
                IslemTuru: h.FisTuru switch
                {
                    BankaFisTuru.GelenHavale => "Gelen Havale",
                    BankaFisTuru.GidenHavale => "Giden Havale",
                    BankaFisTuru.BankaGelir => "Banka Geliri",
                    BankaFisTuru.BankaGiderMasraf => "Masraf",
                    BankaFisTuru.BankaVirman => "Virman",
                    BankaFisTuru.KasadanCekme => "Transfer (kasaya)",
                    BankaFisTuru.BankayaYatirma => "Transfer (kasadan)",
                    BankaFisTuru.PosHesabaGecis => "POS Geçişi",
                    _ => "Kredi Ödemesi"
                },
                CariKartId: h.CariKartId,
                CariKod: h.CariKart?.Kod ?? "-",
                CariUnvan: h.CariKart?.Unvan ?? "-",
                KaynakAdi: h.BankaAdi ?? "Banka",
                HedefAdi: h.HesapNo ?? "-",
                BelgeNo: h.DekontNo ?? "-",
                Aciklama: h.Aciklama ?? string.Empty,
                Giris: giris,
                Cikis: cikis,
                Bakiye: yuruyenBakiye,
                Tutar: tutar,
                ParaBirimi: paraBirimi,
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
        sirketId = _baglam.SirketIdCoz(sirketId);

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
            // Belge tarihi yerel saat (UTC etiketli): gece yarısından sonraki aktarım önceki güne düşmesin.
            var islemTarihi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
            pos.HesabaGecisTarihi = islemTarihi;

            if (pos.BankaHesabi?.HesapTuru == BankaHesapTuru.POSHesabi)
            {
                await PosHesabindanVadesizeAktarAsync(pos, pos.BankaHesabi, islemTarihi, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return;
            }

            // Banka Hareketi Gelir ekle (Net Tutar)
            var bh = new BankaHareketi
            {
                SirketId = sirketId,
                BankaHesabiId = pos.BankaHesabiId,
                BankaAdi = pos.BankaHesabi?.Ad ?? pos.BankaAdi,
                HesapNo = pos.BankaHesabi?.HesapNo,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                OdemeYontemi = OdemeYontemi.KrediKarti,
                Tutar = pos.NetTutar,
                Aciklama = $"POS Tahsilat Aktarımı ({pos.PosCihazAdi} - {pos.KartTuru})",
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            _baglam.BankaHareketleri.Add(bh);
            await _baglam.SaveChangesAsync(cancellationToken);

            // Fiş banka hareketine bağlanır (BankaHareketi/bh.Id). Eskiden POS işlemine bağlıydı; açılıştaki
            // "eksik fişleri tamamla" bu banka hareketini fişsiz sanıp ikinci kez 102 borç / 649 alacak kesiyordu.
            // Banka ekranındaki POS aktarımıyla aynı kayıt: banka borç net, 653 komisyon, 108 alacak brüt.
            var bankaKodu = pos.BankaHesabi != null ? await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, pos.BankaHesabi) : "102";
            var komisyon = Math.Max(0, pos.BrutTutar - pos.NetTutar);
            var satirlar = new List<FinansMuhasebeKaydi.Satir> { new(bankaKodu, pos.NetTutar, 0) };
            if (komisyon > 0) satirlar.Add(new FinansMuhasebeKaydi.Satir("653", komisyon, 0));
            satirlar.Add(new FinansMuhasebeKaydi.Satir("108", 0, pos.NetTutar + komisyon));
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, sirketId, FinansMuhasebeKaydi.BankaBelgeTipi, bh.Id, islemTarihi,
                bh.Aciklama, $"POS-{pos.Id:D6}", satirlar.ToArray());

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Kartlı satıştan gelen işlemde para POS (bloke) hesabındadır: brüt tutar POS hesabından çıkar, net tutar şirketin
    // vadesiz TL hesabına girer. Banka ekranındaki "POS hesaba geçiş" ile aynı kayıt: banka borç net, 653 komisyon, 108 alacak brüt.
    private async Task PosHesabindanVadesizeAktarAsync(PosIslemi pos, BankaHesabi posHesap, DateTime islemTarihi, CancellationToken cancellationToken)
    {
        var vadesiz = await _baglam.BankaHesaplari
            .Where(b => b.AktifMi && b.HesapTuru == BankaHesapTuru.VadesizTL && b.SirketId == posHesap.SirketId)
            .OrderBy(b => b.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("POS tahsilatının aktarılacağı vadesiz TL banka hesabı yok. Önce Banka ekranından bir vadesiz hesap tanımlayın.");

        foreach (var id in new[] { posHesap.Id, vadesiz.Id }.Order())
            await FinansKurallari.BankaHesabiKilitleAsync(_baglam, id);
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, posHesap.SirketId);
        await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, posHesap, pos.BrutTutar);

        var komisyon = Math.Max(0, pos.BrutTutar - pos.NetTutar);
        var fisNo = FinansKurallari.FisNoUret("POS-GEC");
        var posCikis = new BankaHareketi
        {
            SirketId = posHesap.SirketId, BankaHesabiId = posHesap.Id, BankaAdi = posHesap.Ad, HesapNo = posHesap.Iban,
            Tarih = islemTarihi, Tur = HareketTuru.Gider, FisTuru = BankaFisTuru.PosHesabaGecis, FisNo = fisNo,
            OdemeYontemi = OdemeYontemi.KrediKarti, Tutar = pos.BrutTutar, KomisyonTutari = komisyon,
            Aciklama = $"POS Tahsilat Aktarımı -> {vadesiz.Ad} ({pos.Aciklama})".Replace(" ()", string.Empty),
            IslemDurumu = FinansIslemDurumu.Tamamlandi
        };
        _baglam.BankaHareketleri.Add(posCikis);
        await _baglam.SaveChangesAsync(cancellationToken);

        var vadesizGiris = new BankaHareketi
        {
            SirketId = vadesiz.SirketId, BankaHesabiId = vadesiz.Id, BankaAdi = vadesiz.Ad, HesapNo = vadesiz.Iban,
            Tarih = islemTarihi, Tur = HareketTuru.Gelir, FisTuru = BankaFisTuru.PosHesabaGecis, FisNo = fisNo,
            OdemeYontemi = OdemeYontemi.KrediKarti, Tutar = pos.NetTutar, KomisyonTutari = komisyon,
            Aciklama = $"POS Tahsilat Aktarımı <- {posHesap.Ad} (Net: {pos.NetTutar:N2} ₺, Komisyon: {komisyon:N2} ₺)",
            TersKayitId = posCikis.Id, IslemDurumu = FinansIslemDurumu.Tamamlandi
        };
        _baglam.BankaHareketleri.Add(vadesizGiris);
        await _baglam.SaveChangesAsync(cancellationToken);
        posCikis.TersKayitId = vadesizGiris.Id;
        await _baglam.SaveChangesAsync(cancellationToken);

        var satirlar = new List<FinansMuhasebeKaydi.Satir> { new(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, vadesiz), pos.NetTutar, 0) };
        if (komisyon > 0) satirlar.Add(new FinansMuhasebeKaydi.Satir("653", komisyon, 0));
        satirlar.Add(new FinansMuhasebeKaydi.Satir(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, posHesap), 0, pos.BrutTutar));
        await FinansMuhasebeKaydi.FisEkleAsync(_baglam, posHesap.SirketId, FinansMuhasebeKaydi.BankaBelgeTipi, posCikis.Id, islemTarihi,
            posCikis.Aciklama, fisNo, satirlar.ToArray());
    }

    public async Task<List<VadeKalemOzetDto>> GetVadeMerkeziAsync(Guid sirketId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.FinansGoruntule, "Finans Görüntüleme");

        var bugun = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc); // belge tarihleri yerel saat "UTC" etiketli
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
                Tutar: f.OdenecekTutar,
                KalanTutar: f.OdenecekTutar,
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
        var tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc); // belge tarihi yerel saat "UTC" etiketli

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await FinansKurallari.KasaKilitleAsync(_baglam, dto.KasaId);
            var kasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == dto.KasaId && k.SirketId == sirketId, cancellationToken)
                ?? throw new KeyNotFoundException("Kasa bulunamadı.");
            if (FinansKurallari.DovizliMi(kasa.ParaBirimi))
                throw new InvalidOperationException($"Kasa sayım mutabakatı TL kasalar içindir. '{kasa.Ad}' ({kasa.ParaBirimi}) kasasında sayım farkını {kasa.ParaBirimi} tutarı ve kuruyla gelir/gider fişi olarak girin.");
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

                // Kasa fazlası: 100 / 397, kasa noksanı: 197 / 100
                var duzeltme = _baglam.ChangeTracker.Entries<KasaHareketi>().Select(e => e.Entity)
                    .Last(h => h.IlgiliBelgeId == mutabakat.Id && h.FisTuru == KasaFisTuru.SayimFarki);
                await FinansMuhasebeKaydi.KasaHareketiFisiEkleAsync(_baglam, duzeltme, fark > 0 ? "397" : "197");
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

            var tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc); // belge tarihi yerel saat "UTC" etiketli
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
            // Dövizli hesaplar arasında tutar hesabın para birimindedir; TL karşılığı kasanın ortalama defter kuruyla taşınır.
            var (tutar, paraBirimiId, kur, dovizTutar) = await FinansKurallari.KasaTransferTutariAsync(_baglam, kasa, dto.Tutar);
            await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, tutar, dovizTutar);

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
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
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
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = aciklama,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            _baglam.KasaHareketleri.Add(kh);
            _baglam.BankaHareketleri.Add(bh);
            await _baglam.SaveChangesAsync(cancellationToken);
            kh.TersKayitId = bh.Id;
            bh.TersKayitId = kh.Id;
            await _baglam.SaveChangesAsync(cancellationToken);

            // Yevmiye (102 Banka Borç / 100 Kasa Alacak) — seçilen kasa ve hesabın kendi muhasebe kodlarıyla.
            await TransferYevmiyesiEkleAsync(sirketId, islemNo, tarih, "KasaBankaTransfer", kh.Id, aciklama,
                await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, banka), await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa), tutar, cancellationToken);

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

            var tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc); // belge tarihi yerel saat "UTC" etiketli
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
            var (tutar, paraBirimiId, kur, dovizTutar) = await FinansKurallari.BankaTransferTutariAsync(_baglam, banka, dto.Tutar);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, banka, tutar, dovizTutar);

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
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
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
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
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

            // Yevmiye (100 Kasa Borç / 102 Banka Alacak) — seçilen kasa ve hesabın kendi muhasebe kodlarıyla.
            await TransferYevmiyesiEkleAsync(sirketId, islemNo, tarih, "BankaKasaTransfer", bh.Id, aciklama,
                await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa), await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, banka), tutar, cancellationToken);

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

            var tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc); // belge tarihi yerel saat "UTC" etiketli
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
            if (FinansKurallari.DovizliMi(kaynak.ParaBirimi) && dto.Masraf > 0)
                throw new InvalidOperationException("Dövizli hesapta virman masrafı ayrı bir banka gider fişiyle (döviz tutarı ve kurla) girilmelidir.");
            var (tutar, paraBirimiId, kur, dovizTutar) = await FinansKurallari.BankaTransferTutariAsync(_baglam, kaynak, dto.Tutar);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, kaynak, tutar + dto.Masraf, dovizTutar);

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
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
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
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = aciklama,
                IslemDurumu = FinansIslemDurumu.Tamamlandi
            };
            _baglam.BankaHareketleri.Add(bhKaynak);
            _baglam.BankaHareketleri.Add(bhHedef);

            BankaHareketi? masrafHareketi = null;
            if (dto.Masraf > 0)
            {
                _baglam.BankaHareketleri.Add(masrafHareketi = new BankaHareketi
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

            // İki hesabın muhasebe kodu aynıysa virman fişlenmez; farklıysa (alt hesaplar, dövizli hesap) hedef / kaynak fişlenir.
            var kaynakKodu = await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, kaynak);
            var hedefKodu = await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, hedef);
            if (kaynakKodu != hedefKodu)
                await TransferYevmiyesiEkleAsync(sirketId, islemNo, tarih, "BankaVirman", bhKaynak.Id, aciklama, hedefKodu, kaynakKodu, tutar, cancellationToken);
            // Masraf fişi (780 / 102).
            if (masrafHareketi != null)
                await FinansMuhasebeKaydi.BankaHareketiFisiEkleAsync(_baglam, masrafHareketi, "780");
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Kasa/banka hesapları arasında transfer; para birimleri farklıysa döviz alım/satımı. Defter TL'dir:
    // - aynı para birimi: TL karşılığı kaynağın ortalama defter kuruyla taşınır (TL hesapta tutarın kendisi),
    // - TL'den dövize: ödenen TL dövizin defter değeri olur,
    // - dövizden TL'ye: alınan TL ile dövizin defter değeri arasındaki fark kambiyo kârı (646) / zararıdır (656),
    // - dövizden dövize: kaynağın defter değeri hedefe taşınır.
    public async Task HesapTransferiYapAsync(HesapTransferIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default)
    {
        _kullanici.YetkiKontrolEt(x => x.VirmanYap, "Virman Yapma");
        sirketId = _baglam.SirketIdCoz(sirketId);
        if (dto.KaynakKasaMi == dto.HedefKasaMi && dto.KaynakId == dto.HedefId)
            throw new InvalidOperationException("Kaynak ve hedef hesap aynı olamaz.");
        FinansKurallari.TutarDogrula(dto.CikanTutar, "Transfer");
        if (dto.Masraf < 0) throw new ArgumentException("Transfer masrafı negatif olamaz.");
        if (dto.Masraf > 0) FinansKurallari.TutarDogrula(dto.Masraf, "Transfer masrafı");

        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Kilitler sabit sırayla (önce kasalar, küçük Id'den) alınır; ters yönlü eşzamanlı transferler kilitlenmez.
            foreach (var (kasaMi, id) in new[] { (dto.KaynakKasaMi, dto.KaynakId), (dto.HedefKasaMi, dto.HedefId) }.OrderBy(x => x.Item1 ? 0 : 1).ThenBy(x => x.Item2))
            {
                if (kasaMi) await FinansKurallari.KasaKilitleAsync(_baglam, id);
                else await FinansKurallari.BankaHesabiKilitleAsync(_baglam, id);
            }

            Kasa? kaynakKasa = null, hedefKasa = null;
            BankaHesabi? kaynakBanka = null, hedefBanka = null;
            if (dto.KaynakKasaMi) kaynakKasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == dto.KaynakId && k.SirketId == sirketId, cancellationToken) ?? throw new KeyNotFoundException("Kaynak kasa bulunamadı.");
            else kaynakBanka = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == dto.KaynakId && b.SirketId == sirketId, cancellationToken) ?? throw new KeyNotFoundException("Kaynak banka hesabı bulunamadı.");
            if (dto.HedefKasaMi) hedefKasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == dto.HedefId && k.SirketId == sirketId, cancellationToken) ?? throw new KeyNotFoundException("Hedef kasa bulunamadı.");
            else hedefBanka = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == dto.HedefId && b.SirketId == sirketId, cancellationToken) ?? throw new KeyNotFoundException("Hedef banka hesabı bulunamadı.");

            var kaynakAd = kaynakKasa?.Ad ?? kaynakBanka!.Ad;
            var hedefAd = hedefKasa?.Ad ?? hedefBanka!.Ad;
            var kaynakPb = kaynakKasa?.ParaBirimi ?? kaynakBanka!.ParaBirimi;
            var hedefPb = hedefKasa?.ParaBirimi ?? hedefBanka!.ParaBirimi;
            if (!(kaynakKasa?.AktifMi ?? kaynakBanka!.AktifMi)) throw new InvalidOperationException($"'{kaynakAd}' pasif durumda. Pasif hesaba işlem yapılamaz.");
            if (!(hedefKasa?.AktifMi ?? hedefBanka!.AktifMi)) throw new InvalidOperationException($"'{hedefAd}' pasif durumda. Pasif hesaba işlem yapılamaz.");
            if (dto.Masraf > 0 && (kaynakBanka == null || FinansKurallari.DovizliMi(kaynakPb)))
                throw new InvalidOperationException("Transfer masrafı yalnız TL banka hesabından yapılan transferde girilebilir; diğer durumlarda ayrı bir gider fişi girin.");

            var tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc); // belge tarihi yerel saat "UTC" etiketli
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);

            // Kaynaktan çıkan: döviz hesabında ortalama defter kuruyla TL karşılığı.
            var (cikanTl, kaynakPbId, kaynakKur, kaynakDoviz) = kaynakKasa != null
                ? await FinansKurallari.KasaTransferTutariAsync(_baglam, kaynakKasa, dto.CikanTutar)
                : await FinansKurallari.BankaTransferTutariAsync(_baglam, kaynakBanka!, dto.CikanTutar);
            if (kaynakKasa != null) await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kaynakKasa, cikanTl, kaynakDoviz);
            else await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, kaynakBanka!, cikanTl + dto.Masraf, kaynakDoviz);

            // Hedefe giren.
            var ayniPara = string.Equals(kaynakPb, hedefPb, StringComparison.OrdinalIgnoreCase);
            decimal girenTl, girenDoviz, hedefKur;
            int? hedefPbId = null;
            if (ayniPara)
            {
                (girenTl, girenDoviz, hedefKur, hedefPbId) = (cikanTl, kaynakDoviz, kaynakKur, kaynakPbId);
            }
            else
            {
                FinansKurallari.TutarDogrula(dto.GirenTutar, $"Hedefe giren {hedefPb}");
                if (FinansKurallari.DovizliMi(hedefPb))
                {
                    girenTl = cikanTl; // TL'den ya da başka dövizden alınan dövizin defter değeri
                    girenDoviz = dto.GirenTutar;
                    hedefKur = Math.Round(girenTl / girenDoviz, 6, MidpointRounding.AwayFromZero);
                    hedefPbId = await FinansKurallari.ParaBirimiIdAsync(_baglam, hedefPb);
                }
                else
                {
                    (girenTl, girenDoviz, hedefKur) = (dto.GirenTutar, 0m, 1m); // döviz bozdurma: alınan TL
                }
            }
            var kurFarki = girenTl - cikanTl; // yalnız dövizden TL'ye satışta sıfırdan farklı olabilir

            var islemNo = FinansKurallari.FisNoUret(ayniPara ? "VRM" : "DVZ");
            var aciklama = string.IsNullOrWhiteSpace(dto.Aciklama)
                ? (ayniPara ? $"Transfer ({kaynakAd} → {hedefAd})" : $"Döviz {(FinansKurallari.DovizliMi(hedefPb) ? "alımı" : "satışı")}: {dto.CikanTutar:N2} {kaynakPb} → {dto.GirenTutar:N2} {hedefPb}")
                : FinansKurallari.AciklamaDogrula(dto.Aciklama);

            KasaHareketi? kasaCikis = null, kasaGiris = null;
            BankaHareketi? bankaCikis = null, bankaGiris = null;
            if (kaynakKasa != null)
                _baglam.KasaHareketleri.Add(kasaCikis = new KasaHareketi
                {
                    SirketId = sirketId, KasaId = kaynakKasa.Id, Tarih = tarih, Tur = HareketTuru.Gider,
                    FisTuru = hedefKasa != null ? KasaFisTuru.KasaVirman : KasaFisTuru.BankayaYatirma, FisNo = islemNo, Belge = islemNo,
                    Tutar = cikanTl, ParaBirimiId = kaynakPbId, Kur = kaynakKur, DovizTutar = kaynakDoviz,
                    Aciklama = $"{aciklama} (→ {hedefAd})", IslemDurumu = FinansIslemDurumu.Tamamlandi
                });
            else
                _baglam.BankaHareketleri.Add(bankaCikis = new BankaHareketi
                {
                    SirketId = sirketId, BankaHesabiId = kaynakBanka!.Id, BankaAdi = kaynakBanka.Ad, HesapNo = kaynakBanka.HesapNo, Tarih = tarih, Tur = HareketTuru.Gider,
                    FisTuru = hedefKasa != null ? BankaFisTuru.KasadanCekme : BankaFisTuru.BankaVirman, FisNo = islemNo, OdemeYontemi = OdemeYontemi.Virman,
                    Tutar = cikanTl, ParaBirimiId = kaynakPbId, Kur = kaynakKur, DovizTutar = kaynakDoviz,
                    Aciklama = $"{aciklama} (→ {hedefAd})", IslemDurumu = FinansIslemDurumu.Tamamlandi
                });
            if (hedefKasa != null)
                _baglam.KasaHareketleri.Add(kasaGiris = new KasaHareketi
                {
                    SirketId = sirketId, KasaId = hedefKasa.Id, Tarih = tarih, Tur = HareketTuru.Gelir,
                    FisTuru = kaynakKasa != null ? KasaFisTuru.KasaVirman : KasaFisTuru.BankadanCekme, FisNo = islemNo, Belge = islemNo,
                    Tutar = girenTl, ParaBirimiId = hedefPbId, Kur = hedefKur, DovizTutar = girenDoviz,
                    Aciklama = $"{aciklama} (← {kaynakAd})", IslemDurumu = FinansIslemDurumu.Tamamlandi
                });
            else
                _baglam.BankaHareketleri.Add(bankaGiris = new BankaHareketi
                {
                    SirketId = sirketId, BankaHesabiId = hedefBanka!.Id, BankaAdi = hedefBanka.Ad, HesapNo = hedefBanka.HesapNo, Tarih = tarih, Tur = HareketTuru.Gelir,
                    FisTuru = kaynakKasa != null ? BankaFisTuru.BankayaYatirma : BankaFisTuru.BankaVirman, FisNo = islemNo, OdemeYontemi = OdemeYontemi.Virman,
                    Tutar = girenTl, ParaBirimiId = hedefPbId, Kur = hedefKur, DovizTutar = girenDoviz,
                    Aciklama = $"{aciklama} (← {kaynakAd})", IslemDurumu = FinansIslemDurumu.Tamamlandi
                });
            BankaHareketi? masrafHareketi = null;
            if (dto.Masraf > 0)
                _baglam.BankaHareketleri.Add(masrafHareketi = new BankaHareketi
                {
                    SirketId = sirketId, BankaHesabiId = kaynakBanka!.Id, BankaAdi = kaynakBanka.Ad, HesapNo = kaynakBanka.HesapNo, Tarih = tarih, Tur = HareketTuru.Gider,
                    FisTuru = BankaFisTuru.BankaGiderMasraf, FisNo = islemNo, OdemeYontemi = OdemeYontemi.Banka, Tutar = dto.Masraf, MasrafTutari = dto.Masraf,
                    Aciklama = $"Transfer masrafı ({islemNo})", IslemDurumu = FinansIslemDurumu.Tamamlandi
                });
            await _baglam.SaveChangesAsync(cancellationToken);

            // Çıkış ve giriş birbirine bağlanır (transferler tek taraflı iptal edilemez).
            int cikisId = kasaCikis?.Id ?? bankaCikis!.Id, girisId = kasaGiris?.Id ?? bankaGiris!.Id;
            if (kasaCikis != null) kasaCikis.TersKayitId = girisId; else bankaCikis!.TersKayitId = girisId;
            if (kasaGiris != null) kasaGiris.TersKayitId = cikisId; else bankaGiris!.TersKayitId = cikisId;
            await _baglam.SaveChangesAsync(cancellationToken);

            // Hedef / kaynak; dövizden TL'ye satışta fark 646 (kâr) veya 656 (zarar).
            var kaynakKodu = kaynakKasa != null ? await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kaynakKasa) : await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, kaynakBanka!);
            var hedefKodu = hedefKasa != null ? await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, hedefKasa) : await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, hedefBanka!);
            var satirlar = new List<FinansMuhasebeKaydi.Satir> { new(hedefKodu, girenTl, 0), new(kaynakKodu, 0, cikanTl) };
            if (kurFarki > 0) satirlar.Add(new("646", 0, kurFarki));
            else if (kurFarki < 0) satirlar.Add(new("656", -kurFarki, 0));
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, sirketId, kasaCikis != null ? FinansMuhasebeKaydi.KasaBelgeTipi : FinansMuhasebeKaydi.BankaBelgeTipi,
                cikisId, tarih, aciklama, islemNo, satirlar.ToArray());
            if (masrafHareketi != null)
                await FinansMuhasebeKaydi.BankaHareketiFisiEkleAsync(_baglam, masrafHareketi, "780");
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
        // Hesap planında eksik standart hesap varsa otomatik açılır; transfer her zaman fişlenir.
        await FinansMuhasebeKaydi.FisEkleAsync(_baglam, sirketId, belgeTipi, belgeId, tarih, aciklama, islemNo,
            new FinansMuhasebeKaydi.Satir(borcHesapKodu, tutar, 0),
            new FinansMuhasebeKaydi.Satir(alacakHesapKodu, 0, tutar));
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
            var tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc); // belge tarihi yerel saat "UTC" etiketli
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
                if (asil.FaturaId.HasValue)
                    throw new InvalidOperationException("Faturaya bağlı tahsilat/ödeme fatura ekranından iptal edilmelidir.");
                if (asil.OzelKod == CekSenetServisi.CekSenetOzelKodu)
                    throw new InvalidOperationException("Çek/senet tahsilatı veya ödemesi iptal edilemez.");
                if (asil.OzelKod == PersonelServisi.PersonelOzelKodu)
                    throw new InvalidOperationException("Maaş ödemesi ve personel avansı Personel ve Maaş ekranından iptal edilir.");
                IptalEdilebilirMi(asil.IslemDurumu, asil.FisTuru is KasaFisTuru.KasaVirman or KasaFisTuru.BankayaYatirma or KasaFisTuru.BankadanCekme or KasaFisTuru.SayimFarki);

                if (asil.KasaId.HasValue)
                {
                    await FinansKurallari.KasaKilitleAsync(_baglam, asil.KasaId.Value);
                    var kasa = await _baglam.Kasalar.SingleAsync(k => k.Id == asil.KasaId.Value, cancellationToken);
                    // Gelirin iptali kasadan çıkış demektir; bakiye yetmeli.
                    if (asil.Tur == HareketTuru.Gelir)
                        await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, asil.Tutar, asil.DovizTutar);
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
                    // Dövizli kasada ters kayıt aynı döviz tutarı ve kurla girer.
                    ParaBirimiId = asil.ParaBirimiId,
                    Kur = asil.Kur,
                    DovizTutar = asil.DovizTutar,
                    CariKartId = asil.CariKartId,
                    Aciklama = $"Ters Kayıt: İptal ({temizNeden})",
                    Belge = asil.Belge,
                    IslemDurumu = FinansIslemDurumu.TersKayit,
                    TersKayitId = asil.Id
                };
                _baglam.KasaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync(cancellationToken);
                asil.TersKayitId = ters.Id;
                await FinansMuhasebeKaydi.TersFisEkleAsync(_baglam, true, asil.Id, ters, null);
                (cariKartId, cariHareketId, asilTur, tutar, belge) = (asil.CariKartId, asil.IlgiliBelgeId, asil.Tur, asil.Tutar, asil.Belge);
            }
            else
            {
                var asil = await _baglam.BankaHareketleri.SingleOrDefaultAsync(x => x.Id == hareketId && x.SirketId == sirketId, cancellationToken)
                    ?? throw new KeyNotFoundException("Banka hareketi bulunamadı.");
                if (asil.FaturaId.HasValue)
                    throw new InvalidOperationException("Faturaya bağlı tahsilat/ödeme fatura ekranından iptal edilmelidir.");
                if (asil.OzelKod == CekSenetServisi.CekSenetOzelKodu)
                    throw new InvalidOperationException("Çek/senet tahsilatı veya ödemesi iptal edilemez.");
                if (asil.OzelKod == PersonelServisi.PersonelOzelKodu)
                    throw new InvalidOperationException("Maaş ödemesi ve personel avansı Personel ve Maaş ekranından iptal edilir.");
                IptalEdilebilirMi(asil.IslemDurumu, asil.FisTuru is BankaFisTuru.BankaVirman or BankaFisTuru.BankayaYatirma or BankaFisTuru.KasadanCekme or BankaFisTuru.PosHesabaGecis);

                if (asil.BankaHesabiId.HasValue)
                {
                    await FinansKurallari.BankaHesabiKilitleAsync(_baglam, asil.BankaHesabiId.Value);
                    var hesap = await _baglam.BankaHesaplari.SingleAsync(b => b.Id == asil.BankaHesabiId.Value, cancellationToken);
                    if (asil.Tur == HareketTuru.Gelir)
                        await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, hesap, asil.Tutar, asil.DovizTutar);
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
                    ParaBirimiId = asil.ParaBirimiId,
                    Kur = asil.Kur,
                    DovizTutar = asil.DovizTutar,
                    CariKartId = asil.CariKartId,
                    Aciklama = $"Ters Kayıt: İptal ({temizNeden})",
                    IslemDurumu = FinansIslemDurumu.TersKayit,
                    TersKayitId = asil.Id
                };
                _baglam.BankaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync(cancellationToken);
                asil.TersKayitId = ters.Id;
                await FinansMuhasebeKaydi.TersFisEkleAsync(_baglam, false, asil.Id, null, ters);
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
