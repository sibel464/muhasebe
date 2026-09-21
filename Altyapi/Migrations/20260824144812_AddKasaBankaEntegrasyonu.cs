using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddKasaBankaEntegrasyonu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Tutar",
                table: "KasaHareketleri",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Belge",
                table: "KasaHareketleri",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Aciklama",
                table: "KasaHareketleri",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "CariKartId",
                table: "KasaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FaturaId",
                table: "KasaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IlgiliBelgeId",
                table: "KasaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Tutar",
                table: "BankaHareketleri",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "HesapNo",
                table: "BankaHareketleri",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankaAdi",
                table: "BankaHareketleri",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Aciklama",
                table: "BankaHareketleri",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "CariKartId",
                table: "BankaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FaturaId",
                table: "BankaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IlgiliBelgeId",
                table: "BankaHareketleri",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_CariKartId",
                table: "KasaHareketleri",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_Tarih",
                table: "KasaHareketleri",
                column: "Tarih");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KasaHareketi_Tutar",
                table: "KasaHareketleri",
                sql: "\"Tutar\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_CariKartId",
                table: "BankaHareketleri",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_Tarih",
                table: "BankaHareketleri",
                column: "Tarih");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BankaHareketi_Tutar",
                table: "BankaHareketleri",
                sql: "\"Tutar\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_BankaHareketleri_CariKartlar_CariKartId",
                table: "BankaHareketleri",
                column: "CariKartId",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KasaHareketleri_CariKartlar_CariKartId",
                table: "KasaHareketleri",
                column: "CariKartId",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankaHareketleri_CariKartlar_CariKartId",
                table: "BankaHareketleri");

            migrationBuilder.DropForeignKey(
                name: "FK_KasaHareketleri_CariKartlar_CariKartId",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketleri_CariKartId",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketleri_Tarih",
                table: "KasaHareketleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KasaHareketi_Tutar",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_BankaHareketleri_CariKartId",
                table: "BankaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_BankaHareketleri_Tarih",
                table: "BankaHareketleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BankaHareketi_Tutar",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "CariKartId",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "FaturaId",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "IlgiliBelgeId",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "CariKartId",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "FaturaId",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "IlgiliBelgeId",
                table: "BankaHareketleri");

            migrationBuilder.AlterColumn<decimal>(
                name: "Tutar",
                table: "KasaHareketleri",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Belge",
                table: "KasaHareketleri",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Aciklama",
                table: "KasaHareketleri",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<decimal>(
                name: "Tutar",
                table: "BankaHareketleri",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "HesapNo",
                table: "BankaHareketleri",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankaAdi",
                table: "BankaHareketleri",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Aciklama",
                table: "BankaHareketleri",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }
    }
}
