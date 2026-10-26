using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class KdvTevkifati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TevkifatKodu",
                table: "Faturalar",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TevkifatOrani",
                table: "Faturalar",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TevkifatTutari",
                table: "Faturalar",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TevkifatKodu",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "TevkifatOrani",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "TevkifatTutari",
                table: "Faturalar");
        }
    }
}
