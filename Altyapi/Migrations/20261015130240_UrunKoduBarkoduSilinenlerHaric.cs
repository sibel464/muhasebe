using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class UrunKoduBarkoduSilinenlerHaric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Urunler_Kod",
                table: "Urunler");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_Kod",
                table: "Urunler",
                column: "Kod",
                unique: true,
                filter: "\"Silindi\" = false");

            // Barkod benzersizliği de yalnızca silinmemiş ürünler arasında aranır. Eski veride silinmemiş
            // ürünlerde tekrarlanan barkod varsa onlara dokunulmaz; koruma o kayıtlardan sonrakilere uygulanır.
            migrationBuilder.Sql("""
                DO $$
                DECLARE esik integer;
                BEGIN
                    SELECT COALESCE(MAX(u."Id"), 0) INTO esik
                    FROM "Urunler" u
                    WHERE u."Barkod" IS NOT NULL AND u."Silindi" = false
                      AND EXISTS (SELECT 1 FROM "Urunler" d
                                  WHERE d."Barkod" = u."Barkod" AND d."Silindi" = false AND d."Id" <> u."Id");
                    DROP INDEX IF EXISTS "IX_Urunler_Barkod_YeniKayitlar";
                    EXECUTE format(
                        'CREATE UNIQUE INDEX "IX_Urunler_Barkod_YeniKayitlar" ON "Urunler" ("Barkod") WHERE "Barkod" IS NOT NULL AND "Silindi" = false AND "Id" > %s',
                        esik);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski indeksler silinenleri de kapsar: kodu/barkodu yeniden kullanılmış silinmiş ürünlere ek verilir.
            migrationBuilder.Sql("""
                UPDATE "Urunler" u SET "Kod" = u."Kod" || '-SIL' || u."Id"
                WHERE u."Silindi" AND EXISTS (SELECT 1 FROM "Urunler" d WHERE d."Kod" = u."Kod" AND d."Id" <> u."Id");
                UPDATE "Urunler" u SET "Barkod" = NULL
                WHERE u."Silindi" AND u."Barkod" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "Urunler" d WHERE d."Barkod" = u."Barkod" AND d."Id" <> u."Id");
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE son_mevcut_id integer;
                BEGIN
                    SELECT COALESCE(MAX("Id"), 0) INTO son_mevcut_id FROM "Urunler";
                    DROP INDEX IF EXISTS "IX_Urunler_Barkod_YeniKayitlar";
                    EXECUTE format(
                        'CREATE UNIQUE INDEX "IX_Urunler_Barkod_YeniKayitlar" ON "Urunler" ("Barkod") WHERE "Barkod" IS NOT NULL AND "Id" > %s',
                        son_mevcut_id);
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Urunler_Kod",
                table: "Urunler");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_Kod",
                table: "Urunler",
                column: "Kod",
                unique: true);
        }
    }
}
