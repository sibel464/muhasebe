using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using static Altyapi.Servisler.FinansMuhasebeKaydi;

namespace Altyapi.Servisler;

// Personel kartları ve maaş işlemleri. Bordro tutarları mali müşavirin bordrosundan girilir; program hesaplamaz.
// Tahakkuk (ayın son günü): gider hesabı borç (brüt + işveren payları) / 335 net, 196 avans mahsubu, 360 GV+DV, 361 SGK alacak.
// Ödeme ve avans kasa/banka hareketidir (335 veya 196 / 100-102), "PERSONEL" özel koduyla yalnız bu ekrandan iptal edilir.
public class PersonelServisi : IPersonelServisi
{
    public const string TahakkukBelgeTipi = "MAAS_TAHAKKUK";
    public const string PersonelOzelKodu = "PERSONEL";
    public const string VergiSgkOzelKodu = "VERGI_SGK";
    public const string Kasa = "Kasa";
    public const string Banka = "Banka";

    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public PersonelServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private IKullaniciServisi Oturum => _kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama");
    private void Goruntule() => _kullaniciServisi?.YetkiKontrolEt(y => y.PersonelGoruntule, "Personel görüntüleme");
    private void MaasIslemi(string islem) => Oturum.YetkiKontrolEt(y => y.PersonelDuzenle, islem);
    private void ParaCikisi(string islem) => Oturum.YetkiKontrolEt(y => y.PersonelDuzenle && y.OdemeYap, islem);
    private static DateTime TahakkukTarihi(int yil, int ay) => new(yil, ay, DateTime.DaysInMonth(yil, ay), 23, 0, 0, DateTimeKind.Utc);

    private Task KilitleAsync(Guid sirketId) =>
        _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"personel:{sirketId}");

    // ---------------- Kartlar ----------------

    public async Task<List<PersonelListeDto>> ListeAsync(bool ayrilanlarDahil = false, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var personeller = await _baglam.Personeller.AsNoTracking()
            .Where(p => p.SirketId == sirketId && (ayrilanlarDahil || p.IstenCikisTarihi == null))
            .OrderBy(p => p.AdSoyad).ToListAsync();
        var bakiyeler = await BakiyelerAsync(sirketId);
        return personeller.Select(p => new PersonelListeDto
        {
            Id = p.Id, Kod = p.Kod, AdSoyad = p.AdSoyad, Gorev = p.Gorev, Departman = p.Departman, IseGirisTarihi = p.IseGirisTarihi,
            IstenCikisTarihi = p.IstenCikisTarihi, NetUcret = p.NetUcret, Iban = p.Iban,
            OdenecekBakiye = bakiyeler.GetValueOrDefault(p.Id).Odenecek, AvansBakiye = bakiyeler.GetValueOrDefault(p.Id).Avans
        }).ToList();
    }

    // Personel başına ödenecek net (tahakkuk - ödeme) ve mahsup edilmemiş avans (avans - kesinti).
    private async Task<Dictionary<int, (decimal Odenecek, decimal Avans)>> BakiyelerAsync(Guid sirketId, int? personelId = null)
    {
        var satirlar = await _baglam.PersonelHareketleri.AsNoTracking()
            .Where(h => h.SirketId == sirketId && (personelId == null || h.PersonelId == personelId))
            .GroupBy(h => h.PersonelId)
            .Select(g => new
            {
                g.Key,
                Tahakkuk = g.Where(h => h.Tur == PersonelHareketTuru.MaasTahakkuku).Sum(h => h.NetUcret - h.AvansKesintisi),
                Odeme = g.Where(h => h.Tur == PersonelHareketTuru.MaasOdemesi).Sum(h => h.Tutar),
                Avans = g.Where(h => h.Tur == PersonelHareketTuru.Avans).Sum(h => h.Tutar),
                Kesinti = g.Where(h => h.Tur == PersonelHareketTuru.MaasTahakkuku).Sum(h => h.AvansKesintisi)
            }).ToListAsync();
        return satirlar.ToDictionary(x => x.Key, x => (x.Tahakkuk - x.Odeme, x.Avans - x.Kesinti));
    }

    public async Task<Personel?> GetirAsync(int id, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        return await _baglam.Personeller.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.SirketId == sirketId);
    }

    public async Task<Personel> KaydetAsync(Personel personel, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Oturum.YetkiKontrolEt(y => personel.Id == 0 ? y.PersonelEkle : y.PersonelDuzenle, "Personel kaydetme");
        personel.Kod = (personel.Kod ?? "").Trim();
        personel.AdSoyad = (personel.AdSoyad ?? "").Trim();
        if (personel.AdSoyad.Length == 0) throw new InvalidOperationException("Ad soyad zorunludur.");
        personel.TcKimlikNo = Bos(personel.TcKimlikNo);
        if (personel.TcKimlikNo != null && !TcKimlikGecerli(personel.TcKimlikNo))
            throw new InvalidOperationException("T.C. kimlik numarası geçersiz (11 hane ve sağlama tutmalı).");
        personel.Iban = Bos(personel.Iban)?.Replace(" ", "").ToUpperInvariant();
        if (personel.Iban != null && !(personel.Iban.StartsWith("TR") && personel.Iban.Length == 26 && personel.Iban[2..].All(char.IsDigit)))
            throw new InvalidOperationException("IBAN TR ile başlayan 26 karakter olmalıdır.");
        personel.Gorev = Bos(personel.Gorev);
        personel.Departman = Bos(personel.Departman);
        personel.Telefon = Bos(personel.Telefon);
        personel.Eposta = Bos(personel.Eposta);
        personel.Aciklama = (personel.Aciklama ?? "").Trim();
        if (string.IsNullOrWhiteSpace(personel.GiderHesapKodu)) personel.GiderHesapKodu = "770";
        TutarlariDogrula(personel.NetUcret, personel.SgkIsciPayi, personel.IssizlikIsciPayi, personel.GelirVergisi, personel.DamgaVergisi,
            personel.SgkIsverenPayi, personel.IssizlikIsverenPayi);
        personel.IseGirisTarihi = DateTime.SpecifyKind(personel.IseGirisTarihi.Date, DateTimeKind.Utc);
        if (personel.IstenCikisTarihi is DateTime cikis)
        {
            personel.IstenCikisTarihi = DateTime.SpecifyKind(cikis.Date, DateTimeKind.Utc);
            if (cikis.Date < personel.IseGirisTarihi.Date) throw new InvalidOperationException("İşten çıkış tarihi işe giriş tarihinden önce olamaz.");
        }

        if (personel.Kod.Length == 0)
        {
            var sayi = await _baglam.Personeller.IgnoreQueryFilters().CountAsync(p => p.SirketId == sirketId) + 1;
            personel.Kod = $"PRS-{sayi:0000}";
            while (await _baglam.Personeller.IgnoreQueryFilters().AnyAsync(p => p.SirketId == sirketId && p.Kod == personel.Kod && !p.Silindi)) personel.Kod = $"PRS-{++sayi:0000}";
        }
        if (await _baglam.Personeller.AnyAsync(p => p.SirketId == sirketId && p.Kod == personel.Kod && p.Id != personel.Id))
            throw new InvalidOperationException($"{personel.Kod} kodlu başka bir personel var.");

        if (personel.Id == 0)
        {
            personel.SirketId = sirketId;
            _baglam.Personeller.Add(personel);
        }
        else
        {
            var m = await _baglam.Personeller.FirstOrDefaultAsync(p => p.Id == personel.Id && p.SirketId == sirketId)
                ?? throw new KeyNotFoundException("Personel bulunamadı.");
            m.Kod = personel.Kod; m.AdSoyad = personel.AdSoyad; m.TcKimlikNo = personel.TcKimlikNo; m.Gorev = personel.Gorev; m.Departman = personel.Departman;
            m.IseGirisTarihi = personel.IseGirisTarihi; m.IstenCikisTarihi = personel.IstenCikisTarihi; m.Telefon = personel.Telefon; m.Eposta = personel.Eposta;
            m.Iban = personel.Iban; m.GiderHesapKodu = personel.GiderHesapKodu; m.NetUcret = personel.NetUcret; m.SgkIsciPayi = personel.SgkIsciPayi;
            m.IssizlikIsciPayi = personel.IssizlikIsciPayi; m.GelirVergisi = personel.GelirVergisi; m.DamgaVergisi = personel.DamgaVergisi;
            m.SgkIsverenPayi = personel.SgkIsverenPayi; m.IssizlikIsverenPayi = personel.IssizlikIsverenPayi; m.Aciklama = personel.Aciklama;
            m.GuncellemeTarihi = DateTime.UtcNow;
            personel = m;
        }
        await _baglam.SaveChangesAsync();
        return personel;
    }

    private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static bool TcKimlikGecerli(string tc)
    {
        if (tc.Length != 11 || !tc.All(char.IsDigit) || tc[0] == '0') return false;
        var d = tc.Select(c => c - '0').ToArray();
        var tek = d[0] + d[2] + d[4] + d[6] + d[8];
        var cift = d[1] + d[3] + d[5] + d[7];
        return ((tek * 7 - cift) % 10 + 10) % 10 == d[9] && d.Take(10).Sum() % 10 == d[10];
    }

    private static void TutarlariDogrula(params decimal[] tutarlar)
    {
        foreach (var t in tutarlar)
            if (t < 0 || t > FinansKurallari.EnBuyukTutar || decimal.Round(t, 2) != t)
                throw new InvalidOperationException("Ücret ve kesinti tutarları sıfır veya pozitif, en fazla 2 ondalık basamaklı olmalıdır.");
    }

    public async Task SilAsync(int id, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Oturum.YetkiKontrolEt(y => y.PersonelSil, "Personel silme");
        var p = await _baglam.Personeller.FirstOrDefaultAsync(x => x.Id == id && x.SirketId == sirketId)
            ?? throw new KeyNotFoundException("Personel bulunamadı.");
        if (await _baglam.PersonelHareketleri.AnyAsync(h => h.PersonelId == id))
            throw new InvalidOperationException("Maaş veya avans kaydı olan personel silinemez. İşten ayrıldıysa kartına çıkış tarihi yazın.");
        p.Silindi = true;
        p.SilinmeTarihi = DateTime.UtcNow;
        p.SilinmeNedeni = "Kullanıcı sildi";
        await _baglam.SaveChangesAsync();
    }

    // ---------------- Tahakkuk ----------------

    public async Task<MaasDonemDto> DonemAsync(int yil, int ay, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        return await DonemHazirlaAsync(yil, ay, sirketId);
    }

    private async Task<MaasDonemDto> DonemHazirlaAsync(int yil, int ay, Guid sirketId)
    {
        if (ay is < 1 or > 12 || yil is < 2000 or > 2100) throw new ArgumentException("Geçersiz dönem.");
        var donem = yil * 100 + ay;
        var o = new MaasDonemDto { Yil = yil, Ay = ay };
        var basi = new DateTime(yil, ay, 1, 0, 0, 0, DateTimeKind.Utc);
        var sonu = TahakkukTarihi(yil, ay);

        var yapilan = await _baglam.PersonelHareketleri.AsNoTracking().Include(h => h.Personel)
            .Where(h => h.SirketId == sirketId && h.Tur == PersonelHareketTuru.MaasTahakkuku && h.Donem == donem)
            .OrderBy(h => h.Personel!.AdSoyad).ToListAsync();
        o.Yapilanlar = yapilan.Select(h => new MaasTahakkukSatiriDto
        {
            PersonelId = h.PersonelId, Kod = h.Personel?.Kod ?? "", AdSoyad = h.Personel?.AdSoyad ?? "", GiderHesapKodu = h.Personel?.GiderHesapKodu ?? "",
            TcKimlikNo = h.Personel?.TcKimlikNo, Gorev = h.Personel?.Gorev, IseGirisTarihi = h.Personel?.IseGirisTarihi, Iban = h.Personel?.Iban,
            NetUcret = h.NetUcret, SgkIsciPayi = h.SgkIsciPayi, IssizlikIsciPayi = h.IssizlikIsciPayi, GelirVergisi = h.GelirVergisi, DamgaVergisi = h.DamgaVergisi,
            SgkIsverenPayi = h.SgkIsverenPayi, IssizlikIsverenPayi = h.IssizlikIsverenPayi, AvansKesintisi = h.AvansKesintisi
        }).ToList();
        o.FisNolari = await GecerliTahakkukFisleri(sirketId, donem).Select(f => f.FisNo).ToListAsync();

        var yapilanIdler = yapilan.Select(h => h.PersonelId).ToHashSet();
        var bakiyeler = await BakiyelerAsync(sirketId);
        var calisanlar = await _baglam.Personeller.AsNoTracking()
            .Where(p => p.SirketId == sirketId && p.IseGirisTarihi <= sonu && (p.IstenCikisTarihi == null || p.IstenCikisTarihi >= basi))
            .OrderBy(p => p.AdSoyad).ToListAsync();
        foreach (var p in calisanlar.Where(p => !yapilanIdler.Contains(p.Id)))
        {
            var avans = Math.Max(0, bakiyeler.GetValueOrDefault(p.Id).Avans);
            o.Bekleyenler.Add(new MaasTahakkukSatiriDto
            {
                PersonelId = p.Id, Kod = p.Kod, AdSoyad = p.AdSoyad, TcKimlikNo = p.TcKimlikNo, Gorev = p.Gorev, IseGirisTarihi = p.IseGirisTarihi, Iban = p.Iban,
                GiderHesapKodu = p.GiderHesapKodu, NetUcret = p.NetUcret, SgkIsciPayi = p.SgkIsciPayi,
                IssizlikIsciPayi = p.IssizlikIsciPayi, GelirVergisi = p.GelirVergisi, DamgaVergisi = p.DamgaVergisi, SgkIsverenPayi = p.SgkIsverenPayi,
                IssizlikIsverenPayi = p.IssizlikIsverenPayi, AvansBakiye = avans, AvansKesintisi = Math.Min(avans, p.NetUcret)
            });
        }

        if (donem > DateTime.Today.Year * 100 + DateTime.Today.Month)
            o.Engeller.Add("İleri bir ayın maaşı tahakkuk edilemez.");
        if (await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == $"YILSONU_{yil}_KAPANIS" && f.TersFisId == null))
            o.Engeller.Add($"{yil} yılı kapatılmış. Önce Yıl Sonu Kapanışı ekranından kapanışı geri alın.");
        if (await _baglam.MaliDonemler.AnyAsync(m => m.SirketId == sirketId && m.Baslangic <= sonu && m.Bitis >= sonu && m.KilitliMi))
            o.Engeller.Add($"{sonu:dd.MM.yyyy} kilitli bir mali döneme düşüyor.");
        return o;
    }

    private IQueryable<YevmiyeFis> GecerliTahakkukFisleri(Guid sirketId, int donem) =>
        _baglam.YevmiyeFisleri.Where(f => f.SirketId == sirketId && f.BelgeTipi == TahakkukBelgeTipi && f.BelgeId > donem * 100 && f.BelgeId < (donem + 1) * 100
            && f.TersFisId == null && f.Durum == YevmiyeDurumu.Onaylandi);

    public async Task<string> TahakkukEtAsync(int yil, int ay, IReadOnlyList<MaasTahakkukSatiriDto> satirlar, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        MaasIslemi("Maaş tahakkuku");
        if (satirlar.Count == 0) throw new InvalidOperationException("Tahakkuk edilecek personel seçilmedi.");
        if (satirlar.Select(s => s.PersonelId).Distinct().Count() != satirlar.Count) throw new InvalidOperationException("Aynı personel iki kez seçilmiş.");

        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await KilitleAsync(sirketId);
        var o = await DonemHazirlaAsync(yil, ay, sirketId);
        if (o.Engeller.Count > 0) throw new InvalidOperationException(string.Join("\n", o.Engeller));
        var donem = yil * 100 + ay;

        var personeller = await _baglam.Personeller.Where(p => p.SirketId == sirketId).ToDictionaryAsync(p => p.Id);
        foreach (var s in satirlar)
        {
            var bekleyen = o.Bekleyenler.FirstOrDefault(b => b.PersonelId == s.PersonelId);
            if (bekleyen == null)
                throw new InvalidOperationException(o.Yapilanlar.Any(y => y.PersonelId == s.PersonelId)
                    ? $"{s.AdSoyad} için {ay:00}.{yil} maaşı zaten tahakkuk edilmiş."
                    : $"{s.AdSoyad} bu dönemde çalışan olarak görünmüyor (işe giriş / çıkış tarihlerine bakın).");
            TutarlariDogrula(s.NetUcret, s.SgkIsciPayi, s.IssizlikIsciPayi, s.GelirVergisi, s.DamgaVergisi, s.SgkIsverenPayi, s.IssizlikIsverenPayi, s.AvansKesintisi);
            if (s.NetUcret <= 0) throw new InvalidOperationException($"{s.AdSoyad}: net ücret sıfırdan büyük olmalıdır.");
            if (s.AvansKesintisi > bekleyen.AvansBakiye)
                throw new InvalidOperationException($"{s.AdSoyad}: avans kesintisi ({s.AvansKesintisi:N2}) verilmiş avanstan ({bekleyen.AvansBakiye:N2}) fazla olamaz.");
            if (s.AvansKesintisi > s.NetUcret)
                throw new InvalidOperationException($"{s.AdSoyad}: avans kesintisi net ücretten fazla olamaz.");
        }

        var tarih = TahakkukTarihi(yil, ay);
        var donemAdi = $"{ay:00}.{yil}";
        var fisSatirlari = new List<Satir>();
        foreach (var g in satirlar.GroupBy(s => personeller[s.PersonelId].GiderHesapKodu))
            fisSatirlari.Add(new Satir(g.Key, g.Sum(s => s.IsverenMaliyeti), 0));
        void Alacak(string kod, decimal tutar) { if (tutar > 0) fisSatirlari.Add(new Satir(kod, 0, tutar)); }
        Alacak("335", satirlar.Sum(s => s.OdenecekNet));
        Alacak("196", satirlar.Sum(s => s.AvansKesintisi));
        Alacak("360", satirlar.Sum(s => s.GelirVergisi + s.DamgaVergisi));
        Alacak("361", satirlar.Sum(s => s.SgkIsciPayi + s.IssizlikIsciPayi + s.SgkIsverenPayi + s.IssizlikIsverenPayi));

        var sira = await _baglam.YevmiyeFisleri.IgnoreQueryFilters().CountAsync(f => f.SirketId == sirketId && f.BelgeTipi == TahakkukBelgeTipi
            && f.BelgeId > donem * 100 && f.BelgeId < (donem + 1) * 100) + 1;
        if (sira > 99) throw new InvalidOperationException("Bu dönem için çok fazla tahakkuk denemesi yapılmış.");
        var belgeId = donem * 100 + sira;
        await FisEkleAsync(_baglam, sirketId, TahakkukBelgeTipi, belgeId, tarih, $"{donemAdi} maaş tahakkuku ({satirlar.Count} personel)", $"Maaş {donemAdi}", fisSatirlari.ToArray());
        var fis = await _baglam.YevmiyeFisleri.SingleAsync(f => f.SirketId == sirketId && f.BelgeTipi == TahakkukBelgeTipi && f.BelgeId == belgeId);

        foreach (var s in satirlar)
        {
            _baglam.PersonelHareketleri.Add(new PersonelHareketi
            {
                SirketId = sirketId, PersonelId = s.PersonelId, Tur = PersonelHareketTuru.MaasTahakkuku, Tarih = tarih, Donem = donem,
                NetUcret = s.NetUcret, SgkIsciPayi = s.SgkIsciPayi, IssizlikIsciPayi = s.IssizlikIsciPayi, GelirVergisi = s.GelirVergisi, DamgaVergisi = s.DamgaVergisi,
                SgkIsverenPayi = s.SgkIsverenPayi, IssizlikIsverenPayi = s.IssizlikIsverenPayi, AvansKesintisi = s.AvansKesintisi,
                YevmiyeFisId = fis.Id, Aciklama = $"{donemAdi} maaş tahakkuku"
            });
            // Sonraki ay aynı tutarlarla önerilsin.
            var p = personeller[s.PersonelId];
            p.NetUcret = s.NetUcret; p.SgkIsciPayi = s.SgkIsciPayi; p.IssizlikIsciPayi = s.IssizlikIsciPayi; p.GelirVergisi = s.GelirVergisi;
            p.DamgaVergisi = s.DamgaVergisi; p.SgkIsverenPayi = s.SgkIsverenPayi; p.IssizlikIsverenPayi = s.IssizlikIsverenPayi;
        }
        await _baglam.SaveChangesAsync();
        await islem.CommitAsync();
        return fis.FisNo;
    }

    public async Task TahakkukGeriAlAsync(int yil, int ay, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        MaasIslemi("Maaş tahakkukunu geri alma");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await KilitleAsync(sirketId);
        var donem = yil * 100 + ay;
        if (await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == $"YILSONU_{yil}_KAPANIS" && f.TersFisId == null))
            throw new InvalidOperationException($"{yil} yılı kapatılmış. Önce yıl sonu kapanışını geri alın.");
        var fisler = await GecerliTahakkukFisleri(sirketId, donem).Include(f => f.Satirlar).ThenInclude(s => s.Hesap).ToListAsync();
        if (fisler.Count == 0) throw new InvalidOperationException($"{ay:00}.{yil} için geri alınacak maaş tahakkuku yok.");
        var fisIdleri = fisler.Select(f => f.Id).ToList();
        var hareketler = await _baglam.PersonelHareketleri.Include(h => h.Personel)
            .Where(h => h.SirketId == sirketId && h.Tur == PersonelHareketTuru.MaasTahakkuku && h.YevmiyeFisId != null && fisIdleri.Contains(h.YevmiyeFisId.Value)).ToListAsync();

        // Ödemesi yapılmış maaş geri alınırsa personel alacaklı görünür; önce ödemeler iptal edilmeli.
        var bakiyeler = await BakiyelerAsync(sirketId);
        var sorunlu = hareketler.Where(h => bakiyeler.GetValueOrDefault(h.PersonelId).Odenecek - h.OdenecekNet < 0).Select(h => h.Personel?.AdSoyad).Distinct().ToList();
        if (sorunlu.Count > 0)
            throw new InvalidOperationException($"Şu personele bu maaştan ödeme yapılmış: {string.Join(", ", sorunlu)}. Önce ödemeleri iptal edin.");

        var simdi = DateTime.UtcNow;
        foreach (var fis in fisler)
        {
            var tersBelgeTipi = $"TERS_{TahakkukBelgeTipi}";
            await FisEkleAsync(_baglam, sirketId, tersBelgeTipi, fis.Id, fis.Tarih, $"Maaş tahakkuku geri alındı: {fis.FisNo}", $"Ters Fiş ({fis.FisNo})",
                fis.Satirlar.Select(s => new Satir(s.Hesap!.HesapKodu, s.Alacak, s.Borc)).ToArray());
            var ters = await _baglam.YevmiyeFisleri.SingleAsync(f => f.SirketId == sirketId && f.BelgeTipi == tersBelgeTipi && f.BelgeId == fis.Id);
            ters.IptalEdilenFisId = fis.Id;
            fis.TersFisId = ters.Id;
        }
        foreach (var h in hareketler)
        {
            h.Silindi = true;
            h.SilinmeTarihi = simdi;
            h.SilinmeNedeni = "Maaş tahakkuku geri alındı";
        }
        await _baglam.SaveChangesAsync();
        await islem.CommitAsync();
    }

    // ---------------- Ödeme / avans ----------------

    public async Task<List<OdemeHesabiDto>> OdemeHesaplariAsync(Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var liste = new List<OdemeHesabiDto>();
        foreach (var k in (await _baglam.Kasalar.AsNoTracking().Where(k => k.AktifMi).OrderBy(k => k.Ad).ToListAsync())
                     .Where(k => _baglam.SirketIdCoz(k.SirketId) == sirketId && !FinansKurallari.DovizliMi(k.ParaBirimi)))
            liste.Add(new OdemeHesabiDto { Kaynak = Kasa, Id = k.Id, Ad = k.Ad, Bakiye = await FinansKurallari.KasaBakiyesiAsync(_baglam, k.Id) });
        foreach (var b in (await _baglam.BankaHesaplari.AsNoTracking().Where(b => b.AktifMi && b.HesapTuru != BankaHesapTuru.POSHesabi).OrderBy(b => b.Ad).ToListAsync())
                     .Where(b => _baglam.SirketIdCoz(b.SirketId) == sirketId && !FinansKurallari.DovizliMi(b.ParaBirimi)))
            liste.Add(new OdemeHesabiDto { Kaynak = Banka, Id = b.Id, Ad = b.Ad, Bakiye = await FinansKurallari.BankaBakiyesiAsync(_baglam, b.Id) });
        return liste;
    }

    public async Task<int> OdemeYapAsync(int personelId, decimal tutar, DateTime tarih, string kaynak, int hesapId, string? aciklama = null, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        ParaCikisi("Maaş ödemesi");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await KilitleAsync(sirketId);
        var id = await OdemeEkleAsync(sirketId, personelId, tutar, tarih, kaynak, hesapId, aciklama);
        await islem.CommitAsync();
        return id;
    }

    // Ödenecek bakiyesi olan herkese, bakiyesi kadar, seçilen kasa/bankadan ödeme.
    public async Task<int> TopluOdemeAsync(DateTime tarih, string kaynak, int hesapId, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        ParaCikisi("Toplu maaş ödemesi");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await KilitleAsync(sirketId);
        var bakiyeler = (await BakiyelerAsync(sirketId)).Where(x => x.Value.Odenecek > 0).ToList();
        if (bakiyeler.Count == 0) throw new InvalidOperationException("Ödenecek maaş bakiyesi olan personel yok.");
        var toplam = bakiyeler.Sum(x => x.Value.Odenecek);
        await ParaCikisiYapilabilirAsync(sirketId, kaynak, hesapId, toplam);
        foreach (var (personelId, b) in bakiyeler)
            await OdemeEkleAsync(sirketId, personelId, b.Odenecek, tarih, kaynak, hesapId, null);
        await islem.CommitAsync();
        return bakiyeler.Count;
    }

    private async Task<int> OdemeEkleAsync(Guid sirketId, int personelId, decimal tutar, DateTime tarih, string kaynak, int hesapId, string? aciklama)
    {
        var p = await PersonelGetirAsync(sirketId, personelId);
        FinansKurallari.TutarDogrula(tutar, "Ödeme");
        var odenecek = (await BakiyelerAsync(sirketId, personelId)).GetValueOrDefault(personelId).Odenecek;
        if (tutar > odenecek)
            throw new InvalidOperationException(odenecek <= 0
                ? $"{p.AdSoyad} için ödenecek maaş yok. Önce maaşı tahakkuk edin ya da ödemeyi avans olarak verin."
                : $"{p.AdSoyad} için ödenecek maaş {odenecek:N2} TL; daha fazlası ödenemez (fazlası avans olarak verilebilir).");
        return await ParaHareketiEkleAsync(sirketId, p, PersonelHareketTuru.MaasOdemesi, "335", tutar, tarih, kaynak, hesapId,
            string.IsNullOrWhiteSpace(aciklama) ? $"Maaş ödemesi - {p.AdSoyad}" : aciklama.Trim());
    }

    public async Task<int> AvansVerAsync(int personelId, decimal tutar, DateTime tarih, string kaynak, int hesapId, string? aciklama = null, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        ParaCikisi("Personel avansı");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await KilitleAsync(sirketId);
        var p = await PersonelGetirAsync(sirketId, personelId);
        FinansKurallari.TutarDogrula(tutar, "Avans");
        var id = await ParaHareketiEkleAsync(sirketId, p, PersonelHareketTuru.Avans, "196", tutar, tarih, kaynak, hesapId,
            string.IsNullOrWhiteSpace(aciklama) ? $"Personel avansı - {p.AdSoyad}" : aciklama.Trim());
        await islem.CommitAsync();
        return id;
    }

    private async Task<Personel> PersonelGetirAsync(Guid sirketId, int personelId) =>
        await _baglam.Personeller.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personelId && p.SirketId == sirketId)
        ?? throw new KeyNotFoundException("Personel bulunamadı.");

    private async Task ParaCikisiYapilabilirAsync(Guid sirketId, string kaynak, int hesapId, decimal tutar)
    {
        if (kaynak == Kasa)
        {
            await FinansKurallari.KasaKilitleAsync(_baglam, hesapId);
            await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, await KasaGetirAsync(sirketId, hesapId), tutar);
        }
        else
        {
            await FinansKurallari.BankaHesabiKilitleAsync(_baglam, hesapId);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, await BankaGetirAsync(sirketId, hesapId), tutar);
        }
    }

    private async Task<EtkiAlani.Modeller.Finans.Kasa> KasaGetirAsync(Guid sirketId, int id)
    {
        var k = await _baglam.Kasalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Kasa bulunamadı.");
        if (_baglam.SirketIdCoz(k.SirketId) != sirketId) throw new KeyNotFoundException("Kasa bulunamadı.");
        if (!k.AktifMi) throw new InvalidOperationException($"'{k.Ad}' kasası pasif durumda.");
        if (FinansKurallari.DovizliMi(k.ParaBirimi)) throw new InvalidOperationException("Maaş ve vergi ödemeleri TL kasa veya bankadan yapılır.");
        return k;
    }

    private async Task<BankaHesabi> BankaGetirAsync(Guid sirketId, int id)
    {
        var b = await _baglam.BankaHesaplari.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
        if (_baglam.SirketIdCoz(b.SirketId) != sirketId) throw new KeyNotFoundException("Banka hesabı bulunamadı.");
        if (!b.AktifMi) throw new InvalidOperationException($"'{b.Ad}' banka hesabı pasif durumda.");
        if (FinansKurallari.DovizliMi(b.ParaBirimi) || b.HesapTuru == BankaHesapTuru.POSHesabi)
            throw new InvalidOperationException("Maaş ve vergi ödemeleri TL vadesiz hesaptan yapılır.");
        return b;
    }

    // Kasa/banka çıkışı + fişi (karşı hesap borç / 100-102 alacak). Personel hareketi verilirse ona bağlanır.
    private async Task<(KasaHareketi? Kasa, BankaHareketi? Banka)> CikisEkleAsync(Guid sirketId, string kaynak, int hesapId, decimal tutar, DateTime tarih,
        string aciklama, string karsiKod, string ozelKod, int? ilgiliId)
    {
        if (kaynak is not (Kasa or Banka)) throw new ArgumentException("Ödeme kasadan ya da bankadan yapılmalıdır.");
        aciklama = FinansKurallari.AciklamaDogrula(aciklama);
        tarih = FinansKurallari.TarihNormalizeEt(tarih);
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
        await ParaCikisiYapilabilirAsync(sirketId, kaynak, hesapId, tutar);
        if (kaynak == Kasa)
        {
            var h = new KasaHareketi
            {
                SirketId = sirketId, KasaId = hesapId, Tarih = tarih, Tur = HareketTuru.Gider, FisTuru = KasaFisTuru.Gider,
                FisNo = FinansKurallari.FisNoUret("PRS"), Tutar = tutar, Aciklama = aciklama, OzelKod = ozelKod, IlgiliBelgeId = ilgiliId
            };
            _baglam.KasaHareketleri.Add(h);
            await _baglam.SaveChangesAsync();
            await KasaHareketiFisiEkleAsync(_baglam, h, karsiKod);
            return (h, null);
        }
        var hesap = await BankaGetirAsync(sirketId, hesapId);
        var bh = new BankaHareketi
        {
            SirketId = sirketId, BankaHesabiId = hesapId, BankaAdi = hesap.Ad, HesapNo = string.IsNullOrWhiteSpace(hesap.Iban) ? hesap.HesapNo : hesap.Iban,
            Tarih = tarih, Tur = HareketTuru.Gider, FisTuru = BankaFisTuru.GidenHavale, OdemeYontemi = OdemeYontemi.Havale,
            FisNo = FinansKurallari.FisNoUret("PRS"), Tutar = tutar, Aciklama = aciklama, OzelKod = ozelKod, IlgiliBelgeId = ilgiliId
        };
        _baglam.BankaHareketleri.Add(bh);
        await _baglam.SaveChangesAsync();
        await BankaHareketiFisiEkleAsync(_baglam, bh, karsiKod);
        return (null, bh);
    }

    private async Task<int> ParaHareketiEkleAsync(Guid sirketId, Personel p, PersonelHareketTuru tur, string karsiKod, decimal tutar, DateTime tarih,
        string kaynak, int hesapId, string aciklama)
    {
        var ph = new PersonelHareketi { SirketId = sirketId, PersonelId = p.Id, Tur = tur, Tarih = FinansKurallari.TarihNormalizeEt(tarih), Tutar = tutar, Aciklama = aciklama };
        _baglam.PersonelHareketleri.Add(ph);
        await _baglam.SaveChangesAsync();
        var (k, b) = await CikisEkleAsync(sirketId, kaynak, hesapId, tutar, tarih, aciklama, karsiKod, PersonelOzelKodu, ph.Id);
        ph.KasaHareketiId = k?.Id;
        ph.BankaHareketiId = b?.Id;
        await _baglam.SaveChangesAsync();
        return ph.Id;
    }

    // Ödeme ya da avans iptali: kasa/banka hareketi ters kayıtla kapanır (fişi de ters fişle), personel hareketi silinir.
    public async Task HareketIptalAsync(int personelHareketiId, string neden, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        ParaCikisi("Maaş ödemesi / avans iptali");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await KilitleAsync(sirketId);
        var ph = await _baglam.PersonelHareketleri.FirstOrDefaultAsync(h => h.Id == personelHareketiId && h.SirketId == sirketId)
            ?? throw new KeyNotFoundException("Kayıt bulunamadı.");
        if (ph.Tur == PersonelHareketTuru.MaasTahakkuku)
            throw new InvalidOperationException("Maaş tahakkuku bu düğmeyle değil, Maaş Tahakkuku bölümünden dönem olarak geri alınır.");
        if (ph.Tur == PersonelHareketTuru.Avans && (await BakiyelerAsync(sirketId, ph.PersonelId)).GetValueOrDefault(ph.PersonelId).Avans - ph.Tutar < 0)
            throw new InvalidOperationException("Bu avans maaştan düşülmüş. Önce o ayın maaş tahakkukunu geri alın.");
        var temizNeden = string.IsNullOrWhiteSpace(neden) ? "Kullanıcı iptal etti" : neden.Trim();
        var tarih = FinansKurallari.TarihNormalizeEt(null);
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);

        if (ph.KasaHareketiId is int kid)
        {
            var asil = await _baglam.KasaHareketleri.SingleAsync(x => x.Id == kid);
            if (asil.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            {
                if (asil.KasaId.HasValue) await FinansKurallari.KasaKilitleAsync(_baglam, asil.KasaId.Value);
                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                var ters = new KasaHareketi
                {
                    SirketId = asil.SirketId, KasaId = asil.KasaId, Tarih = tarih, Tur = HareketTuru.Gelir, FisTuru = asil.FisTuru, FisNo = FinansKurallari.FisNoUret("IPT"),
                    Tutar = asil.Tutar, Aciklama = $"Ters Kayıt: İptal ({temizNeden})", OzelKod = PersonelOzelKodu, IlgiliBelgeId = ph.Id,
                    IslemDurumu = FinansIslemDurumu.TersKayit, TersKayitId = asil.Id
                };
                _baglam.KasaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync();
                asil.TersKayitId = ters.Id;
                await TersFisEkleAsync(_baglam, true, asil.Id, ters, null);
            }
        }
        else if (ph.BankaHareketiId is int bid)
        {
            var asil = await _baglam.BankaHareketleri.SingleAsync(x => x.Id == bid);
            if (asil.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            {
                if (asil.BankaHesabiId.HasValue) await FinansKurallari.BankaHesabiKilitleAsync(_baglam, asil.BankaHesabiId.Value);
                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                var ters = new BankaHareketi
                {
                    SirketId = asil.SirketId, BankaHesabiId = asil.BankaHesabiId, BankaAdi = asil.BankaAdi, HesapNo = asil.HesapNo, Tarih = tarih, Tur = HareketTuru.Gelir,
                    FisTuru = asil.FisTuru, OdemeYontemi = asil.OdemeYontemi, FisNo = FinansKurallari.FisNoUret("IPT"), Tutar = asil.Tutar,
                    Aciklama = $"Ters Kayıt: İptal ({temizNeden})", OzelKod = PersonelOzelKodu, IlgiliBelgeId = ph.Id,
                    IslemDurumu = FinansIslemDurumu.TersKayit, TersKayitId = asil.Id
                };
                _baglam.BankaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync();
                asil.TersKayitId = ters.Id;
                await TersFisEkleAsync(_baglam, false, asil.Id, null, ters);
            }
        }
        ph.Silindi = true;
        ph.SilinmeTarihi = DateTime.UtcNow;
        ph.SilinmeNedeni = $"İptal: {temizNeden}";
        await _baglam.SaveChangesAsync();
        await islem.CommitAsync();
    }

    public async Task<List<PersonelEkstreSatiriDto>> EkstreAsync(int personelId, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var hareketler = await _baglam.PersonelHareketleri.AsNoTracking().Where(h => h.SirketId == sirketId && h.PersonelId == personelId)
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToListAsync();
        var kasaIdleri = hareketler.Where(h => h.KasaHareketiId != null).Select(h => h.KasaHareketiId!.Value).ToList();
        var bankaIdleri = hareketler.Where(h => h.BankaHareketiId != null).Select(h => h.BankaHareketiId!.Value).ToList();
        var kasaAdlari = await _baglam.KasaHareketleri.AsNoTracking().Where(k => kasaIdleri.Contains(k.Id)).Select(k => new { k.Id, Ad = k.Kasa!.Ad }).ToDictionaryAsync(k => k.Id, k => k.Ad);
        var bankaAdlari = await _baglam.BankaHareketleri.AsNoTracking().Where(b => bankaIdleri.Contains(b.Id)).Select(b => new { b.Id, Ad = b.BankaHesabi!.Ad }).ToDictionaryAsync(b => b.Id, b => b.Ad);

        // Bakiye = personele borç: tahakkuk edilen net artırır, ödeme ve avans azaltır (maaştan düşülen avans zaten verilmişti).
        var bakiye = 0m;
        var liste = new List<PersonelEkstreSatiriDto>();
        foreach (var h in hareketler)
        {
            var tahakkuk = h.Tur == PersonelHareketTuru.MaasTahakkuku;
            bakiye += tahakkuk ? h.NetUcret : -h.Tutar;
            liste.Add(new PersonelEkstreSatiriDto
            {
                Id = h.Id, Tarih = h.Tarih, Aciklama = h.Aciklama, IptalEdilebilir = !tahakkuk, Bakiye = bakiye,
                Islem = h.Tur switch { PersonelHareketTuru.MaasTahakkuku => "Maaş tahakkuku", PersonelHareketTuru.MaasOdemesi => "Maaş ödemesi", _ => "Avans" },
                Tahakkuk = tahakkuk ? h.NetUcret : 0, Odenen = tahakkuk ? 0 : h.Tutar,
                Hesap = tahakkuk ? $"Brüt {h.BrutUcret:N2}" + (h.AvansKesintisi > 0 ? $", avans kesintisi {h.AvansKesintisi:N2}" : "")
                    : h.KasaHareketiId is int k ? $"Kasa: {kasaAdlari.GetValueOrDefault(k)}" : h.BankaHareketiId is int b ? $"Banka: {bankaAdlari.GetValueOrDefault(b)}" : ""
            });
        }
        return liste;
    }

    // ---------------- Vergi / SGK ödemesi ----------------

    private async Task<decimal> AlacakBakiyesiAsync(Guid sirketId, string kod) =>
        await _baglam.YevmiyeSatirlari.Where(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Hesap!.HesapKodu.StartsWith(kod))
            .SumAsync(s => (decimal?)(s.Alacak - s.Borc)) ?? 0m;

    public async Task<VergiSgkBakiyeDto> VergiSgkBakiyeAsync(Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        return new VergiSgkBakiyeDto { OdenecekVergi = await AlacakBakiyesiAsync(sirketId, "360"), OdenecekSgk = await AlacakBakiyesiAsync(sirketId, "361") };
    }

    public async Task<PersonelUyariDto> UyariAsync(DateTime bugun, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var o = new PersonelUyariDto
        {
            CalisanSayisi = await _baglam.Personeller.CountAsync(p => p.SirketId == sirketId && p.IstenCikisTarihi == null)
        };
        var bakiyeler = await BakiyelerAsync(sirketId);
        o.OdenmemisKisi = bakiyeler.Values.Count(b => b.Odenecek > 0);
        o.OdenmemisNet = bakiyeler.Values.Where(b => b.Odenecek > 0).Sum(b => b.Odenecek);
        if (o.CalisanSayisi == 0 && o.OdenmemisKisi == 0) return o;

        var gecenAy = bugun.Date.AddMonths(-1);
        (o.TahakkukYil, o.TahakkukAy) = (gecenAy.Year, gecenAy.Month);
        var donem = await DonemHazirlaAsync(gecenAy.Year, gecenAy.Month, sirketId);
        // Hiç tahakkuk yapılmamış bir firmada (bordroyu yalnız müşavir işliyorsa) her ay uyarmak gürültü olur.
        var hicTahakkukVar = await _baglam.PersonelHareketleri.AnyAsync(h => h.SirketId == sirketId && h.Tur == PersonelHareketTuru.MaasTahakkuku);
        if (donem.Engeller.Count == 0 && hicTahakkukVar) o.TahakkukBekleyen = donem.Bekleyenler.Count;
        o.OdenecekVergi = Math.Max(0, await AlacakBakiyesiAsync(sirketId, "360"));
        o.OdenecekSgk = Math.Max(0, await AlacakBakiyesiAsync(sirketId, "361"));
        return o;
    }

    public async Task VergiSgkOdeAsync(string hesapKodu, decimal tutar, DateTime tarih, string kaynak, int hesapId, string? aciklama = null, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Oturum.YetkiKontrolEt(y => y.OdemeYap && (y.PersonelDuzenle || y.MuhasebeFisEkle), "Vergi / SGK ödemesi");
        if (hesapKodu is not ("360" or "361")) throw new ArgumentException("Ödenecek hesap 360 (vergi) ya da 361 (SGK) olmalıdır.");
        FinansKurallari.TutarDogrula(tutar, "Ödeme");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await KilitleAsync(sirketId);
        var borc = await AlacakBakiyesiAsync(sirketId, hesapKodu);
        var ad = hesapKodu == "360" ? "vergi" : "SGK primi";
        if (tutar > borc)
            throw new InvalidOperationException($"Ödenecek {ad} borcu {Math.Max(0, borc):N2} TL; daha fazlası ödenemez.");
        await CikisEkleAsync(sirketId, kaynak, hesapId, tutar, tarih,
            string.IsNullOrWhiteSpace(aciklama) ? (hesapKodu == "360" ? "Vergi ödemesi (muhtasar / KDV / damga)" : "SGK prim ödemesi") : aciklama.Trim(),
            hesapKodu, VergiSgkOzelKodu, null);
        await islem.CommitAsync();
    }
}
