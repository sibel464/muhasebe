using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class CariHareketServisi : ICariHareketServisi
{
    public const string CariFisBelgeTipi = "CariFis";

    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public CariHareketServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<CariHareket>> CariKartinHareketleriniGetirAsync(int cariKartId)
    {
        return await _baglam.CariHareketler
            .Where(h => h.CariKartId == cariKartId)
            .OrderBy(h => h.Tarih)
            .ThenBy(h => h.OlusturmaTarihi)
            .ToListAsync();
    }

    // Cari fişinde karşı hesap olarak seçilebilecek aktif muhasebe hesapları.
    // Hazır değerler (10x) seçilemez: nakit/banka hareketi Tahsilat/Ödeme ile girilir, yoksa kasa/banka bakiyesi mizandan kopar.
    public async Task<List<(string Kod, string Ad)>> KarsiHesaplariGetirAsync(Guid sirketId)
    {
        var hesaplar = await _baglam.HesapPlanlari.AsNoTracking()
            .Where(h => h.SirketId == sirketId && h.AktifMi && !h.HesapKodu.StartsWith("10"))
            .OrderBy(h => h.HesapKodu)
            .Select(h => new { h.HesapKodu, h.HesapAdi })
            .ToListAsync();
        var liste = hesaplar.Select(h => (Kod: h.HesapKodu, Ad: h.HesapAdi)).ToList();
        // Önerilen standart hesaplar planda henüz yoksa da listelenir; ilk kullanımda otomatik açılır.
        foreach (var (kod, ad) in OnerilenHesaplar)
            if (liste.All(h => h.Kod != kod))
                liste.Add((kod, ad));
        return liste.OrderBy(h => h.Kod, StringComparer.Ordinal).ToList();
    }

    private static readonly (string Kod, string Ad)[] OnerilenHesaplar =
    [
        ("500", "SERMAYE"),
        ("602", "DİĞER GELİRLER"),
        ("649", "DİĞER OLAĞAN GELİR VE KARLAR"),
        ("659", "DİĞER OLAĞAN GİDER VE ZARARLAR (-)"),
        ("770", "GENEL YÖNETİM GİDERLERİ")
    ];

    // Elle girilen cari fişi (açılış/devir, borç dekontu, alacak dekontu). Cari hareketi ve muhasebe fişi
    // aynı işlemde yazılır: cari tarafı müşteride 120, tedarikçide 320; karşısında seçilen hesap.
    public async Task<CariHareket> EkleAsync(CariHareket hareket, string karsiHesapKodu)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.CariDuzenle, "Cari fiş oluşturma");

        if (hareket.CariKartId <= 0)
            throw new ArgumentException("Geçersiz Cari Kart ID.");

        if (hareket.HareketTuru is not (CariHareketTuru.Devir or CariHareketTuru.Diger))
            throw new ArgumentException("Elle yalnızca açılış/devir ve borç/alacak dekontu girilebilir; fatura ve tahsilatlar kendi ekranlarından girilir.");

        if (hareket.Borc < 0 || hareket.Alacak < 0)
            throw new ArgumentException("Borç veya Alacak tutarı negatif olamaz.");

        if (hareket.Borc > 0 && hareket.Alacak > 0)
            throw new ArgumentException("Bir hareket aynı anda hem borç hem alacak oluşturamaz.");

        if (hareket.Borc == 0 && hareket.Alacak == 0)
            throw new ArgumentException("Tutar sıfırdan büyük olmalıdır.");

        if (string.IsNullOrWhiteSpace(hareket.Aciklama))
            throw new ArgumentException("Hareket açıklaması boş bırakılamaz.");

        karsiHesapKodu = karsiHesapKodu?.Trim() ?? string.Empty;
        if (karsiHesapKodu.Length == 0)
            throw new ArgumentException("Karşı muhasebe hesabını seçin.");
        if (karsiHesapKodu.StartsWith("10", StringComparison.Ordinal))
            throw new ArgumentException("Kasa/banka hesapları karşı hesap olamaz. Nakit veya banka işlemi için Tahsilat / Ödeme kullanın.");

        hareket.Aciklama = hareket.Aciklama.Trim();
        hareket.BelgeNo = string.IsNullOrWhiteSpace(hareket.BelgeNo) ? null : hareket.BelgeNo.Trim();
        hareket.Tarih = DateTime.SpecifyKind(hareket.Tarih, DateTimeKind.Utc);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == hareket.CariKartId)
                ?? throw new KeyNotFoundException("İlgili cari kart bulunamadı.");

            if (!cari.Aktif)
                throw new InvalidOperationException("Pasif durumdaki cari karta işlem girilemez.");

            var sirketId = cari.SirketId ?? MuhasebeBaglami.VarsayilanSirketId;
            if (await _baglam.MaliDonemler.AnyAsync(d => d.KilitliMi && d.SirketId == sirketId && d.Baslangic <= hareket.Tarih && d.Bitis >= hareket.Tarih))
                throw new InvalidOperationException("Bu tarih kilitli bir muhasebe dönemine düşüyor; fiş girilemez.");

            var cariHesap = cari.Tur == CariTuru.Tedarikci ? "320" : "120";
            if (karsiHesapKodu == cariHesap)
                throw new ArgumentException($"Karşı hesap carinin kendi hesabı ({cariHesap}) olamaz.");

            hareket.OlusturmaTarihi = DateTime.UtcNow;
            _baglam.CariHareketler.Add(hareket);
            // Bakiye alanları ve versiyon MuhasebeBaglami.SaveChangesAsync tarafından otomatik güncelleniyor.
            await _baglam.SaveChangesAsync();

            var tutar = hareket.Borc + hareket.Alacak;
            var cariBorc = hareket.Borc > 0;
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, sirketId, CariFisBelgeTipi, hareket.Id, hareket.Tarih,
                $"{cari.Unvan} • {hareket.Aciklama}", hareket.BelgeNo,
                cariBorc ? new FinansMuhasebeKaydi.Satir(cariHesap, tutar, 0, cari.Id) : new FinansMuhasebeKaydi.Satir(karsiHesapKodu, tutar, 0),
                cariBorc ? new FinansMuhasebeKaydi.Satir(karsiHesapKodu, 0, tutar) : new FinansMuhasebeKaydi.Satir(cariHesap, 0, tutar, cari.Id));

            await transaction.CommitAsync();
            return hareket;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Cari bakiye güncelleme çakışması tespit edildi. Eş zamanlı işlem başarısız oldu. Lütfen tekrar deneyiniz.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> SilAsync(int id)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.CariDuzenle, "Cari fiş silme");

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var hareket = await _baglam.CariHareketler
                .FirstOrDefaultAsync(h => h.Id == id);

            if (hareket == null) return false;

            // Fatura vb. entegrasyon belgelerinden otomatik gelen hareketler doğrudan silinemez
            if (hareket.IlgiliBelgeId.HasValue || hareket.HareketTuru is not (CariHareketTuru.Devir or CariHareketTuru.Diger))
                throw new InvalidOperationException("Belgeye bağlı otomatik cari hareketler doğrudan silinemez. İlgili belge üzerinden işlem yapınız.");

            var simdi = DateTime.UtcNow;
            hareket.Silindi = true;
            hareket.SilinmeTarihi = simdi;

            // Hareketin muhasebe fişi de silinir; cari bakiye ile mizan birlikte geri döner.
            var fisler = await _baglam.YevmiyeFisleri.Include(f => f.Satirlar)
                .Where(f => f.BelgeTipi == CariFisBelgeTipi && f.BelgeId == id)
                .ToListAsync();
            foreach (var kayit in fisler.SelectMany(f => f.Satirlar.Cast<EtkiAlani.Modeller.Ortak.TemelVarlik>().Append(f)))
            {
                kayit.Silindi = true;
                kayit.SilinmeTarihi = simdi;
                kayit.SilinmeNedeni = "Cari fiş silindi";
                kayit.GuncellemeTarihi = simdi;
            }

            await _baglam.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Cari bakiye güncelleme çakışması tespit edildi. Lütfen tekrar deneyiniz.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<decimal> BakiyeHesaplaAsync(int cariKartId)
    {
        // Doğrulama amaçlı dinamik toplam hesabı (Yaklaşım A)
        var toplamlar = await _baglam.CariHareketler
            .Where(h => h.CariKartId == cariKartId)
            .GroupBy(h => h.CariKartId)
            .Select(g => new
            {
                Borc = g.Sum(x => x.Borc),
                Alacak = g.Sum(x => x.Alacak)
            })
            .FirstOrDefaultAsync();

        if (toplamlar == null) return 0;
        return toplamlar.Borc - toplamlar.Alacak;
    }
}
