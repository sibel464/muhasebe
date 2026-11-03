using EtkiAlani.Modeller.Stok;
using EtkiAlani.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.Yardimcilar;

// Stok ekranlarındaki barkod kutusu: önce okunanın tamamı aranır ("2X4" gibi ürün kodları çarpan sanılmasın),
// bulunmazsa "12*barkod" çarpanı denenir. Terazi etiketinde miktar kilodur.
public static class StokBarkodu
{
    public static async Task<(Urun Urun, decimal Miktar)?> BulAsync(IUrunServisi urunServisi, string okunan)
    {
        okunan = (okunan ?? "").Trim();
        if (okunan.Length == 0) return null;

        var sonuc = await urunServisi.BarkodlaBulAsync(okunan);
        if (sonuc != null) return (sonuc.Urun, sonuc.Miktar);

        var c = CarpanliOkuma.Coz(okunan);
        if (c.Carpan == 1 && c.Kod == okunan) return null;
        sonuc = await urunServisi.BarkodlaBulAsync(c.Kod);
        return sonuc == null ? null : (sonuc.Urun, sonuc.Miktar * c.Carpan);
    }
}
