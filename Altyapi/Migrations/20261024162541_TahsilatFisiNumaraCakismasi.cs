using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class TahsilatFisiNumaraCakismasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_YevmiyeFisleri_SirketId_BelgeTipi_BelgeId",
                table: "YevmiyeFisleri");

            migrationBuilder.CreateIndex(
                name: "IX_YevmiyeFisleri_SirketId_BelgeTipi_BelgeId",
                table: "YevmiyeFisleri",
                columns: new[] { "SirketId", "BelgeTipi", "BelgeId" },
                unique: true,
                filter: "\"BelgeTipi\" NOT IN ('TahsilatOdeme', 'TahsilatOdemeIptal')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_YevmiyeFisleri_SirketId_BelgeTipi_BelgeId",
                table: "YevmiyeFisleri");

            migrationBuilder.CreateIndex(
                name: "IX_YevmiyeFisleri_SirketId_BelgeTipi_BelgeId",
                table: "YevmiyeFisleri",
                columns: new[] { "SirketId", "BelgeTipi", "BelgeId" },
                unique: true);
        }
    }
}
