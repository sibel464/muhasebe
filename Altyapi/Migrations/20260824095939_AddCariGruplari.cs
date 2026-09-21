using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddCariGruplari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvTelefonu",
                table: "CariKartlar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrupKodu",
                table: "CariKartlar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IsTelefonu",
                table: "CariKartlar",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvTelefonu",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "GrupKodu",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "IsTelefonu",
                table: "CariKartlar");
        }
    }
}
