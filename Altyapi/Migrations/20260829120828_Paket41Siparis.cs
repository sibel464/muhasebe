using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket41Siparis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Siparisler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiparisNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SiparisTuru = table.Column<int>(type: "integer", nullable: false),
                    CariKartId = table.Column<int>(type: "integer", nullable: false),
                    SiparisTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeslimTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Durum = table.Column<int>(type: "integer", nullable: false),
                    TeslimatAdSoyad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TeslimatTelefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TeslimatAdres = table.Column<string>(type: "text", nullable: false),
                    TeslimatIl = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TeslimatIlce = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParaBirimiId = table.Column<int>(type: "integer", nullable: false),
                    Kur = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    VadeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OdemeSekli = table.Column<int>(type: "integer", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    ReferansNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BrutToplam = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IskontoToplami = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AraToplam = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    KdvToplami = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GenelToplam = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OlusturanKullaniciId = table.Column<int>(type: "integer", nullable: false),
                    GuncelleyenKullaniciId = table.Column<int>(type: "integer", nullable: true),
                    Versiyon = table.Column<int>(type: "integer", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Siparisler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Siparisler_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_Kullanicilar_GuncelleyenKullaniciId",
                        column: x => x.GuncelleyenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_ParaBirimleri_ParaBirimiId",
                        column: x => x.ParaBirimiId,
                        principalTable: "ParaBirimleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiparisKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiparisId = table.Column<int>(type: "integer", nullable: false),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    UrunKodu = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Barkod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UrunAdi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Birim = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IskontoOrani = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IskontoTutari = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    KdvOrani = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    KdvTutari = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SatirToplami = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SevkEdilenMiktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FaturalananMiktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiparisKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiparisKalemleri_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SiparisKalemleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiparisKalemleri_SiparisId",
                table: "SiparisKalemleri",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisKalemleri_UrunId",
                table: "SiparisKalemleri",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_CariKartId",
                table: "Siparisler",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_GuncelleyenKullaniciId",
                table: "Siparisler",
                column: "GuncelleyenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_OlusturanKullaniciId",
                table: "Siparisler",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_ParaBirimiId",
                table: "Siparisler",
                column: "ParaBirimiId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_SiparisNo",
                table: "Siparisler",
                column: "SiparisNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_SiparisTarihi_SiparisTuru_Durum",
                table: "Siparisler",
                columns: new[] { "SiparisTarihi", "SiparisTuru", "Durum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiparisKalemleri");

            migrationBuilder.DropTable(
                name: "Siparisler");
        }
    }
}
