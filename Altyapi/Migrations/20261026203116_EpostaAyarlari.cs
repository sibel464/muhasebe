using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class EpostaAyarlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EpostaGonderenAdi",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpGuvenlik",
                table: "SistemAyarlari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SmtpKullanici",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "SistemAyarlari",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SmtpSifre",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpSunucu",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SistemAyarlari",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EpostaGonderenAdi", "SmtpGuvenlik", "SmtpKullanici", "SmtpPort", "SmtpSifre", "SmtpSunucu" },
                values: new object[] { null, 0, null, 587, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EpostaGonderenAdi",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "SmtpGuvenlik",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "SmtpKullanici",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "SmtpSifre",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "SmtpSunucu",
                table: "SistemAyarlari");
        }
    }
}
