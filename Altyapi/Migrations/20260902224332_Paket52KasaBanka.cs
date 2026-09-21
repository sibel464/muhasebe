using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket52KasaBanka : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KasaId",
                table: "KasaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankaHesabiId",
                table: "BankaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankaHesaplari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "text", nullable: false),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    Sube = table.Column<string>(type: "text", nullable: false),
                    HesapNo = table.Column<string>(type: "text", nullable: false),
                    Iban = table.Column<string>(type: "text", nullable: false),
                    ParaBirimi = table.Column<string>(type: "text", nullable: false),
                    Bakiye = table.Column<decimal>(type: "numeric", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankaHesaplari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kasalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "text", nullable: false),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    ParaBirimi = table.Column<string>(type: "text", nullable: false),
                    Bakiye = table.Column<decimal>(type: "numeric", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kasalar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_KasaId",
                table: "KasaHareketleri",
                column: "KasaId");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_BankaHesabiId",
                table: "BankaHareketleri",
                column: "BankaHesabiId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankaHareketleri_BankaHesaplari_BankaHesabiId",
                table: "BankaHareketleri",
                column: "BankaHesabiId",
                principalTable: "BankaHesaplari",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KasaHareketleri_Kasalar_KasaId",
                table: "KasaHareketleri",
                column: "KasaId",
                principalTable: "Kasalar",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankaHareketleri_BankaHesaplari_BankaHesabiId",
                table: "BankaHareketleri");

            migrationBuilder.DropForeignKey(
                name: "FK_KasaHareketleri_Kasalar_KasaId",
                table: "KasaHareketleri");

            migrationBuilder.DropTable(
                name: "BankaHesaplari");

            migrationBuilder.DropTable(
                name: "Kasalar");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketleri_KasaId",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_BankaHareketleri_BankaHesabiId",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "KasaId",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "BankaHesabiId",
                table: "BankaHareketleri");
        }
    }
}
