using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueUrunBarkod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski verideki mükerrer barkodlara dokunmadan yalnızca bundan sonra
            // eklenecek ürünleri veritabanı seviyesinde korur.
            migrationBuilder.Sql("""
                DO $$
                DECLARE son_mevcut_id integer;
                BEGIN
                    SELECT COALESCE(MAX("Id"), 0) INTO son_mevcut_id FROM "Urunler";
                    EXECUTE format(
                        'CREATE UNIQUE INDEX "IX_Urunler_Barkod_YeniKayitlar" ON "Urunler" ("Barkod") WHERE "Barkod" IS NOT NULL AND "Id" > %s',
                        son_mevcut_id);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Urunler_Barkod_YeniKayitlar\";");
        }
    }
}
