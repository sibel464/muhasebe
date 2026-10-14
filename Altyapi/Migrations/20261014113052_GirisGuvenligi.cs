using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class GirisGuvenligi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HataliGirisSayisi",
                table: "Kullanicilar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "KilitBitisTarihi",
                table: "Kullanicilar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SifreDegistirmeTarihi",
                table: "Kullanicilar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SifreDegistirmeli",
                table: "Kullanicilar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "HataliGirisSayisi", "KilitBitisTarihi", "SifreDegistirmeTarihi", "SifreDegistirmeli" },
                values: new object[] { 0, null, null, false });

            // Kurulumla gelen varsayılan admin şifresi hâlâ kullanılıyorsa ilk girişte değiştirilmesi zorunludur.
            migrationBuilder.Sql("""
                UPDATE "Kullanicilar" SET "SifreDegistirmeli" = TRUE
                WHERE "SifreHash" = '$2a$11$bFJTJsZZdqJQwi75rB4wM.DQgaRKmvwOSvi0VYrjuKdFj1/QC2UfS';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HataliGirisSayisi",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "KilitBitisTarihi",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "SifreDegistirmeTarihi",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "SifreDegistirmeli",
                table: "Kullanicilar");
        }
    }
}
