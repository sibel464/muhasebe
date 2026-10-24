using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class OrnekUrunleriKaldir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski sürümlerin yeni kuruluma eklediği örnek ürünler (stok hareketi olmadan 150 / 25 stok) kaldırılır.
            // Yalnız örnek kodu taşıyan ve hiçbir harekette/belgede kullanılmamış kart silinir; kullanılmışsa
            // yabancı anahtar silmeyi engeller ve kart olduğu gibi kalır.
            migrationBuilder.Sql(@"
DO $$
DECLARE kayit record;
BEGIN
    FOR kayit IN SELECT ""Id"" FROM ""Urunler"" WHERE (""Id"", ""Kod"") IN ((1, 'URN001'), (2, 'URN002')) LOOP
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM ""StokHareketleri"" WHERE ""UrunId"" = kayit.""Id"")
               AND NOT EXISTS (SELECT 1 FROM ""DepoTransferKalemleri"" WHERE ""UrunId"" = kayit.""Id"")
               AND NOT EXISTS (SELECT 1 FROM ""StokSayimKalemleri"" WHERE ""UrunId"" = kayit.""Id"") THEN
                DELETE FROM ""Urunler"" WHERE ""Id"" = kayit.""Id"";
            END IF;
        EXCEPTION WHEN foreign_key_violation THEN
            NULL;
        END;
    END LOOP;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Örnek ürünler geri eklenmez.
        }
    }
}
