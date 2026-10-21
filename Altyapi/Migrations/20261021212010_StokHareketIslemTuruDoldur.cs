using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class StokHareketIslemTuruDoldur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fatura ve irsaliye stok hareketleri işlem türü yazılmadan "Diğer" (9) olarak kaydediliyordu.
            // Belge numarasıyla (fatura iptal ters kaydında "-IPTAL" ekli) eşleşenler doğru türe çekilir.
            // Fatura: Satis=0, Alis=1, SatisIade=2, AlisIade=3 → AlisFaturasi=1, SatisFaturasi=2.
            // İrsaliye: Satis=0, Alis=1 → SatisIrsaliyesi=4, AlisIrsaliyesi=3.
            migrationBuilder.Sql("""
                UPDATE "StokHareketleri" sh
                SET "IslemTuru" = CASE WHEN f."Tur" IN (1, 3) THEN 1 ELSE 2 END
                FROM "Faturalar" f
                WHERE sh."IslemTuru" = 9 AND sh."IlgiliBelgeId" = f."Id"
                  AND (sh."BelgeNo" = f."FaturaNo" OR sh."BelgeNo" = f."FaturaNo" || '-IPTAL');

                UPDATE "StokHareketleri" sh
                SET "IslemTuru" = CASE WHEN i."Tur" = 0 THEN 4 ELSE 3 END
                FROM "Irsaliyeler" i
                WHERE sh."IslemTuru" = 9 AND sh."IlgiliBelgeId" = i."Id" AND sh."BelgeNo" = i."IrsaliyeNo";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
