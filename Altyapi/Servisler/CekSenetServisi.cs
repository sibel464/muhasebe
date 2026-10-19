using EtkiAlani.Modeller.Cari;
using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public class CekSenetServisi : ICekSenetServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public CekSenetServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    // Çek/senet tahsil ve ödemelerinden doğan kasa/banka hareketlerini işaretler; bu hareketler kasa/banka ekranından silinemez.
    public const string CekSenetOzelKodu = "CEKSENET";

    private IKullaniciServisi Oturum => _kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama");

    // Alınan çek/senet portföyden ciro, tahsil, teminat veya iade ile çıkar; verilen çek/senet ödenir veya iade alınır.
    // Tahsil edildi, ödendi, ciro edildi ve iade edildi son durumlardır; geri alınamaz.
    private static readonly Dictionary<CekSenetDurumu, CekSenetDurumu[]> AlinanGecisleri = new()
    {
        [CekSenetDurumu.Portfoyde] = [CekSenetDurumu.CiroEdildi, CekSenetDurumu.TahsileVerildi, CekSenetDurumu.TeminataVerildi, CekSenetDurumu.TahsilEdildi, CekSenetDurumu.Karsiliksiz, CekSenetDurumu.IadeEdildi],
        [CekSenetDurumu.TahsileVerildi] = [CekSenetDurumu.TahsilEdildi, CekSenetDurumu.Karsiliksiz, CekSenetDurumu.Portfoyde],
        [CekSenetDurumu.TeminataVerildi] = [CekSenetDurumu.Portfoyde],
        [CekSenetDurumu.Karsiliksiz] = [CekSenetDurumu.TahsilEdildi, CekSenetDurumu.IadeEdildi],
    };

    private static readonly Dictionary<CekSenetDurumu, CekSenetDurumu[]> VerilenGecisleri = new()
    {
        [CekSenetDurumu.Portfoyde] = [CekSenetDurumu.Odendi, CekSenetDurumu.Karsiliksiz, CekSenetDurumu.IadeEdildi],
        [CekSenetDurumu.Karsiliksiz] = [CekSenetDurumu.Odendi, CekSenetDurumu.IadeEdildi],
    };

    public async Task<CekSenet> EkleAsync(CekSenetEkleTalebi talep)
    {
        Oturum.YetkiKontrolEt(y => y.CekSenetIslemYap, "Çek/Senet Kaydı Ekleme");

        FinansKurallari.TutarDogrula(talep.Tutar, "Çek/senet");
        if (string.IsNullOrWhiteSpace(talep.BelgeNo))
            throw new ArgumentException("Çek/senet numarası zorunludur.");
        if (talep.VadeTarihi.Date < talep.DuzenlemeTarihi.Date)
            throw new ArgumentException("Vade tarihi düzenleme tarihinden önce olamaz.");

        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == talep.CariKartId)
            ?? throw new KeyNotFoundException("Cari kart bulunamadı.");
        if (!cari.Aktif)
            throw new InvalidOperationException("Pasif durumdaki cari karta çek/senet girilemez.");

        var belgeNo = talep.BelgeNo.Trim();
        if (await _baglam.CekSenetler.AnyAsync(x => x.Tur == talep.Tur && x.BelgeNo == belgeNo && x.CariKartId == cari.Id))
            throw new InvalidOperationException($"'{belgeNo}' numaralı çek/senet bu cari için zaten kayıtlı.");

        var cekSenet = new CekSenet
        {
            SirketId = cari.SirketId,
            Tur = talep.Tur,
            Durum = CekSenetDurumu.Portfoyde,
            BelgeNo = belgeNo,
            VadeTarihi = DateTime.SpecifyKind(talep.VadeTarihi, DateTimeKind.Utc),
            DuzenlemeTarihi = DateTime.SpecifyKind(talep.DuzenlemeTarihi, DateTimeKind.Utc),
            Tutar = talep.Tutar,
            CariKartId = cari.Id,
            Kesideci = talep.Kesideci?.Trim() ?? string.Empty,
            BankaAdi = talep.BankaAdi?.Trim(),
            SubeAdi = talep.SubeAdi?.Trim(),
            HesapNo = talep.HesapNo?.Trim(),
            Aciklama = talep.Aciklama?.Trim() ?? string.Empty,
            ParaBirimiId = talep.ParaBirimiId,
            Kur = talep.Kur,
            DovizTutar = talep.DovizTutar
        };

        await using var tx = _baglam.Database.CurrentTransaction == null ? await _baglam.Database.BeginTransactionAsync() : null;
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, cekSenet.DuzenlemeTarihi, cari.SirketId);
        _baglam.CekSenetler.Add(cekSenet);
        await _baglam.SaveChangesAsync();

        // Alınan çek/senet carinin borcunu kapatır (101/121 borç - 120 alacak), verilen çek/senet borcumuzu
        // (320 borç - 103/321 alacak). Tahsilat/Ödeme ekranıyla aynı kayıt: geçmişte görünür, oradan iptal edilebilir.
        var alinan = cekSenet.Tur is CekSenetTuru.AlinanCek or CekSenetTuru.AlinanSenet;
        var cariKodu = alinan ? "120" : "320";
        var aciklama = string.IsNullOrWhiteSpace(cekSenet.Aciklama) ? $"{DurumAdi(CekSenetDurumu.Portfoyde)} {TurAdi(cekSenet.Tur)} No: {cekSenet.BelgeNo}" : cekSenet.Aciklama;
        _baglam.CariHareketler.Add(new CariHareket
        {
            CariKartId = cari.Id, Tarih = cekSenet.DuzenlemeTarihi, BelgeNo = cekSenet.BelgeNo, IlgiliBelgeId = cekSenet.Id, Aciklama = aciklama,
            HareketTuru = alinan ? CariHareketTuru.Tahsilat : CariHareketTuru.Odeme,
            Alacak = alinan ? cekSenet.Tutar : 0, Borc = alinan ? 0 : cekSenet.Tutar
        });
        await _baglam.SaveChangesAsync();
        await FinansMuhasebeKaydi.FisEkleAsync(_baglam, cari.SirketId, "TahsilatOdeme", cekSenet.Id, cekSenet.DuzenlemeTarihi, aciklama, cekSenet.BelgeNo,
            alinan
                ? [new FinansMuhasebeKaydi.Satir(PortfoyKodu(cekSenet.Tur), cekSenet.Tutar, 0), new FinansMuhasebeKaydi.Satir(cariKodu, 0, cekSenet.Tutar, cari.Id)]
                : [new FinansMuhasebeKaydi.Satir(cariKodu, cekSenet.Tutar, 0, cari.Id), new FinansMuhasebeKaydi.Satir(PortfoyKodu(cekSenet.Tur), 0, cekSenet.Tutar)]);
        if (tx != null) await tx.CommitAsync();

        return cekSenet;
    }

    public async Task<IReadOnlyList<CekSenet>> TumunuGetirAsync(CekSenetTuru? tur = null, CekSenetDurumu? durum = null)
    {
        Oturum.YetkiKontrolEt(y => y.CekSenetGoruntule || y.CekSenetIslemYap, "Çek/Senet Görüntüleme");
        var sorgu = _baglam.CekSenetler.Include(x => x.CariKart).AsQueryable();

        if (tur.HasValue) sorgu = sorgu.Where(x => x.Tur == tur.Value);
        if (durum.HasValue) sorgu = sorgu.Where(x => x.Durum == durum.Value);

        return await sorgu.OrderBy(x => x.VadeTarihi).ToListAsync();
    }

    public async Task DurumGuncelleAsync(int cekSenetId, CekSenetDurumu yeniDurum, int? kasaId = null, int? bankaHesabiId = null, string? aciklama = null, int? ciroCariKartId = null)
    {
        Oturum.YetkiKontrolEt(y => y.CekSenetIslemYap, "Çek/Senet Durum Değişikliği");

        await using var tx = _baglam.Database.CurrentTransaction == null ? await _baglam.Database.BeginTransactionAsync() : null;

        // Aynı çek üzerinde eşzamanlı iki durum değişikliği (örn. iki kez tahsil) engellenir.
        await _baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"CekSenetler\" WHERE \"Id\" = {cekSenetId} FOR UPDATE");
        var cek = await _baglam.CekSenetler.Include(x => x.CariKart).FirstOrDefaultAsync(x => x.Id == cekSenetId)
            ?? throw new KeyNotFoundException("Çek/Senet bulunamadı.");

        if (cek.Durum == yeniDurum) return;

        var alinan = cek.Tur is CekSenetTuru.AlinanCek or CekSenetTuru.AlinanSenet;
        var gecisler = alinan ? AlinanGecisleri : VerilenGecisleri;
        if (!gecisler.TryGetValue(cek.Durum, out var izinliler) || !izinliler.Contains(yeniDurum))
            throw new InvalidOperationException($"'{DurumAdi(cek.Durum)}' durumundaki {(alinan ? "alınan" : "verilen")} çek/senet '{DurumAdi(yeniDurum)}' durumuna alınamaz.");

        var paraHareketi = yeniDurum is CekSenetDurumu.TahsilEdildi or CekSenetDurumu.Odendi;
        if (paraHareketi && kasaId.HasValue == bankaHesabiId.HasValue)
            throw new ArgumentException(yeniDurum == CekSenetDurumu.TahsilEdildi
                ? "Tahsilatın gireceği kasa veya banka hesabını seçiniz."
                : "Ödemenin yapılacağı kasa veya banka hesabını seçiniz.");

        CariKart? ciroCari = null;
        if (yeniDurum == CekSenetDurumu.CiroEdildi)
        {
            ciroCari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == ciroCariKartId)
                ?? throw new ArgumentException("Çekin ciro edileceği cariyi seçiniz.");
            if (ciroCari.Id == cek.CariKartId) throw new InvalidOperationException("Çek, alındığı cariye ciro edilemez; geri vermek için 'İade Edildi' seçin.");
            if (!ciroCari.Aktif) throw new InvalidOperationException("Pasif cariye ciro yapılamaz.");
        }

        // Belge tarihleri yerel saatle (UTC etiketli) saklanır.
        var islemTarihi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, cek.SirketId);

        // Çek alınırken/verilirken cariye ve muhasebeye işlendiyse ("kayıtlı") durum değişiklikleri o kaydın üzerine kurulur;
        // bu özellikten önce muhasebeleşmeden girilmiş eski çeklerde tahsil/ödeme doğrudan cariyi kapatır.
        var kayitli = await PortfoyFisiVarAsync(cek.Id);
        var eskiDurum = cek.Durum;
        var cariKodu = alinan ? "120" : "320";
        cek.Durum = yeniDurum;
        var detayAciklama = string.IsNullOrWhiteSpace(aciklama) ? $"{cek.Tur} - Durum Değişikliği: {DurumAdi(yeniDurum)}" : aciklama.Trim();

        KasaHareketi? kasaH = null;
        BankaHareketi? bankaH = null;
        if (paraHareketi)
        {
            var giris = yeniDurum == CekSenetDurumu.TahsilEdildi;
            var hareketAciklamasi = giris
                ? $"Çek/Senet Tahsilatı (Belge No: {cek.BelgeNo}) - {detayAciklama}"
                : $"Verilen Çek/Senet Ödemesi (Belge No: {cek.BelgeNo}) - {detayAciklama}";

            if (kasaId.HasValue)
            {
                await FinansKurallari.KasaKilitleAsync(_baglam, kasaId.Value);
                var kasa = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == kasaId.Value)
                    ?? throw new KeyNotFoundException("Kasa bulunamadı.");
                if (!kasa.AktifMi)
                    throw new InvalidOperationException($"'{kasa.Ad}' kasası pasif durumda.");
                if (!giris)
                    await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, cek.Tutar);

                _baglam.KasaHareketleri.Add(kasaH = new KasaHareketi
                {
                    SirketId = kasa.SirketId ?? cek.SirketId,
                    KasaId = kasa.Id,
                    Tarih = islemTarihi,
                    Tur = giris ? HareketTuru.Gelir : HareketTuru.Gider,
                    FisTuru = giris ? KasaFisTuru.Gelir : KasaFisTuru.Gider,
                    FisNo = FinansKurallari.FisNoUret("CEK"),
                    Tutar = cek.Tutar,
                    Aciklama = hareketAciklamasi,
                    Belge = cek.BelgeNo,
                    OzelKod = CekSenetOzelKodu,
                    IlgiliBelgeId = cek.Id
                });
            }
            else
            {
                await FinansKurallari.BankaHesabiKilitleAsync(_baglam, bankaHesabiId!.Value);
                var hesap = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == bankaHesabiId.Value)
                    ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
                if (!hesap.AktifMi)
                    throw new InvalidOperationException($"'{hesap.Ad}' banka hesabı pasif durumda.");
                if (!giris)
                    await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, hesap, cek.Tutar);

                _baglam.BankaHareketleri.Add(bankaH = new BankaHareketi
                {
                    SirketId = hesap.SirketId ?? cek.SirketId,
                    BankaHesabiId = hesap.Id,
                    BankaAdi = hesap.Ad,
                    HesapNo = string.IsNullOrWhiteSpace(hesap.Iban) ? hesap.HesapNo : hesap.Iban,
                    Tarih = islemTarihi,
                    Tur = giris ? HareketTuru.Gelir : HareketTuru.Gider,
                    FisTuru = giris ? BankaFisTuru.GelenHavale : BankaFisTuru.GidenHavale,
                    OzelKod = CekSenetOzelKodu,
                    IlgiliBelgeId = cek.Id,
                    FisNo = FinansKurallari.FisNoUret("CEK"),
                    DekontNo = cek.BelgeNo,
                    Tutar = cek.Tutar,
                    Aciklama = hareketAciklamasi,
                    OdemeYontemi = cek.Tur is CekSenetTuru.AlinanSenet or CekSenetTuru.VerilenSenet ? OdemeYontemi.Senet : OdemeYontemi.Cek
                });
            }
        }

        await _baglam.SaveChangesAsync();

        var portfoyKodu = PortfoyKodu(cek.Tur);

        // Tahsil/ödeme: portföydeki kayıtlı çekte karşı hesap çek hesabıdır (101/121, 103/321; cari zaten kapanmıştı).
        // Karşılıksız kalıp borcu yeniden açılmış çekte ya da muhasebeleşmemiş eski çekte para doğrudan cariyi kapatır.
        var cariyiKapatir = !kayitli || eskiDurum == CekSenetDurumu.Karsiliksiz;
        if (paraHareketi)
        {
            var karsiKod = cariyiKapatir ? cariKodu : portfoyKodu;
            int? karsiCari = cariyiKapatir ? cek.CariKartId : null;
            if (cariyiKapatir)
            {
                if (kasaH != null) kasaH.CariKartId = cek.CariKartId;
                if (bankaH != null) bankaH.CariKartId = cek.CariKartId;
                CariHareketEkle(cek.CariKartId, islemTarihi, cek, alinan ? CariHareketTuru.Tahsilat : CariHareketTuru.Odeme,
                    borc: alinan ? 0 : cek.Tutar, alacak: alinan ? cek.Tutar : 0, $"{TurAdi(cek.Tur)} {(alinan ? "tahsilatı" : "ödemesi")} No: {cek.BelgeNo}");
                await _baglam.SaveChangesAsync();
            }
            if (kasaH != null)
                await FinansMuhasebeKaydi.KasaHareketiFisiEkleAsync(_baglam, kasaH, karsiKod, karsiCari);
            if (bankaH != null)
                await FinansMuhasebeKaydi.BankaHareketiFisiEkleAsync(_baglam, bankaH, karsiKod, karsiCari);
        }
        else if (yeniDurum == CekSenetDurumu.CiroEdildi && ciroCari != null)
        {
            // Ciro: çekin verildiği carinin (tedarikçi) borcu kapanır (320 borç), çek portföyden çıkar (101/121 alacak).
            // Muhasebeleşmemiş eski çekte çeki veren müşterinin borcu da burada kapanır (120 alacak).
            var ciroKodu = ciroCari.Tur == CariTuru.Musteri ? "120" : "320";
            var ciroAciklama = $"{TurAdi(cek.Tur)} ciro No: {cek.BelgeNo} - {ciroCari.Unvan}";
            CariHareketEkle(ciroCari.Id, islemTarihi, cek, CariHareketTuru.Odeme, borc: cek.Tutar, alacak: 0, ciroAciklama);
            if (!kayitli)
                CariHareketEkle(cek.CariKartId, islemTarihi, cek, CariHareketTuru.Tahsilat, borc: 0, alacak: cek.Tutar, ciroAciklama);
            await _baglam.SaveChangesAsync();
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, cek.SirketId, "CekSenetCiro", cek.Id, islemTarihi, ciroAciklama, cek.BelgeNo,
                new FinansMuhasebeKaydi.Satir(ciroKodu, cek.Tutar, 0, ciroCari.Id),
                kayitli ? new FinansMuhasebeKaydi.Satir(portfoyKodu, 0, cek.Tutar) : new FinansMuhasebeKaydi.Satir(cariKodu, 0, cek.Tutar, cek.CariKartId));
        }
        else if (kayitli && eskiDurum != CekSenetDurumu.Karsiliksiz && yeniDurum is CekSenetDurumu.Karsiliksiz or CekSenetDurumu.IadeEdildi)
        {
            // Karşılıksız / iade: çek kapattığı borcu geri açar. Alınan: 120 borç - 101/121 alacak (müşteri yeniden borçlu);
            // verilen: 103/321 borç - 320 alacak (tedarikçiye borcumuz geri gelir). Karşılıksızdan iadeye geçiş ayrıca kayıt üretmez.
            var neden = yeniDurum == CekSenetDurumu.Karsiliksiz ? "karşılıksız" : "iade";
            var acik = $"{TurAdi(cek.Tur)} {neden} No: {cek.BelgeNo}";
            CariHareketEkle(cek.CariKartId, islemTarihi, cek, CariHareketTuru.Diger, borc: alinan ? cek.Tutar : 0, alacak: alinan ? 0 : cek.Tutar, acik);
            await _baglam.SaveChangesAsync();
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, cek.SirketId, $"CekSenet{yeniDurum}", cek.Id, islemTarihi, acik, cek.BelgeNo,
                alinan
                    ? [new FinansMuhasebeKaydi.Satir(cariKodu, cek.Tutar, 0, cek.CariKartId), new FinansMuhasebeKaydi.Satir(portfoyKodu, 0, cek.Tutar)]
                    : [new FinansMuhasebeKaydi.Satir(portfoyKodu, cek.Tutar, 0), new FinansMuhasebeKaydi.Satir(cariKodu, 0, cek.Tutar, cek.CariKartId)]);
        }

        if (tx != null)
            await tx.CommitAsync();
    }

    private static string PortfoyKodu(CekSenetTuru tur) => tur switch
    {
        CekSenetTuru.AlinanCek => "101",
        CekSenetTuru.VerilenCek => "103",
        CekSenetTuru.AlinanSenet => "121",
        _ => "321"
    };

    private static string TurAdi(CekSenetTuru tur) => tur switch
    {
        CekSenetTuru.AlinanCek => "Alınan çek",
        CekSenetTuru.VerilenCek => "Verilen çek",
        CekSenetTuru.AlinanSenet => "Alınan senet",
        _ => "Verilen senet"
    };

    // Çek alınırken/verilirken muhasebeleşti mi (Tahsilat/Ödeme ya da bu ekrandan girilen çeklerin 101/103/121/321 satırlı fişi).
    private Task<bool> PortfoyFisiVarAsync(int cekId) => _baglam.YevmiyeFisleri.AnyAsync(f => f.BelgeTipi == "TahsilatOdeme" && f.BelgeId == cekId &&
        f.Satirlar.Any(s => s.Hesap!.HesapKodu == "101" || s.Hesap.HesapKodu == "103" || s.Hesap.HesapKodu == "121" || s.Hesap.HesapKodu == "321"));

    private void CariHareketEkle(int cariId, DateTime tarih, CekSenet cek, CariHareketTuru tur, decimal borc, decimal alacak, string aciklama) =>
        _baglam.CariHareketler.Add(new CariHareket
        {
            CariKartId = cariId, Tarih = tarih, BelgeNo = cek.BelgeNo, IlgiliBelgeId = cek.Id, HareketTuru = tur, Borc = borc, Alacak = alacak, Aciklama = aciklama
        });

    public async Task SilAsync(int id, string neden)
    {
        Oturum.YetkiKontrolEt(y => y.CekSenetIslemYap, "Çek/Senet Silme");
        if (string.IsNullOrWhiteSpace(neden))
            throw new ArgumentException("Silme nedeni zorunludur.");

        var cek = await _baglam.CekSenetler.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Kayıt bulunamadı.");

        // Portföyden çıkmış (tahsil, ciro, ödeme vb.) bir çeki silmek kasa/banka ve muhasebe kayıtlarını yetim bırakır.
        if (cek.Durum != CekSenetDurumu.Portfoyde)
            throw new InvalidOperationException($"'{DurumAdi(cek.Durum)}' durumundaki çek/senet silinemez. Yalnızca portföydeki kayıtlar silinebilir.");

        // Cariye ve muhasebeye işlenmiş çek tek başına silinirse cari bakiye bozulur: tahsilat/ödeme iptaliyle aynı
        // ters kayıtla geri alınır (cari ve muhasebe kaydı ters çevrilir, çek silinmiş olarak işaretlenir).
        if (await PortfoyFisiVarAsync(cek.Id))
        {
            await new TahsilatOdemeServisi(_baglam, Oturum).IptalCekirdekAsync("Portfoy", cek.Id, neden.Trim(), yetkiKontrolu: false, cekSilmeNedeni: neden.Trim());
            return;
        }

        cek.SilindiMi = true;
        cek.SilmeNedeni = neden.Trim();

        await _baglam.SaveChangesAsync();
    }

    private static string DurumAdi(CekSenetDurumu durum) => durum switch
    {
        CekSenetDurumu.Portfoyde => "Portföyde",
        CekSenetDurumu.CiroEdildi => "Ciro Edildi",
        CekSenetDurumu.TahsileVerildi => "Tahsile Verildi",
        CekSenetDurumu.TeminataVerildi => "Teminata Verildi",
        CekSenetDurumu.TahsilEdildi => "Tahsil Edildi",
        CekSenetDurumu.Odendi => "Ödendi",
        CekSenetDurumu.Karsiliksiz => "Karşılıksız",
        _ => "İade Edildi"
    };
}
