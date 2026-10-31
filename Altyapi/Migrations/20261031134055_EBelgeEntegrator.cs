using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class EBelgeEntegrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EBelgeEntegrator",
                table: "SistemAyarlari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EBelgeKullanici",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EBelgeSifre",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EBelgeTestOrtami",
                table: "SistemAyarlari",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SistemAyarlari",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EBelgeEntegrator", "EBelgeKullanici", "EBelgeSifre", "EBelgeTestOrtami" },
                values: new object[] { 0, null, null, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EBelgeEntegrator",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "EBelgeKullanici",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "EBelgeSifre",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "EBelgeTestOrtami",
                table: "SistemAyarlari");
        }
    }
}
