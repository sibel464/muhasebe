using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class CariHareketServisi : ICariHareketServisi
{
    private readonly MuhasebeBaglami _baglam;

    public CariHareketServisi(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
    }

    public async Task<List<CariHareket>> CariKartinHareketleriniGetirAsync(int cariKartId)
    {
        return await _baglam.CariHareketler
            .Where(h => h.CariKartId == cariKartId)
            .OrderBy(h => h.Tarih)
            .ThenBy(h => h.OlusturmaTarihi)
            .ToListAsync();
    }

    public async Task<CariHareket> EkleAsync(CariHareket hareket)
    {
        // 1. Temel İş Kuralları ve Ticari Doğrulamalar
        if (hareket.CariKartId <= 0)
            throw new ArgumentException("Geçersiz Cari Kart ID.");

        if (hareket.Borc < 0 || hareket.Alacak < 0)
            throw new ArgumentException("Borç veya Alacak tutarı negatif olamaz.");

        if (hareket.Borc > 0 && hareket.Alacak > 0)
            throw new ArgumentException("Bir hareket aynı anda hem borç hem alacak oluşturamaz.");

        if (string.IsNullOrWhiteSpace(hareket.Aciklama))
            throw new ArgumentException("Hareket açıklaması boş bırakılamaz.");

        hareket.Aciklama = hareket.Aciklama.Trim();
        if (hareket.BelgeNo != null)
            hareket.BelgeNo = hareket.BelgeNo.Trim();

        // 2. Concurrency-Safe Transaction
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            // İlgili Cari Kartı kilitlemek veya Concurrency Token ile yüklemek
            var cari = await _baglam.CariKartlar
                .FirstOrDefaultAsync(c => c.Id == hareket.CariKartId);

            if (cari == null)
                throw new KeyNotFoundException("İlgili cari kart bulunamadı.");

            if (!cari.Aktif)
                throw new InvalidOperationException("Pasif durumdaki cari karta işlem girilemez.");

            hareket.OlusturmaTarihi = DateTime.UtcNow;
            _baglam.CariHareketler.Add(hareket);

            // Bakiye alanlarını transaction güvenliğinde güncelle
            cari.Borc += hareket.Borc;
            cari.Alacak += hareket.Alacak;
            cari.GuncellemeTarihi = DateTime.UtcNow;
            
            // Versiyon sayacını arttır (Concurrency Check için)
            cari.Versiyon++;

            await _baglam.SaveChangesAsync();
            await transaction.CommitAsync();

            return hareket;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Cari bakiye güncelleme çakışması tespit edildi. Eş zamanlı işlem başarısız oldu. Lütfen tekrar deneyiniz.");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> SilAsync(int id)
    {
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var hareket = await _baglam.CariHareketler
                .FirstOrDefaultAsync(h => h.Id == id);

            if (hareket == null) return false;

            // Fatura vb. entegrasyon belgelerinden otomatik gelen hareketler doğrudan silinemez
            if (hareket.IlgiliBelgeId.HasValue && hareket.HareketTuru != CariHareketTuru.Devir && hareket.HareketTuru != CariHareketTuru.Diger)
            {
                throw new InvalidOperationException("Belgeye bağlı otomatik cari hareketler doğrudan silinemez. İlgili belge üzerinden işlem yapınız.");
            }

            var cari = await _baglam.CariKartlar
                .FirstOrDefaultAsync(c => c.Id == hareket.CariKartId);

            if (cari == null)
                throw new KeyNotFoundException("Cari kart bulunamadı.");

            // Soft delete işaretle
            hareket.Silindi = true;

            // Cari kart üzerindeki bakiyeleri güvenle geri çek
            cari.Borc -= hareket.Borc;
            cari.Alacak -= hareket.Alacak;
            cari.GuncellemeTarihi = DateTime.UtcNow;
            cari.Versiyon++;

            await _baglam.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Cari bakiye güncelleme çakışması tespit edildi. Lütfen tekrar deneyiniz.");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<decimal> BakiyeHesaplaAsync(int cariKartId)
    {
        // Doğrulama amaçlı dinamik toplam hesabı (Yaklaşım A)
        var toplamlar = await _baglam.CariHareketler
            .Where(h => h.CariKartId == cariKartId)
            .GroupBy(h => h.CariKartId)
            .Select(g => new
            {
                Borc = g.Sum(x => x.Borc),
                Alacak = g.Sum(x => x.Alacak)
            })
            .FirstOrDefaultAsync();

        if (toplamlar == null) return 0;
        return toplamlar.Borc - toplamlar.Alacak;
    }
}
