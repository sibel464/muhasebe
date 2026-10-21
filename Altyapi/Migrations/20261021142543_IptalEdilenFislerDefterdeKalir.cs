using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class IptalEdilenFislerDefterdeKalir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ters fişle iptal edilen onaylı fiş "İptal" durumuna alınıyordu; raporlar onu saymayıp ters fişi saydığı için
            // tutar iki kez geri alınmış görünüyordu. Asıl fiş defterde kalır, iptal bilgisi TersFisId'den okunur.
            migrationBuilder.Sql("""
                UPDATE "YevmiyeFisleri" SET "Durum" = 2
                WHERE "Durum" = 3 AND "TersFisId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
