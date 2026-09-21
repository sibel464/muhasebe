using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddFaturaAndStokHareketi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notlar",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "KdvTutar",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "ToplamTutar",
                table: "FaturaKalemleri");

            migrationBuilder.AlterColumn<decimal>(
                name: "KdvToplam",
                table: "Faturalar",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "GenelToplam",
                table: "Faturalar",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "FaturaNo",
                table: "Faturalar",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "CariKartId",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AraToplam",
                table: "Faturalar",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                table: "Faturalar",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Durum",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "GenelIskontoOrani",
                table: "Faturalar",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GenelIskontoTutari",
                table: "Faturalar",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SatirIskontosuToplam",
                table: "Faturalar",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Versiyon",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "YuvarlamaTutari",
                table: "Faturalar",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Miktar",
                table: "FaturaKalemleri",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "KdvOrani",
                table: "FaturaKalemleri",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "BirimFiyat",
                table: "FaturaKalemleri",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "Birim",
                table: "FaturaKalemleri",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "IskontoOrani",
                table: "FaturaKalemleri",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IskontoTutari",
                table: "FaturaKalemleri",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvTutari",
                table: "FaturaKalemleri",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SatirToplami",
                table: "FaturaKalemleri",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UrunAd",
                table: "FaturaKalemleri",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UrunKodu",
                table: "FaturaKalemleri",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "StokHareketleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HareketTuru = table.Column<int>(type: "integer", nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BelgeNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IlgiliBelgeId = table.Column<int>(type: "integer", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StokHareketleri", x => x.Id);
                    table.CheckConstraint("CK_StokHareketi_Miktar", "\"Miktar\" > 0");
                    table.ForeignKey(
                        name: "FK_StokHareketleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_FaturaNo",
                table: "Faturalar",
                column: "FaturaNo",
                unique: true,
                filter: "\"FaturaNo\" IS NOT NULL AND \"FaturaNo\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_Tarih",
                table: "StokHareketleri",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_UrunId",
                table: "StokHareketleri",
                column: "UrunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StokHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_FaturaNo",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "Aciklama",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "Durum",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "GenelIskontoOrani",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "GenelIskontoTutari",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "SatirIskontosuToplam",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "Versiyon",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "YuvarlamaTutari",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "Birim",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "IskontoOrani",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "IskontoTutari",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "KdvTutari",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "SatirToplami",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "UrunAd",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "UrunKodu",
                table: "FaturaKalemleri");

            migrationBuilder.AlterColumn<decimal>(
                name: "KdvToplam",
                table: "Faturalar",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "GenelToplam",
                table: "Faturalar",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "FaturaNo",
                table: "Faturalar",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "CariKartId",
                table: "Faturalar",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "AraToplam",
                table: "Faturalar",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "Notlar",
                table: "Faturalar",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Miktar",
                table: "FaturaKalemleri",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "KdvOrani",
                table: "FaturaKalemleri",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BirimFiyat",
                table: "FaturaKalemleri",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvTutar",
                table: "FaturaKalemleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ToplamTutar",
                table: "FaturaKalemleri",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
