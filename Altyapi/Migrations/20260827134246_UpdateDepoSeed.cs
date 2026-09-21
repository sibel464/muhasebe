using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDepoSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DepoAdi", "DepoKodu", "DepoTuru", "OlusturmaTarihi" },
                values: new object[] { "Mağaza", "MAGAZA", 1, new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Depolar",
                columns: new[] { "Id", "AktifMi", "DepoAdi", "DepoKodu", "DepoTuru", "GuncellemeTarihi", "OlusturmaTarihi", "Silindi", "SubeId" },
                values: new object[,]
                {
                    { 2, true, "Depo 1", "DEPO1", 2, null, new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, 1 },
                    { 3, true, "Depo 2", "DEPO2", 2, null, new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), false, 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.UpdateData(
                table: "Depolar",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DepoAdi", "DepoKodu", "DepoTuru", "OlusturmaTarihi" },
                values: new object[] { "Merkez Depo", "MERKEZ", 2, new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc) });
        }
    }
}
