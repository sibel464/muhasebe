using Altyapi.Veri;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    // Denetim kaydı (kim, ne zaman, neyi değiştirdi) veritabanı düzeyinde korunur: satır silinemez, tablo boşaltılamaz,
    // kaydın kimliği/tarihi/kullanıcısı/işlemi değiştirilemez. Yalnız KVKK maskelemesi için eski/yeni değer alanları
    // (ve güncelleme tarihi) değişebilir. Program hatası ya da elle yapılan bir işlem denetim izini sessizce bozamaz.
    [DbContext(typeof(MuhasebeBaglami))]
    [Migration("20261125120000_DenetimKaydiKorumasi")]
    public partial class DenetimKaydiKorumasi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION denetim_kaydi_koru() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' OR TG_OP = 'TRUNCATE' THEN
                        RAISE EXCEPTION 'Denetim kaydı silinemez.' USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    IF (to_jsonb(NEW) - ARRAY['EskiDegerJson','YeniDegerJson','GuncellemeTarihi'])
                       IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['EskiDegerJson','YeniDegerJson','GuncellemeTarihi']) THEN
                        RAISE EXCEPTION 'Denetim kaydı değiştirilemez.' USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    RETURN NEW;
                END $$;

                DROP TRIGGER IF EXISTS "TR_AuditLoglar_Koruma" ON "AuditLoglar";
                CREATE TRIGGER "TR_AuditLoglar_Koruma" BEFORE UPDATE OR DELETE ON "AuditLoglar"
                    FOR EACH ROW EXECUTE FUNCTION denetim_kaydi_koru();

                DROP TRIGGER IF EXISTS "TR_AuditLoglar_Bosaltma" ON "AuditLoglar";
                CREATE TRIGGER "TR_AuditLoglar_Bosaltma" BEFORE TRUNCATE ON "AuditLoglar"
                    FOR EACH STATEMENT EXECUTE FUNCTION denetim_kaydi_koru();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_AuditLoglar_Bosaltma" ON "AuditLoglar";
                DROP TRIGGER IF EXISTS "TR_AuditLoglar_Koruma" ON "AuditLoglar";
                DROP FUNCTION IF EXISTS denetim_kaydi_koru();
                """);
        }
    }
}
