using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddFinansalIslemCekirdegi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HesapPlanlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    HesapKodu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HesapAdi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tip = table.Column<int>(type: "integer", nullable: false),
                    UstHesapId = table.Column<int>(type: "integer", nullable: true),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HesapPlanlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HesapPlanlari_HesapPlanlari_UstHesapId",
                        column: x => x.UstHesapId,
                        principalTable: "HesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HesapPlanlari_Sirketler_SirketId",
                        column: x => x.SirketId,
                        principalTable: "Sirketler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "YevmiyeFisleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    FisNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BelgeTipi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BelgeId = table.Column<int>(type: "integer", nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YevmiyeFisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YevmiyeFisleri_Sirketler_SirketId",
                        column: x => x.SirketId,
                        principalTable: "Sirketler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "YevmiyeSatirlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FisId = table.Column<int>(type: "integer", nullable: false),
                    HesapId = table.Column<int>(type: "integer", nullable: false),
                    Borc = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Alacak = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YevmiyeSatirlari", x => x.Id);
                    table.CheckConstraint("CK_YevmiyeSatiri_BorcAlacak", "\"Borc\" >= 0 AND \"Alacak\" >= 0 AND ((\"Borc\" > 0 AND \"Alacak\" = 0) OR (\"Borc\" = 0 AND \"Alacak\" > 0))");
                    table.ForeignKey(
                        name: "FK_YevmiyeSatirlari_HesapPlanlari_HesapId",
                        column: x => x.HesapId,
                        principalTable: "HesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YevmiyeSatirlari_YevmiyeFisleri_FisId",
                        column: x => x.FisId,
                        principalTable: "YevmiyeFisleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "HesapPlanlari",
                columns: new[] { "Id", "AktifMi", "GuncellemeTarihi", "HesapAdi", "HesapKodu", "OlusturmaTarihi", "Silindi", "SirketId", "Tip", "UstHesapId" },
                values: new object[,]
                {
                    { 100, true, null, "KASA", "100", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 1, null },
                    { 102, true, null, "BANKA", "102", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 1, null },
                    { 120, true, null, "ALICILAR", "120", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 1, null },
                    { 153, true, null, "TİCARİ MALLAR", "153", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 1, null },
                    { 191, true, null, "İNDİRİLECEK KDV", "191", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 1, null },
                    { 320, true, null, "SATICILAR", "320", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 2, null },
                    { 391, true, null, "HESAPLANAN KDV", "391", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 2, null },
                    { 600, true, null, "YURT İÇİ SATIŞLAR", "600", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), 3, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_HesapPlanlari_SirketId_HesapKodu",
                table: "HesapPlanlari",
                columns: new[] { "SirketId", "HesapKodu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HesapPlanlari_UstHesapId",
                table: "HesapPlanlari",
                column: "UstHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_YevmiyeFisleri_SirketId_BelgeTipi_BelgeId",
                table: "YevmiyeFisleri",
                columns: new[] { "SirketId", "BelgeTipi", "BelgeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YevmiyeFisleri_SirketId_FisNo",
                table: "YevmiyeFisleri",
                columns: new[] { "SirketId", "FisNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YevmiyeFisleri_SirketId_Tarih",
                table: "YevmiyeFisleri",
                columns: new[] { "SirketId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_YevmiyeSatirlari_FisId",
                table: "YevmiyeSatirlari",
                column: "FisId");

            migrationBuilder.CreateIndex(
                name: "IX_YevmiyeSatirlari_HesapId",
                table: "YevmiyeSatirlari",
                column: "HesapId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YevmiyeSatirlari");

            migrationBuilder.DropTable(
                name: "HesapPlanlari");

            migrationBuilder.DropTable(
                name: "YevmiyeFisleri");
        }
    }
}
