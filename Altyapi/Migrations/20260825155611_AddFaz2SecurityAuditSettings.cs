using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddFaz2SecurityAuditSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Versiyon",
                table: "Urunler",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AuditLogGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FaturaGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FaturaIptal",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FaturaOlustur",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FaturaOnayla",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IrsaliyeGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IrsaliyeIptal",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IrsaliyeOlustur",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IrsaliyeOnayla",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Versiyon",
                table: "Irsaliyeler",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditLoglar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KullaniciId = table.Column<int>(type: "integer", nullable: true),
                    KullaniciAdi = table.Column<string>(type: "text", nullable: true),
                    IslemTuru = table.Column<int>(type: "integer", nullable: false),
                    TabloAdi = table.Column<string>(type: "text", nullable: false),
                    KayitId = table.Column<string>(type: "text", nullable: false),
                    EskiDegerJson = table.Column<string>(type: "text", nullable: true),
                    YeniDegerJson = table.Column<string>(type: "text", nullable: true),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IstemciBilgisi = table.Column<string>(type: "text", nullable: true),
                    IslemDetayi = table.Column<string>(type: "text", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLoglar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SistemAyarlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirmaUnvani = table.Column<string>(type: "text", nullable: false),
                    VergiDairesi = table.Column<string>(type: "text", nullable: true),
                    VergiNo = table.Column<string>(type: "text", nullable: true),
                    Telefon = table.Column<string>(type: "text", nullable: true),
                    Eposta = table.Column<string>(type: "text", nullable: true),
                    WebSitesi = table.Column<string>(type: "text", nullable: true),
                    Adres = table.Column<string>(type: "text", nullable: true),
                    FisHeader = table.Column<string>(type: "text", nullable: true),
                    FisFooter = table.Column<string>(type: "text", nullable: true),
                    VarsayilanKdv = table.Column<decimal>(type: "numeric", nullable: false),
                    ParaBirimiSembolu = table.Column<string>(type: "text", nullable: false),
                    ParaBirimiKodu = table.Column<string>(type: "text", nullable: false),
                    VarsayilanVadeGunu = table.Column<int>(type: "integer", nullable: false),
                    FaturaNotu = table.Column<string>(type: "text", nullable: true),
                    IrsaliyeNotu = table.Column<string>(type: "text", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SistemAyarlari", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "KullaniciYetkileri",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AuditLogGoruntule", "FaturaGoruntule", "FaturaIptal", "FaturaOlustur", "FaturaOnayla", "IrsaliyeGoruntule", "IrsaliyeIptal", "IrsaliyeOlustur", "IrsaliyeOnayla" },
                values: new object[] { true, true, true, true, true, true, true, true, true });

            migrationBuilder.InsertData(
                table: "SistemAyarlari",
                columns: new[] { "Id", "Adres", "Eposta", "FaturaNotu", "FirmaUnvani", "FisFooter", "FisHeader", "GuncellemeTarihi", "IrsaliyeNotu", "OlusturmaTarihi", "ParaBirimiKodu", "ParaBirimiSembolu", "Silindi", "Telefon", "VarsayilanKdv", "VarsayilanVadeGunu", "VergiDairesi", "VergiNo", "WebSitesi" },
                values: new object[] { 1, "Atatürk Cad. No: 15/A Kadıköy / İstanbul", "info@ornekticaret.com", "Mal tesliminde faturanın ibrazı zorunludur.", "Örnek Ticaret Ltd. Şti.", "Bizi tercih ettiğiniz için teşekkür ederiz!", "Örnek Ticaret Ltd. Şti.", null, "İrsaliye muhteviyatı ürünler eksiksiz teslim alınmıştır.", new DateTime(2026, 8, 23, 0, 0, 0, 0, DateTimeKind.Utc), "TRY", "₺", false, "0216 555 00 00", 20.00m, 30, "Kadıköy V.D.", "1234567890", "www.ornekticaret.com" });

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 1,
                column: "Versiyon",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 2,
                column: "Versiyon",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLoglar");

            migrationBuilder.DropTable(
                name: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "Versiyon",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "AuditLogGoruntule",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "FaturaGoruntule",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "FaturaIptal",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "FaturaOlustur",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "FaturaOnayla",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "IrsaliyeGoruntule",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "IrsaliyeIptal",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "IrsaliyeOlustur",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "IrsaliyeOnayla",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "Versiyon",
                table: "Irsaliyeler");
        }
    }
}
