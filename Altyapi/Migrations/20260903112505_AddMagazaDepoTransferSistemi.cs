using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddMagazaDepoTransferSistemi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CariKartId",
                table: "StokHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FisNo",
                table: "StokHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HedefDepoId",
                table: "StokHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IslemTuru",
                table: "StokHareketleri",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Adres",
                table: "Depolar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MuhasebeKodu",
                table: "Depolar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sorumlu",
                table: "Depolar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefon",
                table: "Depolar",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepoTransferFisleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FisNo = table.Column<string>(type: "text", nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KaynakDepoId = table.Column<int>(type: "integer", nullable: false),
                    HedefDepoId = table.Column<int>(type: "integer", nullable: false),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    SevkEdenPersonel = table.Column<string>(type: "text", nullable: true),
                    TeslimAlanPersonel = table.Column<string>(type: "text", nullable: true),
                    BelgeNo = table.Column<string>(type: "text", nullable: true),
                    ToplamMiktar = table.Column<decimal>(type: "numeric", nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepoTransferFisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepoTransferFisleri_Depolar_HedefDepoId",
                        column: x => x.HedefDepoId,
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepoTransferFisleri_Depolar_KaynakDepoId",
                        column: x => x.KaynakDepoId,
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DepoTransferKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepoTransferFisiId = table.Column<int>(type: "integer", nullable: false),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    Miktar = table.Column<decimal>(type: "numeric", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric", nullable: false),
                    Not = table.Column<string>(type: "text", nullable: true),
                    RafKodu = table.Column<string>(type: "text", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepoTransferKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepoTransferKalemleri_DepoTransferFisleri_DepoTransferFisiId",
                        column: x => x.DepoTransferFisiId,
                        principalTable: "DepoTransferFisleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepoTransferKalemleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Adres", "MuhasebeKodu", "Sorumlu", "Telefon" },
                values: new object[] { null, "153.01.001", null, null });

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Adres", "MuhasebeKodu", "Sorumlu", "Telefon" },
                values: new object[] { null, "153.01.001", null, null });

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Adres", "MuhasebeKodu", "Sorumlu", "Telefon" },
                values: new object[] { null, "153.01.001", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_CariKartId",
                table: "StokHareketleri",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_HedefDepoId",
                table: "StokHareketleri",
                column: "HedefDepoId");

            migrationBuilder.CreateIndex(
                name: "IX_DepoTransferFisleri_HedefDepoId",
                table: "DepoTransferFisleri",
                column: "HedefDepoId");

            migrationBuilder.CreateIndex(
                name: "IX_DepoTransferFisleri_KaynakDepoId",
                table: "DepoTransferFisleri",
                column: "KaynakDepoId");

            migrationBuilder.CreateIndex(
                name: "IX_DepoTransferKalemleri_DepoTransferFisiId",
                table: "DepoTransferKalemleri",
                column: "DepoTransferFisiId");

            migrationBuilder.CreateIndex(
                name: "IX_DepoTransferKalemleri_UrunId",
                table: "DepoTransferKalemleri",
                column: "UrunId");

            migrationBuilder.AddForeignKey(
                name: "FK_StokHareketleri_CariKartlar_CariKartId",
                table: "StokHareketleri",
                column: "CariKartId",
                principalTable: "CariKartlar",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StokHareketleri_Depolar_HedefDepoId",
                table: "StokHareketleri",
                column: "HedefDepoId",
                principalTable: "Depolar",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StokHareketleri_CariKartlar_CariKartId",
                table: "StokHareketleri");

            migrationBuilder.DropForeignKey(
                name: "FK_StokHareketleri_Depolar_HedefDepoId",
                table: "StokHareketleri");

            migrationBuilder.DropTable(
                name: "DepoTransferKalemleri");

            migrationBuilder.DropTable(
                name: "DepoTransferFisleri");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_CariKartId",
                table: "StokHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_HedefDepoId",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "CariKartId",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "FisNo",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "HedefDepoId",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "IslemTuru",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "Adres",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "MuhasebeKodu",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "Sorumlu",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "Telefon",
                table: "Depolar");
        }
    }
}
