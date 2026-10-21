using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AyarlarMersisVeOrnekFirmaTemizligi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MersisNo",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            // Kurulumla gelen örnek firma künyesi belgelere basılıyordu. Yalnız hâlâ örnek değeri taşıyan alanlar boşaltılır;
            // kullanıcının girdiği bilgiye dokunulmaz. Örnek unvan yerine varsayılan şirketin unvanı yazılır.
            migrationBuilder.Sql("""
                UPDATE "SistemAyarlari" SET
                    "FirmaUnvani" = CASE WHEN "FirmaUnvani" = 'Örnek Ticaret Ltd. Şti.'
                        THEN COALESCE((SELECT "Unvan" FROM "Sirketler" WHERE "Id" = '11111111-1111-1111-1111-111111111111'), '') ELSE "FirmaUnvani" END,
                    "VergiDairesi" = NULLIF("VergiDairesi", 'Kadıköy V.D.'),
                    "VergiNo" = NULLIF("VergiNo", '1234567890'),
                    "Telefon" = NULLIF("Telefon", '0216 555 00 00'),
                    "Eposta" = NULLIF("Eposta", 'info@ornekticaret.com'),
                    "WebSitesi" = NULLIF("WebSitesi", 'www.ornekticaret.com'),
                    "Adres" = NULLIF("Adres", 'Atatürk Cad. No: 15/A Kadıköy / İstanbul'),
                    "FisHeader" = NULLIF("FisHeader", 'Örnek Ticaret Ltd. Şti.');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MersisNo",
                table: "SistemAyarlari");

        }
    }
}
