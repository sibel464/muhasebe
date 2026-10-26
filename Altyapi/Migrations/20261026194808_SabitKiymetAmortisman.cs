using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class SabitKiymetAmortisman : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SabitKiymetler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HesapKodu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BirikmisAmortismanHesapKodu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GiderHesapKodu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AlisTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Maliyet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FaydaliOmurYil = table.Column<int>(type: "integer", nullable: false),
                    Yontem = table.Column<int>(type: "integer", nullable: false),
                    KistAmortisman = table.Column<bool>(type: "boolean", nullable: false),
                    DevirBirikmisAmortisman = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DevirSonYil = table.Column<int>(type: "integer", nullable: true),
                    SeriNo = table.Column<string>(type: "text", nullable: true),
                    Konum = table.Column<string>(type: "text", nullable: true),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    EldenCikarmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EldenCikarmaFisId = table.Column<int>(type: "integer", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SabitKiymetler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AmortismanKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SabitKiymetId = table.Column<int>(type: "integer", nullable: false),
                    Yil = table.Column<int>(type: "integer", nullable: false),
                    Tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    YevmiyeFisId = table.Column<int>(type: "integer", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmortismanKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmortismanKayitlari_SabitKiymetler_SabitKiymetId",
                        column: x => x.SabitKiymetId,
                        principalTable: "SabitKiymetler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmortismanKayitlari_SabitKiymetId_Yil",
                table: "AmortismanKayitlari",
                columns: new[] { "SabitKiymetId", "Yil" },
                unique: true,
                filter: "\"Silindi\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SabitKiymetler_SirketId_Kod",
                table: "SabitKiymetler",
                columns: new[] { "SirketId", "Kod" },
                unique: true,
                filter: "\"Silindi\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmortismanKayitlari");

            migrationBuilder.DropTable(
                name: "SabitKiymetler");
        }
    }
}
