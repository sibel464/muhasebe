using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket53Doviz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DovizTutar",
                table: "KasaHareketleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Kur",
                table: "KasaHareketleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ParaBirimiId",
                table: "KasaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DovizAlacak",
                table: "CariHareketler",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DovizBorc",
                table: "CariHareketler",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Kur",
                table: "CariHareketler",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ParaBirimiId",
                table: "CariHareketler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DovizTutar",
                table: "BankaHareketleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Kur",
                table: "BankaHareketleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ParaBirimiId",
                table: "BankaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_ParaBirimiId",
                table: "KasaHareketleri",
                column: "ParaBirimiId");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_ParaBirimiId",
                table: "CariHareketler",
                column: "ParaBirimiId");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_ParaBirimiId",
                table: "BankaHareketleri",
                column: "ParaBirimiId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankaHareketleri_ParaBirimleri_ParaBirimiId",
                table: "BankaHareketleri",
                column: "ParaBirimiId",
                principalTable: "ParaBirimleri",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CariHareketler_ParaBirimleri_ParaBirimiId",
                table: "CariHareketler",
                column: "ParaBirimiId",
                principalTable: "ParaBirimleri",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KasaHareketleri_ParaBirimleri_ParaBirimiId",
                table: "KasaHareketleri",
                column: "ParaBirimiId",
                principalTable: "ParaBirimleri",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankaHareketleri_ParaBirimleri_ParaBirimiId",
                table: "BankaHareketleri");

            migrationBuilder.DropForeignKey(
                name: "FK_CariHareketler_ParaBirimleri_ParaBirimiId",
                table: "CariHareketler");

            migrationBuilder.DropForeignKey(
                name: "FK_KasaHareketleri_ParaBirimleri_ParaBirimiId",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketleri_ParaBirimiId",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_CariHareketler_ParaBirimiId",
                table: "CariHareketler");

            migrationBuilder.DropIndex(
                name: "IX_BankaHareketleri_ParaBirimiId",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "DovizTutar",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "Kur",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "ParaBirimiId",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "DovizAlacak",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "DovizBorc",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "Kur",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "ParaBirimiId",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "DovizTutar",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "Kur",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "ParaBirimiId",
                table: "BankaHareketleri");
        }
    }
}
