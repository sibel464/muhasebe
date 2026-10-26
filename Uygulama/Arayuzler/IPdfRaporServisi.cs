using Uygulama.Dto.Rapor;

namespace Uygulama.Arayuzler;

public interface IPdfRaporServisi
{
    byte[] ExportToPdf<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari);
    string RaporHtmlOlustur<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari);
    byte[] PdfOlustur(RaporTablosu tablo);
    string HtmlOlustur(RaporTablosu tablo);
}
