using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket11Finans360OperasyonMerkezi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "KasaSayimMutabakatlari",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "Kasalar",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "KasaHareketleri",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "CekSenetler",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "BankaHesaplari",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "BankaHareketleri",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankaMutabakatlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankaHesabiId = table.Column<int>(type: "integer", nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EkstreBakiyesi = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_BankaMutabakatlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankaMutabakatlari_BankaHesaplari_BankaHesabiId",
                        column: x => x.BankaHesabiId,
                        principalTable: "BankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PosIslemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankaHesabiId = table.Column<int>(type: "integer", nullable: false),
                    BankaAdi = table.Column<string>(type: "text", nullable: false),
                    PosCihazAdi = table.Column<string>(type: "text", nullable: false),
                    KartTuru = table.Column<string>(type: "text", nullable: false),
                    TaksitSayisi = table.Column<int>(type: "integer", nullable: false),
                    BrutTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    KomisyonOrani = table.Column<decimal>(type: "numeric", nullable: false),
                    KomisyonTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    NetTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    IslemTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BeklenenHesabaGecisTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HesabaGecisTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    FaturaId = table.Column<int>(type: "integer", nullable: true),
                    CariKartId = table.Column<int>(type: "integer", nullable: true),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosIslemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosIslemleri_BankaHesaplari_BankaHesabiId",
                        column: x => x.BankaHesabiId,
                        principalTable: "BankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankaMutabakatlari_BankaHesabiId",
                table: "BankaMutabakatlari",
                column: "BankaHesabiId");

            migrationBuilder.CreateIndex(
                name: "IX_PosIslemleri_BankaHesabiId",
                table: "PosIslemleri",
                column: "BankaHesabiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankaMutabakatlari");

            migrationBuilder.DropTable(
                name: "PosIslemleri");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "KasaSayimMutabakatlari");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "Kasalar");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "CekSenetler");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "BankaHesaplari");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "BankaHareketleri");
        }
    }
}
