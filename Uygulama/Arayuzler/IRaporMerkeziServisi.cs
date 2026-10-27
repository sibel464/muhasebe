using Uygulama.Dto.Rapor;

namespace Uygulama.Arayuzler;

public interface IRaporMerkeziServisi
{
    Task<YoneticiOzetDto> GetYoneticiOzetiAsync(SatisRaporFiltreDto filtre);
    Task<DonemKarsilastirmaResultDto> GetDonemKarsilastirmaAsync(SatisRaporFiltreDto filtre);
    Task<PagedResult<SatisRaporuKalemiDto>> GetSatisRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<TopUrunRaporKalemiDto>> GetEnCokSatanUrunlerAsync(SatisRaporFiltreDto filtre, int topCount = 10);
    Task<List<TopUrunRaporKalemiDto>> GetEnKarliUrunlerAsync(SatisRaporFiltreDto filtre, bool enDusukKar = false, int topCount = 10);
    Task<PagedResult<AlisRaporuKalemiDto>> GetAlisRaporuAsync(SatisRaporFiltreDto filtre);
    Task<PagedResult<CariRaporuKalemiDto>> GetCariRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<CariYaslandirmaRaporuKalemiDto>> GetCariYaslandirmaAsync(SatisRaporFiltreDto filtre);
    Task<List<VadesiGecmisAlacakDto>> GetVadesiGecmisAlacaklarAsync(SatisRaporFiltreDto filtre, int enAzGecikmeGun = 1);
    Task<List<TahsilatPerformansiKalemiDto>> GetTahsilatPerformansiAsync(SatisRaporFiltreDto filtre);
    Task<PagedResult<StokRaporuKalemiDto>> GetStokRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<HareketsizUrunKalemiDto>> GetHareketsizUrunlerAsync(SatisRaporFiltreDto filtre, int gunFilter = 30);
    Task<List<StokDegerRaporuKalemiDto>> GetStokDegerRaporuAsync(SatisRaporFiltreDto filtre);
    Task<PagedResult<FinansRaporuKalemiDto>> GetFinansRaporuAsync(SatisRaporFiltreDto filtre);
    Task<NakitAkisRaporuDto> GetNakitAkisRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<KasaBankaRaporKalemiDto>> GetKasaBankaRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<PosRaporuKalemiDto>> GetPosRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<CekSenetRaporuKalemiDto>> GetCekSenetRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<KdvRaporuKalemiDto>> GetKdvRaporuAsync(SatisRaporFiltreDto filtre);
    Task<List<BaBsKalemiDto>> GetBaBsRaporuAsync(SatisRaporFiltreDto filtre, decimal sinir = 5000m);
    Task<List<RaporFavoriDto>> GetFavoriFiltrelerAsync(int sirketId);
    Task FavoriFiltreKaydetAsync(int sirketId, string raporAd, SatisRaporFiltreDto filtre, string kullaniciAd);
    Task BenchmarkPerformanceAsync(SatisRaporFiltreDto filtre);
}
