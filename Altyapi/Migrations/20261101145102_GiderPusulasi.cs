using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class GiderPusulasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GiderPusulalari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    BelgeNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SaticiAdSoyad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SaticiTcKimlikNo = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    SaticiAdres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GiderHesapKodu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BrutTutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StopajOrani = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    StopajTutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetTutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    KasaHareketiId = table.Column<int>(type: "integer", nullable: true),
                    BankaHareketiId = table.Column<int>(type: "integer", nullable: true),
                    YevmiyeFisId = table.Column<int>(type: "integer", nullable: true),
                    IptalEdildi = table.Column<bool>(type: "boolean", nullable: false),
                    IptalTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IptalNedeni = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiderPusulalari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiderPusulasiKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GiderPusulasiId = table.Column<int>(type: "integer", nullable: false),
                    Cinsi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Miktar = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Birim = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiderPusulasiKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiderPusulasiKalemleri_GiderPusulalari_GiderPusulasiId",
                        column: x => x.GiderPusulasiId,
                        principalTable: "GiderPusulalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiderPusulalari_SirketId_BelgeNo",
                table: "GiderPusulalari",
                columns: new[] { "SirketId", "BelgeNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiderPusulalari_SirketId_Tarih",
                table: "GiderPusulalari",
                columns: new[] { "SirketId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_GiderPusulasiKalemleri_GiderPusulasiId",
                table: "GiderPusulasiKalemleri",
                column: "GiderPusulasiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiderPusulasiKalemleri");

            migrationBuilder.DropTable(
                name: "GiderPusulalari");
        }
    }
}
