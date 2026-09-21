using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddYetkiGelistirmeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CekSenetGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CekSenetIslemYap",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DepoGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DepoTransferYap",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "KullaniciYetkileri",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CekSenetGoruntule", "CekSenetIslemYap", "DepoGoruntule", "DepoTransferYap" },
                values: new object[] { true, true, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CekSenetGoruntule",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "CekSenetIslemYap",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "DepoGoruntule",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "DepoTransferYap",
                table: "KullaniciYetkileri");
        }
    }
}
