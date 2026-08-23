using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Altyapi.Veri;

public class MuhasebeBaglamiFabrikasi : IDesignTimeDbContextFactory<MuhasebeBaglami>
{
    public MuhasebeBaglami CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MuhasebeBaglami>();
        
        // Tasarım anında kullanılacak varsayılan bağlantı dizesi
        optionsBuilder.UseNpgsql("Host=127.0.0.1;Port=5432;Database=Muhasebe;Username=postgres;");

        return new MuhasebeBaglami(optionsBuilder.Options);
    }
}
