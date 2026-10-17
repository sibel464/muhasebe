using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class SiparisIrsaliyeCariHareketSirketId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "Siparisler",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "Irsaliyeler",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "CariHareketler",
                type: "uuid",
                nullable: true);

            // Mevcut kayıtlar carisinin firmasına bağlanır; carisi firmasızsa varsayılan şirkete.
            foreach (var tablo in new[] { "Siparisler", "Irsaliyeler", "CariHareketler" })
                migrationBuilder.Sql($@"UPDATE ""{tablo}"" t SET ""SirketId"" = COALESCE(c.""SirketId"", '11111111-1111-1111-1111-111111111111')
                    FROM ""CariKartlar"" c WHERE c.""Id"" = t.""CariKartId"" AND t.""SirketId"" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_SirketId",
                table: "Siparisler",
                column: "SirketId");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_SirketId",
                table: "Irsaliyeler",
                column: "SirketId");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_SirketId",
                table: "CariHareketler",
                column: "SirketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Siparisler_SirketId",
                table: "Siparisler");

            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_SirketId",
                table: "Irsaliyeler");

            migrationBuilder.DropIndex(
                name: "IX_CariHareketler_SirketId",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "CariHareketler");
        }
    }
}
