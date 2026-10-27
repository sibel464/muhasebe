using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Fatura;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public sealed record SecenekOgesi<T>(string Ad, T Deger);

// Tek planın düzenlenmesi: yeni planda örnek fatura, fatura listesinde seçilen faturadır.
public class TekrarlayanFaturaPlanModeli : TemelGorunumModeli
{
    private readonly ITekrarlayanFaturaServisi _servis;
    private readonly TekrarlayanFatura _plan;
    private string _ad, _vadeGunu, _durum = string.Empty;
    private int _periyotAy;
    private DateTime? _baslangic, _bitis;
    private bool _onayla, _aktif;

    public TekrarlayanFaturaPlanModeli(ITekrarlayanFaturaServisi servis, TekrarlayanFatura plan, string ornekBilgisi)
    {
        _servis = servis;
        _plan = plan;
        OrnekBilgisi = ornekBilgisi;
        _ad = plan.Ad;
        _periyotAy = plan.PeriyotAy;
        _baslangic = plan.BaslangicTarihi == default ? null : plan.BaslangicTarihi;
        _bitis = plan.BitisTarihi;
        _vadeGunu = plan.VadeGunu?.ToString() ?? string.Empty;
        _onayla = plan.Onayla;
        _aktif = plan.Aktif;
        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync());
    }

    public static IReadOnlyList<SecenekOgesi<int>> Periyotlar { get; } =
    [
        new("Her ay", 1), new("2 ayda bir", 2), new("3 ayda bir", 3), new("6 ayda bir", 6), new("Her yıl", 12)
    ];

    public string Baslik => _plan.Id == 0 ? "Tekrarlayan Fatura Oluştur" : "Tekrarlayan Faturayı Düzenle";
    public string OrnekBilgisi { get; }
    public bool TakvimDegistirilebilir => _plan.OlusturulanAdet == 0;
    public string Ad { get => _ad; set => OzellikAyarla(ref _ad, value); }
    public int PeriyotAy { get => _periyotAy; set => OzellikAyarla(ref _periyotAy, value); }
    public DateTime? Baslangic { get => _baslangic; set => OzellikAyarla(ref _baslangic, value); }
    public DateTime? Bitis { get => _bitis; set => OzellikAyarla(ref _bitis, value); }
    public string VadeGunu { get => _vadeGunu; set => OzellikAyarla(ref _vadeGunu, value); }
    public bool Onayla { get => _onayla; set => OzellikAyarla(ref _onayla, value); }
    public bool Aktif { get => _aktif; set => OzellikAyarla(ref _aktif, value); }
    public string Durum { get => _durum; private set => OzellikAyarla(ref _durum, value); }
    public ICommand KaydetKomutu { get; }
    public Action? Kaydedildi { get; set; }

    private async Task KaydetAsync()
    {
        try
        {
            if (Baslangic is not DateTime baslangic) { Durum = "İlk fatura tarihini seçin."; return; }
            int? vade = null;
            if (!string.IsNullOrWhiteSpace(VadeGunu))
            {
                if (!int.TryParse(VadeGunu.Trim(), out var v) || v < 0) { Durum = "Vade günü 0 ya da daha büyük bir sayı olmalı (boş bırakılabilir)."; return; }
                vade = v;
            }
            await _servis.KaydetAsync(new TekrarlayanFatura
            {
                Id = _plan.Id, OrnekFaturaId = _plan.OrnekFaturaId, Ad = Ad, PeriyotAy = PeriyotAy, BaslangicTarihi = baslangic,
                BitisTarihi = Bitis, VadeGunu = vade, Onayla = Onayla, Aktif = Aktif
            });
            Kaydedildi?.Invoke();
        }
        catch (Exception ex)
        {
            Durum = ex.Message;
        }
    }
}

public class TekrarlayanFaturalarModeli : TemelGorunumModeli
{
    private readonly ITekrarlayanFaturaServisi _servis;
    private TekrarlayanFaturaListeDto? _secili;
    private string _durum = string.Empty;
    private bool _calisiyor;

    public TekrarlayanFaturalarModeli(ITekrarlayanFaturaServisi servis)
    {
        _servis = servis;
        YenileKomutu = new KomutSarici(async _ => await YukleAsync(), _ => !_calisiyor);
        TumunuKesKomutu = new KomutSarici(async _ => await KesAsync(null), _ => !_calisiyor && Planlar.Any(p => p.BekleyenAdet > 0));
        SeciliKesKomutu = new KomutSarici(async _ => await KesAsync(Secili?.Id), _ => !_calisiyor && Secili?.BekleyenAdet > 0);
        DuzenleKomutu = new KomutSarici(async _ => await DuzenleAsync(), _ => !_calisiyor && Secili != null);
        DurdurBaslatKomutu = new KomutSarici(async _ => await DurdurBaslatAsync(), _ => !_calisiyor && Secili != null);
        SilKomutu = new KomutSarici(async _ => await SilAsync(), _ => !_calisiyor && Secili != null);
    }

    public ObservableCollection<TekrarlayanFaturaListeDto> Planlar { get; } = new();
    public TekrarlayanFaturaListeDto? Secili { get => _secili; set { OzellikAyarla(ref _secili, value); CommandManager.InvalidateRequerySuggested(); } }
    public string Durum { get => _durum; private set => OzellikAyarla(ref _durum, value); }
    // Pencere kapanınca fatura listesi yenilensin mi
    public bool FaturaKesildi { get; private set; }

    public ICommand YenileKomutu { get; }
    public ICommand TumunuKesKomutu { get; }
    public ICommand SeciliKesKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand DurdurBaslatKomutu { get; }
    public ICommand SilKomutu { get; }

    // Pencere tarafı sağlar (düzenleme diyaloğu, silme onayı)
    public Func<TekrarlayanFatura, string, bool>? PlanDuzenle { get; set; }
    public Func<string, bool>? Onay { get; set; }

    private void Calis(bool deger) { _calisiyor = deger; CommandManager.InvalidateRequerySuggested(); }

    public async Task YukleAsync(string? mesaj = null)
    {
        try
        {
            var seciliId = Secili?.Id;
            var liste = await _servis.ListeAsync();
            Planlar.Clear();
            foreach (var p in liste) Planlar.Add(p);
            Secili = Planlar.FirstOrDefault(p => p.Id == seciliId);
            var bekleyen = Planlar.Sum(p => p.BekleyenAdet);
            Durum = mesaj ?? (Planlar.Count == 0
                ? "Henüz tekrarlayan fatura yok. Fatura listesinde örnek alınacak faturanın satırındaki 🔁 düğmesiyle oluşturun."
                : bekleyen > 0 ? $"Kesilmeyi bekleyen {bekleyen} fatura var." : "Kesilmeyi bekleyen fatura yok.");
        }
        catch (Exception ex)
        {
            Durum = "Liste alınamadı: " + ex.Message;
        }
    }

    private async Task KesAsync(int? planId)
    {
        try
        {
            Calis(true);
            Durum = "Faturalar kesiliyor...";
            var sonuclar = await _servis.BekleyenleriKesAsync(null, planId);
            var kesilen = sonuclar.Where(s => s.Hata == null).ToList();
            var hatali = sonuclar.Where(s => s.Hata != null).ToList();
            FaturaKesildi |= kesilen.Count > 0;
            var mesaj = kesilen.Count == 0 ? "Fatura kesilmedi." : $"{kesilen.Count} fatura kesildi: " + string.Join(", ", kesilen.Select(s => s.FaturaNo).Take(10)) + (kesilen.Count > 10 ? " ..." : "") + ".";
            if (hatali.Count > 0)
                mesaj += " Kesilemeyen: " + string.Join("; ", hatali.Select(h => $"{h.PlanAdi} ({h.Tarih:dd.MM.yyyy}): {h.Hata}"));
            await YukleAsync(mesaj);
        }
        catch (Exception ex)
        {
            Durum = "Faturalar kesilemedi: " + ex.Message;
        }
        finally
        {
            Calis(false);
        }
    }

    private async Task DuzenleAsync()
    {
        if (Secili == null || PlanDuzenle == null) return;
        try
        {
            var plan = await _servis.GetirAsync(Secili.Id);
            if (plan == null) { await YukleAsync(); return; }
            if (PlanDuzenle(plan, $"Örnek fatura: {Secili.OrnekFaturaNo} · {Secili.CariUnvan} · {Secili.Tutar:N2} TL"))
                await YukleAsync();
        }
        catch (Exception ex)
        {
            Durum = ex.Message;
        }
    }

    private async Task DurdurBaslatAsync()
    {
        if (Secili == null) return;
        try
        {
            var plan = await _servis.GetirAsync(Secili.Id);
            if (plan == null) return;
            plan.Aktif = !plan.Aktif;
            await _servis.KaydetAsync(plan);
            await YukleAsync(plan.Aktif ? $"'{plan.Ad}' yeniden başlatıldı." : $"'{plan.Ad}' durduruldu; fatura kesilmeyecek.");
        }
        catch (Exception ex)
        {
            Durum = ex.Message;
        }
    }

    private async Task SilAsync()
    {
        if (Secili == null) return;
        if (Onay != null && !Onay($"'{Secili.Ad}' planı silinsin mi? Daha önce kesilmiş faturalar silinmez.")) return;
        try
        {
            await _servis.SilAsync(Secili.Id);
            await YukleAsync("Plan silindi.");
        }
        catch (Exception ex)
        {
            Durum = ex.Message;
        }
    }
}
