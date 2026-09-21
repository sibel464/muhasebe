using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddSilmeNedeni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "YevmiyeSatirlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "YevmiyeSatirlari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "YevmiyeFisleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "YevmiyeFisleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "UrunSatisFiyatlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "UrunSatisFiyatlari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "UrunLokasyonStoklari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "UrunLokasyonStoklari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "Urunler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Urunler",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "Subeler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Subeler",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "StokHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "StokHareketleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "SistemAyarlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "SistemAyarlari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "Siparisler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Siparisler",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "SiparisKalemleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "SiparisKalemleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "ParaBirimleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "ParaBirimleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "MerkeziTanimlar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "MerkeziTanimlar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "MaliDonemler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "MaliDonemler",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "KullaniciYetkileri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "KullaniciYetkileri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "Kullanicilar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Kullanicilar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "KdvOranlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "KdvOranlari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "KasaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "KasaHareketleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "Irsaliyeler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Irsaliyeler",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "IrsaliyeKalemleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "IrsaliyeKalemleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "IrsaliyeKalemiStokDagilimlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "IrsaliyeKalemiStokDagilimlari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "HesapPlanlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "HesapPlanlari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "Faturalar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Faturalar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "FaturaKalemleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "FaturaKalemleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "FaturaKalemiStokDagilimleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "FaturaKalemiStokDagilimleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "Depolar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Depolar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "CariTipiTanimlari",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "CariTipiTanimlari",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "CariKartlar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "CariKartlar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "CariHareketler",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "CariHareketler",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "BelgeSerileri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "BelgeSerileri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "BankaHareketleri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "BankaHareketleri",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SilinmeNedeni",
                table: "AuditLoglar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "AuditLoglar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "BelgeSerileri",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "CariTipiTanimlari",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "CariTipiTanimlari",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 100,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 102,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 120,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 153,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 191,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 320,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 391,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "HesapPlanlari",
                keyColumn: "Id",
                keyValue: 600,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "KdvOranlari",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "KullaniciYetkileri",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "MaliDonemler",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ParaBirimleri",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "SistemAyarlari",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Subeler",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "SilinmeNedeni", "SilinmeTarihi" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "YevmiyeSatirlari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "YevmiyeSatirlari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "YevmiyeFisleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "YevmiyeFisleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "UrunSatisFiyatlari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "UrunSatisFiyatlari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "UrunLokasyonStoklari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "UrunLokasyonStoklari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "Subeler");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Subeler");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "SistemAyarlari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Siparisler");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "SiparisKalemleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "SiparisKalemleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "ParaBirimleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "ParaBirimleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "MerkeziTanimlar");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "MerkeziTanimlar");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "MaliDonemler");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "MaliDonemler");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "KullaniciYetkileri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "KdvOranlari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "KdvOranlari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "IrsaliyeKalemleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "IrsaliyeKalemleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "IrsaliyeKalemiStokDagilimlari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "IrsaliyeKalemiStokDagilimlari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "HesapPlanlari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "HesapPlanlari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "FaturaKalemiStokDagilimleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "FaturaKalemiStokDagilimleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "CariTipiTanimlari");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "CariTipiTanimlari");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "BelgeSerileri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "BelgeSerileri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "SilinmeNedeni",
                table: "AuditLoglar");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "AuditLoglar");
        }
    }
}
