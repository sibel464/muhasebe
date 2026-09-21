using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class TamProfesyonelStokSistemi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                table: "Urunler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepoId",
                table: "StokHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepoTuru",
                table: "Depolar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FaturaKalemiStokDagilimleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaturaKalemiId = table.Column<int>(type: "integer", nullable: false),
                    DepoId = table.Column<int>(type: "integer", nullable: false),
                    Miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaturaKalemiStokDagilimleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaturaKalemiStokDagilimleri_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaturaKalemiStokDagilimleri_FaturaKalemleri_FaturaKalemiId",
                        column: x => x.FaturaKalemiId,
                        principalTable: "FaturaKalemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UrunLokasyonStoklari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    DepoId = table.Column<int>(type: "integer", nullable: false),
                    MevcutMiktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RezerveMiktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrunLokasyonStoklari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UrunLokasyonStoklari_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UrunLokasyonStoklari_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UrunSatisFiyatlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    TaksitSayisi = table.Column<int>(type: "integer", nullable: false),
                    Fiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ParaBirimiId = table.Column<int>(type: "integer", nullable: true),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrunSatisFiyatlari", x => x.Id);
                    table.CheckConstraint("CK_UrunSatisFiyati_TaksitSayisi", "\"TaksitSayisi\" >= 1 AND \"TaksitSayisi\" <= 9");
                    table.ForeignKey(
                        name: "FK_UrunSatisFiyatlari_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 1,
                column: "DepoTuru",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 1,
                column: "Aciklama",
                value: null);

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 2,
                column: "Aciklama",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_DepoId",
                table: "StokHareketleri",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalemiStokDagilimleri_DepoId",
                table: "FaturaKalemiStokDagilimleri",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalemiStokDagilimleri_FaturaKalemiId_DepoId",
                table: "FaturaKalemiStokDagilimleri",
                columns: new[] { "FaturaKalemiId", "DepoId" });

            migrationBuilder.CreateIndex(
                name: "IX_UrunLokasyonStoklari_DepoId",
                table: "UrunLokasyonStoklari",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_UrunLokasyonStoklari_UrunId_DepoId",
                table: "UrunLokasyonStoklari",
                columns: new[] { "UrunId", "DepoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UrunSatisFiyatlari_UrunId_TaksitSayisi_ParaBirimiId",
                table: "UrunSatisFiyatlari",
                columns: new[] { "UrunId", "TaksitSayisi", "ParaBirimiId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StokHareketleri_Depolar_DepoId",
                table: "StokHareketleri",
                column: "DepoId",
                principalTable: "Depolar",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StokHareketleri_Depolar_DepoId",
                table: "StokHareketleri");

            migrationBuilder.DropTable(
                name: "FaturaKalemiStokDagilimleri");

            migrationBuilder.DropTable(
                name: "UrunLokasyonStoklari");

            migrationBuilder.DropTable(
                name: "UrunSatisFiyatlari");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_DepoId",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "Aciklama",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "DepoId",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "DepoTuru",
                table: "Depolar");
        }
    }
}
