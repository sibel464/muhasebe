using Altyapi.Veri;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations;

[DbContext(typeof(MuhasebeBaglami))]
[Migration("20260921000000_RedactAuditPasswordHash")]
public sealed class RedactAuditPasswordHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "AuditLoglar"
            SET "EskiDegerJson" = CASE
                    WHEN "EskiDegerJson" IS NULL THEN NULL
                    ELSE ("EskiDegerJson"::jsonb - 'SifreHash')::text
                END,
                "YeniDegerJson" = CASE
                    WHEN "YeniDegerJson" IS NULL THEN NULL
                    ELSE ("YeniDegerJson"::jsonb - 'SifreHash')::text
                END
            WHERE "TabloAdi" = 'Kullanici'
              AND ("EskiDegerJson" LIKE '%"SifreHash"%'
                   OR "YeniDegerJson" LIKE '%"SifreHash"%');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Temizlenen parola hash değerleri geri getirilemez.
    }
}
