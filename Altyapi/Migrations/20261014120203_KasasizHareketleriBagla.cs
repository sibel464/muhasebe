using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class KasasizHareketleriBagla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski kayıtlar kasa seçilmeden oluşturulmuştu ve hiçbir kasanın bakiyesinde görünmüyordu.
            // Varsayılan (ilk aktif) kasaya bağlanır.
            migrationBuilder.Sql("""
                UPDATE "KasaHareketleri" h
                SET "KasaId" = k."Id", "SirketId" = COALESCE(h."SirketId", k."SirketId")
                FROM (SELECT "Id", "SirketId" FROM "Kasalar" WHERE "AktifMi" ORDER BY "Id" LIMIT 1) k
                WHERE h."KasaId" IS NULL;
                """);

            // Banka hareketleri yalnızca sistemde tek aktif banka hesabı varsa ona bağlanır; birden fazlaysa belirsizdir.
            migrationBuilder.Sql("""
                UPDATE "BankaHareketleri" h
                SET "BankaHesabiId" = b."Id", "SirketId" = COALESCE(h."SirketId", b."SirketId")
                FROM (SELECT "Id", "SirketId" FROM "BankaHesaplari" WHERE "AktifMi") b
                WHERE h."BankaHesabiId" IS NULL
                  AND (SELECT COUNT(*) FROM "BankaHesaplari" WHERE "AktifMi") = 1;
                """);

            // Cari dışı gelir/gider fişleri varsayılan 'Tahsilat' türüyle kaydedilmişti; türleri düzeltilir.
            migrationBuilder.Sql("""
                UPDATE "KasaHareketleri" SET "FisTuru" = CASE WHEN "Tur" = 0 THEN 2 ELSE 3 END
                WHERE "CariKartId" IS NULL AND "FaturaId" IS NULL AND "FisTuru" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
