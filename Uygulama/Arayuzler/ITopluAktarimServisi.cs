namespace Uygulama.Arayuzler;

public enum AktarimTuru { Cari, Urun }

// Önizlemedeki bir satır. Hata varsa satır aktarılmaz; uyarı bilgi amaçlıdır.
public sealed record AktarimSatiri(int SatirNo, string Kod, string Ad, string Ozet, string? Hata, string? Uyari)
{
    public bool Gecerli => Hata == null;
    public string Durum => Hata != null ? "✖ " + Hata : Uyari != null ? "⚠ " + Uyari : "✔ Aktarılabilir";
}

public sealed record AktarimSonucu(int SatirNo, string Ad, bool Basarili, string Mesaj);

public interface ITopluAktarimServisi
{
    // Excel'de açılabilen (UTF-8, ";" ayırıcılı) örnek şablon CSV'si; sütun başlıkları ve bir örnek satır içerir.
    string SablonCsv(AktarimTuru tur);

    IReadOnlyList<AktarimSatiri> Onizle(AktarimTuru tur, string dosyaYolu);

    // Geçerli satırları kaydeder (her satır kendi başına; biri hata verirse diğerleri devam eder).
    // Cari açılış bakiyesi devir fişiyle (karşı hesap 500), ürün açılış stoğu varsayılan stok yerine işlenir.
    Task<IReadOnlyList<AktarimSonucu>> AktarAsync(AktarimTuru tur, string dosyaYolu, DateTime acilisTarihi, CancellationToken cancellationToken = default);
}
