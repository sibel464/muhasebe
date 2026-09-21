using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Altyapi.Veri;

#nullable disable
namespace Altyapi.Migrations;

[DbContext(typeof(MuhasebeBaglami))]
[Migration("20260902223000_Paket6FirmaKodSequence")]
public partial class Paket6FirmaKodSequence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE SEQUENCE IF NOT EXISTS firma_kodu_seq START WITH 1;
        SELECT setval('firma_kodu_seq', GREATEST(
            COALESCE((SELECT MAX(CAST(SUBSTRING("SirketKodu" FROM 5) AS bigint)) FROM "Sirketler" WHERE "SirketKodu" ~ '^FRM-[0-9]+$'), 0) + 1,
            (SELECT last_value FROM firma_kodu_seq)), false);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP SEQUENCE IF EXISTS firma_kodu_seq;");
}
