using EtkiAlani.Modeller.Finans;

namespace Uygulama.Dto.Finans360;

public sealed class FinansDashboardOzetDto
{
    public decimal ToplamKasa { get; set; }
    public decimal ToplamBanka { get; set; }
    public decimal BugunkiTahsilat { get; set; }
    public decimal BugunkiOdeme { get; set; }
    public decimal BeklenenTahsilat { get; set; }
    public decimal BeklenenOdeme { get; set; }
    public decimal VadesiGecenAlacak { get; set; }
    public decimal VadesiGecenBorc { get; set; }
    public List<KasaOzetDto> KasaDagilimi { get; set; } = new();
    public List<BankaHesapOzetDto> BankaHesapBakiyeleri { get; set; } = new();
    public List<YuruyenFinansSatirDto> SonFinansalHareketler { get; set; } = new();
    public List<VadeKalemOzetDto> YaklasanVadeler { get; set; } = new();
    public List<VadeKalemOzetDto> VadesiGecenler { get; set; } = new();
    public CekSenetOzetDto CekSenetDurumu { get; set; } = null!;
}

public sealed record CekSenetOzetDto(
    int PortfoydekiAlinanCekSayisi,
    decimal PortfoydekiAlinanCekTutar,
    int PortfoydekiVerilenCekSayisi,
    decimal PortfoydekiVerilenCekTutar,
    int PortfoydekiAlinanSenetSayisi,
    decimal PortfoydekiAlinanSenetTutar,
    int PortfoydekiVerilenSenetSayisi,
    decimal PortfoydekiVerilenSenetTutar
);

public sealed record KasaOzetDto(
    int Id,
    string KasaKodu,
    string KasaAdi,
    string ParaBirimi,
    string SubeAdi,
    decimal MevcutBakiye,
    decimal BugunkiGiris,
    decimal BugunkiCikis,
    DateTime? SonIslemTarihi,
    bool Aktif
);

public sealed record BankaHesapOzetDto(
    int Id,
    string BankaAdi,
    string HesapKodu,
    string HesapAdi,
    string Iban,
    string HesapNo,
    string Sube,
    string ParaBirimi,
    decimal Bakiye,
    decimal KullanilabilirBakiye,
    string Aciklama,
    bool Aktif
);

public sealed record YuruyenFinansSatirDto(
    int Id,
    string Kaynak,
    DateTime Tarih,
    string Saat,
    string IslemNo,
    string IslemTuru,
    int? CariKartId,
    string CariKod,
    string CariUnvan,
    string KaynakAdi,
    string HedefAdi,
    string BelgeNo,
    string Aciklama,
    decimal Giris,
    decimal Cikis,
    decimal Bakiye,
    decimal Tutar,
    string ParaBirimi,
    string KullaniciAdi,
    string Durum
);

public sealed record PosIslemOzetDto(
    int Id,
    string BankaAdi,
    string PosCihazi,
    string KartTuru,
    int TaksitSayisi,
    decimal BrutTutar,
    decimal KomisyonOrani,
    decimal KomisyonTutari,
    decimal NetTutar,
    DateTime IslemTarihi,
    DateTime BeklenenVadeTarihi,
    DateTime? AktarimTarihi,
    PosIslemDurumu Durum
);

public sealed record VadeKalemOzetDto(
    int Id,
    string BelgeTuru,
    string YonetimTuru,
    int? CariKartId,
    string CariKod,
    string CariUnvan,
    string BelgeNo,
    DateTime VadeTarihi,
    decimal Tutar,
    decimal KalanTutar,
    int GecikmeGunSayisi,
    string Durum
);

public sealed record KasaMutabakatKayitDto(
    int KasaId,
    decimal SayimMiktari,
    string Aciklama,
    bool DuzeltmeHareketOlustur = false
);

public sealed record KasaBankaTransferIstegiDto(
    int KasaId,
    int BankaHesapId,
    decimal Tutar,
    string Aciklama
);

public sealed record BankaKasaTransferIstegiDto(
    int BankaHesapId,
    int KasaId,
    decimal Tutar,
    string Aciklama
);

public sealed record BankaVirmanIstegiDto(
    int KaynakBankaHesapId,
    int HedefBankaHesapId,
    decimal Tutar,
    decimal Masraf,
    string Aciklama
);

public sealed class GlobalFinansFiltreDto
{
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public int? CariKartId { get; set; }
    public int? KasaId { get; set; }
    public int? BankaHesapId { get; set; }
    public string? IslemTuru { get; set; }
    public string? Durum { get; set; }
    public int SayfaNo { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 50;
}

public sealed record SayfaliFinansHareketDto(
    List<YuruyenFinansSatirDto> Items,
    int ToplamKayitSayisi,
    int SayfaNo,
    int SayfaBoyutu
);
