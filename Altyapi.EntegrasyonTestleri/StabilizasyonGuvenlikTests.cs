using Altyapi.Servisler;
using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class StabilizasyonGuvenlikTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Stok_servisleri_oturumsuz_cagrida_veritabanina_gitmeden_reddeder(bool operasyon)
    {
        await using var db = new MuhasebeBaglami(new DbContextOptionsBuilder<MuhasebeBaglami>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=Unavailable_Test;Username=unused;Timeout=1").Options);
        if (operasyon)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new StokOperasyonServisi(db).GetMatrisOzetAsync(Guid.NewGuid()));
        else
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new Stok360Servisi(db).GetUrunOzetAsync(1, Guid.NewGuid()));
    }
}
