using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations;

public partial class StabilizasyonEksikIliskiIndeksleri : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Modelde mevcut olan, eski elle yazılmış migration zincirinde eksik kalan nesneler.
        // Var olan kayıtlar değiştirilmez; ilişkisiz veri varsa transaction başarısız olur.
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_MuhasebeBaglantiKodlari_SirketId" ON "MuhasebeBaglantiKodlari" ("SirketId");
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MuhasebeBaglantiKodlari_Sirketler_SirketId' AND conrelid = '"MuhasebeBaglantiKodlari"'::regclass) THEN
                    ALTER TABLE "MuhasebeBaglantiKodlari" ADD CONSTRAINT "FK_MuhasebeBaglantiKodlari_Sirketler_SirketId" FOREIGN KEY ("SirketId") REFERENCES "Sirketler" ("Id") ON DELETE CASCADE;
                END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS "IX_RevaluasyonKayitlari_YevmiyeFisId" ON "RevaluasyonKayitlari" ("YevmiyeFisId");
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_RevaluasyonKayitlari_YevmiyeFisleri_YevmiyeFisId' AND conrelid = '"RevaluasyonKayitlari"'::regclass) THEN
                    ALTER TABLE "RevaluasyonKayitlari" ADD CONSTRAINT "FK_RevaluasyonKayitlari_YevmiyeFisleri_YevmiyeFisId" FOREIGN KEY ("YevmiyeFisId") REFERENCES "YevmiyeFisleri" ("Id") ON DELETE NO ACTION;
                END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS "IX_DepoTransferFisleri_SirketId" ON "DepoTransferFisleri" ("SirketId");
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_DepoTransferFisleri_Sirketler_SirketId' AND conrelid = '"DepoTransferFisleri"'::regclass) THEN
                    ALTER TABLE "DepoTransferFisleri" ADD CONSTRAINT "FK_DepoTransferFisleri_Sirketler_SirketId" FOREIGN KEY ("SirketId") REFERENCES "Sirketler" ("Id") ON DELETE NO ACTION;
                END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS "IX_StokHareketleri_SirketId" ON "StokHareketleri" ("SirketId");
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_StokHareketleri_Sirketler_SirketId' AND conrelid = '"StokHareketleri"'::regclass) THEN
                    ALTER TABLE "StokHareketleri" ADD CONSTRAINT "FK_StokHareketleri_Sirketler_SirketId" FOREIGN KEY ("SirketId") REFERENCES "Sirketler" ("Id") ON DELETE NO ACTION;
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Nesneler onarım öncesinde de var olabileceğinden otomatik silmek güvenli değildir.
        throw new System.NotSupportedException("Şema onarımı otomatik geri alınamaz. Veri koruyan ileri migration kullanın.");
    }
}
