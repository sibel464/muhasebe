using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankaHareketleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    Tutar = table.Column<decimal>(type: "numeric", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    HesapNo = table.Column<string>(type: "text", nullable: true),
                    BankaAdi = table.Column<string>(type: "text", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankaHareketleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KasaHareketleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    Tutar = table.Column<decimal>(type: "numeric", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: false),
                    Belge = table.Column<string>(type: "text", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KasaHareketleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KullaniciAdi = table.Column<string>(type: "text", nullable: false),
                    SifreHash = table.Column<string>(type: "text", nullable: false),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    Soyad = table.Column<string>(type: "text", nullable: false),
                    Eposta = table.Column<string>(type: "text", nullable: true),
                    Aktif = table.Column<bool>(type: "boolean", nullable: false),
                    Rol = table.Column<string>(type: "text", nullable: false),
                    SonGirisTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Musteriler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    VergiNo = table.Column<string>(type: "text", nullable: true),
                    TcNo = table.Column<string>(type: "text", nullable: true),
                    Telefon = table.Column<string>(type: "text", nullable: true),
                    Eposta = table.Column<string>(type: "text", nullable: true),
                    Adres = table.Column<string>(type: "text", nullable: true),
                    Sehir = table.Column<string>(type: "text", nullable: true),
                    Aktif = table.Column<bool>(type: "boolean", nullable: false),
                    Bakiye = table.Column<decimal>(type: "numeric", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Musteriler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tedarikciler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    VergiNo = table.Column<string>(type: "text", nullable: true),
                    Telefon = table.Column<string>(type: "text", nullable: true),
                    Eposta = table.Column<string>(type: "text", nullable: true),
                    Adres = table.Column<string>(type: "text", nullable: true),
                    Sehir = table.Column<string>(type: "text", nullable: true),
                    Aktif = table.Column<bool>(type: "boolean", nullable: false),
                    Bakiye = table.Column<decimal>(type: "numeric", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tedarikciler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Urunler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Barkod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Kategori = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Birim = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AlisFiyati = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SatisFiyati = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    KdvOrani = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    StokMiktari = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumStok = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Aktif = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Urunler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faturalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaturaNo = table.Column<string>(type: "text", nullable: false),
                    Tur = table.Column<int>(type: "integer", nullable: false),
                    Tarih = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MusteriId = table.Column<int>(type: "integer", nullable: true),
                    TedarikciId = table.Column<int>(type: "integer", nullable: true),
                    AraToplam = table.Column<decimal>(type: "numeric", nullable: false),
                    KdvToplam = table.Column<decimal>(type: "numeric", nullable: false),
                    GenelToplam = table.Column<decimal>(type: "numeric", nullable: false),
                    Notlar = table.Column<string>(type: "text", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faturalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faturalar_Musteriler_MusteriId",
                        column: x => x.MusteriId,
                        principalTable: "Musteriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Faturalar_Tedarikciler_TedarikciId",
                        column: x => x.TedarikciId,
                        principalTable: "Tedarikciler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FaturaKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaturaId = table.Column<int>(type: "integer", nullable: false),
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    Miktar = table.Column<decimal>(type: "numeric", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "numeric", nullable: false),
                    KdvOrani = table.Column<decimal>(type: "numeric", nullable: false),
                    KdvTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "numeric", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaturaKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaturaKalemleri_Faturalar_FaturaId",
                        column: x => x.FaturaId,
                        principalTable: "Faturalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FaturaKalemleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalemleri_FaturaId",
                table: "FaturaKalemleri",
                column: "FaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalemleri_UrunId",
                table: "FaturaKalemleri",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_MusteriId",
                table: "Faturalar",
                column: "MusteriId");

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_TedarikciId",
                table: "Faturalar",
                column: "TedarikciId");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KullaniciAdi",
                table: "Kullanicilar",
                column: "KullaniciAdi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_Kod",
                table: "Urunler",
                column: "Kod",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankaHareketleri");

            migrationBuilder.DropTable(
                name: "FaturaKalemleri");

            migrationBuilder.DropTable(
                name: "KasaHareketleri");

            migrationBuilder.DropTable(
                name: "Kullanicilar");

            migrationBuilder.DropTable(
                name: "Faturalar");

            migrationBuilder.DropTable(
                name: "Urunler");

            migrationBuilder.DropTable(
                name: "Musteriler");

            migrationBuilder.DropTable(
                name: "Tedarikciler");
        }
    }
}
