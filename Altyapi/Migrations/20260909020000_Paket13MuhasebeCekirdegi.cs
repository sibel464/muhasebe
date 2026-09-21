using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    public partial class Paket13MuhasebeCekirdegi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Seviye",
                table: "HesapPlanlari",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BakiyeYonu",
                table: "HesapPlanlari",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ParaBirimiKodu",
                table: "HesapPlanlari",
                type: "text",
                nullable: false,
                defaultValue: "TRY");

            migrationBuilder.AddColumn<bool>(
                name: "SistemHesabi",
                table: "HesapPlanlari",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                table: "HesapPlanlari",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FisTuru",
                table: "YevmiyeFisleri",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MaliDonemId",
                table: "YevmiyeFisleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubeId",
                table: "YevmiyeFisleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Referans",
                table: "YevmiyeFisleri",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TersFisId",
                table: "YevmiyeFisleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IptalEdilenFisId",
                table: "YevmiyeFisleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KullaniciId",
                table: "YevmiyeFisleri",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "OlusturanKullaniciAdi",
                table: "YevmiyeFisleri",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BelgeNo",
                table: "YevmiyeSatirlari",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ParaBirimi",
                table: "YevmiyeSatirlari",
                type: "text",
                nullable: false,
                defaultValue: "TRY");

            migrationBuilder.AddColumn<decimal>(
                name: "Kur",
                table: "YevmiyeSatirlari",
                type: "numeric",
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DovizBorc",
                table: "YevmiyeSatirlari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DovizAlacak",
                table: "YevmiyeSatirlari",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CariKartId",
                table: "YevmiyeSatirlari",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinansGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "KasaGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "KasaIslem",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BankaIslem",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "VirmanYap",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinansIptal",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Mutabakat",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "POSYonet",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinansRaporlari",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MuhasebeFisGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MuhasebeFisEkle",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MuhasebeFisDuzenle",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MuhasebeFisOnayla",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MuhasebeFisIptal",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "HesapPlaniYonet",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MizanGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MaliyetHesaplariGoruntule",
                table: "KullaniciYetkileri",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "MuhasebeBaglantiKodlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    KategoriKodu = table.Column<string>(type: "text", nullable: false),
                    KategoriAdi = table.Column<string>(type: "text", nullable: false),
                    VarSayilanHesapKodu = table.Column<string>(type: "text", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuhasebeBaglantiKodlari", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MuhasebeBaglantiKodlari");
            migrationBuilder.DropColumn(name: "Seviye", table: "HesapPlanlari");
            migrationBuilder.DropColumn(name: "BakiyeYonu", table: "HesapPlanlari");
            migrationBuilder.DropColumn(name: "ParaBirimiKodu", table: "HesapPlanlari");
            migrationBuilder.DropColumn(name: "SistemHesabi", table: "HesapPlanlari");
            migrationBuilder.DropColumn(name: "Aciklama", table: "HesapPlanlari");
            migrationBuilder.DropColumn(name: "FisTuru", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "MaliDonemId", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "SubeId", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "Referans", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "TersFisId", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "IptalEdilenFisId", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "KullaniciId", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "OlusturanKullaniciAdi", table: "YevmiyeFisleri");
            migrationBuilder.DropColumn(name: "BelgeNo", table: "YevmiyeSatirlari");
            migrationBuilder.DropColumn(name: "ParaBirimi", table: "YevmiyeSatirlari");
            migrationBuilder.DropColumn(name: "Kur", table: "YevmiyeSatirlari");
            migrationBuilder.DropColumn(name: "DovizBorc", table: "YevmiyeSatirlari");
            migrationBuilder.DropColumn(name: "DovizAlacak", table: "YevmiyeSatirlari");
            migrationBuilder.DropColumn(name: "CariKartId", table: "YevmiyeSatirlari");
            migrationBuilder.DropColumn(name: "MuhasebeFisGoruntule", table: "KullaniciYetkileri");
            migrationBuilder.DropColumn(name: "MuhasebeFisEkle", table: "KullaniciYetkileri");
            migrationBuilder.DropColumn(name: "MuhasebeFisDuzenle", table: "KullaniciYetkileri");
            migrationBuilder.DropColumn(name: "MuhasebeFisOnayla", table: "KullaniciYetkileri");
            migrationBuilder.DropColumn(name: "MuhasebeFisIptal", table: "KullaniciYetkileri");
            migrationBuilder.DropColumn(name: "HesapPlaniYonet", table: "KullaniciYetkileri");
            migrationBuilder.DropColumn(name: "MizanGoruntule", table: "KullaniciYetkileri");
            migrationBuilder.DropColumn(name: "MaliyetHesaplariGoruntule", table: "KullaniciYetkileri");
        }
    }
}
