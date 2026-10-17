using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class SirketsizKayitlariVarsayilanSirketeBagla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Şirket ayrımı: firma alanı boş kalmış eski kayıtlar varsayılan şirkete bağlanır (aksi halde hiçbir firmada görünmezler).
            migrationBuilder.Sql("UPDATE \"Kasalar\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"BankaHesaplari\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"KasaHareketleri\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"BankaHareketleri\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"BankaMutabakatlari\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"KasaSayimMutabakatlari\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"CariKartlar\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Faturalar\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Urunler\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"StokHareketleri\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"CekSenetler\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"PosIslemleri\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"DepoTransferFisleri\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111' WHERE \"SirketId\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Veri bağlama işlemi geri alınmaz: hangi kaydın önceden boş olduğu bilinmez ve boş firma alanı geçersiz durumdur.
        }
    }
}
