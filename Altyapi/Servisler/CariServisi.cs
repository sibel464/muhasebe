using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Altyapi.Veri;
using Npgsql;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Altyapi.Servisler;

public class CariServisi : ICariServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public CariServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<CariKart>> TumunuGetirAsync(CariTuru? turFilter = null)
    {
        var sorgu = _baglam.CariKartlar.AsQueryQueryFilterSafe(); // Global query filter already applied

        if (turFilter.HasValue)
        {
            sorgu = TurFiltresiUygula(sorgu, turFilter.Value);
        }

        return await sorgu
            .Include(c => c.CariTipi)
            .OrderBy(c => c.Unvan)
            .ToListAsync();
    }

    public async Task<CariKart?> IdIleGetirAsync(int id)
    {
        return await _baglam.CariKartlar
            .Include(c => c.CariTipi)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<CariKart?> KodIleGetirAsync(string kod)
    {
        return await _baglam.CariKartlar
            .Include(c => c.CariTipi)
            .FirstOrDefaultAsync(c => c.Kod == kod);
    }

    public async Task<CariKart> EkleAsync(CariKart cari)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.CariEkle, "Cari Kart Ekleme");

        // Temel Ticari Validasyonlar
        if (string.IsNullOrWhiteSpace(cari.Kod))
            throw new ArgumentException("Cari kodu boş olamaz.");

        if (string.IsNullOrWhiteSpace(cari.Unvan))
            throw new ArgumentException("Cari ünvan/ad boş olamaz.");

        // Boşlukları temizle
        cari.Kod = KodNormalizeEt(cari.Kod);
        cari.GrupKodu = GrupKoduNormalizeEt(cari.GrupKodu);
        cari.Unvan = cari.Unvan.Trim();
        if (cari.Ad != null) cari.Ad = cari.Ad.Trim();
        if (cari.Soyad != null) cari.Soyad = cari.Soyad.Trim();

        var kodVarMi = await KodMevcutMuAsync(cari.Kod);
        if (kodVarMi)
            throw new InvalidOperationException($"'{cari.Kod}' kodlu cari kart zaten mevcut.");

        cari.OlusturmaTarihi = DateTime.UtcNow;
        cari.Silindi = false;

        _baglam.CariKartlar.Add(cari);
        try { await _baglam.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new InvalidOperationException("Bu cari kodu zaten kullanılıyor.", ex); }
        return cari;
    }

    public async Task<CariKart> OtomatikKodlaEkleAsync(CariKart cari, bool otomatikGrupKodu = false, bool otomatikCariKodu = true)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.CariEkle, "Cari Kart Ekleme");
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(43415249)");
            if (otomatikCariKodu) cari.Kod = await SonrakiKoduHesaplaAsync();
            if (otomatikGrupKodu) cari.GrupKodu = await SonrakiGrupKodunuHesaplaAsync();
            var sonuc = await EkleAsync(cari);
            await tx.CommitAsync();
            return sonuc;
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public Task<string> OtomatikKodOnizleAsync() => SonrakiKoduHesaplaAsync();
    public Task<string> OtomatikGrupKoduOnizleAsync() => SonrakiGrupKodunuHesaplaAsync();

    public async Task<List<string>> GrupKodlariniGetirAsync() => await _baglam.CariKartlar
        .Where(x => x.GrupKodu != null && x.GrupKodu != "")
        .Select(x => x.GrupKodu!).Distinct().OrderBy(x => x).ToListAsync();

    public async Task<List<string>> CariKodlariniGetirAsync() => await _baglam.CariKartlar
        .Select(x => x.Kod).Distinct().OrderBy(x => x).ToListAsync();

    private async Task<string> SonrakiKoduHesaplaAsync()
    {
        var kodlar = await _baglam.CariKartlar.IgnoreQueryFilters().Where(x => x.Kod.StartsWith("C-")).Select(x => x.Kod).ToListAsync();
        var son = kodlar.Select(x => Regex.Match(x, "^C-(\\d{6})$"))
            .Where(x => x.Success).Select(x => int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).DefaultIfEmpty().Max();
        if (son == 999999) throw new InvalidOperationException("Otomatik cari kodu kapasitesi doldu.");
        return $"C-{son + 1:000000}";
    }

    private async Task<string> SonrakiGrupKodunuHesaplaAsync()
    {
        var kodlar = await _baglam.CariKartlar.IgnoreQueryFilters().Where(x => x.GrupKodu != null && x.GrupKodu.StartsWith("GRP-")).Select(x => x.GrupKodu!).Distinct().ToListAsync();
        var son = kodlar.Select(x => Regex.Match(x, "^GRP-(\\d{6})$"))
            .Where(x => x.Success).Select(x => int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).DefaultIfEmpty().Max();
        if (son == 999999) throw new InvalidOperationException("Otomatik grup kodu kapasitesi doldu.");
        return $"GRP-{son + 1:000000}";
    }

    // "Müşteri ve Tedarikçi" türündeki cari hem müşteri hem tedarikçi aramalarında yer alır.
    private static IQueryable<CariKart> TurFiltresiUygula(IQueryable<CariKart> sorgu, CariTuru tur) =>
        tur == CariTuru.MusteriTedarikci
            ? sorgu.Where(c => c.Tur == CariTuru.MusteriTedarikci)
            : sorgu.Where(c => c.Tur == tur || c.Tur == CariTuru.MusteriTedarikci);

    private static string KodNormalizeEt(string kod) => kod.Trim().ToUpperInvariant();
    private static string? GrupKoduNormalizeEt(string? kod) => string.IsNullOrWhiteSpace(kod) ? null : kod.Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"));

    public async Task<CariKart> GuncelleAsync(CariKart cari)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.CariDuzenle, "Cari Kart Güncelleme");

        if (string.IsNullOrWhiteSpace(cari.Kod))
            throw new ArgumentException("Cari kodu boş olamaz.");

        if (string.IsNullOrWhiteSpace(cari.Unvan))
            throw new ArgumentException("Cari ünvan/ad boş olamaz.");

        cari.Kod = KodNormalizeEt(cari.Kod);
        cari.GrupKodu = GrupKoduNormalizeEt(cari.GrupKodu);
        cari.Unvan = cari.Unvan.Trim();

        var kodVarMi = await KodMevcutMuAsync(cari.Kod, cari.Id);
        if (kodVarMi)
            throw new InvalidOperationException($"'{cari.Kod}' kodlu cari kart zaten mevcut.");

        var mevcutCari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cari.Id);
        if (mevcutCari == null)
            throw new KeyNotFoundException("Güncellenecek cari kart bulunamadı.");

        // Güncelleme
        mevcutCari.Kod = cari.Kod;
        mevcutCari.Tur = cari.Tur;
        mevcutCari.CariTipiId = cari.CariTipiId;
        mevcutCari.Unvan = cari.Unvan;
        mevcutCari.GrupKodu = cari.GrupKodu;
        mevcutCari.Ad = cari.Ad?.Trim();
        mevcutCari.Soyad = cari.Soyad?.Trim();
        mevcutCari.VergiDairesi = cari.VergiDairesi;
        mevcutCari.VergiNo = cari.VergiNo;
        mevcutCari.TcNo = cari.TcNo;
        mevcutCari.Telefon = cari.Telefon;
        mevcutCari.EvTelefonu = cari.EvTelefonu;
        mevcutCari.IsTelefonu = cari.IsTelefonu;
        mevcutCari.CepTelefonu = cari.CepTelefonu;
        mevcutCari.Eposta = cari.Eposta;
        mevcutCari.WebSitesi = cari.WebSitesi;
        mevcutCari.Adres = cari.Adres;
        mevcutCari.Il = cari.Il;
        mevcutCari.Ilce = cari.Ilce;
        mevcutCari.PostaKodu = cari.PostaKodu;
        mevcutCari.FotografYolu = cari.FotografYolu;
        mevcutCari.Aciklama = cari.Aciklama;
        mevcutCari.Aktif = cari.Aktif;
        mevcutCari.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        return mevcutCari;
    }

    public async Task<bool> SilAsync(int id, string? silmeNedeni = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.CariSil, "Cari Kart Silme");

        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == id);
        if (cari == null)
            throw new KeyNotFoundException("Silinecek cari kart bulunamadı.");

        // Ticari İlişki Güvenlik Kontrolü
        var islemGormus = await IslemGormusMuAsync(id);
        if (islemGormus)
            throw new InvalidOperationException("Bu cari kart geçmiş işlemler nedeniyle silinemez. Pasif duruma alınabilir.");

        cari.Silindi = true;
        cari.GuncellemeTarihi = DateTime.UtcNow;
        cari.SilinmeTarihi = DateTime.UtcNow;
        cari.SilinmeNedeni = silmeNedeni;

        await _baglam.SaveChangesAsync();
        return true;
    }

    public async Task<List<CariKart>> AraAsync(string aramaMetni, CariTuru? turFilter = null)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return await TumunuGetirAsync(turFilter);

        var sorgu = _baglam.CariKartlar.AsQueryable();

        if (turFilter.HasValue)
        {
            sorgu = TurFiltresiUygula(sorgu, turFilter.Value);
        }

        return await sorgu
            .Where(c => EF.Functions.ILike(c.Kod, $"%{aramaMetni}%") ||
                        EF.Functions.ILike(c.Unvan, $"%{aramaMetni}%") ||
                        (c.Ad != null && EF.Functions.ILike(c.Ad, $"%{aramaMetni}%")) ||
                        (c.Soyad != null && EF.Functions.ILike(c.Soyad, $"%{aramaMetni}%")) ||
                        (c.VergiNo != null && EF.Functions.ILike(c.VergiNo, $"%{aramaMetni}%")) ||
                        (c.Telefon != null && EF.Functions.ILike(c.Telefon, $"%{aramaMetni}%")))
            .OrderBy(c => c.Unvan)
            .ToListAsync();
    }

    public async Task<List<CariKart>> FiltreliAraAsync(CariFiltre filtre)
    {
        ArgumentNullException.ThrowIfNull(filtre);

        var sorgu = _baglam.CariKartlar
            .Include(c => c.CariTipi)
            .AsQueryable();

        if (filtre.Tur.HasValue)
            sorgu = TurFiltresiUygula(sorgu, filtre.Tur.Value);

        if (filtre.CariTipiId.HasValue)
            sorgu = sorgu.Where(c => c.CariTipiId == filtre.CariTipiId.Value);

        if (!string.IsNullOrWhiteSpace(filtre.Kod))
            sorgu = sorgu.Where(c => EF.Functions.ILike(c.Kod, $"%{filtre.Kod.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(filtre.GrupKodu))
            sorgu = sorgu.Where(c => c.GrupKodu != null && EF.Functions.ILike(c.GrupKodu, $"%{filtre.GrupKodu.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(filtre.Il))
            sorgu = sorgu.Where(c => c.Il != null && EF.Functions.ILike(c.Il, $"%{filtre.Il}%"));

        if (!string.IsNullOrWhiteSpace(filtre.AramaMetni))
        {
            sorgu = sorgu.Where(c => EF.Functions.ILike(c.Kod, $"%{filtre.AramaMetni}%") ||
                                     EF.Functions.ILike(c.Unvan, $"%{filtre.AramaMetni}%") ||
                                     (c.Ad != null && EF.Functions.ILike(c.Ad, $"%{filtre.AramaMetni}%")) ||
                                     (c.Soyad != null && EF.Functions.ILike(c.Soyad, $"%{filtre.AramaMetni}%")) ||
                                     (c.VergiNo != null && EF.Functions.ILike(c.VergiNo, $"%{filtre.AramaMetni}%")) ||
                                     (c.Telefon != null && EF.Functions.ILike(c.Telefon, $"%{filtre.AramaMetni}%")));
        }

        // Bakiye filtreleri
        if (filtre.SadeceBorclular)
            sorgu = sorgu.Where(c => c.Borc > c.Alacak);
        else if (filtre.SadeceAlacaklilar)
            sorgu = sorgu.Where(c => c.Alacak > c.Borc);

        return await sorgu
            .OrderBy(c => c.Unvan)
            .ToListAsync();
    }

    public async Task<bool> KodMevcutMuAsync(string kod, int? haricId = null)
    {
        var normal = KodNormalizeEt(kod);
        return await _baglam.CariKartlar
            .AnyAsync(c => c.Kod.ToUpper() == normal && (!haricId.HasValue || c.Id != haricId.Value));
    }

    public async Task<bool> IslemGormusMuAsync(int id)
    {
        // Müşteriyle/cariyle ilişkili fatura var mı kontrol et
        return await _baglam.CariHareketler.AnyAsync(h => h.CariKartId == id)
            || await _baglam.Faturalar.AnyAsync(f => f.CariKartId == id)
            || await _baglam.Siparisler.AnyAsync(s => s.CariKartId == id)
            || await _baglam.Irsaliyeler.AnyAsync(i => i.CariKartId == id)
            || await _baglam.CekSenetler.AnyAsync(c => c.CariKartId == id)
            || await _baglam.KasaHareketleri.AnyAsync(h => h.CariKartId == id)
            || await _baglam.BankaHareketleri.AnyAsync(h => h.CariKartId == id);
    }
}

// Global sorgu filtrelerinin düzgün çalışmasını sağlamak için yardımcı uzantı metodu
internal static class QueryExtensions
{
    public static IQueryable<T> AsQueryQueryFilterSafe<T>(this DbSet<T> dbSet) where T : class
    {
        return dbSet.AsQueryable();
    }
}
