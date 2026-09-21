using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class LogoKasaBankaGelistirmeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                table: "Kasalar",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MuhasebeKodu",
                table: "Kasalar",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FisNo",
                table: "KasaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FisTuru",
                table: "KasaHareketleri",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MakbuzNo",
                table: "KasaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MasrafKodu",
                table: "KasaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OzelKod",
                table: "KasaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BlokeBakiye",
                table: "BankaHesaplari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "HesapTuru",
                table: "BankaHesaplari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "KrediLimiti",
                table: "BankaHesaplari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MuhasebeKodu",
                table: "BankaHesaplari",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PosKomisyonOrani",
                table: "BankaHesaplari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ValorGunu",
                table: "BankaHesaplari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DekontNo",
                table: "BankaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FisNo",
                table: "BankaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FisTuru",
                table: "BankaHareketleri",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "KomisyonTutari",
                table: "BankaHareketleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MasrafTutari",
                table: "BankaHareketleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OzelKod",
                table: "BankaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValorTarihi",
                table: "BankaHareketleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KasaSayimMutabakatlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KasaId = table.Column<int>(type: "integer", nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Adet200 = table.Column<int>(type: "integer", nullable: false),
                    Adet100 = table.Column<int>(type: "integer", nullable: false),
                    Adet50 = table.Column<int>(type: "integer", nullable: false),
                    Adet20 = table.Column<int>(type: "integer", nullable: false),
                    Adet10 = table.Column<int>(type: "integer", nullable: false),
                    Adet5 = table.Column<int>(type: "integer", nullable: false),
                    BozukParaToplam = table.Column<decimal>(type: "numeric", nullable: false),
                    SistemBakiyesi = table.Column<decimal>(type: "numeric", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    OlusturanKullaniciId = table.Column<int>(type: "integer", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KasaSayimMutabakatlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KasaSayimMutabakatlari_Kasalar_KasaId",
                        column: x => x.KasaId,
                        principalTable: "Kasalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KasaSayimMutabakatlari_KasaId",
                table: "KasaSayimMutabakatlari",
                column: "KasaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KasaSayimMutabakatlari");

            migrationBuilder.DropColumn(
                name: "Aciklama",
                table: "Kasalar");

            migrationBuilder.DropColumn(
                name: "MuhasebeKodu",
                table: "Kasalar");

            migrationBuilder.DropColumn(
                name: "FisNo",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "FisTuru",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "MakbuzNo",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "MasrafKodu",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "OzelKod",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "BlokeBakiye",
                table: "BankaHesaplari");

            migrationBuilder.DropColumn(
                name: "HesapTuru",
                table: "BankaHesaplari");

            migrationBuilder.DropColumn(
                name: "KrediLimiti",
                table: "BankaHesaplari");

            migrationBuilder.DropColumn(
                name: "MuhasebeKodu",
                table: "BankaHesaplari");

            migrationBuilder.DropColumn(
                name: "PosKomisyonOrani",
                table: "BankaHesaplari");

            migrationBuilder.DropColumn(
                name: "ValorGunu",
                table: "BankaHesaplari");

            migrationBuilder.DropColumn(
                name: "DekontNo",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "FisNo",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "FisTuru",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "KomisyonTutari",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "MasrafTutari",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "OzelKod",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "ValorTarihi",
                table: "BankaHareketleri");
        }
    }
}
