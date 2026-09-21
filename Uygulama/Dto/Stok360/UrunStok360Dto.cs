using EtkiAlani.Modeller.Stok;

namespace Uygulama.Dto.Stok360;

public sealed record UrunStokOzetDto(
    int UrunId,
    string Kod,
    string Ad,
    string? Barkod,
    string? Kategori,
    string? Marka,
    string Birim,
    decimal AlisFiyati,
    decimal PerakendeSatisFiyati,
    decimal ToptanSatisFiyati,
    decimal KdvOrani,
    decimal ToplamFizikselStok,
    decimal ToplamRezerveStok,
    decimal ToplamKullanilabilirStok,
    decimal MinimumStok,
    bool Aktif,
    string StokDurumu, // Normal, Kritik, StokYok
    DateTime? SonAlisTarihi,
    DateTime? SonSatisTarihi
);

public sealed record LokasyonStokOzetDto(
    int DepoId,
    string DepoKodu,
    string DepoAdi,
    string DepoTuru,
    decimal FizikselStok,
    decimal RezerveStok,
    decimal KullanilabilirStok,
    decimal MinimumStok,
    string Durum,
    DateTime? SonHareketTarihi
);

public sealed record YuruyenStokSatirDto(
    int Id,
    DateTime Tarih,
    string FisNo,
    string BelgeNo,
    string HareketTuruText,
    StokHareketTuru HareketTuru,
    StokIslemTuru IslemTuru,
    string IslemTuruText,
    string DepoAdi,
    string HedefDepoAdi,
    string CariUnvan,
    decimal GirisMiktar,
    decimal CikisMiktar,
    decimal YuruyenBakiye,
    decimal BirimFiyat,
    decimal ToplamTutar,
    string Aciklama,
    bool IptalEdildi,
    string Kullanici,
    int? IlgiliBelgeId
);

public sealed record UrunSatisGecmisDto(
    int FaturaId,
    DateTime Tarih,
    string FaturaNo,
    string CariUnvan,
    decimal Miktar,
    decimal BirimFiyat,
    decimal IskontoOrani,
    decimal NetFiyat,
    decimal KdvTutari,
    decimal ToplamTutar,
    string DepoAdi
);

public sealed record UrunAlisGecmisDto(
    int FaturaId,
    DateTime Tarih,
    string FaturaNo,
    string TedarikciUnvan,
    decimal Miktar,
    decimal BirimFiyat,
    decimal IskontoOrani,
    decimal NetFiyat,
    decimal KdvTutari,
    decimal ToplamTutar,
    string DepoAdi
);

public sealed record UrunSiparisRezervasyonDto(
    int SiparisId,
    DateTime Tarih,
    string SiparisNo,
    string CariUnvan,
    decimal SiparisMiktari,
    decimal RezerveMiktari,
    decimal KalanSevkMiktari,
    DateTime RezerveTarihi,
    string Durum,
    string DepoAdi
);

public sealed record UrunMaliyetKarlilikDto(
    decimal SonAlisMaliyeti,
    decimal AgirlikliOrtalamaMaliyet,
    decimal ToplamSatilanMiktar,
    decimal ToplamSatisCirosu,
    decimal ToplamSatisMaliyeti,
    decimal BrutKar,
    decimal BrutKarYuzdesi
);

public sealed record UrunStok360Sorgu(
    DateTime? Baslangic = null,
    DateTime? Bitis = null,
    int? DepoId = null,
    StokHareketTuru? HareketTuru = null,
    StokIslemTuru? IslemTuru = null,
    string? BelgeNo = null,
    int? CariKartId = null,
    bool IptalleriGoster = false,
    int Sayfa = 1,
    int SayfaBoyutu = 50
);
