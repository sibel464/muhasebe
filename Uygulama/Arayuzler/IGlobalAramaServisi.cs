namespace Uygulama.Arayuzler;
public sealed record GlobalAramaSonucu(string Kategori,int Id,string Kod,string Baslik,string? AltBilgi);
public interface IGlobalAramaServisi { Task<IReadOnlyList<GlobalAramaSonucu>> AraAsync(string metin,CancellationToken token=default); }
