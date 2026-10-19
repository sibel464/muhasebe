namespace Uygulama.Arayuzler;

// KVKK md. 11 talepleri: kişinin hakkında tutulan verilerin dökümü ve yasal saklama süresi dolmuşsa anonimleştirme.
public sealed record KvkkSaklamaDurumu(bool AnonimlestirilebilirMi, DateTime? SonKayitTarihi, DateTime? SaklamaBitisi, string Aciklama);

public interface IKvkkServisi
{
    Task<string> KisiselVeriDokumuAsync(int cariId);
    Task<KvkkSaklamaDurumu> SaklamaDurumuAsync(int cariId);
    Task AnonimlestirAsync(int cariId, string talepAciklamasi);
}
