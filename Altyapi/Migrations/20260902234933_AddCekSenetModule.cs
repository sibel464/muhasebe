using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddCekSenetModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CekSenetler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    BelgeNo = table.Column<string>(type: "text", nullable: false),
                    VadeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DuzenlemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tutar = table.Column<decimal>(type: "numeric", nullable: false),
                    ParaBirimiId = table.Column<int>(type: "integer", nullable: true),
                    Kur = table.Column<decimal>(type: "numeric", nullable: false),
                    DovizTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    Kesideci = table.Column<string>(type: "text", nullable: false),
                    BankaAdi = table.Column<string>(type: "text", nullable: true),
                    SubeAdi = table.Column<string>(type: "text", nullable: true),
                    HesapNo = table.Column<string>(type: "text", nullable: true),
                    CariKartId = table.Column<int>(type: "integer", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    SilindiMi = table.Column<bool>(type: "boolean", nullable: false),
                    SilmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CekSenetler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CekSenetler_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CekSenetler_BelgeNo",
                table: "CekSenetler",
                column: "BelgeNo");

            migrationBuilder.CreateIndex(
                name: "IX_CekSenetler_CariKartId",
                table: "CekSenetler",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_CekSenetler_VadeTarihi",
                table: "CekSenetler",
                column: "VadeTarihi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CekSenetler");
        }
    }
}
