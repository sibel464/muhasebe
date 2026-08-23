using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureCariKart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_Musteriler_MusteriId",
                table: "Faturalar");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_Tedarikciler_TedarikciId",
                table: "Faturalar");

            migrationBuilder.DropTable(
                name: "Musteriler");

            migrationBuilder.DropTable(
                name: "Tedarikciler");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_MusteriId",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "MusteriId",
                table: "Faturalar");

            migrationBuilder.RenameColumn(
                name: "TedarikciId",
                table: "Faturalar",
                newName: "CariKartId");

            migrationBuilder.RenameIndex(
                name: "IX_Faturalar_TedarikciId",
                table: "Faturalar",
                newName: "IX_Faturalar_CariKartId");

            migrationBuilder.CreateTable(
                name: "CariKartlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    Tip = table.Column<int>(type: "integer", nullable: false),
                    Unvan = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Soyad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VergiDairesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VergiNo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    TcNo = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Telefon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CepTelefonu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Eposta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WebSitesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Il = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Ilce = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostaKodu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Aktif = table.Column<bool>(type: "boolean", nullable: false),
                    Borc = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Alacak = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CariKartlar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_Kod",
                table: "CariKartlar",
                column: "Kod",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_CariKartlar_CariKartId",
                table: "Faturalar",
                column: "CariKartId",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_CariKartlar_CariKartId",
                table: "Faturalar");

            migrationBuilder.DropTable(
                name: "CariKartlar");

            migrationBuilder.RenameColumn(
                name: "CariKartId",
                table: "Faturalar",
                newName: "TedarikciId");

            migrationBuilder.RenameIndex(
                name: "IX_Faturalar_CariKartId",
                table: "Faturalar",
                newName: "IX_Faturalar_TedarikciId");

            migrationBuilder.AddColumn<int>(
                name: "MusteriId",
                table: "Faturalar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Musteriler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    Adres = table.Column<string>(type: "text", nullable: true),
                    Aktif = table.Column<bool>(type: "boolean", nullable: false),
                    Bakiye = table.Column<decimal>(type: "numeric", nullable: false),
                    Eposta = table.Column<string>(type: "text", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sehir = table.Column<string>(type: "text", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    TcNo = table.Column<string>(type: "text", nullable: true),
                    Telefon = table.Column<string>(type: "text", nullable: true),
                    VergiNo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Musteriler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tedarikciler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    Adres = table.Column<string>(type: "text", nullable: true),
                    Aktif = table.Column<bool>(type: "boolean", nullable: false),
                    Bakiye = table.Column<decimal>(type: "numeric", nullable: false),
                    Eposta = table.Column<string>(type: "text", nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sehir = table.Column<string>(type: "text", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    Telefon = table.Column<string>(type: "text", nullable: true),
                    VergiNo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tedarikciler", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_MusteriId",
                table: "Faturalar",
                column: "MusteriId");

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_Musteriler_MusteriId",
                table: "Faturalar",
                column: "MusteriId",
                principalTable: "Musteriler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_Tedarikciler_TedarikciId",
                table: "Faturalar",
                column: "TedarikciId",
                principalTable: "Tedarikciler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
