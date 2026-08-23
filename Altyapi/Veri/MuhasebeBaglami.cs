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
    public DbSet<CariKart> CariKartlar => Set<CariKart>();

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

        modelBuilder.Entity<Kullanici>().ToTable("Kullanicilar");
        modelBuilder.Entity<CariKart>().ToTable("CariKartlar");
        modelBuilder.Entity<Urun>().ToTable("Urunler");
        modelBuilder.Entity<Fatura>().ToTable("Faturalar");
        modelBuilder.Entity<FaturaKalemi>().ToTable("FaturaKalemleri");
        modelBuilder.Entity<KasaHareketi>().ToTable("KasaHareketleri");
        modelBuilder.Entity<BankaHareketi>().ToTable("BankaHareketleri");

        // Fatura ilişkileri
        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.CariKart)
            .WithMany()
            .HasForeignKey(f => f.CariKartId)
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
        modelBuilder.Entity<CariKart>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Urun>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Fatura>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<FaturaKalemi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<KasaHareketi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<BankaHareketi>().HasQueryFilter(x => !x.Silindi);

        // Indexler
        modelBuilder.Entity<Kullanici>().HasIndex(k => k.KullaniciAdi).IsUnique();
        modelBuilder.Entity<Urun>().HasIndex(u => u.Kod).IsUnique();
        modelBuilder.Entity<CariKart>().HasIndex(c => c.Kod).IsUnique();

        // Ürün Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<Urun>(entity =>
        {
            entity.Property(u => u.Kod).IsRequired().HasMaxLength(50);
            entity.Property(u => u.Ad).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Birim).IsRequired().HasMaxLength(20);
            entity.Property(u => u.Kategori).HasMaxLength(100);
            entity.Property(u => u.Barkod).HasMaxLength(50);

            entity.Property(u => u.AlisFiyati).HasPrecision(18, 4);
            entity.Property(u => u.SatisFiyati).HasPrecision(18, 4);
            entity.Property(u => u.StokMiktari).HasPrecision(18, 4);
            entity.Property(u => u.MinimumStok).HasPrecision(18, 4);
            entity.Property(u => u.KdvOrani).HasPrecision(5, 2);
        });

        // CariKart Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<CariKart>(entity =>
        {
            entity.Property(c => c.Kod).IsRequired().HasMaxLength(50);
            entity.Property(c => c.Unvan).IsRequired().HasMaxLength(250);
            entity.Property(c => c.Ad).HasMaxLength(100);
            entity.Property(c => c.Soyad).HasMaxLength(100);
            entity.Property(c => c.VergiDairesi).HasMaxLength(100);
            entity.Property(c => c.VergiNo).HasMaxLength(15);
            entity.Property(c => c.TcNo).HasMaxLength(11);
            entity.Property(c => c.Telefon).HasMaxLength(20);
            entity.Property(c => c.CepTelefonu).HasMaxLength(20);
            entity.Property(c => c.Eposta).HasMaxLength(100);
            entity.Property(c => c.WebSitesi).HasMaxLength(100);
            entity.Property(c => c.Adres).HasMaxLength(500);
            entity.Property(c => c.Il).HasMaxLength(50);
            entity.Property(c => c.Ilce).HasMaxLength(50);
            entity.Property(c => c.PostaKodu).HasMaxLength(10);
            entity.Property(c => c.Aciklama).HasMaxLength(500);

            entity.Property(c => c.Borc).HasPrecision(18, 4);
            entity.Property(c => c.Alacak).HasPrecision(18, 4);
        });

        // Seed Data
        modelBuilder.Entity<Kullanici>().HasData(
            new Kullanici 
            { 
                Id = 1, 
                KullaniciAdi = "admin", 
                SifreHash = "$2a$11$qFm4zP4k4Vv5P5v5P5v5PeP4O4JzY.U25hWz9.eWbQ2yJq6G9qW", 
                Ad = "Sistem", 
                Soyad = "Yöneticisi", 
                Rol = "Yonetici", 
                Aktif = true,
                OlusturmaTarihi = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Urun>().HasData(
            new Urun 
            { 
                Id = 1, 
                Kod = "URN001", 
                Ad = "A4 Kağıt (500'lü)", 
                Kategori = "Kırtasiye", 
                Birim = "Paket", 
                AlisFiyati = 45.0000m, 
                SatisFiyati = 65.0000m, 
                KdvOrani = 20.00m, 
                StokMiktari = 150.0000m, 
                MinimumStok = 20.0000m, 
                Aktif = true,
                OlusturmaTarihi = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc)
            },
            new Urun 
            { 
                Id = 2, 
                Kod = "URN002", 
                Ad = "Laptop Çantası", 
                Kategori = "Elektronik", 
                Birim = "Adet", 
                AlisFiyati = 250.0000m, 
                SatisFiyati = 399.0000m, 
                KdvOrani = 20.00m, 
                StokMiktari = 25.0000m, 
                MinimumStok = 5.0000m, 
                Aktif = true,
                OlusturmaTarihi = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
