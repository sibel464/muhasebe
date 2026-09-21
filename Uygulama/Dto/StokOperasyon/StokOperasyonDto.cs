using EtkiAlani.Modeller.Stok;

namespace Uygulama.Dto.StokOperasyon;

public sealed record StokGirisIstegi(
    int UrunId,
    int DepoId,
    decimal Miktar,
    decimal BirimFiyat,
    DateTime Tarih,
    string IslemNedeni,
    string? ReferansNo,
    string? Aciklama
);

public sealed record StokCikisIstegi(
    int UrunId,
    int DepoId,
    decimal Miktar,
    decimal BirimFiyat,
    DateTime Tarih,
    string IslemNedeni,
    string? ReferansNo,
    string? Aciklama
);

public sealed record StokTransferIstegi(
    int KaynakDepoId,
    int HedefDepoId,
    int UrunId,
    decimal Miktar,
    decimal BirimFiyat,
    DateTime Tarih,
    string? TransferNo,
    string? Aciklama
);

public sealed record StokSayimIstegi(
    int DepoId,
    DateTime Tarih,
    string? Aciklama,
    List<StokSayimKalemIstegi> Kalemler
);

public sealed record StokSayimKalemIstegi(
    int UrunId,
    decimal SayilanMiktar
);

public sealed record StokOperasyonMatrisOzetDto(
    int UrunId,
    string UrunKodu,
    string UrunAdi,
    string? Barkod,
    string? Kategori,
    string? Marka,
    string Birim,
    decimal MagazaFiziksel,
    decimal MagazaRezerve,
    decimal MagazaKullanilabilir,
    decimal Depo1Fiziksel,
    decimal Depo1Rezerve,
    decimal Depo1Kullanilabilir,
    decimal Depo2Fiziksel,
    decimal Depo2Rezerve,
    decimal Depo2Kullanilabilir,
    decimal ToplamFiziksel,
    decimal ToplamRezerve,
    decimal ToplamKullanilabilir,
    decimal MinimumStok,
    decimal AlisFiyati,
    decimal SatisFiyati,
    string StokDurumu,
    bool Aktif
);
