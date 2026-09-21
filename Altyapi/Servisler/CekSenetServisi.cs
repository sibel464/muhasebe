using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public class CekSenetServisi : ICekSenetServisi
{
    private readonly MuhasebeBaglami _baglam;

    public CekSenetServisi(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
    }

    public async Task<CekSenet> EkleAsync(CekSenetEkleTalebi talep)
    {
        if (talep.Tutar <= 0) throw new ArgumentException("Tutar sıfırdan büyük olmalıdır.");
        
        var cari = await _baglam.CariKartlar.FindAsync(talep.CariKartId) 
            ?? throw new KeyNotFoundException("Cari kart bulunamadı.");

        var cekSenet = new CekSenet
        {
            Tur = talep.Tur,
            Durum = CekSenetDurumu.Portfoyde,
            BelgeNo = talep.BelgeNo ?? string.Empty,
            VadeTarihi = DateTime.SpecifyKind(talep.VadeTarihi, DateTimeKind.Utc),
            DuzenlemeTarihi = DateTime.SpecifyKind(talep.DuzenlemeTarihi, DateTimeKind.Utc),
            Tutar = talep.Tutar,
            CariKartId = cari.Id,
            Kesideci = talep.Kesideci ?? string.Empty,
            BankaAdi = talep.BankaAdi,
            SubeAdi = talep.SubeAdi,
            HesapNo = talep.HesapNo,
            Aciklama = talep.Aciklama ?? string.Empty,
            ParaBirimiId = talep.ParaBirimiId,
            Kur = talep.Kur,
            DovizTutar = talep.DovizTutar
        };

        _baglam.CekSenetler.Add(cekSenet);
        await _baglam.SaveChangesAsync();

        return cekSenet;
    }

    public async Task<IReadOnlyList<CekSenet>> TumunuGetirAsync(CekSenetTuru? tur = null, CekSenetDurumu? durum = null)
    {
        var sorgu = _baglam.CekSenetler.Include(x => x.CariKart).AsQueryable();

        if (tur.HasValue) sorgu = sorgu.Where(x => x.Tur == tur.Value);
        if (durum.HasValue) sorgu = sorgu.Where(x => x.Durum == durum.Value);

        return await sorgu.OrderBy(x => x.VadeTarihi).ToListAsync();
    }

    public async Task DurumGuncelleAsync(int cekSenetId, CekSenetDurumu yeniDurum, int? kasaId = null, int? bankaHesabiId = null, string? aciklama = null)
    {
        var cek = await _baglam.CekSenetler.Include(x => x.CariKart).FirstOrDefaultAsync(x => x.Id == cekSenetId)
            ?? throw new KeyNotFoundException("Çek/Senet bulunamadı.");

        if (cek.Durum == yeniDurum) return;

        cek.Durum = yeniDurum;
        var detayAciklama = string.IsNullOrWhiteSpace(aciklama) ? $"{cek.Tur} - Durum Değişikliği: {yeniDurum}" : aciklama;

        // Finansal Giriş / Çıkış Hareketi Oluşturma
        if (yeniDurum == CekSenetDurumu.TahsilEdildi)
        {
            if (kasaId.HasValue)
            {
                _baglam.KasaHareketleri.Add(new KasaHareketi
                {
                    KasaId = kasaId.Value,
                    Tarih = DateTime.UtcNow,
                    Tur = HareketTuru.Gelir,
                    Tutar = cek.Tutar,
                    Aciklama = $"Çek/Senet Tahsilatı (Belge No: {cek.BelgeNo}) - {detayAciklama}",
                    CariKartId = cek.CariKartId,
                    Belge = cek.BelgeNo
                });
            }
            else if (bankaHesabiId.HasValue)
            {
                _baglam.BankaHareketleri.Add(new BankaHareketi
                {
                    BankaHesabiId = bankaHesabiId.Value,
                    Tarih = DateTime.UtcNow,
                    Tur = HareketTuru.Gelir,
                    Tutar = cek.Tutar,
                    Aciklama = $"Çek/Senet Tahsilatı (Belge No: {cek.BelgeNo}) - {detayAciklama}",
                    CariKartId = cek.CariKartId,
                    OdemeYontemi = OdemeYontemi.Cek
                });
            }
        }
        else if (yeniDurum == CekSenetDurumu.Odendi)
        {
            if (kasaId.HasValue)
            {
                _baglam.KasaHareketleri.Add(new KasaHareketi
                {
                    KasaId = kasaId.Value,
                    Tarih = DateTime.UtcNow,
                    Tur = HareketTuru.Gider,
                    Tutar = cek.Tutar,
                    Aciklama = $"Verilen Çek/Senet Ödemesi (Belge No: {cek.BelgeNo}) - {detayAciklama}",
                    CariKartId = cek.CariKartId,
                    Belge = cek.BelgeNo
                });
            }
            else if (bankaHesabiId.HasValue)
            {
                _baglam.BankaHareketleri.Add(new BankaHareketi
                {
                    BankaHesabiId = bankaHesabiId.Value,
                    Tarih = DateTime.UtcNow,
                    Tur = HareketTuru.Gider,
                    Tutar = cek.Tutar,
                    Aciklama = $"Verilen Çek/Senet Ödemesi (Belge No: {cek.BelgeNo}) - {detayAciklama}",
                    CariKartId = cek.CariKartId,
                    OdemeYontemi = OdemeYontemi.Cek
                });
            }
        }

        await _baglam.SaveChangesAsync();
    }

    public async Task SilAsync(int id, string neden)
    {
        var cek = await _baglam.CekSenetler.FindAsync(id) ?? throw new KeyNotFoundException("Kayıt bulunamadı.");
        
        cek.SilindiMi = true;
        cek.SilmeNedeni = neden;
        
        await _baglam.SaveChangesAsync();
    }
}
