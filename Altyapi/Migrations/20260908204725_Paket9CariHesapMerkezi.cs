using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket9CariHesapMerkezi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Siparisler_CariKartId",
                table: "Siparisler");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketleri_CariKartId",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_CariKartId",
                table: "Irsaliyeler");

            migrationBuilder.DropIndex(
                name: "IX_CariHareketler_CariKartId",
                table: "CariHareketler");

            migrationBuilder.DropIndex(
                name: "IX_BankaHareketleri_CariKartId",
                table: "BankaHareketleri");

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "CariKartlar",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RiskLimiti",
                table: "CariKartlar",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VadeGunu",
                table: "CariKartlar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CariNotlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    CariKartId = table.Column<int>(type: "integer", nullable: false),
                    KullaniciId = table.Column<int>(type: "integer", nullable: false),
                    KullaniciAdi = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Baslik = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Metin = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CariNotlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CariNotlari_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_CariKartId_SiparisTarihi_Id",
                table: "Siparisler",
                columns: new[] { "CariKartId", "SiparisTarihi", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_CariKartId_Tarih_Id",
                table: "KasaHareketleri",
                columns: new[] { "CariKartId", "Tarih", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_CariKartId_Tarih_Id",
                table: "Irsaliyeler",
                columns: new[] { "CariKartId", "Tarih", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_SirketId_CariKartId_Tarih_Id",
                table: "Faturalar",
                columns: new[] { "SirketId", "CariKartId", "Tarih", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CekSenetler_CariKartId_VadeTarihi_Id",
                table: "CekSenetler",
                columns: new[] { "CariKartId", "VadeTarihi", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_CariKartId_Tarih_Id",
                table: "CariHareketler",
                columns: new[] { "CariKartId", "Tarih", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_CariKartId_Tarih_Id",
                table: "BankaHareketleri",
                columns: new[] { "CariKartId", "Tarih", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CariNotlari_CariKartId",
                table: "CariNotlari",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_CariNotlari_SirketId_CariKartId_OlusturmaTarihi",
                table: "CariNotlari",
                columns: new[] { "SirketId", "CariKartId", "OlusturmaTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CariNotlari");

            migrationBuilder.DropIndex(
                name: "IX_Siparisler_CariKartId_SiparisTarihi_Id",
                table: "Siparisler");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketleri_CariKartId_Tarih_Id",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_CariKartId_Tarih_Id",
                table: "Irsaliyeler");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_SirketId_CariKartId_Tarih_Id",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_CekSenetler_CariKartId_VadeTarihi_Id",
                table: "CekSenetler");

            migrationBuilder.DropIndex(
                name: "IX_CariHareketler_CariKartId_Tarih_Id",
                table: "CariHareketler");

            migrationBuilder.DropIndex(
                name: "IX_BankaHareketleri_CariKartId_Tarih_Id",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "RiskLimiti",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "VadeGunu",
                table: "CariKartlar");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_CariKartId",
                table: "Siparisler",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_CariKartId",
                table: "KasaHareketleri",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_CariKartId",
                table: "Irsaliyeler",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_CariKartId",
                table: "CariHareketler",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_CariKartId",
                table: "BankaHareketleri",
                column: "CariKartId");
        }
    }
}
