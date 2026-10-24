using Altyapi.Veri;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Siparis;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public class SiparisServisi : ISiparisServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi _kullaniciServisi;

    public SiparisServisi(MuhasebeBaglami baglam, IKullaniciServisi kullaniciServisi)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<Siparis>> ListeleAsync(SiparisFiltre? filtre = null)
    {
        filtre ??= new SiparisFiltre();
        var sorgu = _baglam.Siparisler.AsNoTracking()
            .Include(x => x.CariKart).Include(x => x.ParaBirimi)
            .Include(x => x.OlusturanKullanici).Include(x => x.Kalemler)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtre.Arama))
        {
            var arama = filtre.Arama.Trim();
            sorgu = sorgu.Where(x => x.SiparisNo.Contains(arama) ||
                x.ReferansNo.Contains(arama) ||
                x.CariKart!.Kod.Contains(arama) ||
                x.CariKart.Unvan.Contains(arama) ||
                (x.CariKart.Telefon != null && x.CariKart.Telefon.Contains(arama)));
        }
        if (!string.IsNullOrWhiteSpace(filtre.SiparisNo))
            sorgu = sorgu.Where(x => x.SiparisNo.Contains(filtre.SiparisNo.Trim()));
        if (filtre.Tur.HasValue) sorgu = sorgu.Where(x => x.SiparisTuru == filtre.Tur);
        if (filtre.Durum.HasValue) sorgu = sorgu.Where(x => x.Durum == filtre.Durum);
        if (filtre.CariKartId.HasValue) sorgu = sorgu.Where(x => x.CariKartId == filtre.CariKartId);
        if (filtre.OlusturanKullaniciId.HasValue) sorgu = sorgu.Where(x => x.OlusturanKullaniciId == filtre.OlusturanKullaniciId);
        if (filtre.BaslangicTarihi.HasValue) sorgu = sorgu.Where(x => x.SiparisTarihi >= Utc(filtre.BaslangicTarihi.Value.Date));
        if (filtre.BitisTarihi.HasValue) sorgu = sorgu.Where(x => x.SiparisTarihi < Utc(filtre.BitisTarihi.Value.Date.AddDays(1)));
        if (filtre.TeslimTarihi.HasValue) sorgu = sorgu.Where(x => x.TeslimTarihi != null && x.TeslimTarihi.Value.Date == filtre.TeslimTarihi.Value.Date);
        return await sorgu.OrderByDescending(x => x.SiparisTarihi).ThenByDescending(x => x.Id).ToListAsync();
    }

    public Task<Siparis?> GetirAsync(int id) => _baglam.Siparisler.AsNoTracking()
        .Include(x => x.CariKart).Include(x => x.ParaBirimi)
        .Include(x => x.OlusturanKullanici).Include(x => x.GuncelleyenKullanici)
        .Include(x => x.Kalemler).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<Siparis> EkleAsync(Siparis siparis, bool taslak)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.FaturaOlustur, "Sipariş Oluşturma");
        Dogrula(siparis);
        var kullaniciId = AktifKullaniciId();
        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            await CariDogrulaAsync(siparis.CariKartId);
            await KalemleriDogrulaVeHesaplaAsync(siparis);
            siparis.SiparisNo = await SiraliSiparisNoUretAsync(siparis.SiparisTarihi);
            siparis.Durum = taslak ? SiparisDurumu.Taslak : SiparisDurumu.Beklemede;
            siparis.OlusturanKullaniciId = kullaniciId;
            siparis.OlusturmaTarihi = DateTime.UtcNow;
            siparis.SiparisTarihi = Utc(siparis.SiparisTarihi);
            siparis.TeslimTarihi = Utc(siparis.TeslimTarihi);
            siparis.VadeTarihi = Utc(siparis.VadeTarihi);
            _baglam.Siparisler.Add(siparis);
            await _baglam.SaveChangesAsync();
            await transaction.CommitAsync();
            return siparis;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Siparis> GuncelleAsync(Siparis siparis)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.FaturaOlustur, "Sipariş Düzenleme");
        Dogrula(siparis);
        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var mevcut = await _baglam.Siparisler.Include(x => x.Kalemler).SingleOrDefaultAsync(x => x.Id == siparis.Id)
                ?? throw new KeyNotFoundException("Güncellenecek sipariş bulunamadı.");
            if (mevcut.Durum is not (SiparisDurumu.Taslak or SiparisDurumu.Beklemede))
                throw new InvalidOperationException("Yalnızca taslak veya bekleyen siparişler düzenlenebilir.");
            _baglam.Entry(mevcut).Property(x => x.Versiyon).OriginalValue = siparis.Versiyon;
            if (mevcut.CariKartId != siparis.CariKartId) await CariDogrulaAsync(siparis.CariKartId);
            await KalemleriDogrulaVeHesaplaAsync(siparis);

            mevcut.SiparisTuru = siparis.SiparisTuru;
            mevcut.CariKartId = siparis.CariKartId;
            mevcut.SiparisTarihi = Utc(siparis.SiparisTarihi);
            mevcut.TeslimTarihi = Utc(siparis.TeslimTarihi);
            mevcut.TeslimatAdSoyad = siparis.TeslimatAdSoyad;
            mevcut.TeslimatTelefon = siparis.TeslimatTelefon;
            mevcut.TeslimatAdres = siparis.TeslimatAdres;
            mevcut.TeslimatIl = siparis.TeslimatIl;
            mevcut.TeslimatIlce = siparis.TeslimatIlce;
            mevcut.ParaBirimiId = siparis.ParaBirimiId;
            mevcut.Kur = siparis.Kur;
            mevcut.VadeTarihi = Utc(siparis.VadeTarihi);
            mevcut.OdemeSekli = siparis.OdemeSekli;
            mevcut.Aciklama = siparis.Aciklama;
            mevcut.ReferansNo = siparis.ReferansNo;
            mevcut.BrutToplam = siparis.BrutToplam;
            mevcut.IskontoToplami = siparis.IskontoToplami;
            mevcut.AraToplam = siparis.AraToplam;
            mevcut.KdvToplami = siparis.KdvToplami;
            mevcut.GenelToplam = siparis.GenelToplam;
            mevcut.GuncelleyenKullaniciId = AktifKullaniciId();
            mevcut.GuncellemeTarihi = DateTime.UtcNow;
            _baglam.SiparisKalemleri.RemoveRange(mevcut.Kalemler);
            mevcut.Kalemler = siparis.Kalemler;
            await _baglam.SaveChangesAsync();
            await transaction.CommitAsync();
            return mevcut;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            throw new DbUpdateConcurrencyException("Bu sipariş başka bir kullanıcı tarafından güncellendi. Lütfen kayıtları yenileyip tekrar deneyin.", ex);
        }
    }

    public async Task<Siparis> DurumDegistirAsync(int id, SiparisDurumu yeniDurum, int versiyon)
    {
        if (yeniDurum == SiparisDurumu.Iptal)
            _kullaniciServisi.YetkiKontrolEt(y => y.FaturaIptal, "Sipariş İptali");
        else if (yeniDurum == SiparisDurumu.Onaylandi)
            _kullaniciServisi.YetkiKontrolEt(y => y.FaturaOnayla, "Sipariş Onayı");
        else
            _kullaniciServisi.YetkiKontrolEt(y => y.FaturaOlustur, "Sipariş Durumu Değiştirme");
        var siparis = await _baglam.Siparisler.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Sipariş bulunamadı.");
        if (!GecisGecerli(siparis.Durum, yeniDurum))
            throw new InvalidOperationException($"{siparis.Durum} durumundan {yeniDurum} durumuna geçilemez.");
        // Sevk edilmiş ya da faturalanmış sipariş iptal edilirse irsaliye/fatura sahipsiz kalır; önce onlar iptal edilmeli.
        if (yeniDurum == SiparisDurumu.Iptal)
        {
            if (await _baglam.Irsaliyeler.AnyAsync(x => x.SiparisId == id && x.Durum != IrsaliyeDurumu.Iptal))
                throw new InvalidOperationException("Bu siparişe bağlı irsaliye var; siparişi iptal etmeden önce irsaliyeyi iptal edin.");
            if (await _baglam.Faturalar.AnyAsync(x => x.SiparisId == id && x.Durum != BelgeDurumu.Iptal))
                throw new InvalidOperationException("Bu siparişe bağlı fatura var; siparişi iptal etmeden önce faturayı iptal edin.");
        }
        _baglam.Entry(siparis).Property(x => x.Versiyon).OriginalValue = versiyon;
        siparis.Durum = yeniDurum;
        siparis.GuncelleyenKullaniciId = AktifKullaniciId();
        siparis.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
        return siparis;
    }

    public Task<Siparis> IptalEtAsync(int id, int versiyon) => DurumDegistirAsync(id, SiparisDurumu.Iptal, versiyon);

    public Task<bool> SiparisNoMevcutMuAsync(string siparisNo, int? haricId = null) =>
        _baglam.Siparisler.IgnoreQueryFilters().AnyAsync(x => x.SiparisNo == siparisNo && (!haricId.HasValue || x.Id != haricId));

    public static void Hesapla(Siparis siparis)
    {
        foreach (var kalem in siparis.Kalemler)
        {
            var brut = decimal.Round(kalem.Miktar * kalem.BirimFiyat, 4, MidpointRounding.AwayFromZero);
            kalem.IskontoTutari = decimal.Round(brut * kalem.IskontoOrani / 100m, 4, MidpointRounding.AwayFromZero);
            var net = brut - kalem.IskontoTutari;
            kalem.KdvTutari = decimal.Round(net * kalem.KdvOrani / 100m, 4, MidpointRounding.AwayFromZero);
            kalem.SatirToplami = net + kalem.KdvTutari;
        }
        siparis.BrutToplam = siparis.Kalemler.Sum(x => x.Miktar * x.BirimFiyat);
        siparis.IskontoToplami = siparis.Kalemler.Sum(x => x.IskontoTutari);
        siparis.AraToplam = siparis.BrutToplam - siparis.IskontoToplami;
        siparis.KdvToplami = siparis.Kalemler.Sum(x => x.KdvTutari);
        siparis.GenelToplam = siparis.AraToplam + siparis.KdvToplami;
    }

    private async Task CariDogrulaAsync(int cariKartId)
    {
        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(x => x.Id == cariKartId)
            ?? throw new ArgumentException("Seçilen cari bulunamadı.");
        if (!cari.Aktif) throw new InvalidOperationException("Pasif cariye sipariş girilemez.");
    }

    private async Task KalemleriDogrulaVeHesaplaAsync(Siparis siparis)
    {
        var ids = siparis.Kalemler.Select(x => x.UrunId).Distinct().ToList();
        var urunler = await _baglam.Urunler.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        foreach (var kalem in siparis.Kalemler)
        {
            if (!urunler.TryGetValue(kalem.UrunId, out var urun) || !urun.Aktif)
                throw new ArgumentException("Seçilen ürün bulunamadı veya pasif.");
            if (kalem.Miktar <= 0 || kalem.BirimFiyat < 0)
                throw new ArgumentException("Miktar sıfırdan büyük, birim fiyat sıfır veya daha büyük olmalıdır.");
            if (kalem.IskontoOrani is < 0 or > 100 || kalem.KdvOrani is < 0 or > 100)
                throw new ArgumentException("İskonto ve KDV oranı 0-100 arasında olmalıdır.");
            kalem.UrunKodu = urun.Kod;
            kalem.Barkod = urun.Barkod ?? string.Empty;
            kalem.UrunAdi = urun.Ad;
            kalem.Birim = urun.Birim;
        }
        Hesapla(siparis);
    }

    private static void Dogrula(Siparis siparis)
    {
        if (siparis.CariKartId <= 0) throw new ArgumentException("Cari seçilmelidir.");
        if (siparis.ParaBirimiId <= 0) throw new ArgumentException("Para birimi seçilmelidir.");
        if (siparis.Kur <= 0) throw new ArgumentException("Kur sıfırdan büyük olmalıdır.");
        if (siparis.Kalemler.Count == 0) throw new ArgumentException("En az bir ürün eklenmelidir.");
        if (siparis.TeslimTarihi.HasValue && siparis.TeslimTarihi.Value.Date < siparis.SiparisTarihi.Date)
            throw new ArgumentException("Teslim tarihi sipariş tarihinden önce olamaz.");
        if (!string.IsNullOrWhiteSpace(siparis.TeslimatTelefon) &&
            siparis.TeslimatTelefon.Any(c => !char.IsDigit(c) && c is not (' ' or '+' or '(' or ')' or '-')))
            throw new ArgumentException("Teslimat telefonu yalnızca rakam ve telefon biçimlendirme karakterleri içerebilir.");
    }

    private int AktifKullaniciId() => _kullaniciServisi.AktifKullanici?.Id
        ?? throw new InvalidOperationException("Sipariş işlemi için aktif oturum bulunamadı.");

    private async Task<string> SiraliSiparisNoUretAsync(DateTime tarih)
    {
        const string belgeTipi = "Siparis";
        const string seriKodu = "SIP";
        // Seri satırı henüz yokken (ilk belge) satır kilidi işe yaramaz; eşzamanlı iki ilk belge ikisi de seri açmasın.
        await _baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1111836499, hashtext({_baglam.SirketIdCoz(null).ToString() + belgeTipi + seriKodu}))");
        var seri = await _baglam.BelgeSerileri
            .FromSqlInterpolated($@"SELECT * FROM ""BelgeSerileri"" WHERE ""SirketId"" = {_baglam.SirketIdCoz(null)} AND ""BelgeTipi"" = {belgeTipi} AND ""Seri"" = {seriKodu} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (seri == null)
        {
            seri = new BelgeSerisi { SirketId = _baglam.SirketIdCoz(null), BelgeTipi = "Siparis", Seri = "SIP", Prefix = "SIP", SonNumara = 0 };
            _baglam.BelgeSerileri.Add(seri);
        }
        seri.SonNumara++;
        return $"SIP-{tarih.Year}-{seri.SonNumara:D6}";
    }

    private static bool GecisGecerli(SiparisDurumu eski, SiparisDurumu yeni) => (eski, yeni) switch
    {
        (SiparisDurumu.Taslak, SiparisDurumu.Beklemede or SiparisDurumu.Iptal) => true,
        (SiparisDurumu.Beklemede, SiparisDurumu.Taslak or SiparisDurumu.Onaylandi or SiparisDurumu.Iptal) => true,
        // Kısmen karşılandı yalnız sevkiyatla oluşur; elle yalnız kapatma (Tamamlandı) veya iptal yapılır.
        (SiparisDurumu.Onaylandi, SiparisDurumu.Tamamlandi or SiparisDurumu.Iptal) => true,
        (SiparisDurumu.KismenKarsilandi, SiparisDurumu.Tamamlandi or SiparisDurumu.Iptal) => true,
        _ => false
    };

    // Belge tarihleri yerel saatle (UTC etiketli) saklanır; saat dilimi çevrimi yapılmaz.
    private static DateTime Utc(DateTime tarih) => DateTime.SpecifyKind(tarih, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? tarih) => tarih.HasValue ? Utc(tarih.Value) : null;
}
