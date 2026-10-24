using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class KimlikSayaclariniOnar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Başlangıç verisi (HasData) kimlik numarasını açıkça verir ve sayacı ilerletmez; yeni kurulumda ilk
            // kullanıcı/depo/ürün/KDV oranı/para birimi eklenirken "duplicate key" hatası alınıyordu (depoda 3 kez).
            // Tüm kimlik sayaçları tablodaki en büyük numaraya getirilir; boş tabloda sonraki değer 1 kalır.
            migrationBuilder.Sql(@"
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT c.table_schema, c.table_name, c.column_name, pg_get_serial_sequence(format('%I.%I', c.table_schema, c.table_name), c.column_name) AS sayac
        FROM information_schema.columns c
        WHERE c.table_schema = current_schema()
          AND pg_get_serial_sequence(format('%I.%I', c.table_schema, c.table_name), c.column_name) IS NOT NULL
    LOOP
        EXECUTE format(
            'SELECT setval(%L, COALESCE((SELECT MAX(%I) FROM %I.%I), 0) + 1, false)',
            r.sayac, r.column_name, r.table_schema, r.table_name);
    END LOOP;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
