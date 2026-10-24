using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class DurumKayitlariSatirSurumu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "YevmiyeFisleri",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "StokSayimlari",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RevaluasyonKayitlari",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PosIslemleri",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MaliDonemler",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "KasaSayimMutabakatlari",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "KasaHareketleri",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "EBelgeKayitlari",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "DepoTransferFisleri",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "CekSenetler",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "CariHareketler",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "BelgeKapamalar",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "BankaHareketleri",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "YevmiyeFisleri");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "StokSayimlari");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RevaluasyonKayitlari");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PosIslemleri");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MaliDonemler");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "KasaSayimMutabakatlari");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "EBelgeKayitlari");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "DepoTransferFisleri");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "CekSenetler");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "BelgeKapamalar");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "BankaHareketleri");
        }
    }
}
