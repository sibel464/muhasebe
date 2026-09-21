using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket54BelgeKapama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KapaliDovizTutar",
                table: "CariHareketler",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KapaliTutar",
                table: "CariHareketler",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BelgeKapamalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OdenenCariHareketId = table.Column<int>(type: "integer", nullable: false),
                    FaturaCariHareketId = table.Column<int>(type: "integer", nullable: false),
                    KapananTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    KapananDovizTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    KapamaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BelgeKapamalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BelgeKapamalar_CariHareketler_FaturaCariHareketId",
                        column: x => x.FaturaCariHareketId,
                        principalTable: "CariHareketler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BelgeKapamalar_CariHareketler_OdenenCariHareketId",
                        column: x => x.OdenenCariHareketId,
                        principalTable: "CariHareketler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BelgeKapamalar_FaturaCariHareketId",
                table: "BelgeKapamalar",
                column: "FaturaCariHareketId");

            migrationBuilder.CreateIndex(
                name: "IX_BelgeKapamalar_OdenenCariHareketId",
                table: "BelgeKapamalar",
                column: "OdenenCariHareketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BelgeKapamalar");

            migrationBuilder.DropColumn(
                name: "KapaliDovizTutar",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "KapaliTutar",
                table: "CariHareketler");
        }
    }
}
