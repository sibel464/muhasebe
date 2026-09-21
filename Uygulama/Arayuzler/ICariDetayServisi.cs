using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Uygulama.Dto.Cari360;

namespace Uygulama.Arayuzler;

public interface ICariDetayServisi
{
    Task<List<CariUrunAnalizDto>> GetUrunSayfasiAsync(int cariId, Guid sirketId, bool alis, CariSorgu sorgu, CancellationToken cancellationToken = default);
    Task<List<CariParaSecimi>> GetParaBirimleriAsync(int cariId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<CariSayfaDto> GetSayfaAsync(int cariId, Guid sirketId, CariSekme sekme, CariSorgu sorgu, CancellationToken cancellationToken = default);
    Task<CariRiskDto> GetRiskAsync(int cariId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<CariBelgeDetayDto> GetBelgeAsync(int cariId, Guid sirketId, string kaynak, int belgeId, CancellationToken cancellationToken = default);
    Task NotEkleAsync(int cariId, Guid sirketId, string baslik, string metin, CancellationToken cancellationToken = default);
    Task<CariDetayDto?> GetCariGenelBilgiAsync(int cariId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<CariFinansOzetDto> GetCariFinansOzetAsync(int cariId, Guid sirketId, CancellationToken cancellationToken = default);
    
    // Filtreleme parametreleri eklenebilir, şimdilik basit liste dönüyoruz.
    Task<List<CariHareketDto>> GetCariHareketlerAsync(int cariId, Guid sirketId, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
    Task<List<CariFaturaDto>> GetCariFaturalarAsync(int cariId, Guid sirketId, string? tur = null, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
    Task<List<CariSiparisDto>> GetCariSiparislerAsync(int cariId, Guid sirketId, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
    Task<List<CariIrsaliyeDto>> GetCariIrsaliyelerAsync(int cariId, Guid sirketId, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
    Task<List<CariTahsilatOdemeDto>> GetCariTahsilatOdemelerAsync(int cariId, Guid sirketId, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
    Task<List<CariIadeDto>> GetCariIadelerAsync(int cariId, Guid sirketId, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
    Task<List<CariCekSenetDto>> GetCariCekSenetlerAsync(int cariId, Guid sirketId, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
    Task<List<CariUrunAnalizDto>> GetCariUrunAnaliziAsync(int cariId, Guid sirketId, bool iptalleriGoster = false, CancellationToken cancellationToken = default);
}
