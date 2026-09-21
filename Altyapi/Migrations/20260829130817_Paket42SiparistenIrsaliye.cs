using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket42SiparistenIrsaliye : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AracPlaka",
                table: "Irsaliyeler",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GuncelleyenKullaniciId",
                table: "Irsaliyeler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OlusturanKullaniciId",
                table: "Irsaliyeler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferansNo",
                table: "Irsaliyeler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SevkTarihi",
                table: "Irsaliyeler",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SiparisId",
                table: "Irsaliyeler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoforAdi",
                table: "Irsaliyeler",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoforTelefon",
                table: "Irsaliyeler",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tasiyici",
                table: "Irsaliyeler",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeslimatAdSoyad",
                table: "Irsaliyeler",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeslimatAdres",
                table: "Irsaliyeler",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeslimatIl",
                table: "Irsaliyeler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeslimatIlce",
                table: "Irsaliyeler",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeslimatTelefon",
                table: "Irsaliyeler",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                table: "IrsaliyeKalemleri",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Barkod",
                table: "IrsaliyeKalemleri",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SiparisKalemiId",
                table: "IrsaliyeKalemleri",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IrsaliyeKalemiStokDagilimlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IrsaliyeKalemiId = table.Column<int>(type: "integer", nullable: false),
                    DepoId = table.Column<int>(type: "integer", nullable: false),
                    Miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrsaliyeKalemiStokDagilimlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrsaliyeKalemiStokDagilimlari_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IrsaliyeKalemiStokDagilimlari_IrsaliyeKalemleri_IrsaliyeKal~",
                        column: x => x.IrsaliyeKalemiId,
                        principalTable: "IrsaliyeKalemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_GuncelleyenKullaniciId",
                table: "Irsaliyeler",
                column: "GuncelleyenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_OlusturanKullaniciId",
                table: "Irsaliyeler",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_SiparisId",
                table: "Irsaliyeler",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_IrsaliyeKalemleri_SiparisKalemiId",
                table: "IrsaliyeKalemleri",
                column: "SiparisKalemiId");

            migrationBuilder.CreateIndex(
                name: "IX_IrsaliyeKalemiStokDagilimlari_DepoId",
                table: "IrsaliyeKalemiStokDagilimlari",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_IrsaliyeKalemiStokDagilimlari_IrsaliyeKalemiId_DepoId",
                table: "IrsaliyeKalemiStokDagilimlari",
                columns: new[] { "IrsaliyeKalemiId", "DepoId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IrsaliyeKalemleri_SiparisKalemleri_SiparisKalemiId",
                table: "IrsaliyeKalemleri",
                column: "SiparisKalemiId",
                principalTable: "SiparisKalemleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irsaliyeler_Kullanicilar_GuncelleyenKullaniciId",
                table: "Irsaliyeler",
                column: "GuncelleyenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irsaliyeler_Kullanicilar_OlusturanKullaniciId",
                table: "Irsaliyeler",
                column: "OlusturanKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Irsaliyeler_Siparisler_SiparisId",
                table: "Irsaliyeler",
                column: "SiparisId",
                principalTable: "Siparisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IrsaliyeKalemleri_SiparisKalemleri_SiparisKalemiId",
                table: "IrsaliyeKalemleri");

            migrationBuilder.DropForeignKey(
                name: "FK_Irsaliyeler_Kullanicilar_GuncelleyenKullaniciId",
                table: "Irsaliyeler");

            migrationBuilder.DropForeignKey(
                name: "FK_Irsaliyeler_Kullanicilar_OlusturanKullaniciId",
                table: "Irsaliyeler");

            migrationBuilder.DropForeignKey(
                name: "FK_Irsaliyeler_Siparisler_SiparisId",
                table: "Irsaliyeler");

            migrationBuilder.DropTable(
                name: "IrsaliyeKalemiStokDagilimlari");

            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_GuncelleyenKullaniciId",
                table: "Irsaliyeler");

            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_OlusturanKullaniciId",
                table: "Irsaliyeler");

            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_SiparisId",
                table: "Irsaliyeler");

            migrationBuilder.DropIndex(
                name: "IX_IrsaliyeKalemleri_SiparisKalemiId",
                table: "IrsaliyeKalemleri");

            migrationBuilder.DropColumn(
                name: "AracPlaka",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "GuncelleyenKullaniciId",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "OlusturanKullaniciId",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "ReferansNo",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SevkTarihi",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SiparisId",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SoforAdi",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SoforTelefon",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "Tasiyici",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "TeslimatAdSoyad",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "TeslimatAdres",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "TeslimatIl",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "TeslimatIlce",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "TeslimatTelefon",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "Aciklama",
                table: "IrsaliyeKalemleri");

            migrationBuilder.DropColumn(
                name: "Barkod",
                table: "IrsaliyeKalemleri");

            migrationBuilder.DropColumn(
                name: "SiparisKalemiId",
                table: "IrsaliyeKalemleri");
        }
    }
}
