using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public class FinancialTransactionService : IFinancialTransactionService
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public FinancialTransactionService(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<YevmiyeFis> CreateFromInvoiceAsync(int faturaId)
    {
        YetkiyiDogrula();
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOnayla, "Fatura Finansal İşlem Oluşturma");

        var transactionSahibi = _baglam.Database.CurrentTransaction == null;
        await using var transaction = transactionSahibi
            ? await _baglam.Database.BeginTransactionAsync()
            : null;
        try
        {
            var fatura = await _baglam.Faturalar
                .Include(f => f.Kalemler)
                .ThenInclude(k => k.StokDagilimlari)
                .FirstOrDefaultAsync(f => f.Id == faturaId);

            if (fatura == null)
                throw new KeyNotFoundException("Finansal işlem oluşturulacak fatura bulunamadı.");

            if (fatura.Durum != BelgeDurumu.Taslak && !await FinansalFisVarMiAsync(fatura))
                throw new InvalidOperationException("Yalnızca taslak faturalar finansal işleme alınabilir.");

            if (!fatura.SirketId.HasValue)
                throw new InvalidOperationException("Faturanın şirket bağlantısı bulunamadı.");

            if (fatura.Kalemler.Count == 0)
                throw new InvalidOperationException("Faturada en az bir kalem bulunmalıdır.");

            if (fatura.Tur is not (FaturaTuru.Satis or FaturaTuru.Alis or FaturaTuru.SatisIade or FaturaTuru.AlisIade))
                throw new InvalidOperationException("Desteklenmeyen fatura türü.");

            var sirketId = fatura.SirketId.Value;
            var mevcutFis = await _baglam.YevmiyeFisleri
                .FirstOrDefaultAsync(f => f.SirketId == sirketId && f.BelgeTipi == "Fatura" && f.BelgeId == fatura.Id);
            if (mevcutFis != null)
                throw new InvalidOperationException("Bu fatura için daha önce yevmiye fişi oluşturulmuştur.");

            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == fatura.CariKartId);
            if (cari == null || !cari.Aktif)
                throw new InvalidOperationException("Faturanın aktif cari kartı bulunamadı.");

            var urunIdleri = fatura.Kalemler.Select(k => k.UrunId).Distinct().ToList();
            var urunler = await _baglam.Urunler
                .Where(u => urunIdleri.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);
            if (urunler.Count != urunIdleri.Count || urunler.Values.Any(u => !u.Aktif))
                throw new InvalidOperationException("Faturadaki ürünlerden biri bulunamadı veya aktif değil.");

            var hesapKodlari = new[] { "120", "153", "191", "320", "391", "600" };
            var tumHesaplar = await _baglam.HesapPlanlari
                .Where(h => (h.SirketId == sirketId || h.SirketId == MuhasebeBaglami.VarsayilanSirketId) && hesapKodlari.Contains(h.HesapKodu) && h.AktifMi)
                .ToListAsync();

            var hesaplar = new Dictionary<string, HesapPlani>();
            foreach (var kod in hesapKodlari)
            {
                var hesap = tumHesaplar.FirstOrDefault(h => h.SirketId == sirketId && h.HesapKodu == kod)
                         ?? tumHesaplar.FirstOrDefault(h => h.SirketId == MuhasebeBaglami.VarsayilanSirketId && h.HesapKodu == kod);
                if (hesap == null)
                    throw new InvalidOperationException($"{kod} kodlu hesap planı hesabı bulunamadı.");
                hesaplar[kod] = hesap;
            }

            var matrah = fatura.GenelToplam - fatura.KdvToplam;
            if (matrah < 0 || fatura.KdvToplam < 0 || fatura.GenelToplam <= 0)
                throw new InvalidOperationException("Fatura toplamları geçersiz.");

            var yevmiyeSatirlari = fatura.Tur switch
            {
                FaturaTuru.Satis => new List<YevmiyeSatiri>
                {
                    SatirOlustur(hesaplar["120"], fatura.GenelToplam, 0, fatura.Aciklama),
                    SatirOlustur(hesaplar["600"], 0, matrah, fatura.Aciklama),
                    SatirOlustur(hesaplar["391"], 0, fatura.KdvToplam, fatura.Aciklama)
                }
                , FaturaTuru.Alis => new List<YevmiyeSatiri>
                {
                    SatirOlustur(hesaplar["153"], matrah, 0, fatura.Aciklama),
                    SatirOlustur(hesaplar["191"], fatura.KdvToplam, 0, fatura.Aciklama),
                    SatirOlustur(hesaplar["320"], 0, fatura.GenelToplam, fatura.Aciklama)
                }
                , FaturaTuru.SatisIade => new List<YevmiyeSatiri>
                {
                    SatirOlustur(hesaplar["600"], matrah, 0, fatura.Aciklama),
                    SatirOlustur(hesaplar["391"], fatura.KdvToplam, 0, fatura.Aciklama),
                    SatirOlustur(hesaplar["120"], 0, fatura.GenelToplam, fatura.Aciklama)
                }
                , FaturaTuru.AlisIade => new List<YevmiyeSatiri>
                {
                    SatirOlustur(hesaplar["320"], fatura.GenelToplam, 0, fatura.Aciklama),
                    SatirOlustur(hesaplar["153"], 0, matrah, fatura.Aciklama),
                    SatirOlustur(hesaplar["191"], 0, fatura.KdvToplam, fatura.Aciklama)
                }
                , _ => throw new InvalidOperationException("Desteklenmeyen fatura türü.")
            };

            if (fatura.KdvToplam == 0)
                yevmiyeSatirlari.RemoveAll(s => s.Borc == 0 && s.Alacak == 0);

            var toplamBorc = yevmiyeSatirlari.Sum(s => s.Borc);
            var toplamAlacak = yevmiyeSatirlari.Sum(s => s.Alacak);
            if (toplamBorc != toplamAlacak)
                throw new InvalidOperationException("Yevmiye fişi dengelenemedi: borç ve alacak toplamları eşit değil.");

            _baglam.CariHareketler.Add(new CariHareket
            {
                CariKartId = fatura.CariKartId,
                Tarih = fatura.Tarih,
                BelgeNo = fatura.FaturaNo,
                IlgiliBelgeId = fatura.Id,
                Aciklama = $"{fatura.FaturaNo} Nolu {(fatura.Tur == FaturaTuru.Satis ? "Satış" : "Alış")} Faturası",
                HareketTuru = fatura.Tur switch
                {
                    FaturaTuru.Satis => CariHareketTuru.SatisFaturasi,
                    FaturaTuru.Alis => CariHareketTuru.AlisFaturasi,
                    FaturaTuru.SatisIade => CariHareketTuru.SatisIadesi,
                    _ => CariHareketTuru.AlisIadesi
                },
                Borc = fatura.Tur is FaturaTuru.Satis or FaturaTuru.AlisIade ? fatura.GenelToplam : 0,
                Alacak = fatura.Tur is FaturaTuru.Alis or FaturaTuru.SatisIade ? fatura.GenelToplam : 0
            });

            foreach (var kalem in fatura.Kalemler)
            {
                // İrsaliye stok çıkışını zaten oluşturmuştur; fatura yalnız finansal kayıt üretir.
                if (fatura.KaynakTuru == FaturaKaynakTuru.Irsaliyeden)
                    continue;
                if (kalem.StokDagilimlari == null || kalem.StokDagilimlari.Count == 0)
                {
                    kalem.StokDagilimlari = new List<FaturaKalemiStokDagilimi>
                    {
                        new FaturaKalemiStokDagilimi { DepoId = 1, Miktar = kalem.Miktar }
                    };
                }

                var toplamDagilim = kalem.StokDagilimlari.Sum(d => d.Miktar);
                if (toplamDagilim != kalem.Miktar)
                    throw new InvalidOperationException($"'{urunler[kalem.UrunId].Ad}' ürününün satış miktarı ile stok dağılımı eşleşmiyor ({kalem.Miktar} != {toplamDagilim}).");

                foreach (var dagilim in kalem.StokDagilimlari)
                {
                    if (dagilim.Miktar <= 0) continue;

                    if (dagilim.DepoId is < 1 or > 3)
                        throw new InvalidOperationException("Geçersiz stok alanı seçildi. Yalnızca Mağaza, Depo 1 ve Depo 2 kullanılabilir.");

                    var lokasyon = await _baglam.UrunLokasyonStoklari
                        .FirstOrDefaultAsync(s => s.UrunId == kalem.UrunId && s.DepoId == dagilim.DepoId);

                    if (lokasyon == null)
                    {
                        var mevcStok = (dagilim.DepoId == 1 && urunler.TryGetValue(kalem.UrunId, out var u)) ? u.StokMiktari : 0;
                        lokasyon = new UrunLokasyonStogu
                        {
                            UrunId = kalem.UrunId,
                            DepoId = dagilim.DepoId,
                            MevcutMiktar = mevcStok,
                            RezerveMiktar = 0
                        };
                        _baglam.UrunLokasyonStoklari.Add(lokasyon);
                        await _baglam.SaveChangesAsync();
                    }

                    var depoAdi = dagilim.DepoId switch { 1 => "Mağazada", 2 => "Depo 1'de", 3 => "Depo 2'de", _ => "Seçilen stok alanında" };

                    if (fatura.Tur is FaturaTuru.Satis or FaturaTuru.AlisIade)
                    {
                        if (lokasyon.MevcutMiktar - lokasyon.RezerveMiktar < dagilim.Miktar)
                        {
                            var mevcut = lokasyon.MevcutMiktar - lokasyon.RezerveMiktar;
                            throw new InvalidOperationException($"{depoAdi} yeterli stok bulunmuyor. Mevcut: {mevcut}, İstenen: {dagilim.Miktar}.");
                        }

                        lokasyon.MevcutMiktar -= dagilim.Miktar;
                        lokasyon.GuncellemeTarihi = DateTime.UtcNow;

                        if (urunler.TryGetValue(kalem.UrunId, out var targetUrun))
                        {
                            targetUrun.StokMiktari -= dagilim.Miktar;
                            targetUrun.GuncellemeTarihi = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        lokasyon.MevcutMiktar += dagilim.Miktar;
                        lokasyon.GuncellemeTarihi = DateTime.UtcNow;

                        if (urunler.TryGetValue(kalem.UrunId, out var targetUrun))
                        {
                            targetUrun.StokMiktari += dagilim.Miktar;
                            targetUrun.GuncellemeTarihi = DateTime.UtcNow;
                        }
                    }

                    _baglam.StokHareketleri.Add(new StokHareketi
                    {
                        UrunId = kalem.UrunId,
                        DepoId = dagilim.DepoId,
                        Tarih = fatura.Tarih,
                        BelgeNo = fatura.FaturaNo,
                        IlgiliBelgeId = fatura.Id,
                        Aciklama = $"{fatura.FaturaNo} Nolu Fatura Kalemi ({urunler[kalem.UrunId].Ad})",
                        HareketTuru = fatura.Tur is FaturaTuru.Alis or FaturaTuru.SatisIade
                            ? StokHareketTuru.Giris
                            : StokHareketTuru.Cikis,
                        Miktar = dagilim.Miktar,
                        BirimFiyat = kalem.BirimFiyat
                    });
                }
            }

            var yevmiyeFisi = new YevmiyeFis
            {
                SirketId = sirketId,
                FisNo = $"YF-{fatura.Id:D10}",
                Tarih = fatura.Tarih,
                BelgeTipi = "Fatura",
                BelgeId = fatura.Id,
                Aciklama = fatura.Aciklama,
                Durum = YevmiyeDurumu.Onaylandi,
                Satirlar = yevmiyeSatirlari
            };
            _baglam.YevmiyeFisleri.Add(yevmiyeFisi);

            fatura.Durum = BelgeDurumu.Onaylandi;
            fatura.GuncellemeTarihi = DateTime.UtcNow;

            await _baglam.SaveChangesAsync();
            if (transactionSahibi)
                await transaction!.CommitAsync();
            return yevmiyeFisi;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (transactionSahibi)
                await transaction!.RollbackAsync();
            throw new InvalidOperationException("Fatura başka bir işlem tarafından işlendi. Lütfen kaydı yenileyin.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            if (transactionSahibi)
                await transaction!.RollbackAsync();
            throw new InvalidOperationException("Bu fatura için finansal işlem zaten oluşturulmuştur.", ex);
        }
        catch
        {
            if (transactionSahibi)
                await transaction!.RollbackAsync();
            throw;
        }
    }

    private async Task<bool> FinansalFisVarMiAsync(Fatura fatura)
    {
        return fatura.SirketId.HasValue && await _baglam.YevmiyeFisleri
            .AnyAsync(f => f.SirketId == fatura.SirketId.Value && f.BelgeTipi == "Fatura" && f.BelgeId == fatura.Id);
    }

    private void YetkiyiDogrula()
    {
        if (_kullaniciServisi == null)
            throw new UnauthorizedAccessException("Finansal işlem için yetkilendirme servisi kullanılabilir değil.");

        _kullaniciServisi.YetkiKontrolEt(y => y.FaturaOnayla, "Fatura Finansal İşlem Oluşturma");
    }

    private static YevmiyeSatiri SatirOlustur(HesapPlani hesap, decimal borc, decimal alacak, string aciklama)
    {
        return new YevmiyeSatiri
        {
            HesapId = hesap.Id,
            Borc = borc,
            Alacak = alacak,
            Aciklama = aciklama
        };
    }
}
