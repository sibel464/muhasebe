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

        _baglam.CekSenetler.Add(cekSenet);
        await _baglam.SaveChangesAsync();

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

    public async Task DurumGuncelleAsync(int cekSenetId, CekSenetDurumu yeniDurum, int? kasaId = null, int? bankaHesabiId = null, string? aciklama = null)
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

        var islemTarihi = DateTime.UtcNow;
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, cek.SirketId);

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

        // Çek/senet tahsili: 100/102 borç - 101/121 alacak; verilen çek/senet ödemesi: 103/321 borç - 100/102 alacak.
        // Cari hesap çek alındığında/verildiğinde kapandığı için tahsil/ödeme cariye ikinci kez işlenmez.
        var portfoyKodu = cek.Tur switch
        {
            CekSenetTuru.AlinanCek => "101",
            CekSenetTuru.VerilenCek => "103",
            CekSenetTuru.AlinanSenet => "121",
            _ => "321"
        };
        if (kasaH != null)
            await FinansMuhasebeKaydi.KasaHareketiFisiEkleAsync(_baglam, kasaH, portfoyKodu);
        if (bankaH != null)
            await FinansMuhasebeKaydi.BankaHareketiFisiEkleAsync(_baglam, bankaH, portfoyKodu);

        if (tx != null)
            await tx.CommitAsync();
    }

    public async Task SilAsync(int id, string neden)
    {
        Oturum.YetkiKontrolEt(y => y.CekSenetIslemYap, "Çek/Senet Silme");
        if (string.IsNullOrWhiteSpace(neden))
            throw new ArgumentException("Silme nedeni zorunludur.");

        var cek = await _baglam.CekSenetler.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Kayıt bulunamadı.");

        // Portföyden çıkmış (tahsil, ciro, ödeme vb.) bir çeki silmek kasa/banka ve muhasebe kayıtlarını yetim bırakır.
        if (cek.Durum != CekSenetDurumu.Portfoyde)
            throw new InvalidOperationException($"'{DurumAdi(cek.Durum)}' durumundaki çek/senet silinemez. Yalnızca portföydeki kayıtlar silinebilir.");

        // Tahsilat/Ödeme ekranından alınan çek cari hesaba ve muhasebeye işlenmiştir; tek başına silinirse cari bakiye bozulur.
        var tahsilatlaGeldi = await _baglam.YevmiyeFisleri.AnyAsync(f => f.BelgeTipi == "TahsilatOdeme" && f.BelgeId == cek.Id &&
            f.Satirlar.Any(s => s.Hesap!.HesapKodu == "101" || s.Hesap.HesapKodu == "103" || s.Hesap.HesapKodu == "121" || s.Hesap.HesapKodu == "321"));
        if (tahsilatlaGeldi)
            throw new InvalidOperationException("Bu çek/senet Tahsilat/Ödeme ekranından girilmiş ve cari hesaba işlenmiştir; silinemez.");

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
