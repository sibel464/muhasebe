namespace Altyapi.Servisler;

// Rapor tarih aralığı gün bazındadır: başlangıç gününün başından bitiş gününün SONUNA kadar.
// Tarih seçici "18.10.2026 00:00" verir; doğrudan "<= bitiş" karşılaştırılırsa o günün bütün kayıtları
// rapordan düşer. Belge tarihleri yerel saat "UTC" etiketiyle saklandığından sınırlar da UTC etiketli üretilir.
public readonly record struct RaporTarihAraligi(DateTime Baslangic, DateTime BitisSonrasi)
{
    // Rapor başlığında gösterilecek bitiş günü (dahil).
    public DateTime BitisGunu => BitisSonrasi == DateTime.MaxValue ? DateTime.MaxValue : BitisSonrasi.AddDays(-1);

    public static RaporTarihAraligi Olustur(DateTime? baslangic, DateTime? bitis) => new(
        baslangic is DateTime b ? DateTime.SpecifyKind(b.Date, DateTimeKind.Utc) : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
        bitis is DateTime s ? DateTime.SpecifyKind(s.Date.AddDays(1), DateTimeKind.Utc) : DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
}
