using EtkiAlani.Modeller.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IFinancialTransactionService
{
    Task<YevmiyeFis> CreateFromInvoiceAsync(int faturaId);
}
