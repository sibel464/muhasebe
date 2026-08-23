using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;

namespace Uygulama.Servisler;

public class MockUrunServisi : IUrunServisi
{
    private readonly List<Urun> _urunler = new()
    {
        new Urun { Id = 1, Kod = "URN001", Ad = "A4 Kağıt (500'lü)", Kategori = "Kırtasiye", Birim = "Paket", AlisFiyati = 45.00m, SatisFiyati = 65.00m, KdvOrani = 20, StokMiktari = 150, MinimumStok = 20, Aktif = true },
        new Urun { Id = 2, Kod = "URN002", Ad = "Tükenmez Kalem (12'li)", Kategori = "Kırtasiye", Birim = "Kutu", AlisFiyati = 18.00m, SatisFiyati = 30.00m, KdvOrani = 20, StokMiktari = 80, MinimumStok = 10, Aktif = true },
        new Urun { Id = 3, Kod = "URN003", Ad = "Laptop Çantası", Kategori = "Elektronik", Birim = "Adet", AlisFiyati = 250.00m, SatisFiyati = 399.00m, KdvOrani = 20, StokMiktari = 25, MinimumStok = 5, Aktif = true },
        new Urun { Id = 4, Kod = "URN004", Ad = "USB Bellek 32GB", Kategori = "Elektronik", Birim = "Adet", AlisFiyati = 120.00m, SatisFiyati = 189.00m, KdvOrani = 20, StokMiktari = 3, MinimumStok = 10, Aktif = true },
        new Urun { Id = 5, Kod = "URN005", Ad = "Masa Lambası", Kategori = "Ofis", Birim = "Adet", AlisFiyati = 180.00m, SatisFiyati = 289.00m, KdvOrani = 20, StokMiktari = 12, MinimumStok = 3, Aktif = true },
        new Urun { Id = 6, Kod = "URN006", Ad = "Zımba Makinesi", Kategori = "Kırtasiye", Birim = "Adet", AlisFiyati = 35.00m, SatisFiyati = 55.00m, KdvOrani = 20, StokMiktari = 0, MinimumStok = 5, Aktif = false },
    };

    private int _sonrakiId = 7;

    public Task<List<Urun>> TumunuGetirAsync()
        => Task.FromResult(_urunler.ToList());

    public Task<Urun?> IdIleGetirAsync(int id)
        => Task.FromResult(_urunler.FirstOrDefault(u => u.Id == id));

    public Task<Urun?> KodIleGetirAsync(string kod)
        => Task.FromResult(_urunler.FirstOrDefault(u => u.Kod == kod));

    public Task<Urun> EkleAsync(Urun urun)
    {
        urun.Id = _sonrakiId++;
        urun.OlusturmaTarihi = DateTime.UtcNow;
        _urunler.Add(urun);
        return Task.FromResult(urun);
    }

    public Task<Urun> GuncelleAsync(Urun urun)
    {
        var mevcutIndex = _urunler.FindIndex(u => u.Id == urun.Id);
        if (mevcutIndex >= 0)
        {
            urun.GuncellemeTarihi = DateTime.UtcNow;
            _urunler[mevcutIndex] = urun;
        }
        return Task.FromResult(urun);
    }

    public Task<bool> SilAsync(int id)
    {
        var urun = _urunler.FirstOrDefault(u => u.Id == id);
        if (urun == null) return Task.FromResult(false);
        urun.Silindi = true;
        urun.Aktif = false;
        return Task.FromResult(true);
    }

    public Task<List<Urun>> AraAsync(string aramaMetni)
    {
        var sonuclar = _urunler.Where(u =>
            u.Kod.Contains(aramaMetni, StringComparison.OrdinalIgnoreCase) ||
            u.Ad.Contains(aramaMetni, StringComparison.OrdinalIgnoreCase) ||
            (u.Kategori != null && u.Kategori.Contains(aramaMetni, StringComparison.OrdinalIgnoreCase))
        ).ToList();
        return Task.FromResult(sonuclar);
    }

    public Task<bool> KodMevcutMuAsync(string kod, int? haricId = null)
    {
        var mevcut = _urunler.Any(u => u.Kod == kod && (!haricId.HasValue || u.Id != haricId.Value));
        return Task.FromResult(mevcut);
    }
}
