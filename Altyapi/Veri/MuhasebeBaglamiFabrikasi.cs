using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Altyapi.Veri;

public class MuhasebeBaglamiFabrikasi : IDesignTimeDbContextFactory<MuhasebeBaglami>
{
    public MuhasebeBaglami CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MuhasebeBaglami>();
        
        // Tasarım anında kullanılacak varsayılan bağlantı dizesi
        optionsBuilder.UseNpgsql("Host=127.0.0.1;Port=5432;Database=Muhasebe_Test;Username=postgres;");

        // Tasarım anında model hash uyarısını logla (engelleme)
        optionsBuilder.ConfigureWarnings(w =>
            w.Log(RelationalEventId.PendingModelChangesWarning));

        return new MuhasebeBaglami(optionsBuilder.Options);
    }
}
