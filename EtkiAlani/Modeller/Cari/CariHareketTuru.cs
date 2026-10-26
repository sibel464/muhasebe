namespace EtkiAlani.Modeller.Cari;

public enum CariHareketTuru
{
    Devir,
    SatisFaturasi,
    AlisFaturasi,
    Tahsilat,
    Odeme,
    SatisIadesi,
    AlisIadesi,
    Diger
}

public static class CariHareketTuruAdlari
{
    public static string Ad(this CariHareketTuru tur) => tur switch
    {
        CariHareketTuru.Devir => "Devir Bakiyesi",
        CariHareketTuru.SatisFaturasi => "Satış Faturası",
        CariHareketTuru.AlisFaturasi => "Alış Faturası",
        CariHareketTuru.Tahsilat => "Tahsilat",
        CariHareketTuru.Odeme => "Ödeme",
        CariHareketTuru.SatisIadesi => "Satış İadesi",
        CariHareketTuru.AlisIadesi => "Alış İadesi",
        CariHareketTuru.Diger => "Virman / Düzeltme",
        _ => "Diğer"
    };
}
