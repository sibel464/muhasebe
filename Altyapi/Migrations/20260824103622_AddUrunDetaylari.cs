using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddUrunDetaylari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SatisFiyati",
                table: "Urunler",
                newName: "ToptanSatisFiyati");

            migrationBuilder.AddColumn<decimal>(
                name: "PerakendeSatisFiyati",
                table: "Urunler",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ResimYolu",
                table: "Urunler",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PerakendeSatisFiyati", "ResimYolu", "ToptanSatisFiyati" },
                values: new object[] { 65.0000m, null, 60.0000m });

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PerakendeSatisFiyati", "ResimYolu", "ToptanSatisFiyati" },
                values: new object[] { 399.0000m, null, 350.0000m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerakendeSatisFiyati",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "ResimYolu",
                table: "Urunler");

            migrationBuilder.RenameColumn(
                name: "ToptanSatisFiyati",
                table: "Urunler",
                newName: "SatisFiyati");

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 1,
                column: "SatisFiyati",
                value: 65.0000m);

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 2,
                column: "SatisFiyati",
                value: 399.0000m);
        }
    }
}
