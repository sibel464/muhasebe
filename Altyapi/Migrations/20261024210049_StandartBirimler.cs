using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class StandartBirimler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Yeni kurulumda ölçü birimi listesi boştu: müşteri önce Tanımlar'dan birim eklemeden hiç ürün ekleyemiyordu.
            // Standart birimler eklenir; aynı kodla zaten olan birime dokunulmaz (elle eklenmiş birimler korunur).
            // Kısa adlar e-Fatura birim kodu çevirisinin (UblTrFaturaUretici.BirimKodu) tanıdığı adlardır.
            migrationBuilder.Sql(@"
INSERT INTO ""MerkeziTanimlar"" (""Grup"", ""Kod"", ""Ad"", ""KisaAd"", ""SiraNo"", ""AktifMi"", ""OlusturmaTarihi"", ""Silindi"")
SELECT 'BIRIM', v.kod, v.ad, v.kisa, v.sira, true, now() at time zone 'utc', false
FROM (VALUES
                ('ADET', 'Adet', 'Adet', 10),
                ('KG', 'Kilogram', 'Kg', 20),
                ('GRAM', 'Gram', 'Gr', 30),
                ('TON', 'Ton', 'Ton', 40),
                ('LITRE', 'Litre', 'Lt', 50),
                ('ML', 'Mililitre', 'Ml', 60),
                ('METRE', 'Metre', 'Mt', 70),
                ('M2', 'Metrekare', 'M2', 80),
                ('M3', 'Metreküp', 'M3', 90),
                ('PAKET', 'Paket', 'Paket', 100),
                ('KUTU', 'Kutu', 'Kutu', 110),
                ('KOLI', 'Koli', 'Koli', 120),
                ('CIFT', 'Çift', 'Çift', 130),
                ('DUZINE', 'Düzine', 'Düzine', 140),
                ('TAKIM', 'Takım', 'Takım', 150),
                ('SAAT', 'Saat', 'Saat', 160)
) AS v(kod, ad, kisa, sira)
WHERE NOT EXISTS (SELECT 1 FROM ""MerkeziTanimlar"" m WHERE m.""Grup"" = 'BIRIM' AND m.""Kod"" = v.kod);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eklenen birimler ürünlerde kullanılmış olabilir; geri alınırken silinmez.
        }
    }
}
