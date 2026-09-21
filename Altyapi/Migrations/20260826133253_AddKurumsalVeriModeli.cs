using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class AddKurumsalVeriModeli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepoId",
                table: "Urunler",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "Urunler",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaliDonemId",
                table: "Faturalar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParaBirimiId",
                table: "Faturalar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "Faturalar",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubeId",
                table: "Faturalar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SirketId",
                table: "CariKartlar",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubeId",
                table: "CariKartlar",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KdvOranlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Oran = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KdvOranlari", x => x.Id);
                    table.CheckConstraint("CK_KdvOrani_Oran", "\"Oran\" >= 0 AND \"Oran\" <= 100");
                });

            migrationBuilder.CreateTable(
                name: "ParaBirimleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Ad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sembol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    VarsayilanMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParaBirimleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sirketler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SirketKodu = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Unvan = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    VergiNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VergiDairesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sirketler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BelgeSerileri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    BelgeTipi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Seri = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SonNumara = table.Column<long>(type: "bigint", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BelgeSerileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BelgeSerileri_Sirketler_SirketId",
                        column: x => x.SirketId,
                        principalTable: "Sirketler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaliDonemler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    DonemAdi = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Baslangic = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Bitis = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KilitliMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaliDonemler", x => x.Id);
                    table.CheckConstraint("CK_MaliDonem_Tarih", "\"Baslangic\" <= \"Bitis\"");
                    table.ForeignKey(
                        name: "FK_MaliDonemler_Sirketler_SirketId",
                        column: x => x.SirketId,
                        principalTable: "Sirketler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SirketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubeKodu = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubeAdi = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subeler_Sirketler_SirketId",
                        column: x => x.SirketId,
                        principalTable: "Sirketler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Depolar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubeId = table.Column<int>(type: "integer", nullable: false),
                    DepoKodu = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DepoAdi = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AktifMi = table.Column<bool>(type: "boolean", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Silindi = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Depolar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Depolar_Subeler_SubeId",
                        column: x => x.SubeId,
                        principalTable: "Subeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "KdvOranlari",
                columns: new[] { "Id", "AktifMi", "GuncellemeTarihi", "Kod", "OlusturmaTarihi", "Oran", "Silindi" },
                values: new object[] { 1, true, null, "KDV20", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), 20m, false });

            migrationBuilder.InsertData(
                table: "ParaBirimleri",
                columns: new[] { "Id", "Ad", "GuncellemeTarihi", "Kod", "OlusturmaTarihi", "Sembol", "Silindi", "VarsayilanMi" },
                values: new object[] { 1, "Türk Lirası", null, "TRY", new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), "₺", false, true });

            migrationBuilder.InsertData(
                table: "Sirketler",
                columns: new[] { "Id", "Adres", "AktifMi", "Email", "OlusturmaTarihi", "SirketKodu", "Telefon", "Unvan", "VergiDairesi", "VergiNo" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), null, true, null, new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), "MERKEZ", null, "Varsayılan Şirket", null, null });

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DepoId", "SirketId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Urunler",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DepoId", "SirketId" },
                values: new object[] { null, null });

            migrationBuilder.InsertData(
                table: "BelgeSerileri",
                columns: new[] { "Id", "BelgeTipi", "Format", "GuncellemeTarihi", "OlusturmaTarihi", "Prefix", "Seri", "Silindi", "SirketId", "SonNumara" },
                values: new object[] { 1, "Fatura", "{0}{1:000000}", null, new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), "FTR", "FTR", false, new Guid("11111111-1111-1111-1111-111111111111"), 0L });

            migrationBuilder.InsertData(
                table: "MaliDonemler",
                columns: new[] { "Id", "Baslangic", "Bitis", "DonemAdi", "GuncellemeTarihi", "KilitliMi", "OlusturmaTarihi", "Silindi", "SirketId" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc), "2026", null, false, new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.InsertData(
                table: "Subeler",
                columns: new[] { "Id", "Adres", "AktifMi", "GuncellemeTarihi", "OlusturmaTarihi", "Silindi", "SirketId", "SubeAdi", "SubeKodu", "Telefon" },
                values: new object[] { 1, null, true, null, new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, new Guid("11111111-1111-1111-1111-111111111111"), "Merkez Şube", "MERKEZ", null });

            migrationBuilder.InsertData(
                table: "Depolar",
                columns: new[] { "Id", "AktifMi", "DepoAdi", "DepoKodu", "GuncellemeTarihi", "OlusturmaTarihi", "Silindi", "SubeId" },
                values: new object[] { 1, true, "Merkez Depo", "MERKEZ", null, new DateTime(2026, 8, 26, 0, 0, 0, 0, DateTimeKind.Utc), false, 1 });

            migrationBuilder.Sql("UPDATE \"CariKartlar\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111', \"SubeId\" = 1 WHERE \"SirketId\" IS NULL OR \"SubeId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Urunler\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111', \"DepoId\" = 1 WHERE \"SirketId\" IS NULL OR \"DepoId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Faturalar\" SET \"SirketId\" = '11111111-1111-1111-1111-111111111111', \"SubeId\" = 1, \"MaliDonemId\" = 1, \"ParaBirimiId\" = 1 WHERE \"SirketId\" IS NULL OR \"SubeId\" IS NULL OR \"MaliDonemId\" IS NULL OR \"ParaBirimiId\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_DepoId",
                table: "Urunler",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_SirketId_DepoId",
                table: "Urunler",
                columns: new[] { "SirketId", "DepoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_MaliDonemId",
                table: "Faturalar",
                column: "MaliDonemId");

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_ParaBirimiId",
                table: "Faturalar",
                column: "ParaBirimiId");

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_SirketId_MaliDonemId",
                table: "Faturalar",
                columns: new[] { "SirketId", "MaliDonemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_SirketId_SubeId_Tarih",
                table: "Faturalar",
                columns: new[] { "SirketId", "SubeId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_SubeId",
                table: "Faturalar",
                column: "SubeId");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_SirketId_SubeId",
                table: "CariKartlar",
                columns: new[] { "SirketId", "SubeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_SubeId",
                table: "CariKartlar",
                column: "SubeId");

            migrationBuilder.CreateIndex(
                name: "IX_BelgeSerileri_SirketId_BelgeTipi_Seri",
                table: "BelgeSerileri",
                columns: new[] { "SirketId", "BelgeTipi", "Seri" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_SubeId_DepoKodu",
                table: "Depolar",
                columns: new[] { "SubeId", "DepoKodu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KdvOranlari_Kod",
                table: "KdvOranlari",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaliDonemler_SirketId_DonemAdi",
                table: "MaliDonemler",
                columns: new[] { "SirketId", "DonemAdi" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParaBirimleri_Kod",
                table: "ParaBirimleri",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sirketler_SirketKodu",
                table: "Sirketler",
                column: "SirketKodu",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subeler_SirketId_SubeKodu",
                table: "Subeler",
                columns: new[] { "SirketId", "SubeKodu" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartlar_Sirketler_SirketId",
                table: "CariKartlar",
                column: "SirketId",
                principalTable: "Sirketler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartlar_Subeler_SubeId",
                table: "CariKartlar",
                column: "SubeId",
                principalTable: "Subeler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_MaliDonemler_MaliDonemId",
                table: "Faturalar",
                column: "MaliDonemId",
                principalTable: "MaliDonemler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_ParaBirimleri_ParaBirimiId",
                table: "Faturalar",
                column: "ParaBirimiId",
                principalTable: "ParaBirimleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_Sirketler_SirketId",
                table: "Faturalar",
                column: "SirketId",
                principalTable: "Sirketler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_Subeler_SubeId",
                table: "Faturalar",
                column: "SubeId",
                principalTable: "Subeler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Urunler_Depolar_DepoId",
                table: "Urunler",
                column: "DepoId",
                principalTable: "Depolar",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Urunler_Sirketler_SirketId",
                table: "Urunler",
                column: "SirketId",
                principalTable: "Sirketler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CariKartlar_Sirketler_SirketId",
                table: "CariKartlar");

            migrationBuilder.DropForeignKey(
                name: "FK_CariKartlar_Subeler_SubeId",
                table: "CariKartlar");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_MaliDonemler_MaliDonemId",
                table: "Faturalar");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_ParaBirimleri_ParaBirimiId",
                table: "Faturalar");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_Sirketler_SirketId",
                table: "Faturalar");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_Subeler_SubeId",
                table: "Faturalar");

            migrationBuilder.DropForeignKey(
                name: "FK_Urunler_Depolar_DepoId",
                table: "Urunler");

            migrationBuilder.DropForeignKey(
                name: "FK_Urunler_Sirketler_SirketId",
                table: "Urunler");

            migrationBuilder.DropTable(
                name: "BelgeSerileri");

            migrationBuilder.DropTable(
                name: "Depolar");

            migrationBuilder.DropTable(
                name: "KdvOranlari");

            migrationBuilder.DropTable(
                name: "MaliDonemler");

            migrationBuilder.DropTable(
                name: "ParaBirimleri");

            migrationBuilder.DropTable(
                name: "Subeler");

            migrationBuilder.DropTable(
                name: "Sirketler");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_DepoId",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_Urunler_SirketId_DepoId",
                table: "Urunler");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_MaliDonemId",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_ParaBirimiId",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_SirketId_MaliDonemId",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_SirketId_SubeId_Tarih",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_SubeId",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_SirketId_SubeId",
                table: "CariKartlar");

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_SubeId",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "DepoId",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "Urunler");

            migrationBuilder.DropColumn(
                name: "MaliDonemId",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "ParaBirimiId",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "SubeId",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "SirketId",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "SubeId",
                table: "CariKartlar");
        }
    }
}
