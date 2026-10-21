using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.EBelge;

public class EBelgeServisi : IEBelgeServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly IEBelgeEntegratoru _entegrator;

    public EBelgeServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null, IEBelgeEntegratoru? entegrator = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _entegrator = entegrator ?? new DosyaEBelgeEntegratoru(() =>
            _baglam.SistemAyarlari.AsNoTracking().Where(a => a.Id == 1).Select(a => a.EBelgeKlasoru).FirstOrDefault() ?? string.Empty);
    }

    public string EntegratorAdi => _entegrator.Ad;
    public bool DogrudanGonderir => _entegrator.DogrudanGonderir;

    public async Task<EBelgeOnKontrol> OnKontrolAsync(EBelgeHazirlamaIstegi istek, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaGoruntule, "e-Belge Ön Kontrol");
        var fatura = await FaturaGetirAsync(istek.FaturaId, izle: false, cancellationToken);
        IadeBilgisiUygula(fatura, istek);
        var girdi = await GirdiOlusturAsync(fatura, istek.Senaryo, cancellationToken);
        var hatalar = DurumHatalari(fatura);
        hatalar.AddRange(UblTrFaturaUretici.Dogrula(girdi));
        return new EBelgeOnKontrol(fatura.Id, fatura.FaturaNo, girdi.Tur, girdi.Senaryo, fatura.EBelgeDurumu,
            fatura.CariKart?.Unvan ?? string.Empty, girdi.Alici.VknTckn, fatura.Tur == FaturaTuru.AlisIade,
            fatura.IadeEdilenFaturaNo, fatura.IadeEdilenFaturaTarihi, fatura.EBelgeMesaji, hatalar);
    }

    public async Task<EBelgeGonderimSonucu> HazirlaVeGonderAsync(EBelgeHazirlamaIstegi istek, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaOnayla, "e-Belge Hazırlama/Gönderme");
        var fatura = await FaturaGetirAsync(istek.FaturaId, izle: true, cancellationToken);

        IadeBilgisiUygula(fatura, istek);

        var girdi = await GirdiOlusturAsync(fatura, istek.Senaryo, cancellationToken);
        var hatalar = DurumHatalari(fatura);
        hatalar.AddRange(UblTrFaturaUretici.Dogrula(girdi));
        if (hatalar.Count > 0)
            throw new InvalidOperationException("e-Belge hazırlanamadı:\n- " + string.Join("\n- ", hatalar));

        // ETTN belgenin kimliğidir: hata sonrası yeniden hazırlamada aynı ETTN kullanılır (aynı belge iki kez oluşmasın).
        fatura.Ettn ??= girdi.Ettn;
        fatura.EBelgeTuru = girdi.Tur;
        fatura.EFaturaSenaryosu = girdi.Senaryo;
        var xml = UblTrFaturaUretici.UretMetin(girdi with { Ettn = fatura.Ettn.Value });

        EBelgeGonderimSonucu sonuc;
        try
        {
            sonuc = await _entegrator.GonderAsync(new EBelgePaketi(fatura.FaturaNo, fatura.Ettn.Value, girdi.Tur, girdi.Alici.VknTckn,
                fatura.CariKart?.EFaturaEtiketi, xml), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sonuc = new EBelgeGonderimSonucu(false, EBelgeDurumu.Hata, ex.Message);
        }

        fatura.EBelgeDurumu = sonuc.Durum;
        fatura.EBelgeTarihi = DateTime.UtcNow;
        fatura.EBelgeMesaji = sonuc.Mesaj;
        _baglam.EBelgeKayitlari.Add(new EBelgeKaydi
        {
            FaturaId = fatura.Id, Ettn = fatura.Ettn.Value, Tur = girdi.Tur, Durum = sonuc.Durum, Entegrator = _entegrator.Ad,
            Xml = xml, DosyaYolu = sonuc.DosyaYolu, Mesaj = sonuc.Mesaj, KullaniciId = _kullaniciServisi?.AktifKullanici?.Id
        });
        await _baglam.SaveChangesAsync(cancellationToken);
        return sonuc;
    }

    public async Task DurumIsaretleAsync(int faturaId, EBelgeDurumu durum, string? aciklama = null, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaOnayla, "e-Belge Durum İşaretleme");
        var fatura = await FaturaGetirAsync(faturaId, izle: true, cancellationToken);
        if (fatura.EBelgeDurumu == EBelgeDurumu.Yok || fatura.Ettn == null)
            throw new InvalidOperationException("Bu fatura için henüz e-belge hazırlanmadı.");
        if (durum is EBelgeDurumu.Yok or EBelgeDurumu.Hazirlandi)
            throw new InvalidOperationException("Bu durum elle işaretlenemez.");
        if (fatura.EBelgeDurumu == EBelgeDurumu.IptalEdildi)
            throw new InvalidOperationException("İptal edilmiş e-belgenin durumu değiştirilemez.");
        if (durum == EBelgeDurumu.Reddedildi && !(fatura.EBelgeTuru == EBelgeTuru.EFatura && fatura.EFaturaSenaryosu == EFaturaSenaryosu.Ticari))
            throw new InvalidOperationException("Yalnız ticari senaryodaki e-Fatura alıcı tarafından reddedilebilir.");

        fatura.EBelgeDurumu = durum;
        fatura.EBelgeTarihi = DateTime.UtcNow;
        fatura.EBelgeMesaji = string.IsNullOrWhiteSpace(aciklama) ? $"Kullanıcı tarafından '{durum}' olarak işaretlendi." : aciklama.Trim();
        _baglam.EBelgeKayitlari.Add(new EBelgeKaydi
        {
            FaturaId = fatura.Id, Ettn = fatura.Ettn.Value, Tur = fatura.EBelgeTuru, Durum = durum, Entegrator = _entegrator.Ad,
            Mesaj = fatura.EBelgeMesaji, KullaniciId = _kullaniciServisi?.AktifKullanici?.Id
        });
        await _baglam.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> SonXmlGetirAsync(int faturaId, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaGoruntule, "e-Belge Görüntüleme");
        // Şirket ayrımı fatura üzerinden: başka şirketin faturası bulunamaz.
        await FaturaGetirAsync(faturaId, izle: false, cancellationToken);
        return await _baglam.EBelgeKayitlari.AsNoTracking()
            .Where(k => k.FaturaId == faturaId && k.Xml != "")
            .OrderByDescending(k => k.Id).Select(k => k.Xml).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EBelgeTopluSonuc>> TopluHazirlaAsync(DateTime baslangic, DateTime bitis, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaOnayla, "Toplu e-Belge Hazırlama");
        var aralik = RaporTarihAraligi.Olustur(baslangic, bitis);
        var adaylar = await _baglam.Faturalar.AsNoTracking()
            .Where(f => f.Durum == BelgeDurumu.Onaylandi && (f.Tur == FaturaTuru.Satis || f.Tur == FaturaTuru.AlisIade) &&
                        (f.EBelgeDurumu == EBelgeDurumu.Yok || f.EBelgeDurumu == EBelgeDurumu.Hata) &&
                        f.Tarih >= aralik.Baslangic && f.Tarih < aralik.BitisSonrasi)
            .OrderBy(f => f.Tarih).ThenBy(f => f.FaturaNo)
            .Select(f => new { f.Id, f.FaturaNo, Unvan = f.CariKart != null ? f.CariKart.Unvan : "" })
            .ToListAsync(cancellationToken);

        var sonuclar = new List<EBelgeTopluSonuc>();
        foreach (var a in adaylar)
        {
            try
            {
                var s = await HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(a.Id), cancellationToken);
                sonuclar.Add(new EBelgeTopluSonuc(a.Id, a.FaturaNo, a.Unvan, s.Basarili, s.Mesaj ?? string.Empty));
            }
            catch (InvalidOperationException ex)
            {
                // Doğrulama hatası: bu fatura atlanır, yarım kalan değişiklik sonraki kayda karışmasın.
                foreach (var giris in _baglam.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                {
                    if (giris.State == EntityState.Added) { giris.State = EntityState.Detached; continue; }
                    giris.CurrentValues.SetValues(giris.OriginalValues);
                    giris.State = EntityState.Unchanged;
                }
                sonuclar.Add(new EBelgeTopluSonuc(a.Id, a.FaturaNo, a.Unvan, false, ex.Message));
            }
        }
        return sonuclar;
    }

    private static void IadeBilgisiUygula(Fatura fatura, EBelgeHazirlamaIstegi istek)
    {
        if (fatura.Tur != FaturaTuru.AlisIade)
            return;
        if (!string.IsNullOrWhiteSpace(istek.IadeEdilenFaturaNo))
            fatura.IadeEdilenFaturaNo = istek.IadeEdilenFaturaNo.Trim();
        if (istek.IadeEdilenFaturaTarihi.HasValue)
            fatura.IadeEdilenFaturaTarihi = DateTime.SpecifyKind(istek.IadeEdilenFaturaTarihi.Value.Date, DateTimeKind.Utc);
    }

    private static List<string> DurumHatalari(Fatura fatura)
    {
        var hatalar = new List<string>();
        if (fatura.EBelgeDurumu is EBelgeDurumu.Gonderildi or EBelgeDurumu.KabulEdildi or EBelgeDurumu.Reddedildi or EBelgeDurumu.IptalEdildi)
            hatalar.Add("Bu faturanın e-belgesi zaten gönderildi; yeniden hazırlanamaz.");
        return hatalar;
    }

    private async Task<Fatura> FaturaGetirAsync(int faturaId, bool izle, CancellationToken cancellationToken)
    {
        var sorgu = _baglam.Faturalar.Include(f => f.Kalemler).Include(f => f.CariKart).AsQueryable();
        if (!izle)
            sorgu = sorgu.AsNoTracking();
        return await sorgu.FirstOrDefaultAsync(f => f.Id == faturaId, cancellationToken)
               ?? throw new KeyNotFoundException("Fatura bulunamadı.");
    }

    private async Task<UblFaturaGirdisi> GirdiOlusturAsync(Fatura fatura, EFaturaSenaryosu? senaryo, CancellationToken cancellationToken)
    {
        var ayar = await _baglam.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(a => a.Id == 1, cancellationToken);
        var sirketId = fatura.SirketId ?? MuhasebeBaglami.VarsayilanSirketId;
        var sirket = await _baglam.Sirketler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sirketId, cancellationToken);
        var firma = BelgeYazdirmaServisi.FirmaBilgisiOlustur(ayar, sirket, sirketId == MuhasebeBaglami.VarsayilanSirketId);

        var cari = fatura.CariKart ?? new CariKart();
        var tur = cari.EFaturaMukellefi ? EBelgeTuru.EFatura : EBelgeTuru.EArsiv;
        var secilenSenaryo = senaryo
            ?? (fatura.EBelgeDurumu != EBelgeDurumu.Yok ? fatura.EFaturaSenaryosu : ayar?.EFaturaVarsayilanSenaryo ?? EFaturaSenaryosu.Temel);
        return new UblFaturaGirdisi(fatura, fatura.Ettn ?? Guid.NewGuid(), tur, secilenSenaryo, Satici(firma), Alici(cari, tur));
    }

    private static UblTaraf Satici(FirmaBilgisi f) => new(
        Rakamlar(f.VergiNo), f.FirmaUnvani, VergiDairesi: f.VergiDairesi, Adres: f.Adres, Ilce: f.Ilce, Il: f.Il, PostaKodu: f.PostaKodu,
        Telefon: f.Telefon, Eposta: f.Eposta, MersisNo: f.MersisNo, WebSitesi: f.WebSitesi);

    // Alıcı kimliği: 10 haneli vergi no, yoksa 11 haneli T.C. kimlik no; e-Arşivde ikisi de yoksa nihai tüketici.
    private static UblTaraf Alici(CariKart c, EBelgeTuru tur)
    {
        var vkn = Rakamlar(c.VergiNo);
        var tckn = Rakamlar(c.TcNo);
        var kimlik = vkn.Length is 10 or 11 ? vkn : tckn.Length == 11 ? tckn : string.Empty;
        if (kimlik.Length == 0 && tur == EBelgeTuru.EArsiv)
            kimlik = UblTrFaturaUretici.NihaiTuketiciTckn;
        return new UblTaraf(kimlik, c.Unvan, c.Ad, c.Soyad, c.VergiDairesi, c.Adres, c.Ilce, c.Il, c.PostaKodu,
            c.CepTelefonu ?? c.Telefon, c.Eposta);
    }

    private static string Rakamlar(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    private void Yetki(Func<EtkiAlani.Modeller.Kullanici.KullaniciYetkileri, bool> sorgu, string islem) =>
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(sorgu, islem);
}
