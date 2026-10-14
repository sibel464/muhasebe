using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public sealed class SirketServisi : ISirketServisi
{
    private readonly MuhasebeBaglami _db;
    private readonly IKullaniciServisi _kullaniciServisi;
    public SirketServisi(MuhasebeBaglami db, IKullaniciServisi kullaniciServisi)
    {
        _db = db;
        _kullaniciServisi = kullaniciServisi;
    }

    private void YonetimYetkisiKontrolEt(string islem) =>
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet, islem);

    public Task<List<Sirket>> ListeleAsync(string? arama = null, bool? aktif = null)
    {
        var q = _db.Sirketler.AsNoTracking();
        if (aktif.HasValue) q = q.Where(x => x.AktifMi == aktif.Value);
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var a = arama.Trim();
            q = q.Where(x => EF.Functions.ILike(x.SirketKodu, $"%{a}%") || EF.Functions.ILike(x.Unvan, $"%{a}%") ||
                (x.VergiNo != null && EF.Functions.ILike(x.VergiNo, $"%{a}%")) || (x.Telefon != null && EF.Functions.ILike(x.Telefon, $"%{a}%")) ||
                (x.Email != null && EF.Functions.ILike(x.Email, $"%{a}%")));
        }
        return q.OrderBy(x => x.Unvan).ToListAsync();
    }

    public async Task<string> SonrakiKodAsync()
    {
        YonetimYetkisiKontrolEt("Firma Kodu Üretme");
        var sira = await _db.Database.SqlQueryRaw<long>("SELECT nextval('firma_kodu_seq') AS \"Value\"").SingleAsync();
        return $"FRM-{sira:000000}";
    }

    public async Task<Sirket> KaydetAsync(Sirket sirket, bool otomatikKod)
    {
        YonetimYetkisiKontrolEt("Firma Kaydetme");
        ArgumentNullException.ThrowIfNull(sirket);
        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1179797849)");
        if (otomatikKod) sirket.SirketKodu = await SonrakiKodAsync();
        sirket.SirketKodu = sirket.SirketKodu.Trim().ToUpperInvariant();
        sirket.Unvan = sirket.Unvan.Trim();
        if (string.IsNullOrWhiteSpace(sirket.SirketKodu) || string.IsNullOrWhiteSpace(sirket.Unvan)) throw new InvalidOperationException("Firma kodu ve firma ünvanı zorunludur.");
        if (await _db.Sirketler.AnyAsync(x => x.SirketKodu == sirket.SirketKodu && x.Id != sirket.Id)) throw new InvalidOperationException("Bu firma kodu daha önce kullanılmış.");
        if (sirket.VergiNo is { Length: > 0 } && sirket.VergiNo.Length is not (10 or 11)) throw new InvalidOperationException("VKN 10, TCKN 11 haneli olmalıdır.");
        sirket.VergiNo = BosIseNull(sirket.VergiNo);
        sirket.VergiDairesi = BosIseNull(sirket.VergiDairesi);
        sirket.Telefon = BosIseNull(sirket.Telefon);
        sirket.Email = BosIseNull(sirket.Email)?.ToLowerInvariant();
        sirket.Adres = BosIseNull(sirket.Adres);
        if (sirket.VergiNo is { } vergiNo && !vergiNo.All(char.IsDigit))
            throw new InvalidOperationException("VKN/TCKN yalnızca rakamlardan oluşmalıdır.");
        if (sirket.Email is { } email && (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')))
            throw new InvalidOperationException("Geçerli bir e-posta adresi girilmelidir.");
        var mevcut = await _db.Sirketler.FirstOrDefaultAsync(x => x.Id == sirket.Id);
        if (mevcut == null) _db.Sirketler.Add(sirket);
        else { mevcut.SirketKodu=sirket.SirketKodu; mevcut.Unvan=sirket.Unvan; mevcut.VergiNo=sirket.VergiNo; mevcut.VergiDairesi=sirket.VergiDairesi; mevcut.Telefon=sirket.Telefon; mevcut.Email=sirket.Email; mevcut.Adres=sirket.Adres; mevcut.AktifMi=sirket.AktifMi; sirket=mevcut; }
        await _db.SaveChangesAsync(); await tx.CommitAsync(); return sirket;
    }

    public async Task DurumDegistirAsync(Guid id, bool aktif)
    {
        YonetimYetkisiKontrolEt("Firma Durumu Değiştirme");
        var x = await _db.Sirketler.SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Firma bulunamadı.");
        if (!aktif && await _db.Subeler.AnyAsync(s => s.SirketId == id && s.AktifMi))
            throw new InvalidOperationException("Aktif şubeleri bulunan firma pasife alınamaz. Önce şubeleri pasife alın.");
        x.AktifMi = aktif;
        await _db.SaveChangesAsync();
    }
    public Task<List<Sube>> SubeleriGetirAsync(Guid id) => _db.Set<Sube>().AsNoTracking().Where(x=>x.SirketId==id).OrderBy(x=>x.SubeAdi).ToListAsync();
    public Task<List<MaliDonem>> DonemleriGetirAsync(Guid id) => _db.Set<MaliDonem>().AsNoTracking().Where(x=>x.SirketId==id).OrderByDescending(x=>x.Baslangic).ToListAsync();

    public async Task<Sube> SubeKaydetAsync(Sube sube)
    {
        YonetimYetkisiKontrolEt("Şube Kaydetme");
        ArgumentNullException.ThrowIfNull(sube);
        sube.SubeKodu = sube.SubeKodu?.Trim().ToUpperInvariant() ?? string.Empty;
        sube.SubeAdi = sube.SubeAdi?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sube.SubeKodu) || string.IsNullOrWhiteSpace(sube.SubeAdi))
            throw new InvalidOperationException("Şube kodu ve şube adı zorunludur.");
        if (!await _db.Sirketler.AnyAsync(s => s.Id == sube.SirketId && s.AktifMi))
            throw new InvalidOperationException("Şube yalnızca aktif bir firmaya bağlanabilir.");
        if (await _db.Subeler.AnyAsync(s => s.SirketId == sube.SirketId && s.SubeKodu == sube.SubeKodu && s.Id != sube.Id))
            throw new InvalidOperationException("Bu şube kodu firmada daha önce kullanılmış.");
        var mevcut = await _db.Subeler.FirstOrDefaultAsync(s => s.Id == sube.Id);
        if (mevcut == null)
        {
            sube.Telefon = BosIseNull(sube.Telefon); sube.Adres = BosIseNull(sube.Adres); sube.OlusturmaTarihi = DateTime.UtcNow;
            _db.Subeler.Add(sube);
        }
        else
        {
            mevcut.SubeKodu = sube.SubeKodu; mevcut.SubeAdi = sube.SubeAdi;
            mevcut.Telefon = BosIseNull(sube.Telefon); mevcut.Adres = BosIseNull(sube.Adres);
            mevcut.AktifMi = sube.AktifMi; mevcut.GuncellemeTarihi = DateTime.UtcNow; sube = mevcut;
        }
        await _db.SaveChangesAsync();
        return sube;
    }

    public async Task SubeDurumDegistirAsync(int subeId, bool aktif)
    {
        YonetimYetkisiKontrolEt("Şube Durumu Değiştirme");
        var sube = await _db.Subeler.SingleOrDefaultAsync(s => s.Id == subeId) ?? throw new KeyNotFoundException("Şube bulunamadı.");
        if (aktif && !await _db.Sirketler.AnyAsync(s => s.Id == sube.SirketId && s.AktifMi))
            throw new InvalidOperationException("Pasif firmaya bağlı şube aktifleştirilemez.");
        if (!aktif && await _db.Depolar.AnyAsync(d => d.SubeId == subeId && d.AktifMi))
            throw new InvalidOperationException("Aktif depoları bulunan şube pasife alınamaz. Önce depoları pasife alın.");
        sube.AktifMi = aktif; sube.GuncellemeTarihi = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<MaliDonem> MaliDonemKaydetAsync(MaliDonem donem)
    {
        YonetimYetkisiKontrolEt("Mali Dönem Kaydetme");
        ArgumentNullException.ThrowIfNull(donem);
        donem.DonemAdi = donem.DonemAdi?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(donem.DonemAdi)) throw new InvalidOperationException("Mali dönem adı zorunludur.");
        if (donem.Baslangic.Date > donem.Bitis.Date) throw new InvalidOperationException("Mali dönem başlangıcı bitişinden sonra olamaz.");
        if (!await _db.Sirketler.AnyAsync(s => s.Id == donem.SirketId)) throw new InvalidOperationException("Firma bulunamadı.");
        var baslangic = DateTime.SpecifyKind(donem.Baslangic.Date, DateTimeKind.Utc);
        var bitis = DateTime.SpecifyKind(donem.Bitis.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        if (await _db.MaliDonemler.AnyAsync(d => d.SirketId == donem.SirketId && d.Id != donem.Id && d.Baslangic <= bitis && d.Bitis >= baslangic))
            throw new InvalidOperationException("Mali dönem tarihleri aynı firmadaki başka bir dönemle çakışıyor.");
        if (await _db.MaliDonemler.AnyAsync(d => d.SirketId == donem.SirketId && d.Id != donem.Id && d.DonemAdi == donem.DonemAdi))
            throw new InvalidOperationException("Bu mali dönem adı daha önce kullanılmış.");
        var kaliciDeger = await _db.MaliDonemler.AsNoTracking().FirstOrDefaultAsync(d => d.Id == donem.Id);
        if (kaliciDeger?.KilitliMi == true && (kaliciDeger.Baslangic != baslangic || kaliciDeger.Bitis != bitis))
            throw new InvalidOperationException("Kilitli mali dönemin tarihleri değiştirilemez.");
        var mevcut = await _db.MaliDonemler.FirstOrDefaultAsync(d => d.Id == donem.Id);
        if (mevcut == null)
        {
            donem.Baslangic = baslangic; donem.Bitis = bitis; donem.OlusturmaTarihi = DateTime.UtcNow; _db.MaliDonemler.Add(donem);
        }
        else
        {
            mevcut.DonemAdi = donem.DonemAdi; mevcut.Baslangic = baslangic; mevcut.Bitis = bitis;
            mevcut.GuncellemeTarihi = DateTime.UtcNow; donem = mevcut;
        }
        await _db.SaveChangesAsync();
        return donem;
    }

    public async Task MaliDonemKilitleAsync(int donemId, bool kilitli)
    {
        YonetimYetkisiKontrolEt("Mali Dönem Kilitleme");
        var donem = await _db.MaliDonemler.SingleOrDefaultAsync(d => d.Id == donemId) ?? throw new KeyNotFoundException("Mali dönem bulunamadı.");
        donem.KilitliMi = kilitli; donem.GuncellemeTarihi = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public Task<List<BelgeSerisi>> BelgeSerileriniGetirAsync(Guid sirketId) => _db.BelgeSerileri.AsNoTracking()
        .Where(s => s.SirketId == sirketId).OrderBy(s => s.BelgeTipi).ThenBy(s => s.Seri).ToListAsync();

    public async Task<BelgeSerisi> BelgeSerisiKaydetAsync(BelgeSerisi seri)
    {
        YonetimYetkisiKontrolEt("Belge Serisi Kaydetme");
        ArgumentNullException.ThrowIfNull(seri);
        seri.BelgeTipi = seri.BelgeTipi?.Trim() ?? string.Empty;
        seri.Seri = seri.Seri?.Trim().ToUpperInvariant() ?? string.Empty;
        seri.Prefix = BosIseNull(seri.Prefix)?.ToUpperInvariant(); seri.Format = BosIseNull(seri.Format);
        if (string.IsNullOrWhiteSpace(seri.BelgeTipi) || string.IsNullOrWhiteSpace(seri.Seri))
            throw new InvalidOperationException("Belge tipi ve seri kodu zorunludur.");
        if (seri.SonNumara < 0) throw new InvalidOperationException("Son belge numarası negatif olamaz.");
        if (!await _db.Sirketler.AnyAsync(s => s.Id == seri.SirketId && s.AktifMi))
            throw new InvalidOperationException("Belge serisi yalnızca aktif firmaya tanımlanabilir.");
        if (await _db.BelgeSerileri.AnyAsync(s => s.Id != seri.Id && s.SirketId == seri.SirketId && s.BelgeTipi == seri.BelgeTipi && s.Seri == seri.Seri))
            throw new InvalidOperationException("Bu belge tipi ve seri kodu daha önce kullanılmış.");
        var mevcut = await _db.BelgeSerileri.FirstOrDefaultAsync(s => s.Id == seri.Id);
        if (mevcut == null) { seri.OlusturmaTarihi = DateTime.UtcNow; _db.BelgeSerileri.Add(seri); }
        else
        {
            mevcut.BelgeTipi = seri.BelgeTipi; mevcut.Seri = seri.Seri; mevcut.Prefix = seri.Prefix;
            mevcut.Format = seri.Format; mevcut.GuncellemeTarihi = DateTime.UtcNow; seri = mevcut;
        }
        await _db.SaveChangesAsync();
        return seri;
    }

    public async Task<string> SonrakiBelgeNoAsync(int belgeSerisiId)
    {
        YonetimYetkisiKontrolEt("Belge Numarası Üretme");
        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(42454, {belgeSerisiId})");
        var seri = await _db.BelgeSerileri.SingleOrDefaultAsync(s => s.Id == belgeSerisiId)
            ?? throw new KeyNotFoundException("Belge serisi bulunamadı.");
        checked { seri.SonNumara++; }
        string numara;
        try
        {
            numara = seri.Format is { Length: > 0 }
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture, seri.Format, seri.SonNumara)
                : $"{seri.Prefix ?? seri.Seri}{seri.SonNumara:000000}";
        }
        catch (FormatException ex) { throw new InvalidOperationException("Belge numarası formatı geçersiz.", ex); }
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return numara;
    }

    private static string? BosIseNull(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
