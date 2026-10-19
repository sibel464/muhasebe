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

    // Kasa/banka hareketleri ve ekranları tutarı TL olarak işler (kur ve döviz tutarı alınmaz); dövizli hesap
    // açılırsa TL girilen bakiye revalüasyonda döviz sanılıp kurla çarpılırdı. Tam döviz desteği gelene kadar
    // yeni hesap ve para birimi değişikliği yalnız TRY olabilir; mevcut dövizli hesaplar olduğu gibi kalır.
    public static void YalnizTlHesapAcilabilir(string paraBirimi, string? kayitliParaBirimi = null)
    {
        if (paraBirimi != "TRY" && !string.Equals(paraBirimi, kayitliParaBirimi, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Dövizli kasa/banka hesabı henüz desteklenmiyor; hesap TL (TRY) olarak açılmalıdır. Dövizli tahsilat ve ödemeler cari/fatura üzerinden kurla girilebilir.");
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

    public static async Task KasaCikisiYapilabilirAsync(MuhasebeBaglami baglam, Kasa kasa, decimal tutar)
    {
        var bakiye = await KasaBakiyesiAsync(baglam, kasa.Id);
        if (bakiye - tutar < 0)
            throw new InvalidOperationException($"'{kasa.Ad}' kasasında yeterli bakiye yok. Mevcut bakiye: {bakiye:N2}, istenen çıkış: {tutar:N2}. Kasa bakiyesi eksiye düşemez.");
    }

    // Kredi hesaplarında tanımlı limit kadar eksi bakiyeye izin verilir.
    public static async Task BankaCikisiYapilabilirAsync(MuhasebeBaglami baglam, BankaHesabi hesap, decimal tutar)
    {
        var bakiye = await BankaBakiyesiAsync(baglam, hesap.Id);
        var limit = Math.Max(0, hesap.KrediLimiti);
        if (bakiye - tutar < -limit)
            throw new InvalidOperationException($"'{hesap.Ad}' hesabında yeterli bakiye yok. Mevcut bakiye: {bakiye:N2}, kullanılabilir limit: {limit:N2}, istenen çıkış: {tutar:N2}.");
    }

    public static string FisNoUret(string onEk) =>
        $"{onEk}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
