using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddDinamikCariTipleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Tip",
                table: "CariKartlar",
                newName: "CariTipiId");

            migrationBuilder.CreateTable(
                name: "CariTipiTanimlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    SiraNo = table.Column<int>(type: "integer", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CariTipiTanimlari", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CariTipiTanimlari",
                columns: new[] { "Id", "Aciklama", "Ad", "AktifMi", "GuncellemeTarihi", "Kod", "OlusturmaTarihi", "Silindi", "SiraNo" },
                values: new object[,]
                {
                    { 1, null, "Şahıs / Bireysel", true, null, "BIREYSEL", new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, 20 },
                    { 2, null, "Kurumsal / Şirket", true, null, "KURUMSAL", new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, 10 }
                });

            // Eski enum: Bireysel=0, Kurumsal=1. Yeni tanım kimlikleri: 1 ve 2.
            migrationBuilder.Sql("""
                UPDATE "CariKartlar"
                SET "CariTipiId" = CASE "CariTipiId"
                    WHEN 0 THEN 1
                    WHEN 1 THEN 2
                    ELSE 2
                END;
                SELECT setval(pg_get_serial_sequence('"CariTipiTanimlari"', 'Id'),
                    (SELECT MAX("Id") FROM "CariTipiTanimlari"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_CariTipiId",
                table: "CariKartlar",
                column: "CariTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_CariTipiTanimlari_Kod",
                table: "CariTipiTanimlari",
                column: "Kod",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartlar_CariTipiTanimlari_CariTipiId",
                table: "CariKartlar",
                column: "CariTipiId",
                principalTable: "CariTipiTanimlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CariKartlar_CariTipiTanimlari_CariTipiId",
                table: "CariKartlar");

            migrationBuilder.Sql("""
                UPDATE "CariKartlar"
                SET "CariTipiId" = CASE "CariTipiId"
                    WHEN 1 THEN 0
                    WHEN 2 THEN 1
                    ELSE 1
                END;
                """);

            migrationBuilder.DropTable(
                name: "CariTipiTanimlari");

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_CariTipiId",
                table: "CariKartlar");

            migrationBuilder.RenameColumn(
                name: "CariTipiId",
                table: "CariKartlar",
                newName: "Tip");
        }
    }
}
