using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddKullaniciYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KullaniciYetkileri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KullaniciId = table.Column<int>(type: "integer", nullable: false),
                    CariGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    CariEkle = table.Column<bool>(type: "boolean", nullable: false),
                    CariDuzenle = table.Column<bool>(type: "boolean", nullable: false),
                    CariSil = table.Column<bool>(type: "boolean", nullable: false),
                    PersonelGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    PersonelEkle = table.Column<bool>(type: "boolean", nullable: false),
                    PersonelDuzenle = table.Column<bool>(type: "boolean", nullable: false),
                    PersonelSil = table.Column<bool>(type: "boolean", nullable: false),
                    BankaGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    BankaEkle = table.Column<bool>(type: "boolean", nullable: false),
                    BankaDuzenle = table.Column<bool>(type: "boolean", nullable: false),
                    BankaSil = table.Column<bool>(type: "boolean", nullable: false),
                    StokGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    StokEkle = table.Column<bool>(type: "boolean", nullable: false),
                    StokDuzenle = table.Column<bool>(type: "boolean", nullable: false),
                    StokSil = table.Column<bool>(type: "boolean", nullable: false),
                    StokFiyatDegistir = table.Column<bool>(type: "boolean", nullable: false),
                    SatisYap = table.Column<bool>(type: "boolean", nullable: false),
                    SatisIptalEt = table.Column<bool>(type: "boolean", nullable: false),
                    SatisBeklet = table.Column<bool>(type: "boolean", nullable: false),
                    FisYazdir = table.Column<bool>(type: "boolean", nullable: false),
                    SatisGecmisiGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    KasaYonet = table.Column<bool>(type: "boolean", nullable: false),
                    TahsilatYap = table.Column<bool>(type: "boolean", nullable: false),
                    OdemeYap = table.Column<bool>(type: "boolean", nullable: false),
                    GelirEkle = table.Column<bool>(type: "boolean", nullable: false),
                    GiderEkle = table.Column<bool>(type: "boolean", nullable: false),
                    RaporGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    FinansRaporuGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    StokRaporuGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    SatisRaporuGoruntule = table.Column<bool>(type: "boolean", nullable: false),
                    AyarlariYonet = table.Column<bool>(type: "boolean", nullable: false),
                    KullaniciYonet = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KullaniciYetkileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KullaniciYetkileri_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "KullaniciYetkileri",
                columns: new[] { "Id", "AyarlariYonet", "BankaDuzenle", "BankaEkle", "BankaGoruntule", "BankaSil", "CariDuzenle", "CariEkle", "CariGoruntule", "CariSil", "FinansRaporuGoruntule", "FisYazdir", "GelirEkle", "GiderEkle", "GuncellemeTarihi", "KasaYonet", "KullaniciId", "KullaniciYonet", "OdemeYap", "OlusturmaTarihi", "PersonelDuzenle", "PersonelEkle", "PersonelGoruntule", "PersonelSil", "RaporGoruntule", "SatisBeklet", "SatisGecmisiGoruntule", "SatisIptalEt", "SatisRaporuGoruntule", "SatisYap", "Silindi", "StokDuzenle", "StokEkle", "StokFiyatDegistir", "StokGoruntule", "StokRaporuGoruntule", "StokSil", "TahsilatYap" },
                values: new object[] { 1, true, true, true, true, true, true, true, true, true, true, true, true, true, null, true, 1, true, true, new DateTime(2026, 8, 23, 0, 0, 0, 0, DateTimeKind.Utc), true, true, true, true, true, true, true, true, true, true, false, true, true, true, true, true, true, true });

            migrationBuilder.UpdateData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1,
                column: "SifreHash",
                value: "$2a$11$bFJTJsZZdqJQwi75rB4wM.DQgaRKmvwOSvi0VYrjuKdFj1/QC2UfS");

            migrationBuilder.CreateIndex(
                name: "IX_KullaniciYetkileri_KullaniciId",
                table: "KullaniciYetkileri",
                column: "KullaniciId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KullaniciYetkileri");

            migrationBuilder.UpdateData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1,
                column: "SifreHash",
                value: "$2a$11$qFm4zP4k4Vv5P5v5P5v5PeP4O4JzY.U25hWz9.eWbQ2yJq6G9qW");
        }
    }
}
