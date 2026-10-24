using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Finans;
using Altyapi.Veri;

namespace Altyapi.Servisler;

// Kasa ve banka servislerinin ortak iş kuralları: tutar sınırı, kilitli dönem,
// satır kilidi ve bakiye yeterliliği tek yerde tutulur.
internal static class FinansKurallari
{
    public const decimal EnBuyukTutar = 99999999999999.9999m;
    public const int AciklamaUzunlugu = 500;

    public const string YerelParaBirimi = "TRY";

    public static bool DovizliMi(string? paraBirimi) =>
        !string.IsNullOrWhiteSpace(paraBirimi) && !string.Equals(paraBirimi, YerelParaBirimi, StringComparison.OrdinalIgnoreCase);

    // Dövizli kasa/bankada hareket hesabın para biriminde döviz tutarı ve kurla girilir, TL tutar döviz × kurdur
    // (defter, mizan ve raporlar TL'den çalışır; döviz bakiyesi döviz tutarlarından). TL hesapta girilen değerler aynen kalır.
    public static async Task<(decimal Tutar, int? ParaBirimiId, decimal Kur, decimal DovizTutar)> HesapTutariAsync(
        MuhasebeBaglami baglam, string hesapAdi, string hesapParaBirimi, decimal tutar, decimal dovizTutar, decimal kur, int? paraBirimiId = null)
    {
        if (!DovizliMi(hesapParaBirimi))
            return (tutar, paraBirimiId, kur > 0 ? kur : 1, dovizTutar);
        if (dovizTutar <= 0 || kur <= 0)
            throw new ArgumentException($"'{hesapAdi}' bir {hesapParaBirimi} hesabıdır; {hesapParaBirimi} tutarı ve kur girilmelidir.");
        TutarDogrula(dovizTutar, $"{hesapParaBirimi}");
        if (kur > 1_000_000m || decimal.Round(kur, 6) != kur)
            throw new ArgumentException("Kur sıfırdan büyük olmalı ve en fazla 6 ondalık basamak içermelidir.");
        var tl = Math.Round(dovizTutar * kur, 2, MidpointRounding.AwayFromZero);
        TutarDogrula(tl, "TL karşılığı");
        return (tl, await ParaBirimiIdAsync(baglam, hesapParaBirimi), kur, dovizTutar);
    }

    // Para birimi tablosunda yalnız TRY tohumlanır; dövizli hesap ilk hareketinde kodu tabloya eklenir.
    public static async Task<int> ParaBirimiIdAsync(MuhasebeBaglami baglam, string kod)
    {
        kod = kod.Trim().ToUpperInvariant();
        var mevcut = await baglam.ParaBirimleri.Where(p => p.Kod == kod).Select(p => (int?)p.Id).FirstOrDefaultAsync();
        if (mevcut.HasValue)
            return mevcut.Value;
        var yeni = new EtkiAlani.Modeller.Kurumsal.ParaBirimi { Kod = kod, Ad = kod, Sembol = kod };
        baglam.ParaBirimleri.Add(yeni);
        await baglam.SaveChangesAsync();
        return yeni.Id;
    }

    public static void TutarDogrula(decimal tutar, string islemAdi)
    {
        if (tutar <= 0 || tutar > EnBuyukTutar)
            throw new ArgumentException($"{islemAdi} tutarı sıfırdan büyük ve geçerli bir değer olmalıdır.");
        if (decimal.Round(tutar, 2) != tutar)
            throw new ArgumentException($"{islemAdi} tutarı en fazla 2 ondalık basamak içerebilir.");
    }

    public static string AciklamaDogrula(string? aciklama)
    {
        if (string.IsNullOrWhiteSpace(aciklama))
            throw new ArgumentException("Açıklama boş bırakılamaz.");
        var temiz = aciklama.Trim();
        if (temiz.Length > AciklamaUzunlugu)
            throw new ArgumentException($"Açıklama en fazla {AciklamaUzunlugu} karakter olabilir.");
        return temiz;
    }

    public static DateTime TarihNormalizeEt(DateTime? tarih)
    {
        // Belge tarihi yerel saat (UTC etiketli); tarih verilmezse gerçek UTC gece 00-03 arası işlemi önceki güne atardı.
        var deger = DateTime.SpecifyKind(tarih ?? DateTime.Now, DateTimeKind.Utc);
        if (deger.Date > DateTime.Now.Date.AddDays(1))
            throw new ArgumentException("İleri tarihli kasa/banka hareketi girilemez.");
        return deger;
    }

    public static async Task DonemAcikOlmaliAsync(MuhasebeBaglami baglam, DateTime tarih, Guid? sirketId)
    {
        var kilitli = await baglam.MaliDonemler.AnyAsync(d =>
            d.KilitliMi && d.Baslangic <= tarih && d.Bitis >= tarih &&
            (sirketId == null || d.SirketId == sirketId));
        if (kilitli)
            throw new InvalidOperationException($"{tarih:dd.MM.yyyy} tarihi kilitli bir mali döneme ait. Bu tarihe kayıt eklenemez veya silinemez.");
    }

    // Aynı kasa/hesap üzerinde eşzamanlı çıkışların bakiyeyi birlikte eksiye düşürmesini engeller.
    // Çağıran tarafın açık bir transaction içinde olması gerekir.
    public static Task KasaKilitleAsync(MuhasebeBaglami baglam, int kasaId) =>
        baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Kasalar\" WHERE \"Id\" = {kasaId} FOR UPDATE");

    public static Task BankaHesabiKilitleAsync(MuhasebeBaglami baglam, int bankaHesabiId) =>
        baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"BankaHesaplari\" WHERE \"Id\" = {bankaHesabiId} FOR UPDATE");

    // İptal edilen işlem ile ters kaydı birbirini götürür; ikisi de bakiyeye katılmaz.
    public static async Task<decimal> KasaBakiyesiAsync(MuhasebeBaglami baglam, int kasaId)
    {
        var gelir = await baglam.KasaHareketleri
            .Where(k => k.KasaId == kasaId && k.IslemDurumu == FinansIslemDurumu.Tamamlandi && k.Tur == HareketTuru.Gelir)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;
        var gider = await baglam.KasaHareketleri
            .Where(k => k.KasaId == kasaId && k.IslemDurumu == FinansIslemDurumu.Tamamlandi && k.Tur == HareketTuru.Gider)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;
        return gelir - gider;
    }

    public static async Task<decimal> BankaBakiyesiAsync(MuhasebeBaglami baglam, int bankaHesabiId)
    {
        var gelir = await baglam.BankaHareketleri
            .Where(b => b.BankaHesabiId == bankaHesabiId && b.IslemDurumu == FinansIslemDurumu.Tamamlandi && b.Tur == HareketTuru.Gelir)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;
        var gider = await baglam.BankaHareketleri
            .Where(b => b.BankaHesabiId == bankaHesabiId && b.IslemDurumu == FinansIslemDurumu.Tamamlandi && b.Tur == HareketTuru.Gider)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;
        return gelir - gider;
    }

    // Dövizli hesabın bakiyesi hesabın para birimindedir (döviz tutarlarının toplamı).
    public static async Task<decimal> KasaDovizBakiyesiAsync(MuhasebeBaglami baglam, int kasaId) =>
        await baglam.KasaHareketleri
            .Where(k => k.KasaId == kasaId && k.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(k => (decimal?)(k.Tur == HareketTuru.Gelir ? k.DovizTutar : -k.DovizTutar)) ?? 0;

    public static async Task<decimal> BankaDovizBakiyesiAsync(MuhasebeBaglami baglam, int bankaHesabiId) =>
        await baglam.BankaHareketleri
            .Where(b => b.BankaHesabiId == bankaHesabiId && b.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            .SumAsync(b => (decimal?)(b.Tur == HareketTuru.Gelir ? b.DovizTutar : -b.DovizTutar)) ?? 0;

    // Aynı para birimindeki iki hesap arası transferde (virman, kasadan bankaya...) tutar hesabın para birimindedir.
    // Dövizli hesapta TL karşılığı kaynak hesabın ortalama defter kuruyla taşınır (tamamı çıkıyorsa TL bakiyenin tamamı);
    // kur farkı dönem sonu revalüasyonda ortaya çıkar. TL hesapta tutar aynen döner.
    public static async Task<(decimal Tutar, int? ParaBirimiId, decimal Kur, decimal DovizTutar)> KasaTransferTutariAsync(MuhasebeBaglami baglam, Kasa kaynak, decimal tutar) =>
        DovizliMi(kaynak.ParaBirimi)
            ? await TransferTutariAsync(baglam, kaynak.ParaBirimi, tutar, await KasaBakiyesiAsync(baglam, kaynak.Id), await KasaDovizBakiyesiAsync(baglam, kaynak.Id))
            : (tutar, null, 1m, 0m);

    public static async Task<(decimal Tutar, int? ParaBirimiId, decimal Kur, decimal DovizTutar)> BankaTransferTutariAsync(MuhasebeBaglami baglam, BankaHesabi kaynak, decimal tutar) =>
        DovizliMi(kaynak.ParaBirimi)
            ? await TransferTutariAsync(baglam, kaynak.ParaBirimi, tutar, await BankaBakiyesiAsync(baglam, kaynak.Id), await BankaDovizBakiyesiAsync(baglam, kaynak.Id))
            : (tutar, null, 1m, 0m);

    private static async Task<(decimal, int?, decimal, decimal)> TransferTutariAsync(MuhasebeBaglami baglam, string paraBirimi, decimal dovizTutar, decimal tlBakiye, decimal dovizBakiye)
    {
        if (dovizBakiye <= 0 || tlBakiye <= 0)
            return (0m, null, 0m, dovizTutar); // yetersiz bakiye; çıkış denetimi ayrıntılı mesajı verir
        var kur = Math.Round(tlBakiye / dovizBakiye, 6, MidpointRounding.AwayFromZero);
        var tl = dovizTutar >= dovizBakiye ? tlBakiye : Math.Round(dovizTutar * kur, 2, MidpointRounding.AwayFromZero);
        return (tl, await ParaBirimiIdAsync(baglam, paraBirimi), kur, dovizTutar);
    }

    public static string DovizliHesaptaYapilamaz(string hesapAdi, string paraBirimi) =>
        $"'{hesapAdi}' bir {paraBirimi} hesabıdır. Bu işlem dövizli hesapta yapılamaz; TL hesap seçin " +
        $"veya işlemi kasa/banka fişi ya da tahsilat/ödeme ekranından {paraBirimi} tutarı ve kuruyla girin.";

    // Dövizli hesapta yeterlilik döviz tutarıyla denetlenir; döviz tutarı verilmeyen akış dövizli hesabı kullanamaz.
    public static async Task KasaCikisiYapilabilirAsync(MuhasebeBaglami baglam, Kasa kasa, decimal tutar, decimal? dovizTutar = null)
    {
        if (DovizliMi(kasa.ParaBirimi))
        {
            if (dovizTutar is not > 0)
                throw new InvalidOperationException(DovizliHesaptaYapilamaz(kasa.Ad, kasa.ParaBirimi));
            var dovizBakiye = await KasaDovizBakiyesiAsync(baglam, kasa.Id);
            if (dovizBakiye - dovizTutar.Value < 0)
                throw new InvalidOperationException($"'{kasa.Ad}' kasasında yeterli bakiye yok. Mevcut bakiye: {dovizBakiye:N2} {kasa.ParaBirimi}, istenen çıkış: {dovizTutar.Value:N2} {kasa.ParaBirimi}. Kasa bakiyesi eksiye düşemez.");
            return;
        }
        var bakiye = await KasaBakiyesiAsync(baglam, kasa.Id);
        if (bakiye - tutar < 0)
            throw new InvalidOperationException($"'{kasa.Ad}' kasasında yeterli bakiye yok. Mevcut bakiye: {bakiye:N2}, istenen çıkış: {tutar:N2}. Kasa bakiyesi eksiye düşemez.");
    }

    // Kredi hesaplarında tanımlı limit kadar eksi bakiyeye izin verilir.
    public static async Task BankaCikisiYapilabilirAsync(MuhasebeBaglami baglam, BankaHesabi hesap, decimal tutar, decimal? dovizTutar = null)
    {
        var limit = Math.Max(0, hesap.KrediLimiti);
        if (DovizliMi(hesap.ParaBirimi))
        {
            if (dovizTutar is not > 0)
                throw new InvalidOperationException(DovizliHesaptaYapilamaz(hesap.Ad, hesap.ParaBirimi));
            var dovizBakiye = await BankaDovizBakiyesiAsync(baglam, hesap.Id);
            if (dovizBakiye - dovizTutar.Value < -limit)
                throw new InvalidOperationException($"'{hesap.Ad}' hesabında yeterli bakiye yok. Mevcut bakiye: {dovizBakiye:N2} {hesap.ParaBirimi}, kullanılabilir limit: {limit:N2}, istenen çıkış: {dovizTutar.Value:N2} {hesap.ParaBirimi}.");
            return;
        }
        var bakiye = await BankaBakiyesiAsync(baglam, hesap.Id);
        if (bakiye - tutar < -limit)
            throw new InvalidOperationException($"'{hesap.Ad}' hesabında yeterli bakiye yok. Mevcut bakiye: {bakiye:N2}, kullanılabilir limit: {limit:N2}, istenen çıkış: {tutar:N2}.");
    }

    public static string FisNoUret(string onEk) =>
        $"{onEk}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
