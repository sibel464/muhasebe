using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1,
                column: "SifreHash",
                value: "$2a$11$qFm4zP4k4Vv5P5v5P5v5PeP4O4JzY.U25hWz9.eWbQ2yJq6G9qW");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1,
                column: "SifreHash",
                value: "admin");
        }
    }
}
