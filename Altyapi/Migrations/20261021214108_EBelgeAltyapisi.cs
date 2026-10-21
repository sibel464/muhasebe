using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class EBelgeAltyapisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EBelgeKlasoru",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EFaturaVarsayilanSenaryo",
                table: "SistemAyarlari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Il",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ilce",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostaKodu",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Il",
                table: "Sirketler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ilce",
                table: "Sirketler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostaKodu",
                table: "Sirketler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EBelgeDurumu",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EBelgeMesaji",
                table: "Faturalar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EBelgeTarihi",
                table: "Faturalar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EBelgeTuru",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EFaturaSenaryosu",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "Ettn",
                table: "Faturalar",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IadeEdilenFaturaNo",
                table: "Faturalar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IadeEdilenFaturaTarihi",
                table: "Faturalar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EFaturaEtiketi",
                table: "CariKartlar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EFaturaMukellefi",
                table: "CariKartlar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EBelgeKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaturaId = table.Column<int>(type: "integer", nullable: false),
                    Ettn = table.Column<Guid>(type: "uuid", nullable: false),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    Entegrator = table.Column<string>(type: "text", nullable: false),
                    Xml = table.Column<string>(type: "text", nullable: false),
                    DosyaYolu = table.Column<string>(type: "text", nullable: true),
                    Mesaj = table.Column<string>(type: "text", nullable: true),
                    KullaniciId = table.Column<int>(type: "integer", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EBelgeKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EBelgeKayitlari_Faturalar_FaturaId",
                        column: x => x.FaturaId,
                        principalTable: "Faturalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Sirketler",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "Il", "Ilce", "PostaKodu" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "SistemAyarlari",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EBelgeKlasoru", "EFaturaVarsayilanSenaryo", "Il", "Ilce", "PostaKodu" },
                values: new object[] { null, 0, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_Ettn",
                table: "Faturalar",
                column: "Ettn",
                unique: true,
                filter: "\"Ettn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeKayitlari_FaturaId",
                table: "EBelgeKayitlari",
                column: "FaturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EBelgeKayitlari");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_Ettn",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "EBelgeKlasoru",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "EFaturaVarsayilanSenaryo",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "Il",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "Ilce",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "PostaKodu",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "Il",
                table: "Sirketler");

            migrationBuilder.DropColumn(
                name: "Ilce",
                table: "Sirketler");

            migrationBuilder.DropColumn(
                name: "PostaKodu",
                table: "Sirketler");

            migrationBuilder.DropColumn(
                name: "EBelgeDurumu",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "EBelgeMesaji",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "EBelgeTarihi",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "EBelgeTuru",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "EFaturaSenaryosu",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "Ettn",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "IadeEdilenFaturaNo",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "IadeEdilenFaturaTarihi",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "EFaturaEtiketi",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "EFaturaMukellefi",
                table: "CariKartlar");
        }
    }
}
