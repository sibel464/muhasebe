using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Altyapi.Veri;

// İşlem (transaction) geri alınınca bağlamın izlediği değişiklikler de bırakılır.
// EF geri almada bellekteki kayıtları eski hâline döndürmez: yarıda kalan bir kaydın satırları "eklenecek"
// olarak kalır ve aynı ekranda yapılan bir sonraki başarılı kayıtla birlikte veritabanına yazılabilirdi
// (ör. POS'ta hata veren satışın kalemleri bir sonraki satışla kaydedilirdi).
public sealed class GeriAlmaTemizleyici : DbTransactionInterceptor
{
    public static readonly GeriAlmaTemizleyici Ornek = new();

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        eventData.Context?.ChangeTracker.Clear();
        base.TransactionRolledBack(transaction, eventData);
    }

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        eventData.Context?.ChangeTracker.Clear();
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }
}
