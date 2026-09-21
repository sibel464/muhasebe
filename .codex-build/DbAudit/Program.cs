using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var cs = "Host=127.0.0.1;Port=5432;Database=Muhasebe;Username=postgres;";
var options = new DbContextOptionsBuilder<MuhasebeBaglami>().UseNpgsql(cs).Options;
await using var db = new MuhasebeBaglami(options);

if (args.Contains("apply"))
{
    var snapshot = new
    {
        Tarih = DateTime.UtcNow,
        Depolar = await db.Depolar.IgnoreQueryFilters().AsNoTracking().OrderBy(d => d.Id)
            .Select(d => new { d.Id, d.DepoKodu, d.DepoAdi, d.DepoTuru, d.AktifMi }).ToListAsync(),
        Stoklar = await db.UrunLokasyonStoklari.AsNoTracking().OrderBy(s => s.UrunId).ThenBy(s => s.DepoId)
            .Select(s => new { s.Id, s.UrunId, s.DepoId, s.MevcutMiktar, s.RezerveMiktar }).ToListAsync(),
        Urunler = await db.Urunler.IgnoreQueryFilters().AsNoTracking().OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Kod, u.StokMiktari }).ToListAsync()
    };
    var backupPath = Path.Combine(AppContext.BaseDirectory, $"depo-stok-yedek-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    await File.WriteAllTextAsync(backupPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));

    await using var tx = await db.Database.BeginTransactionAsync();
    var depoMap = new Dictionary<int, int> { [1] = 4, [2] = 5, [3] = 6 };
    var tumStoklar = await db.UrunLokasyonStoklari.OrderBy(s => s.UrunId).ThenBy(s => s.DepoId).ToListAsync();
    foreach (var urunGrubu in tumStoklar.GroupBy(s => s.UrunId))
    {
        var hedefToplam = urunGrubu.Where(s => s.DepoId is 4 or 5 or 6).Sum(s => s.MevcutMiktar);
        if (hedefToplam == 0)
        {
            foreach (var eski in urunGrubu.Where(s => depoMap.ContainsKey(s.DepoId) && s.MevcutMiktar != 0))
            {
                var hedefId = depoMap[eski.DepoId];
                var hedef = urunGrubu.FirstOrDefault(s => s.DepoId == hedefId);
                if (hedef == null)
                {
                    hedef = new EtkiAlani.Modeller.Stok.UrunLokasyonStogu
                    {
                        UrunId = eski.UrunId, DepoId = hedefId,
                        MevcutMiktar = eski.MevcutMiktar, RezerveMiktar = eski.RezerveMiktar
                    };
                    db.UrunLokasyonStoklari.Add(hedef);
                }
                else
                {
                    hedef.MevcutMiktar += eski.MevcutMiktar;
                    hedef.RezerveMiktar += eski.RezerveMiktar;
                }
            }
        }

        foreach (var eski in urunGrubu.Where(s => depoMap.ContainsKey(s.DepoId)))
        {
            eski.MevcutMiktar = 0;
            eski.RezerveMiktar = 0;
        }
    }

    var kartlar = await db.Depolar.IgnoreQueryFilters().ToListAsync();
    foreach (var depo in kartlar) depo.AktifMi = depo.Id is 4 or 5 or 6;
    await db.SaveChangesAsync();

    var urunToplamlari = await db.UrunLokasyonStoklari
        .Where(s => s.DepoId == 4 || s.DepoId == 5 || s.DepoId == 6)
        .GroupBy(s => s.UrunId).Select(g => new { UrunId = g.Key, Miktar = g.Sum(s => s.MevcutMiktar) }).ToListAsync();
    var urunlerGuncelle = await db.Urunler.IgnoreQueryFilters().ToListAsync();
    foreach (var urun in urunlerGuncelle)
        urun.StokMiktari = urunToplamlari.FirstOrDefault(x => x.UrunId == urun.Id)?.Miktar ?? 0;
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    Console.WriteLine($"UYGULANDI|Yedek={backupPath}");
}

var depolar = await db.Depolar.IgnoreQueryFilters().AsNoTracking()
    .Select(d => new { d.Id, d.DepoKodu, d.DepoAdi, d.DepoTuru, d.AktifMi }).OrderBy(d => d.Id).ToListAsync();
foreach (var d in depolar)
{
    var stok = await db.UrunLokasyonStoklari.AsNoTracking().Where(s => s.DepoId == d.Id).SumAsync(s => s.MevcutMiktar);
    Console.WriteLine($"DEPO|{d.Id}|{d.DepoKodu}|{d.DepoAdi}|{d.DepoTuru}|Aktif={d.AktifMi}|Stok={stok}");
}

var urunler = await db.Urunler.AsNoTracking().Where(u => u.Aktif).OrderBy(u => u.Id).Take(20)
    .Select(u => new { u.Id, u.Kod, u.StokMiktari }).ToListAsync();
foreach (var u in urunler)
{
    var dagilim = await db.UrunLokasyonStoklari.AsNoTracking().Where(s => s.UrunId == u.Id)
        .GroupBy(s => s.DepoId).Select(g => new { DepoId = g.Key, Miktar = g.Sum(x => x.MevcutMiktar) }).OrderBy(x => x.DepoId).ToListAsync();
    Console.WriteLine($"URUN|{u.Kod}|Ana={u.StokMiktari}|" + string.Join(",", dagilim.Select(x => $"D{x.DepoId}={x.Miktar}")));
}
