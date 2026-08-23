using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;

namespace Altyapi.Veri;

public class MuhasebeBaglami : DbContext
{
    public MuhasebeBaglami(DbContextOptions<MuhasebeBaglami> options) : base(options) { }

    // Kullanici
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();

    // Cari
    public DbSet<Musteri> Musteriler => Set<Musteri>();
    public DbSet<Tedarikci> Tedarikciler => Set<Tedarikci>();

    // Stok
    public DbSet<Urun> Urunler => Set<Urun>();

    // Fatura
    public DbSet<Fatura> Faturalar => Set<Fatura>();
    public DbSet<FaturaKalemi> FaturaKalemleri => Set<FaturaKalemi>();

    // Finans
    public DbSet<KasaHareketi> KasaHareketleri => Set<KasaHareketi>();
    public DbSet<BankaHareketi> BankaHareketleri => Set<BankaHareketi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tablo isimlerini Turkce olarak tanımla
        modelBuilder.Entity<Kullanici>().ToTable("Kullanicilar");
        modelBuilder.Entity<Musteri>().ToTable("Musteriler");
        modelBuilder.Entity<Tedarikci>().ToTable("Tedarikciler");
        modelBuilder.Entity<Urun>().ToTable("Urunler");
        modelBuilder.Entity<Fatura>().ToTable("Faturalar");
        modelBuilder.Entity<FaturaKalemi>().ToTable("FaturaKalemleri");
        modelBuilder.Entity<KasaHareketi>().ToTable("KasaHareketleri");
        modelBuilder.Entity<BankaHareketi>().ToTable("BankaHareketleri");

        // Fatura ilişkileri
        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.Musteri)
            .WithMany()
            .HasForeignKey(f => f.MusteriId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.Tedarikci)
            .WithMany()
            .HasForeignKey(f => f.TedarikciId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FaturaKalemi>()
            .HasOne(k => k.Fatura)
            .WithMany(f => f.Kalemler)
            .HasForeignKey(k => k.FaturaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FaturaKalemi>()
            .HasOne(k => k.Urun)
            .WithMany()
            .HasForeignKey(k => k.UrunId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global sorgu filtresi - silinmis kayitlari gizle
        modelBuilder.Entity<Kullanici>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Musteri>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Tedarikci>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Urun>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Fatura>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<FaturaKalemi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<KasaHareketi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<BankaHareketi>().HasQueryFilter(x => !x.Silindi);

        // Indexler
        modelBuilder.Entity<Kullanici>().HasIndex(k => k.KullaniciAdi).IsUnique();
        modelBuilder.Entity<Urun>().HasIndex(u => u.Kod).IsUnique();
    }
}
