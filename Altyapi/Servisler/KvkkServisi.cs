using System.Text;
using System.Text.Json;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

// Muhasebe kayıtları Vergi Usul Kanunu (md. 253) ve Türk Ticaret Kanunu (md. 82) gereği, kaydın yılının sonundan
// itibaren 10 yıl saklanır; bu süre dolmadan kişisel veriler silinemez (KVKK md. 5/2-ç: hukuki yükümlülük).
// Süre dolmuş ya da hiç işlemi olmayan carinin kişisel verileri anonimleştirilir: muhasebe tutarları ve belgeler
// kalır, kişiyi tanımlayan alanlar (ad, kimlik/vergi no, iletişim, adres, IBAN) ve günlükteki eski değerleri silinir.
public sealed class KvkkServisi : IKvkkServisi
{
    public const int SaklamaYili = 10;
    private const string Silinmis = MuhasebeBaglami.SilinmisFiltresi;
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public KvkkServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private IKullaniciServisi Oturum => _kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama");

    private async Task<CariKart> CariGetirAsync(int cariId) =>
        await _baglam.CariKartlar.IgnoreQueryFilters([Silinmis]).FirstOrDefaultAsync(c => c.Id == cariId)
        ?? throw new KeyNotFoundException("Cari bulunamadı.");

    // Cariye bağlı en son muhasebe kaydının (iptal/silinmiş dahil) tarihi.
    private async Task<DateTime?> SonKayitTarihiAsync(int cariId)
    {
        var tarihler = new[]
        {
            await _baglam.CariHareketler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).MaxAsync(x => (DateTime?)x.Tarih),
            await _baglam.Faturalar.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).MaxAsync(x => (DateTime?)x.Tarih),
            await _baglam.Siparisler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).MaxAsync(x => (DateTime?)x.SiparisTarihi),
            await _baglam.Irsaliyeler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).MaxAsync(x => (DateTime?)x.Tarih),
            await _baglam.CekSenetler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).MaxAsync(x => (DateTime?)x.DuzenlemeTarihi),
            await _baglam.KasaHareketleri.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).MaxAsync(x => (DateTime?)x.Tarih),
            await _baglam.BankaHareketleri.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).MaxAsync(x => (DateTime?)x.Tarih)
        };
        return tarihler.Max();
    }

    public async Task<KvkkSaklamaDurumu> SaklamaDurumuAsync(int cariId)
    {
        Oturum.YetkiKontrolEt(y => y.CariGoruntule, "KVKK Saklama Durumu");
        await CariGetirAsync(cariId);
        var son = await SonKayitTarihiAsync(cariId);
        if (son == null)
            return new(true, null, null, "Cariye bağlı muhasebe kaydı yok; kişisel verileri anonimleştirilebilir.");
        var bitis = new DateTime(son.Value.Year + SaklamaYili, 12, 31);
        return DateTime.Today > bitis
            ? new(true, son, bitis, $"Yasal saklama süresi {bitis:dd.MM.yyyy} tarihinde doldu; kişisel veriler anonimleştirilebilir.")
            : new(false, son, bitis,
                $"Son kayıt {son:dd.MM.yyyy} tarihli. Vergi Usul Kanunu ve Türk Ticaret Kanunu gereği muhasebe kayıtları {bitis:dd.MM.yyyy} tarihine kadar saklanmalıdır; " +
                "bu tarihten önce kişisel veriler silinemez (KVKK md. 5/2-ç). Talep sahibine bu gerekçeyle yanıt verilmelidir.");
    }

    public async Task<string> KisiselVeriDokumuAsync(int cariId)
    {
        Oturum.YetkiKontrolEt(y => y.CariGoruntule, "KVKK Kişisel Veri Dökümü");
        var c = await CariGetirAsync(cariId);
        var sb = new StringBuilder();
        sb.AppendLine("KİŞİSEL VERİ DÖKÜMÜ (KVKK md. 11)");
        sb.AppendLine($"Hazırlanma tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}    Hazırlayan: {Oturum.AktifKullanici?.KullaniciAdi}");
        sb.AppendLine(new string('-', 70));
        sb.AppendLine("1. Kimlik ve iletişim bilgileri (cari kartı)");
        void Satir(string ad, string? deger) { if (!string.IsNullOrWhiteSpace(deger)) sb.AppendLine($"   {ad,-18}: {deger}"); }
        Satir("Cari kodu", c.Kod); Satir("Ünvan", c.Unvan); Satir("Ad", c.Ad); Satir("Soyad", c.Soyad);
        Satir("T.C. kimlik no", c.TcNo); Satir("Vergi dairesi", c.VergiDairesi); Satir("Vergi no", c.VergiNo);
        Satir("Telefon", c.Telefon); Satir("Cep telefonu", c.CepTelefonu); Satir("Ev telefonu", c.EvTelefonu); Satir("İş telefonu", c.IsTelefonu);
        Satir("E-posta", c.Eposta); Satir("Web sitesi", c.WebSitesi); Satir("Adres", c.Adres); Satir("İl / İlçe", $"{c.Il} {c.Ilce}".Trim());
        Satir("Posta kodu", c.PostaKodu); Satir("IBAN", c.Iban); Satir("Fotoğraf", string.IsNullOrWhiteSpace(c.FotografYolu) ? null : "Kayıtlı");
        Satir("Not", c.Aciklama); Satir("Kayıt tarihi", c.OlusturmaTarihi.ToString("dd.MM.yyyy"));

        sb.AppendLine();
        sb.AppendLine("2. İşlem kayıtları (ticari ilişki ve yasal yükümlülük gereği işlenir)");
        async Task Ozet(string ad, IQueryable<DateTime> tarihler)
        {
            var liste = await tarihler.ToListAsync();
            if (liste.Count > 0) sb.AppendLine($"   {ad,-22}: {liste.Count} kayıt ({liste.Min():dd.MM.yyyy} - {liste.Max():dd.MM.yyyy})");
        }
        await Ozet("Faturalar", _baglam.Faturalar.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).Select(x => x.Tarih));
        await Ozet("Siparişler", _baglam.Siparisler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).Select(x => x.SiparisTarihi));
        await Ozet("İrsaliyeler", _baglam.Irsaliyeler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).Select(x => x.Tarih));
        await Ozet("Cari hesap hareketleri", _baglam.CariHareketler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).Select(x => x.Tarih));
        await Ozet("Kasa hareketleri", _baglam.KasaHareketleri.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).Select(x => x.Tarih));
        await Ozet("Banka hareketleri", _baglam.BankaHareketleri.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).Select(x => x.Tarih));
        await Ozet("Çek / senet", _baglam.CekSenetler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).Select(x => x.DuzenlemeTarihi));

        var teslimatlar = (await _baglam.Siparisler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId)
                .Select(x => new { x.TeslimatAdSoyad, x.TeslimatTelefon, x.TeslimatAdres, x.TeslimatIlce, x.TeslimatIl }).ToListAsync())
            .Concat(await _baglam.Irsaliyeler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId)
                .Select(x => new { x.TeslimatAdSoyad, x.TeslimatTelefon, x.TeslimatAdres, x.TeslimatIlce, x.TeslimatIl }).ToListAsync())
            .Select(x => string.Join(" / ", new[] { x.TeslimatAdSoyad, x.TeslimatTelefon, $"{x.TeslimatAdres} {x.TeslimatIlce} {x.TeslimatIl}".Trim() }
                .Where(p => !string.IsNullOrWhiteSpace(p))))
            .Where(x => x.Length > 0).Distinct().ToList();
        if (teslimatlar.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("3. Sipariş / irsaliyelerdeki teslimat bilgileri");
            foreach (var t in teslimatlar) sb.AppendLine($"   {t}");
        }

        var durum = await SaklamaDurumuAsync(cariId);
        sb.AppendLine();
        sb.AppendLine("4. İşleme amacı ve saklama süresi");
        sb.AppendLine("   Veriler satış/alış, faturalama, tahsilat-ödeme ve yasal defter tutma amacıyla işlenir; yasal saklama süresince");
        sb.AppendLine("   yalnız yetkili personel ve mevzuat gereği yetkili kamu kurumlarıyla (ör. Gelir İdaresi) paylaşılır.");
        sb.AppendLine($"   {durum.Aciklama}");
        return sb.ToString();
    }

    public async Task AnonimlestirAsync(int cariId, string talepAciklamasi)
    {
        Oturum.YetkiKontrolEt(y => y.CariSil, "KVKK Anonimleştirme");
        if (string.IsNullOrWhiteSpace(talepAciklamasi) || talepAciklamasi.Trim().Length < 3)
            throw new ArgumentException("Talep açıklaması yazılmalıdır (ör. talep tarihi ve başvuru şekli).");
        var durum = await SaklamaDurumuAsync(cariId);
        if (!durum.AnonimlestirilebilirMi) throw new InvalidOperationException(durum.Aciklama);

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        var c = await CariGetirAsync(cariId);
        var fotograf = c.FotografYolu;
        c.Unvan = $"ANONİM-{c.Kod}";
        c.Ad = c.Soyad = c.TcNo = c.VergiNo = c.VergiDairesi = null;
        c.Telefon = c.EvTelefonu = c.IsTelefonu = c.CepTelefonu = c.Eposta = c.WebSitesi = null;
        c.Adres = c.Il = c.Ilce = c.PostaKodu = c.FotografYolu = c.Iban = c.Aciklama = null;
        c.Aktif = false;
        if (!c.Silindi) { c.Silindi = true; c.SilinmeTarihi = DateTime.UtcNow; }
        c.SilinmeNedeni = $"KVKK anonimleştirme: {talepAciklamasi.Trim()} (Yapan: {Oturum.AktifKullanici?.KullaniciAdi ?? "?"})";

        var siparisler = await _baglam.Siparisler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).ToListAsync();
        foreach (var s in siparisler) s.TeslimatAdSoyad = s.TeslimatTelefon = s.TeslimatAdres = s.TeslimatIl = s.TeslimatIlce = "";
        var irsaliyeler = await _baglam.Irsaliyeler.IgnoreQueryFilters([Silinmis]).Where(x => x.CariKartId == cariId).ToListAsync();
        foreach (var i in irsaliyeler) i.TeslimatAdSoyad = i.TeslimatTelefon = i.TeslimatAdres = i.TeslimatIl = i.TeslimatIlce = "";

        _baglam.KisiselVeriMaskele = true;
        try { await _baglam.SaveChangesAsync(); }
        finally { _baglam.KisiselVeriMaskele = false; }

        // Değişiklik günlüğündeki eski kişisel değerler de maskelenir.
        var kayitlar = new List<(string Tablo, List<string> Idler)>
        {
            (nameof(CariKart), [cariId.ToString()]),
            ("Siparis", siparisler.Select(x => x.Id.ToString()).ToList()),
            ("Irsaliye", irsaliyeler.Select(x => x.Id.ToString()).ToList())
        };
        foreach (var (tablo, idler) in kayitlar.Where(k => k.Idler.Count > 0))
        {
            var loglar = await _baglam.AuditLoglar.Where(l => l.TabloAdi == tablo && idler.Contains(l.KayitId)).ToListAsync();
            foreach (var l in loglar)
            {
                l.EskiDegerJson = Maskele(l.EskiDegerJson);
                l.YeniDegerJson = Maskele(l.YeniDegerJson);
            }
        }
        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();

        if (!string.IsNullOrWhiteSpace(fotograf))
            try { if (File.Exists(fotograf)) File.Delete(fotograf); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string? Maskele(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        try
        {
            var alanlar = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (alanlar == null) return json;
            return JsonSerializer.Serialize(alanlar.ToDictionary(a => a.Key, a =>
                MuhasebeBaglami.KisiselVeriAlanlari.Contains(a.Key) && a.Value.ValueKind != JsonValueKind.Null ? JsonSerializer.SerializeToElement("***") : a.Value));
        }
        catch (JsonException) { return null; }
    }
}
