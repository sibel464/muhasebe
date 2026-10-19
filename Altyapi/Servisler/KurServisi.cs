using Altyapi.Veri;
using EtkiAlani.Modeller.Guvenlik;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Doviz;

namespace Altyapi.Servisler;

public class KurServisi : IKurServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly ITcmbKurServisi _tcmbServisi;
    private readonly IKullaniciServisi _kullaniciServisi;

    public KurServisi(MuhasebeBaglami baglam, ITcmbKurServisi tcmbServisi, IKullaniciServisi kullaniciServisi)
    {
        _baglam = baglam;
        _tcmbServisi = tcmbServisi;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<DovizKuruDto>> GetirGunlukKurlarAsync(DateTime? tarih = null)
    {
        // Kur günü yerel takvim günüdür (UTC etiketli); gece 00-03 arası önceki güne düşmez, tür ne gelirse gelsin normalize edilir.
        var hedefTarih = DateTime.SpecifyKind((tarih ?? DateTime.Now).Date, DateTimeKind.Utc);

        // Fetch DB rates for target date first
        // Elle girilen kur her zaman TCMB kurunun önüne geçer (aynı türde en son girilen geçerli);
        // eskiden yalnız kayıt sırasına bakılıyordu, sonradan önbelleğe alınan TCMB kuru elle girileni eziyordu.
        var dbEntities = await _baglam.DovizKurlari
            .AsNoTracking()
            .Where(k => k.Tarih.Date == hedefTarih && !k.Silindi)
            .OrderBy(k => k.Kaynak == "MANUEL" ? 1 : 0)
            .ThenBy(k => k.Id)
            .ToListAsync();

        var dbKurlar = dbEntities.Select(MapToDto).ToList();

        // TCMB'ye ulaşılamazsa (internet yok) veritabanındaki kurlarla (elle girilen dahil) devam edilir.
        List<DovizKuruDto> tcmbKurlar;
        try
        {
            tcmbKurlar = await _tcmbServisi.GunlukKurlariGuncelleVeyaGetirAsync(hedefTarih);
        }
        catch (InvalidOperationException) when (dbKurlar.Count > 0)
        {
            tcmbKurlar = [];
        }

        // Merge: DB rates override TCMB fallback rates
        var dict = new Dictionary<string, DovizKuruDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in tcmbKurlar)
        {
            dict[k.ParaBirimiKodu] = k;
        }
        foreach (var m in dbKurlar.Where(m => m.Kaynak != "TCMB" || !dict.ContainsKey(m.ParaBirimiKodu)))
        {
            dict[m.ParaBirimiKodu] = m;
        }

        return dict.Values.ToList();
    }

    public async Task<DovizKuruDto?> GetirGecerliKurAsync(string paraBirimiKodu, DateTime? tarih = null)
    {
        var hedefTarih = DateTime.SpecifyKind((tarih ?? DateTime.Now).Date, DateTimeKind.Utc);
        if (string.IsNullOrWhiteSpace(paraBirimiKodu) || paraBirimiKodu.Equals("TRY", StringComparison.OrdinalIgnoreCase))
        {
            return new DovizKuruDto
            {
                ParaBirimiKodu = "TRY",
                Tarih = hedefTarih,
                KurTarihi = hedefTarih,
                DovizAlis = 1.0m,
                DovizSatis = 1.0m,
                EfektifAlis = 1.0m,
                EfektifSatis = 1.0m,
                Kaynak = "SİSTEM"
            };
        }

        var kurlar = await GetirGunlukKurlarAsync(tarih);
        return kurlar.FirstOrDefault(k => k.ParaBirimiKodu.Equals(paraBirimiKodu, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<decimal> GetirIslemKuruAsync(string paraBirimiKodu, DateTime? tarih, KurTipiPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(paraBirimiKodu) || paraBirimiKodu.Equals("TRY", StringComparison.OrdinalIgnoreCase))
            return 1.0m;

        var kur = await GetirGecerliKurAsync(paraBirimiKodu, tarih);
        if (kur == null)
            throw new InvalidOperationException($"[{paraBirimiKodu}] için geçerli kur bilgisi bulunamadı.");

        return policy switch
        {
            KurTipiPolicy.DovizAlis => kur.DovizAlis > 0 ? kur.DovizAlis : kur.DovizSatis,
            KurTipiPolicy.DovizSatis => kur.DovizSatis > 0 ? kur.DovizSatis : kur.DovizAlis,
            KurTipiPolicy.EfektifAlis => kur.EfektifAlis > 0 ? kur.EfektifAlis : kur.DovizAlis,
            KurTipiPolicy.EfektifSatis => kur.EfektifSatis > 0 ? kur.EfektifSatis : kur.DovizSatis,
            _ => kur.DovizAlis
        };
    }

    public async Task<DovizKuruDto> KaydetManuelKurAsync(ManuelKurGirisDto dto)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet || y.MuhasebeFisEkle, "Manuel Kur Girişi");

        if (string.IsNullOrWhiteSpace(dto.ParaBirimiKodu))
            throw new ArgumentException("Para birimi kodu boş olamaz.");

        if (dto.DovizAlis <= 0 || dto.DovizSatis <= 0)
            throw new ArgumentException("Döviz alış ve satış kurları pozitif olmalıdır.");

        var hedefTarih = DateTime.SpecifyKind(dto.Tarih.Date, DateTimeKind.Utc);
        var kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;
        var kullaniciAdi = _kullaniciServisi.AktifKullanici?.KullaniciAdi ?? "Sistem";

        var entity = new DovizKuru
        {
            ParaBirimiKodu = dto.ParaBirimiKodu.ToUpperInvariant(),
            Tarih = hedefTarih,
            KurTarihi = hedefTarih,
            DovizAlis = dto.DovizAlis,
            DovizSatis = dto.DovizSatis,
            EfektifAlis = dto.EfektifAlis > 0 ? dto.EfektifAlis : dto.DovizAlis,
            EfektifSatis = dto.EfektifSatis > 0 ? dto.EfektifSatis : dto.DovizSatis,
            Kaynak = "MANUEL",
            OlusturmaTarihi = DateTime.UtcNow,
            KullaniciId = kullaniciId,
            Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? $"Kullanıcı ({kullaniciAdi}) Manuel Kur Girişi" : dto.Aciklama
        };

        _baglam.DovizKurlari.Add(entity);

        // Audit log recording (Rule 13 audit)
        _baglam.AuditLoglar.Add(new AuditLog
        {
            TabloAdi = "DovizKuru",
            KayitId = entity.ParaBirimiKodu,
            IslemTuru = AuditIslemTuru.Ekleme,
            Tarih = DateTime.UtcNow,
            KullaniciAdi = kullaniciAdi,
            YeniDegerJson = System.Text.Json.JsonSerializer.Serialize(dto)
        });

        await _baglam.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<SayfaliSonuc<DovizKuruDto>> GetirKurGecmisiAsync(KurFiltreDto filtre)
    {
        var query = _baglam.DovizKurlari
            .AsNoTracking()
            .Where(k => !k.Silindi);

        if (!string.IsNullOrWhiteSpace(filtre.ParaBirimiKodu))
            query = query.Where(k => k.ParaBirimiKodu == filtre.ParaBirimiKodu.ToUpperInvariant());

        if (filtre.BaslangicTarihi.HasValue)
            query = query.Where(k => k.Tarih >= filtre.BaslangicTarihi.Value.Date);

        if (filtre.BitisTarihi.HasValue)
            query = query.Where(k => k.Tarih <= filtre.BitisTarihi.Value.Date);

        if (!string.IsNullOrWhiteSpace(filtre.Kaynak))
            query = query.Where(k => k.Kaynak == filtre.Kaynak);

        var toplam = await query.CountAsync();
        var sayfa = Math.Max(1, filtre.Sayfa);
        var boyutu = Math.Clamp(filtre.SayfaBoyutu, 10, 500);

        var entities = await query
            .OrderByDescending(k => k.Tarih)
            .ThenBy(k => k.ParaBirimiKodu)
            .Skip((sayfa - 1) * boyutu)
            .Take(boyutu)
            .ToListAsync();

        var liste = entities.Select(MapToDto).ToList();

        return new SayfaliSonuc<DovizKuruDto>
        {
            Veri = liste,
            ToplamKayit = toplam,
            MevcutSayfa = sayfa,
            SayfaBoyutu = boyutu
        };
    }

    private static DovizKuruDto MapToDto(DovizKuru entity)
    {
        return new DovizKuruDto
        {
            Id = entity.Id,
            ParaBirimiKodu = entity.ParaBirimiKodu,
            Tarih = entity.Tarih,
            KurTarihi = entity.KurTarihi,
            DovizAlis = entity.DovizAlis,
            DovizSatis = entity.DovizSatis,
            EfektifAlis = entity.EfektifAlis,
            EfektifSatis = entity.EfektifSatis,
            Kaynak = entity.Kaynak,
            OlusturmaTarihi = entity.OlusturmaTarihi,
            KullaniciId = entity.KullaniciId,
            Aciklama = entity.Aciklama
        };
    }
}
