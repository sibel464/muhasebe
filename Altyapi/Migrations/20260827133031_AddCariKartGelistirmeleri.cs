using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddCariKartGelistirmeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotografYolu",
                table: "CariKartlar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuncelleyenKullaniciId",
                table: "CariKartlar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OlusturanKullaniciId",
                table: "CariKartlar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_GuncelleyenKullaniciId",
                table: "CariKartlar",
                column: "GuncelleyenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_OlusturanKullaniciId",
                table: "CariKartlar",
                column: "OlusturanKullaniciId");

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartlar_Kullanicilar_GuncelleyenKullaniciId",
                table: "CariKartlar",
                column: "GuncelleyenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartlar_Kullanicilar_OlusturanKullaniciId",
                table: "CariKartlar",
                column: "OlusturanKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CariKartlar_Kullanicilar_GuncelleyenKullaniciId",
                table: "CariKartlar");

            migrationBuilder.DropForeignKey(
                name: "FK_CariKartlar_Kullanicilar_OlusturanKullaniciId",
                table: "CariKartlar");

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_GuncelleyenKullaniciId",
                table: "CariKartlar");

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_OlusturanKullaniciId",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "FotografYolu",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "GuncelleyenKullaniciId",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "OlusturanKullaniciId",
                table: "CariKartlar");
        }
    }
}
