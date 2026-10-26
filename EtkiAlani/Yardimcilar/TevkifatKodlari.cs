namespace EtkiAlani.Yardimcilar;

/// <summary>
/// KDV Genel Uygulama Tebliği kısmi tevkifat kodları (GİB e-Fatura kod listesi). Oran faturada değiştirilebilir;
/// mevzuat değişirse buradaki öneri değeri güncellenir.
/// </summary>
public static class TevkifatKodlari
{
    public sealed record Tanim(string Kod, string Ad, int Pay)
    {
        public decimal Oran => Pay / 10m;
        public override string ToString() => $"{Kod} - {Ad} ({Pay}/10)";
    }

    public static IReadOnlyList<Tanim> Liste { get; } =
    [
        new("601", "Yapım işleri ve birlikte ifa edilen mühendislik-mimarlık, etüt-proje hizmetleri", 4),
        new("602", "Etüt, plan-proje, danışmanlık, denetim ve benzeri hizmetler", 9),
        new("603", "Makine, teçhizat, demirbaş ve taşıtların tadil, bakım ve onarımı", 7),
        new("604", "Yemek servis hizmeti", 5),
        new("605", "Organizasyon hizmeti", 5),
        new("606", "İşgücü temin hizmetleri", 9),
        new("607", "Özel güvenlik hizmeti", 9),
        new("608", "Yapı denetim hizmetleri", 9),
        new("609", "Fason tekstil, konfeksiyon, çanta ve ayakkabı dikim işleri", 7),
        new("610", "Turistik mağazalara müşteri bulma / götürme hizmetleri", 9),
        new("611", "Spor kulüplerinin yayın, reklam ve isim hakkı gelirleri", 9),
        new("612", "Temizlik hizmeti", 9),
        new("613", "Çevre ve bahçe bakım hizmetleri", 9),
        new("614", "Servis taşımacılığı hizmeti", 5),
        new("615", "Her türlü baskı ve basım hizmetleri", 7),
        new("616", "Diğer hizmetler (kamu kurum ve kuruluşlarına)", 5),
        new("617", "Hurda metalden elde edilen külçe teslimleri", 7),
        new("618", "Bakır, çinko, demir-çelik, alüminyum ve kurşun külçe teslimleri", 7),
        new("619", "Bakır, çinko ve alüminyum ürünlerinin teslimi", 7),
        new("620", "İstisnadan vazgeçenlerin hurda ve atık teslimi", 7),
        new("621", "Hurda ve atıklardan elde edilen hammadde teslimi", 9),
        new("622", "Pamuk, tiftik, yün, yapağı, ham post ve deri teslimleri", 9),
        new("623", "Ağaç ve orman ürünleri teslimi", 5),
        new("624", "Yük taşımacılığı hizmeti", 2),
        new("625", "Ticari reklam hizmetleri", 3),
        new("626", "Diğer teslimler", 2),
        new("627", "Demir-çelik ürünlerinin teslimi", 5)
    ];

    public static Tanim? Bul(string? kod) => Liste.FirstOrDefault(t => t.Kod == kod?.Trim());

    // Tevkifat tutarı tam KDV üzerinden kuruşa yuvarlanır.
    public static decimal Tutar(decimal kdvToplam, decimal oran) => decimal.Round(kdvToplam * oran, 2, MidpointRounding.AwayFromZero);
}
