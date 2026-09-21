using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket14DovizKurRevaluasyon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."DepoTransferFisleri"') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'DepoTransferFisleri'
                             AND column_name = 'SirketId') THEN
                        ALTER TABLE "DepoTransferFisleri" ADD COLUMN "SirketId" uuid;
                    END IF;

                    IF to_regclass('public."StokHareketleri"') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'StokHareketleri'
                             AND column_name = 'SirketId') THEN
                        ALTER TABLE "StokHareketleri" ADD COLUMN "SirketId" uuid;
                    END IF;

                    IF to_regclass('public."StokHareketleri"') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'StokHareketleri'
                             AND column_name = 'IptalEdildi') THEN
                        ALTER TABLE "StokHareketleri" ADD COLUMN "IptalEdildi" boolean NOT NULL DEFAULT false;
                    END IF;

                    IF to_regclass('public."StokHareketleri"') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'StokHareketleri'
                             AND column_name = 'KullaniciId') THEN
                        ALTER TABLE "StokHareketleri" ADD COLUMN "KullaniciId" integer;
                    END IF;

                    IF to_regclass('public."StokHareketleri"') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'StokHareketleri'
                             AND column_name = 'TersKayitId') THEN
                        ALTER TABLE "StokHareketleri" ADD COLUMN "TersKayitId" integer;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "DovizKurlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParaBirimiKodu = table.Column<string>(type: "text", nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KurTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DovizAlis = table.Column<decimal>(type: "numeric", nullable: false),
                    DovizSatis = table.Column<decimal>(type: "numeric", nullable: false),
                    EfektifAlis = table.Column<decimal>(type: "numeric", nullable: false),
                    EfektifSatis = table.Column<decimal>(type: "numeric", nullable: false),
                    Kaynak = table.Column<string>(type: "text", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KullaniciId = table.Column<int>(type: "integer", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DovizKurlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevaluasyonKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaliDonemId = table.Column<int>(type: "integer", nullable: false),
                    DegerlemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ParaBirimiKodu = table.Column<string>(type: "text", nullable: false),
                    Kur = table.Column<decimal>(type: "numeric", nullable: false),
                    ToplamEskiYerel = table.Column<decimal>(type: "numeric", nullable: false),
                    ToplamYeniYerel = table.Column<decimal>(type: "numeric", nullable: false),
                    ToplamKurFarki = table.Column<decimal>(type: "numeric", nullable: false),
                    YevmiyeFisId = table.Column<int>(type: "integer", nullable: true),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    IptalEdilenId = table.Column<int>(type: "integer", nullable: true),
                    KullaniciId = table.Column<int>(type: "integer", nullable: false),
                    OlusturanKullaniciAdi = table.Column<string>(type: "text", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevaluasyonKayitlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevaluasyonKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RevaluasyonKaydiId = table.Column<int>(type: "integer", nullable: false),
                    KaynakTuru = table.Column<string>(type: "text", nullable: false),
                    KaynakId = table.Column<int>(type: "integer", nullable: false),
                    KaynakKodu = table.Column<string>(type: "text", nullable: false),
                    KaynakAciklama = table.Column<string>(type: "text", nullable: false),
                    HesapId = table.Column<int>(type: "integer", nullable: true),
                    HesapKodu = table.Column<string>(type: "text", nullable: false),
                    DovizBakiye = table.Column<decimal>(type: "numeric", nullable: false),
                    EskiDefterDegeri = table.Column<decimal>(type: "numeric", nullable: false),
                    OncekiDegerlemeKuru = table.Column<decimal>(type: "numeric", nullable: false),
                    YeniDegerlemeKuru = table.Column<decimal>(type: "numeric", nullable: false),
                    YeniYerelDeger = table.Column<decimal>(type: "numeric", nullable: false),
                    KurFarki = table.Column<decimal>(type: "numeric", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false),
                    SilinmeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SilinmeNedeni = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevaluasyonKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevaluasyonKalemleri_RevaluasyonKayitlari_RevaluasyonKaydiId",
                        column: x => x.RevaluasyonKaydiId,
                        principalTable: "RevaluasyonKayitlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevaluasyonKalemleri_RevaluasyonKaydiId",
                table: "RevaluasyonKalemleri",
                column: "RevaluasyonKaydiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RevaluasyonKalemleri");
            migrationBuilder.DropTable(name: "RevaluasyonKayitlari");
            migrationBuilder.DropTable(name: "DovizKurlari");
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "StokHareketleri"
                    DROP COLUMN IF EXISTS "TersKayitId",
                    DROP COLUMN IF EXISTS "KullaniciId",
                    DROP COLUMN IF EXISTS "IptalEdildi",
                    DROP COLUMN IF EXISTS "SirketId";
                ALTER TABLE IF EXISTS "DepoTransferFisleri" DROP COLUMN IF EXISTS "SirketId";
                """);
        }
    }
}
