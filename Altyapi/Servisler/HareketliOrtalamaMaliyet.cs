using Altyapi.Veri;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;

namespace Altyapi.Servisler;

// Raporlardaki satış maliyeti ve stok değeri için hareketli ağırlıklı ortalama birim maliyet.
// Ürünün güncel alış fiyatı geçmiş satışlara uygulanırsa, fiyat değiştikten sonra eski dönemlerin kârı da değişir;
// burada her satış, satış anındaki ortalama maliyetle değerlenir.
//
// Kurallar (stok hareketleri tarih + Id sırasıyla işlenir, depo transferi şirket toplamını değiştirmez):
// - Alış, açılış/devir, sayım fazlası gibi girişler kendi birim fiyatıyla girer ve ortalamayı değiştirir.
// - Satış iadesi ve satış faturası iptalinin ters girişi satış fiyatını taşır; maliyete o anki ortalamayla döner.
// - Alış faturası iptalinin ters çıkışı, girdiği fiyatla geri çıkar; diğer bütün çıkışlar ortalamayla çıkar.
// - Hiç fiyatlı giriş yoksa ürün kartındaki alış fiyatı kullanılır.
public sealed class HareketliOrtalamaMaliyet
{
    private readonly Dictionary<int, List<(DateTime Tarih, decimal Birim)>> _zamanCizelgesi = new();
    private readonly Dictionary<(int FaturaId, int UrunId), (decimal Miktar, decimal Tutar)> _faturaMaliyetleri = new();
    private readonly Dictionary<int, decimal> _kartFiyatlari;

    private HareketliOrtalamaMaliyet(Dictionary<int, decimal> kartFiyatlari) => _kartFiyatlari = kartFiyatlari;

    public static async Task<HareketliOrtalamaMaliyet> HesaplaAsync(MuhasebeBaglami baglam, Guid sirketId, ICollection<int> urunIds, CancellationToken cancellationToken = default)
    {
        var kartFiyatlari = await baglam.Urunler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi]).AsNoTracking()
            .Where(u => u.SirketId == sirketId && urunIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.AlisFiyati, cancellationToken);
        var sonuc = new HareketliOrtalamaMaliyet(kartFiyatlari);
        if (kartFiyatlari.Count == 0)
            return sonuc;

        var hareketler = await baglam.StokHareketleri.AsNoTracking()
            .Where(h => h.Urun!.SirketId == sirketId && urunIds.Contains(h.UrunId) && !h.IptalEdildi && h.IslemTuru != StokIslemTuru.DepoTransferi)
            .OrderBy(h => h.Tarih).ThenBy(h => h.Id)
            .Select(h => new { h.UrunId, h.Tarih, h.HareketTuru, h.Miktar, h.BirimFiyat, h.BelgeNo, h.IlgiliBelgeId })
            .ToListAsync(cancellationToken);

        // IlgiliBelgeId türler arası ortak: fatura hareketi belge numarasıyla (iptal ters kaydında "-IPTAL" ekli) ayırt edilir.
        var belgeIdleri = hareketler.Where(h => h.IlgiliBelgeId.HasValue).Select(h => h.IlgiliBelgeId!.Value).Distinct().ToList();
        var faturalar = await baglam.Faturalar.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.SirketId == sirketId && belgeIdleri.Contains(f.Id))
            .Select(f => new { f.Id, f.FaturaNo, f.Tur })
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        var durumlar = new Dictionary<int, (decimal Miktar, decimal Deger, decimal Birim)>();
        foreach (var h in hareketler)
        {
            var (miktar, deger, birim) = durumlar.TryGetValue(h.UrunId, out var d) ? d : (0m, 0m, 0m);
            if (birim <= 0)
                birim = kartFiyatlari.GetValueOrDefault(h.UrunId);

            int? faturaId = null;
            FaturaTuru? faturaTuru = null;
            var iptalTersi = false;
            if (h.IlgiliBelgeId is int belgeId && faturalar.TryGetValue(belgeId, out var f))
            {
                if (h.BelgeNo == f.FaturaNo) { faturaId = f.Id; faturaTuru = f.Tur; }
                else if (h.BelgeNo == $"{f.FaturaNo}-IPTAL") { faturaTuru = f.Tur; iptalTersi = true; }
            }
            var satisKaynakli = faturaTuru is FaturaTuru.Satis or FaturaTuru.SatisIade;

            if (h.HareketTuru == StokHareketTuru.Giris)
            {
                var girisFiyati = satisKaynakli || h.BirimFiyat <= 0 ? birim : h.BirimFiyat;
                if (miktar <= 0)
                {
                    miktar += h.Miktar;
                    deger = Math.Max(0, miktar) * girisFiyati;
                    birim = girisFiyati;
                }
                else
                {
                    miktar += h.Miktar;
                    deger += h.Miktar * girisFiyati;
                    birim = deger / miktar;
                }
                if (faturaId.HasValue)
                    sonuc.FaturaMaliyetiEkle(faturaId.Value, h.UrunId, h.Miktar, h.Miktar * girisFiyati);
            }
            else
            {
                var alisIptali = iptalTersi && faturaTuru == FaturaTuru.Alis && h.BirimFiyat > 0;
                var cikisFiyati = alisIptali ? h.BirimFiyat : birim;
                miktar -= h.Miktar;
                if (miktar > 0 && alisIptali && deger - h.Miktar * cikisFiyati > 0)
                {
                    deger -= h.Miktar * cikisFiyati;
                    birim = deger / miktar;
                }
                else
                {
                    deger = Math.Max(0, miktar) * birim;
                }
                if (faturaId.HasValue)
                    sonuc.FaturaMaliyetiEkle(faturaId.Value, h.UrunId, h.Miktar, h.Miktar * cikisFiyati);
            }

            durumlar[h.UrunId] = (miktar, deger, birim);
            if (!sonuc._zamanCizelgesi.TryGetValue(h.UrunId, out var cizelge))
                sonuc._zamanCizelgesi[h.UrunId] = cizelge = new List<(DateTime, decimal)>();
            cizelge.Add((h.Tarih, birim));
        }

        return sonuc;
    }

    private void FaturaMaliyetiEkle(int faturaId, int urunId, decimal miktar, decimal tutar)
    {
        var mevcut = _faturaMaliyetleri.GetValueOrDefault((faturaId, urunId));
        _faturaMaliyetleri[(faturaId, urunId)] = (mevcut.Miktar + miktar, mevcut.Tutar + tutar);
    }

    // Verilen andaki (o ana kadarki bütün hareketler işlendikten sonraki) ortalama birim maliyet.
    public decimal TarihtekiBirimMaliyet(int urunId, DateTime tarih)
    {
        if (_zamanCizelgesi.TryGetValue(urunId, out var cizelge))
        {
            // Çizelge tarihe göre sıralı: tarihe kadarki son kaydı ikili aramayla bul.
            int alt = 0, ust = cizelge.Count - 1, bulunan = -1;
            while (alt <= ust)
            {
                var orta = (alt + ust) / 2;
                if (cizelge[orta].Tarih <= tarih) { bulunan = orta; alt = orta + 1; }
                else ust = orta - 1;
            }
            if (bulunan >= 0 && cizelge[bulunan].Birim > 0)
                return cizelge[bulunan].Birim;
            // Tarihten önce hareket yoksa ilk fiyatlı kaydın maliyeti en yakın tahmindir.
            var ilk = cizelge.FirstOrDefault(c => c.Birim > 0);
            if (ilk.Birim > 0)
                return ilk.Birim;
        }
        return _kartFiyatlari.GetValueOrDefault(urunId);
    }

    public decimal GuncelBirimMaliyet(int urunId) => TarihtekiBirimMaliyet(urunId, DateTime.MaxValue);

    // Fatura kaleminin birim maliyeti: faturanın kendi stok hareketinden; hareket yoksa (irsaliyeden faturalanan,
    // stok hareketi irsaliyede olan satışlar) fatura tarihindeki ortalama maliyet.
    public decimal FaturaBirimMaliyeti(int faturaId, int urunId, DateTime faturaTarihi)
    {
        if (_faturaMaliyetleri.TryGetValue((faturaId, urunId), out var m) && m.Miktar > 0 && m.Tutar > 0)
            return m.Tutar / m.Miktar;
        return TarihtekiBirimMaliyet(urunId, faturaTarihi);
    }
}
