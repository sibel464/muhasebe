using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class EBelgeOtomatikGonder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EBelgeOtomatikGonder",
                table: "SistemAyarlari",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SistemAyarlari",
                keyColumn: "Id",
                keyValue: 1,
                column: "EBelgeOtomatikGonder",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EBelgeOtomatikGonder",
                table: "SistemAyarlari");
        }
    }
}
