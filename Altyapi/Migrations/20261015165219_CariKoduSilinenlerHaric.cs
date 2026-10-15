using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class CariKoduSilinenlerHaric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_Kod",
                table: "CariKartlar");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_Kod",
                table: "CariKartlar",
                column: "Kod",
                unique: true,
                filter: "\"Silindi\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski (silinenleri de kapsayan) benzersiz indeks kurulabilsin diye, kodu yeniden kullanılmış
            // silinmiş carilerin koduna ek verilir.
            migrationBuilder.Sql("""
                UPDATE "CariKartlar" c SET "Kod" = c."Kod" || '-SIL' || c."Id"
                WHERE c."Silindi" AND EXISTS (SELECT 1 FROM "CariKartlar" d WHERE d."Kod" = c."Kod" AND d."Id" <> c."Id");
                """);

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_Kod",
                table: "CariKartlar");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_Kod",
                table: "CariKartlar",
                column: "Kod",
                unique: true);
        }
    }
}
