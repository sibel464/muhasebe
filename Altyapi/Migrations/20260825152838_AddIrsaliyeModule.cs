using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddIrsaliyeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Irsaliyeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IrsaliyeNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CariKartId = table.Column<int>(type: "integer", nullable: false),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AraToplam = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    KdvToplam = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GenelToplam = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DonusturulenFaturaId = table.Column<int>(type: "integer", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Irsaliyeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Irsaliyeler_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Irsaliyeler_Faturalar_DonusturulenFaturaId",
                        column: x => x.DonusturulenFaturaId,
                        principalTable: "Faturalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IrsaliyeKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IrsaliyeId = table.Column<int>(type: "integer", nullable: false),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    UrunKodu = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UrunAd = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Birim = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    KdvOrani = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    KdvTutari = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SatirToplami = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrsaliyeKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrsaliyeKalemleri_Irsaliyeler_IrsaliyeId",
                        column: x => x.IrsaliyeId,
                        principalTable: "Irsaliyeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IrsaliyeKalemleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IrsaliyeKalemleri_IrsaliyeId",
                table: "IrsaliyeKalemleri",
                column: "IrsaliyeId");

            migrationBuilder.CreateIndex(
                name: "IX_IrsaliyeKalemleri_UrunId",
                table: "IrsaliyeKalemleri",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_CariKartId",
                table: "Irsaliyeler",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_DonusturulenFaturaId",
                table: "Irsaliyeler",
                column: "DonusturulenFaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_IrsaliyeNo",
                table: "Irsaliyeler",
                column: "IrsaliyeNo",
                unique: true,
                filter: "\"IrsaliyeNo\" IS NOT NULL AND \"IrsaliyeNo\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_Tarih",
                table: "Irsaliyeler",
                column: "Tarih");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IrsaliyeKalemleri");

            migrationBuilder.DropTable(
                name: "Irsaliyeler");
        }
    }
}
