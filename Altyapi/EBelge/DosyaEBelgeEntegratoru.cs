using System.Text;
using Uygulama.Arayuzler;

namespace Altyapi.EBelge;

// Entegratör bağlantısı kurulmamışken kullanılan mod: UBL-TR XML dosyası klasöre yazılır, kullanıcı bunu
// özel entegratörün web portalına (ya da e-Arşiv için GİB portalına) yükler ve sonra durumu programda işaretler.
public sealed class DosyaEBelgeEntegratoru(Func<string> klasorSaglayici) : IEBelgeEntegratoru
{
    public string Ad => "Dosya (portala elle yükleme)";
    public bool DogrudanGonderir => false;

    public static string VarsayilanKlasor =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MuhasebeYonetim", "e-Belge");

    public async Task<EBelgeGonderimSonucu> GonderAsync(EBelgePaketi paket, CancellationToken cancellationToken = default)
    {
        var klasor = klasorSaglayici();
        if (string.IsNullOrWhiteSpace(klasor))
            klasor = VarsayilanKlasor;
        Directory.CreateDirectory(klasor);
        var yol = Path.Combine(klasor, $"{paket.FaturaNo}_{paket.Ettn.ToString().ToUpperInvariant()}.xml");
        await File.WriteAllTextAsync(yol, paket.Xml, new UTF8Encoding(false), cancellationToken);
        return new EBelgeGonderimSonucu(true, EtkiAlani.Modeller.Fatura.EBelgeDurumu.Hazirlandi,
            "XML dosyası oluşturuldu. Entegratör portalına yükledikten sonra durumu 'Gönderildi' olarak işaretleyin.", yol);
    }
}
