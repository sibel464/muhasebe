using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCariHareketConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CariHareket_BorcAlacak",
                table: "CariHareketler");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CariHareket_BorcAlacak",
                table: "CariHareketler",
                sql: "(\"Borc\" >= 0 AND \"Alacak\" >= 0) AND ((\"Borc\" = 0 AND \"Alacak\" > 0) OR (\"Borc\" > 0 AND \"Alacak\" = 0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CariHareket_BorcAlacak",
                table: "CariHareketler");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CariHareket_BorcAlacak",
                table: "CariHareketler",
                sql: "(\"Borc\" >= 0 AND \"Alacak\" >= 0) AND ((\"Borc\" = 0 AND \"Alacak\" >= 0) OR (\"Borc\" >= 0 AND \"Alacak\" = 0))");
        }
    }
}
