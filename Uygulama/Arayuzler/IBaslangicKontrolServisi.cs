namespace Uygulama.Arayuzler;

// Yeni kurulan programda müşterinin işe başlamadan yapması gereken tanımlardan biri. Sayfa, eksik tanımın yapıldığı
// ekranın gezinme anahtarıdır.
public sealed record BaslangicAdimi(string Baslik, string Aciklama, string Sayfa, bool Tamam);

public interface IBaslangicKontrolServisi
{
    // Aktif şirket için sıralı adımlar; tamamlananlar da döner (ilerleme gösterilir).
    Task<IReadOnlyList<BaslangicAdimi>> AdimlariGetirAsync(CancellationToken cancellationToken = default);
}
