using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class GiderPusulasiKdvKira : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KarsiBelgeNo",
                table: "GiderPusulalari",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvOrani",
                table: "GiderPusulalari",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvTutari",
                table: "GiderPusulalari",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KarsiBelgeNo",
                table: "GiderPusulalari");

            migrationBuilder.DropColumn(
                name: "KdvOrani",
                table: "GiderPusulalari");

            migrationBuilder.DropColumn(
                name: "KdvTutari",
                table: "GiderPusulalari");
        }
    }
}
