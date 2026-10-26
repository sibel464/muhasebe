using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class HizmetMasrafKarti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hizmet / masraf kartı: stok tutulmayan kalemler (işçilik, kira, elektrik). Alışı GiderHesapKodu'na yazılır.
            migrationBuilder.AddColumn<string>(
                name: "GiderHesapKodu",
                table: "Urunler",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StokTakibi",
                table: "Urunler",
                type: "boolean",
                nullable: false,
                defaultValue: true); // mevcut ürünlerin hepsi stoklu kalır
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GiderHesapKodu",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "StokTakibi",
                table: "Urunler");
        }
    }
}
