using Altyapi.Veri;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    // Ürün fiyatı değiştirme artık ayrı yetkidir. Mevcut kullanıcılar bugüne kadar kart düzenleme yetkisiyle fiyat
    // değiştirebiliyordu; davranış değişmesin diye bu yetki onlara verilir. Yönetici dilerse sonra kaldırır.
    [DbContext(typeof(MuhasebeBaglami))]
    [Migration("20261125150000_FiyatDegistirmeYetkisi")]
    public partial class FiyatDegistirmeYetkisi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "KullaniciYetkileri" SET "StokFiyatDegistir" = true WHERE "StokDuzenle" AND NOT "StokFiyatDegistir";
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
