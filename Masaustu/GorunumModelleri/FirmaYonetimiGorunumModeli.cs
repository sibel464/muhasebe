using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Kurumsal;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public sealed class FirmaIslemleriGorunumModeli : TemelGorunumModeli
{
    private readonly ISirketServisi _servis;
    private string _arama = "", _mesaj = "Bir firma seçerek şube, mali dönem ve belge serilerini yönetin.";
    private Sirket? _seciliFirma;
    private bool _calisiyor;
    public ObservableCollection<Sirket> Firmalar { get; } = new();
    public ObservableCollection<Sube> Subeler { get; } = new();
    public ObservableCollection<MaliDonem> MaliDonemler { get; } = new();
    public ObservableCollection<BelgeSerisi> BelgeSerileri { get; } = new();
    public string Arama { get => _arama; set { if (OzellikAyarla(ref _arama, value)) _ = YukleAsync(); } }
    public string Mesaj { get => _mesaj; set => OzellikAyarla(ref _mesaj, value); }
    public Sirket? SeciliFirma { get => _seciliFirma; set { if (OzellikAyarla(ref _seciliFirma, value)) { BildirimGonder(nameof(FirmaSeciliMi)); KomutlariGuncelle(); _ = DetayYukleAsync(); } } }
    public bool FirmaSeciliMi => SeciliFirma != null;
    public int ToplamFirma => Firmalar.Count; public int AktifFirma => Firmalar.Count(x => x.AktifMi); public int PasifFirma => ToplamFirma - AktifFirma;
    public int ToplamSube => Subeler.Count; public int AcikDonem => MaliDonemler.Count(x => !x.KilitliMi);
    public ICommand YenileKomutu { get; } public ICommand YeniKomutu { get; } public ICommand DuzenleKomutu { get; } public ICommand DurumKomutu { get; }
    public ICommand YeniSubeKomutu { get; } public ICommand SubeDuzenleKomutu { get; } public ICommand SubeDurumKomutu { get; }
    public ICommand YeniDonemKomutu { get; } public ICommand DonemDuzenleKomutu { get; } public ICommand DonemKilitKomutu { get; }
    public ICommand YeniSeriKomutu { get; } public ICommand SeriDuzenleKomutu { get; } public ICommand NumaraOnizleKomutu { get; }
    public Func<Sirket?, Task<Sirket?>>? FirmaFormuAc { get; set; }
    public Func<Guid, Sube?, Task<Sube?>>? SubeFormuAc { get; set; }
    public Func<Guid, MaliDonem?, Task<MaliDonem?>>? DonemFormuAc { get; set; }
    public Func<Guid, BelgeSerisi?, Task<BelgeSerisi?>>? SeriFormuAc { get; set; }
    public FirmaIslemleriGorunumModeli(ISirketServisi servis)
    {
        _servis = servis; YenileKomutu = new KomutSarici(async () => await YukleAsync()); YeniKomutu = new KomutSarici(async () => await FirmaAc(null));
        DuzenleKomutu = new KomutSarici(async p => await FirmaAc(p as Sirket ?? SeciliFirma), _ => FirmaSeciliMi); DurumKomutu = new KomutSarici(async p => await FirmaDurum((Sirket)p!));
        YeniSubeKomutu = new KomutSarici(async () => await SubeAc(null), () => FirmaSeciliMi); SubeDuzenleKomutu = new KomutSarici(async p => await SubeAc((Sube)p!)); SubeDurumKomutu = new KomutSarici(async p => await SubeDurum((Sube)p!));
        YeniDonemKomutu = new KomutSarici(async () => await DonemAc(null), () => FirmaSeciliMi); DonemDuzenleKomutu = new KomutSarici(async p => await DonemAc((MaliDonem)p!)); DonemKilitKomutu = new KomutSarici(async p => await DonemKilit((MaliDonem)p!));
        YeniSeriKomutu = new KomutSarici(async () => await SeriAc(null), () => FirmaSeciliMi); SeriDuzenleKomutu = new KomutSarici(async p => await SeriAc((BelgeSerisi)p!)); NumaraOnizleKomutu = new KomutSarici(async p => await NumaraUret((BelgeSerisi)p!)); _ = YukleAsync();
    }
    public async Task YukleAsync() { await Calistir(async () => { var id = SeciliFirma?.Id; var l = await _servis.ListeleAsync(Arama); Firmalar.Clear(); foreach (var x in l) Firmalar.Add(x); SeciliFirma = id.HasValue ? Firmalar.FirstOrDefault(x => x.Id == id) : Firmalar.FirstOrDefault(); Ozet(); }); }
    public Task FormAcAsync(Sirket? x) => FirmaAc(x);
    private async Task DetayYukleAsync() { Subeler.Clear(); MaliDonemler.Clear(); BelgeSerileri.Clear(); if (SeciliFirma == null) { Ozet(); return; } try { foreach (var x in await _servis.SubeleriGetirAsync(SeciliFirma.Id)) Subeler.Add(x); foreach (var x in await _servis.DonemleriGetirAsync(SeciliFirma.Id)) MaliDonemler.Add(x); foreach (var x in await _servis.BelgeSerileriniGetirAsync(SeciliFirma.Id)) BelgeSerileri.Add(x); Ozet(); } catch (Exception ex) { Mesaj = ex.Message; } }
    private async Task FirmaAc(Sirket? x) { if (FirmaFormuAc == null) return; var y = await FirmaFormuAc(x); if (y != null) await Calistir(async () => { var k = await _servis.KaydetAsync(y, string.IsNullOrWhiteSpace(y.SirketKodu)); Mesaj = $"Firma kaydedildi: {k.SirketKodu}"; await YukleAsync(); }); }
    private async Task SubeAc(Sube? x) { if (SeciliFirma == null || SubeFormuAc == null) return; var y = await SubeFormuAc(SeciliFirma.Id, x); if (y != null) await Calistir(async () => { await _servis.SubeKaydetAsync(y); Mesaj = "Şube kaydedildi."; await DetayYukleAsync(); }); }
    private async Task DonemAc(MaliDonem? x) { if (SeciliFirma == null || DonemFormuAc == null) return; var y = await DonemFormuAc(SeciliFirma.Id, x); if (y != null) await Calistir(async () => { await _servis.MaliDonemKaydetAsync(y); Mesaj = "Mali dönem kaydedildi."; await DetayYukleAsync(); }); }
    private async Task SeriAc(BelgeSerisi? x) { if (SeciliFirma == null || SeriFormuAc == null) return; var y = await SeriFormuAc(SeciliFirma.Id, x); if (y != null) await Calistir(async () => { await _servis.BelgeSerisiKaydetAsync(y); Mesaj = "Belge serisi kaydedildi."; await DetayYukleAsync(); }); }
    private async Task FirmaDurum(Sirket x) => await Calistir(async () => { await _servis.DurumDegistirAsync(x.Id, !x.AktifMi); Mesaj = "Firma durumu güncellendi."; await YukleAsync(); });
    private async Task SubeDurum(Sube x) => await Calistir(async () => { await _servis.SubeDurumDegistirAsync(x.Id, !x.AktifMi); Mesaj = "Şube durumu güncellendi."; await DetayYukleAsync(); });
    private async Task DonemKilit(MaliDonem x) => await Calistir(async () => { await _servis.MaliDonemKilitleAsync(x.Id, !x.KilitliMi); Mesaj = "Mali dönem kilidi güncellendi."; await DetayYukleAsync(); });
    private async Task NumaraUret(BelgeSerisi x) => await Calistir(async () => Mesaj = $"Yeni belge numarası: {await _servis.SonrakiBelgeNoAsync(x.Id)}");
    private async Task Calistir(Func<Task> f) { if (_calisiyor) return; _calisiyor = true; try { await f(); } catch (Exception ex) { Mesaj = ex.Message; } finally { _calisiyor = false; } }
    private void Ozet() { BildirimGonder(nameof(ToplamFirma)); BildirimGonder(nameof(AktifFirma)); BildirimGonder(nameof(PasifFirma)); BildirimGonder(nameof(ToplamSube)); BildirimGonder(nameof(AcikDonem)); }
    private void KomutlariGuncelle() { foreach (var x in new[] { DuzenleKomutu, YeniSubeKomutu, YeniDonemKomutu, YeniSeriKomutu }) (x as KomutSarici)?.BildirimGonder(); }
}
