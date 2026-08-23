using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Kullanicilar",
                columns: new[] { "Id", "Ad", "Aktif", "Eposta", "GuncellemeTarihi", "KullaniciAdi", "OlusturmaTarihi", "Rol", "SifreHash", "Silindi", "SonGirisTarihi", "Soyad" },
                values: new object[] { 1, "Sistem", true, null, null, "admin", new DateTime(2026, 8, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Yonetici", "admin", false, null, "Yöneticisi" });

            migrationBuilder.InsertData(
                table: "Urunler",
                columns: new[] { "Id", "Ad", "Aktif", "AlisFiyati", "Barkod", "Birim", "GuncellemeTarihi", "Kategori", "KdvOrani", "Kod", "MinimumStok", "OlusturmaTarihi", "SatisFiyati", "Silindi", "StokMiktari" },
                values: new object[,]
                {
                    { 1, "A4 Kağıt (500'lü)", true, 45.0000m, null, "Paket", null, "Kırtasiye", 20.00m, "URN001", 20.0000m, new DateTime(2026, 8, 23, 0, 0, 0, 0, DateTimeKind.Utc), 65.0000m, false, 150.0000m },
                    { 2, "Laptop Çantası", true, 250.0000m, null, "Adet", null, "Elektronik", 20.00m, "URN002", 5.0000m, new DateTime(2026, 8, 23, 0, 0, 0, 0, DateTimeKind.Utc), 399.0000m, false, 25.0000m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Kullanicilar",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
