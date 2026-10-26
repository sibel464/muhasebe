using Uygulama.Dto.Rapor;

namespace Uygulama.Arayuzler;

public interface IExcelExportServisi
{
    byte[] ExportToExcel<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari);
    byte[] ExcelOlustur(RaporTablosu tablo);
}
