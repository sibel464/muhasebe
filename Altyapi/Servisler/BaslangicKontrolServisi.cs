using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

// Yeni müşteri programı kurduğunda neyi tanımlaması gerektiğini bilmiyordu: firma künyesi boşken fatura basılıyor,
// POS hesabı yokken kartlı satış ancak denendiğinde reddediliyordu. Ana sayfadaki "Başlarken" listesi buradan dolar.
public sealed class BaslangicKontrolServisi : IBaslangicKontrolServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IAktifFirmaBaglami? _aktifFirma;
    private readonly IVeritabaniYedekServisi? _yedek;

    public BaslangicKontrolServisi(MuhasebeBaglami baglam, IAktifFirmaBaglami? aktifFirma = null, IVeritabaniYedekServisi? yedek = null)
    {
        _baglam = baglam;
        _aktifFirma = aktifFirma;
        _yedek = yedek;
    }

    public async Task<IReadOnlyList<BaslangicAdimi>> AdimlariGetirAsync(CancellationToken cancellationToken = default)
    {
        var sirketId = _aktifFirma?.SirketId is Guid g && g != Guid.Empty ? g : MuhasebeBaglami.VarsayilanSirketId;
        var ayar = await _baglam.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var sirket = await _baglam.Sirketler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sirketId, cancellationToken);
        var firma = BelgeYazdirmaServisi.FirmaBilgisiOlustur(ayar, sirket, sirketId == MuhasebeBaglami.VarsayilanSirketId);
        var vkn = new string(firma.VergiNo.Where(char.IsDigit).ToArray());
        var firmaTamam = firma.FirmaUnvani.Length > 0 && firma.FirmaUnvani != "Varsayılan Şirket"
            && vkn.Length is 10 or 11 && firma.VergiDairesi.Length > 0 && firma.Adres.Length > 0 && firma.Il.Length > 0 && firma.Ilce.Length > 0;

        var hesaplar = await _baglam.BankaHesaplari.AsNoTracking().Where(b => b.AktifMi && b.SirketId == sirketId)
            .Select(b => b.HesapTuru).Distinct().ToListAsync(cancellationToken);

        var adimlar = new List<BaslangicAdimi>
        {
            new("Firma bilgilerinizi girin",
                "Unvan, vergi dairesi, vergi no ve adres faturalarda basılır; e-Fatura için zorunludur.", "Ayarlar", firmaTamam),
            new("İlk müşteri veya tedarikçinizi ekleyin", "Veresiye satış, alış faturası ve tahsilat cari kart ister.", "CariEkle",
                await _baglam.CariKartlar.AnyAsync(c => c.SirketId == sirketId, cancellationToken)),
            new("İlk ürününüzü ekleyin", "Satış ve stok takibi ürün kartıyla yapılır. Excel'den toplu aktarım da yapabilirsiniz.", "UrunEkle",
                await _baglam.Urunler.AnyAsync(u => u.SirketId == sirketId, cancellationToken)),
            new("Kredi kartı satışları için POS hesabı tanımlayın",
                "Kartla alınan para bu hesaba yazılır; tanımlı değilse kartlı satış yapılamaz.", "Banka", hesaplar.Contains(BankaHesapTuru.POSHesabi)),
            new("Havale/EFT için banka hesabınızı tanımlayın",
                "Havaleyle alınan ödemeler vadesiz TL hesabınıza yazılır.", "Banka", hesaplar.Contains(BankaHesapTuru.VadesizTL))
        };
        if (_yedek != null)
            adimlar.Add(new("Yedeklerin ikinci kopyası için konum seçin",
                "Harici disk, USB bellek veya ağ klasörü: bilgisayar bozulursa verileriniz kaybolmaz.", "Ayarlar",
                !string.IsNullOrWhiteSpace(_yedek.IkinciYedekKlasoru)));
        return adimlar;
    }
}
