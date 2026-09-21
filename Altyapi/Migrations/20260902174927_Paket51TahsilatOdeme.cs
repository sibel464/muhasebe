using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket51TahsilatOdeme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IslemDurumu",
                table: "KasaHareketleri",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Tamamlandi");

            migrationBuilder.AddColumn<int>(
                name: "TersKayitId",
                table: "KasaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IslemDurumu",
                table: "BankaHareketleri",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Tamamlandi");

            migrationBuilder.AddColumn<string>(
                name: "OdemeYontemi",
                table: "BankaHareketleri",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Banka");

            migrationBuilder.AddColumn<int>(
                name: "TersKayitId",
                table: "BankaHareketleri",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IslemDurumu",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "TersKayitId",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "IslemDurumu",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "OdemeYontemi",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "TersKayitId",
                table: "BankaHareketleri");
        }
    }
}
