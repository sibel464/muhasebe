using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddUrunTanimIliskileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerkeziTanimlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Grup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Kod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    KisaAd = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    SiraNo = table.Column<int>(type: "integer", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerkeziTanimlar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerkeziTanimlar_Grup_Kod",
                table: "MerkeziTanimlar",
                columns: new[] { "Grup", "Kod" },
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "BirimTanimiId",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KategoriTanimiId",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarkaTanimiId",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_BirimTanimiId",
                table: "Urunler",
                column: "BirimTanimiId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_KategoriTanimiId",
                table: "Urunler",
                column: "KategoriTanimiId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_MarkaTanimiId",
                table: "Urunler",
                column: "MarkaTanimiId");

            migrationBuilder.AddForeignKey(
                name: "FK_Urunler_MerkeziTanimlar_BirimTanimiId",
                table: "Urunler",
                column: "BirimTanimiId",
                principalTable: "MerkeziTanimlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Urunler_MerkeziTanimlar_KategoriTanimiId",
                table: "Urunler",
                column: "KategoriTanimiId",
                principalTable: "MerkeziTanimlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Urunler_MerkeziTanimlar_MarkaTanimiId",
                table: "Urunler",
                column: "MarkaTanimiId",
                principalTable: "MerkeziTanimlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Urunler_MerkeziTanimlar_BirimTanimiId",
                table: "Urunler");

            migrationBuilder.DropForeignKey(
                name: "FK_Urunler_MerkeziTanimlar_KategoriTanimiId",
                table: "Urunler");

            migrationBuilder.DropForeignKey(
                name: "FK_Urunler_MerkeziTanimlar_MarkaTanimiId",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_BirimTanimiId",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_KategoriTanimiId",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_MarkaTanimiId",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "BirimTanimiId",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "KategoriTanimiId",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "MarkaTanimiId",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_MerkeziTanimlar_Grup_Kod",
                table: "MerkeziTanimlar");

            migrationBuilder.DropTable(
                name: "MerkeziTanimlar");

        }
    }
}
