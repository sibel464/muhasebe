using Uygulama.Dto.Finans360;

namespace Uygulama.Arayuzler;

public interface IBankaEkstreServisi
{
    EkstreOkumaSonucuDto Oku(string dosyaYolu);
    Task<List<EkstreEslesmeDto>> EslestirAsync(int bankaHesabiId, IReadOnlyList<EkstreSatiriDto> satirlar);
    Task<EkstreKayitSonucuDto> KaydetAsync(int bankaHesabiId, IReadOnlyList<EkstreEslesmeDto> secimler);
}
