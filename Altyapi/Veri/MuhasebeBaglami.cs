using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Ayar;
using EtkiAlani.Modeller.Guvenlik;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Tanimlar;
using EtkiAlani.Modeller.Siparis;
using Uygulama.Arayuzler;

namespace Altyapi.Veri;

public class MuhasebeBaglami : DbContext
{
    public static readonly Guid VarsayilanSirketId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IAktifFirmaBaglami? _aktifFirmaBaglami;

    public MuhasebeBaglami(DbContextOptions<MuhasebeBaglami> options, IAktifFirmaBaglami? aktifFirmaBaglami = null) : base(options)
        => _aktifFirmaBaglami = aktifFirmaBaglami;

    // Kullanici & Guvenlik
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<KullaniciYetkileri> KullaniciYetkileri => Set<KullaniciYetkileri>();
    public DbSet<AuditLog> AuditLoglar => Set<AuditLog>();

    // Sistem Ayarlari
    public DbSet<SistemAyarlari> SistemAyarlari => Set<SistemAyarlari>();

    // Cari
    public DbSet<CariKart> CariKartlar => Set<CariKart>();
    public DbSet<CariNotu> CariNotlari => Set<CariNotu>();
    public DbSet<CariHareket> CariHareketler => Set<CariHareket>();
    public DbSet<CariTipiTanimi> CariTipiTanimlari => Set<CariTipiTanimi>();
    public DbSet<MerkeziTanim> MerkeziTanimlar => Set<MerkeziTanim>();

    // Stok
    public DbSet<Urun> Urunler => Set<Urun>();
    public DbSet<StokHareketi> StokHareketleri => Set<StokHareketi>();
    public DbSet<UrunSatisFiyati> UrunSatisFiyatlari => Set<UrunSatisFiyati>();
    public DbSet<UrunLokasyonStogu> UrunLokasyonStoklari => Set<UrunLokasyonStogu>();
    public DbSet<DepoTransferFisi> DepoTransferFisleri => Set<DepoTransferFisi>();
    public DbSet<DepoTransferKalemi> DepoTransferKalemleri => Set<DepoTransferKalemi>();
    public DbSet<StokSayimi> StokSayimlari => Set<StokSayimi>();
    public DbSet<StokSayimKalemi> StokSayimKalemleri => Set<StokSayimKalemi>();

    // Fatura
    public DbSet<Fatura> Faturalar => Set<Fatura>();
    public DbSet<FaturaKalemi> FaturaKalemleri => Set<FaturaKalemi>();
    public DbSet<FaturaKalemiStokDagilimi> FaturaKalemiStokDagilimleri => Set<FaturaKalemiStokDagilimi>();

    // Siparis
    public DbSet<Siparis> Siparisler => Set<Siparis>();
    public DbSet<SiparisKalemi> SiparisKalemleri => Set<SiparisKalemi>();

    // İrsaliye
    public DbSet<Irsaliye> Irsaliyeler => Set<Irsaliye>();
    public DbSet<IrsaliyeKalemi> IrsaliyeKalemleri => Set<IrsaliyeKalemi>();
    public DbSet<IrsaliyeKalemiStokDagilimi> IrsaliyeKalemiStokDagilimlari => Set<IrsaliyeKalemiStokDagilimi>();

    // Finans
    public DbSet<Kasa> Kasalar => Set<Kasa>();
    public DbSet<BankaHesabi> BankaHesaplari => Set<BankaHesabi>();
    public DbSet<KasaHareketi> KasaHareketleri => Set<KasaHareketi>();
    public DbSet<BankaHareketi> BankaHareketleri => Set<BankaHareketi>();
    public DbSet<KasaSayimMutabakat> KasaSayimMutabakatlari => Set<KasaSayimMutabakat>();
    public DbSet<BankaMutabakat> BankaMutabakatlari => Set<BankaMutabakat>();
    public DbSet<PosIslemi> PosIslemleri => Set<PosIslemi>();
    public DbSet<BelgeKapama> BelgeKapamalar => Set<BelgeKapama>();

    // Kurumsal yapı
    public DbSet<Sirket> Sirketler => Set<Sirket>();
    public DbSet<Sube> Subeler => Set<Sube>();
    public DbSet<Depo> Depolar => Set<Depo>();
    public DbSet<MaliDonem> MaliDonemler => Set<MaliDonem>();
    public DbSet<ParaBirimi> ParaBirimleri => Set<ParaBirimi>();
    public DbSet<KdvOrani> KdvOranlari => Set<KdvOrani>();
    public DbSet<BelgeSerisi> BelgeSerileri => Set<BelgeSerisi>();
    public DbSet<HesapPlani> HesapPlanlari => Set<HesapPlani>();
    public DbSet<YevmiyeFis> YevmiyeFisleri => Set<YevmiyeFis>();
    public DbSet<YevmiyeSatiri> YevmiyeSatirlari => Set<YevmiyeSatiri>();
    public DbSet<CekSenet> CekSenetler => Set<CekSenet>();
    public DbSet<MuhasebeBaglantiKodu> MuhasebeBaglantiKodlari => Set<MuhasebeBaglantiKodu>();
    public DbSet<DovizKuru> DovizKurlari => Set<DovizKuru>();
    public DbSet<RevaluasyonKaydi> RevaluasyonKayitlari => Set<RevaluasyonKaydi>();
    public DbSet<RevaluasyonKalemi> RevaluasyonKalemleri => Set<RevaluasyonKalemi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Kullanici>().ToTable("Kullanicilar");
        modelBuilder.Entity<KullaniciYetkileri>().ToTable("KullaniciYetkileri");
        modelBuilder.Entity<AuditLog>().ToTable("AuditLoglar");
        modelBuilder.Entity<SistemAyarlari>().ToTable("SistemAyarlari");

        // Kullanıcı - Yetki (1-1 İlişki)
        modelBuilder.Entity<Kullanici>()
            .HasOne(k => k.Yetkiler)
            .WithOne()
            .HasForeignKey<KullaniciYetkileri>(y => y.KullaniciId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CariKart>().ToTable("CariKartlar");
        modelBuilder.Entity<CariTipiTanimi>().ToTable("CariTipiTanimlari");
        modelBuilder.Entity<MerkeziTanim>().ToTable("MerkeziTanimlar");
        modelBuilder.Entity<CariHareket>().ToTable("CariHareketler", t => t.HasCheckConstraint("CK_CariHareket_BorcAlacak", "(\"Borc\" >= 0 AND \"Alacak\" >= 0) AND ((\"Borc\" = 0 AND \"Alacak\" > 0) OR (\"Borc\" > 0 AND \"Alacak\" = 0))"));
        modelBuilder.Entity<Urun>().ToTable("Urunler");
        modelBuilder.Entity<StokHareketi>().ToTable("StokHareketleri", t => t.HasCheckConstraint("CK_StokHareketi_Miktar", "\"Miktar\" > 0"));
        modelBuilder.Entity<UrunSatisFiyati>().ToTable("UrunSatisFiyatlari", t => t.HasCheckConstraint("CK_UrunSatisFiyati_TaksitSayisi", "\"TaksitSayisi\" >= 1 AND \"TaksitSayisi\" <= 9"));
        modelBuilder.Entity<UrunLokasyonStogu>().ToTable("UrunLokasyonStoklari");
        modelBuilder.Entity<FaturaKalemiStokDagilimi>().ToTable("FaturaKalemiStokDagilimleri");
        modelBuilder.Entity<Fatura>().ToTable("Faturalar");
        modelBuilder.Entity<FaturaKalemi>().ToTable("FaturaKalemleri");
        modelBuilder.Entity<Siparis>().ToTable("Siparisler");
        modelBuilder.Entity<SiparisKalemi>().ToTable("SiparisKalemleri");
        modelBuilder.Entity<Irsaliye>().ToTable("Irsaliyeler");
        modelBuilder.Entity<IrsaliyeKalemi>().ToTable("IrsaliyeKalemleri");
        modelBuilder.Entity<IrsaliyeKalemiStokDagilimi>().ToTable("IrsaliyeKalemiStokDagilimlari");
        modelBuilder.Entity<KasaHareketi>().ToTable("KasaHareketleri", t => t.HasCheckConstraint("CK_KasaHareketi_Tutar", "\"Tutar\" >= 0"));
        modelBuilder.Entity<BankaHareketi>().ToTable("BankaHareketleri", t => t.HasCheckConstraint("CK_BankaHareketi_Tutar", "\"Tutar\" >= 0"));
        modelBuilder.Entity<KasaHareketi>().Property(x => x.IslemDurumu).HasConversion<string>().HasMaxLength(20).HasDefaultValue(FinansIslemDurumu.Tamamlandi);
        modelBuilder.Entity<BankaHareketi>().Property(x => x.IslemDurumu).HasConversion<string>().HasMaxLength(20).HasDefaultValue(FinansIslemDurumu.Tamamlandi);
        modelBuilder.Entity<BankaHareketi>().Property(x => x.OdemeYontemi).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OdemeYontemi.Banka);

        modelBuilder.Entity<Sirket>().ToTable("Sirketler");
        modelBuilder.Entity<Sube>().ToTable("Subeler");
        modelBuilder.Entity<Depo>().ToTable("Depolar");
        modelBuilder.Entity<MaliDonem>().ToTable("MaliDonemler", t => t.HasCheckConstraint("CK_MaliDonem_Tarih", "\"Baslangic\" <= \"Bitis\""));
        modelBuilder.Entity<ParaBirimi>().ToTable("ParaBirimleri");
        modelBuilder.Entity<KdvOrani>().ToTable("KdvOranlari", t => t.HasCheckConstraint("CK_KdvOrani_Oran", "\"Oran\" >= 0 AND \"Oran\" <= 100"));
        modelBuilder.Entity<BelgeKapama>().ToTable("BelgeKapamalar");
        modelBuilder.Entity<CekSenet>().ToTable("CekSenetler");
        modelBuilder.Entity<HesapPlani>().ToTable("HesapPlanlari");
        modelBuilder.Entity<YevmiyeFis>().ToTable("YevmiyeFisleri");
        modelBuilder.Entity<YevmiyeSatiri>().ToTable("YevmiyeSatirlari", t => t.HasCheckConstraint("CK_YevmiyeSatiri_BorcAlacak", "\"Borc\" >= 0 AND \"Alacak\" >= 0 AND ((\"Borc\" > 0 AND \"Alacak\" = 0) OR (\"Borc\" = 0 AND \"Alacak\" > 0))"));
        modelBuilder.Entity<MuhasebeBaglantiKodu>().ToTable("MuhasebeBaglantiKodlari");
        modelBuilder.Entity<DovizKuru>().ToTable("DovizKurlari");
        modelBuilder.Entity<RevaluasyonKaydi>().ToTable("RevaluasyonKayitlari");
        modelBuilder.Entity<RevaluasyonKalemi>().ToTable("RevaluasyonKalemleri");

        modelBuilder.Entity<Sube>()
            .HasOne(s => s.Sirket)
            .WithMany()
            .HasForeignKey(s => s.SirketId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Depo>()
            .HasOne(d => d.Sube)
            .WithMany(s => s.Depolar)
            .HasForeignKey(d => d.SubeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaliDonem>()
            .HasOne(d => d.Sirket)
            .WithMany()
            .HasForeignKey(d => d.SirketId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BelgeSerisi>()
            .HasOne(s => s.Sirket)
            .WithMany()
            .HasForeignKey(s => s.SirketId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HesapPlani>()
            .HasOne(h => h.Sirket)
            .WithMany()
            .HasForeignKey(h => h.SirketId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<HesapPlani>()
            .HasOne(h => h.UstHesap)
            .WithMany(h => h.AltHesaplar)
            .HasForeignKey(h => h.UstHesapId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<YevmiyeFis>()
            .HasOne(f => f.Sirket)
            .WithMany()
            .HasForeignKey(f => f.SirketId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<YevmiyeSatiri>()
            .HasOne(s => s.Fis)
            .WithMany(f => f.Satirlar)
            .HasForeignKey(s => s.FisId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<YevmiyeSatiri>()
            .HasOne(s => s.Hesap)
            .WithMany()
            .HasForeignKey(s => s.HesapId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CariKart>()
            .HasOne(c => c.Sirket)
            .WithMany()
            .HasForeignKey(c => c.SirketId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CariKart>()
            .HasOne(c => c.Sube)
            .WithMany()
            .HasForeignKey(c => c.SubeId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CariKart>()
            .HasOne(c => c.CariTipi)
            .WithMany()
            .HasForeignKey(c => c.CariTipiId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CariKart>()
            .HasOne(c => c.OlusturanKullanici)
            .WithMany()
            .HasForeignKey(c => c.OlusturanKullaniciId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CariKart>()
            .HasOne(c => c.GuncelleyenKullanici)
            .WithMany()
            .HasForeignKey(c => c.GuncelleyenKullaniciId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Urun>()
            .HasOne(u => u.Sirket)
            .WithMany()
            .HasForeignKey(u => u.SirketId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Urun>()
            .HasOne(u => u.Depo)
            .WithMany()
            .HasForeignKey(u => u.DepoId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Urun>().HasOne(u => u.KategoriTanimi).WithMany().HasForeignKey(u => u.KategoriTanimiId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Urun>().HasOne(u => u.MarkaTanimi).WithMany().HasForeignKey(u => u.MarkaTanimiId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Urun>().HasOne(u => u.BirimTanimi).WithMany().HasForeignKey(u => u.BirimTanimiId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.Sirket)
            .WithMany()
            .HasForeignKey(f => f.SirketId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.Sube)
            .WithMany()
            .HasForeignKey(f => f.SubeId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.MaliDonem)
            .WithMany()
            .HasForeignKey(f => f.MaliDonemId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.ParaBirimi)
            .WithMany()
            .HasForeignKey(f => f.ParaBirimiId)
            .OnDelete(DeleteBehavior.SetNull);

        // Concurrency Tokens
        modelBuilder.Entity<CariKart>().Property(c => c.Versiyon).IsConcurrencyToken();
        modelBuilder.Entity<Fatura>().Property(f => f.Versiyon).IsConcurrencyToken();
        modelBuilder.Entity<Urun>().Property(u => u.Versiyon).IsConcurrencyToken();
        modelBuilder.Entity<Irsaliye>().Property(i => i.Versiyon).IsConcurrencyToken();
        modelBuilder.Entity<Siparis>().Property(i => i.Versiyon).IsConcurrencyToken();
        modelBuilder.Entity<BankaHareketi>().ToTable("BankaHareketleri", t => t.HasCheckConstraint("CK_BankaHareketi_Tutar", "\"Tutar\" >= 0"));

        // CariHareket ilişkileri
        modelBuilder.Entity<CariHareket>()
            .HasOne(h => h.CariKart)
            .WithMany()
            .HasForeignKey(h => h.CariKartId)
            .OnDelete(DeleteBehavior.Restrict);

        // StokHareketi ilişkileri
        modelBuilder.Entity<StokHareketi>()
            .HasOne(h => h.Urun)
            .WithMany()
            .HasForeignKey(h => h.UrunId)
            .OnDelete(DeleteBehavior.Restrict);

        // KasaHareketi ilişkileri
        modelBuilder.Entity<KasaHareketi>()
            .HasOne(k => k.CariKart)
            .WithMany()
            .HasForeignKey(k => k.CariKartId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // BankaHareketi ilişkileri
        modelBuilder.Entity<BankaHareketi>()
            .HasOne(b => b.CariKart)
            .WithMany()
            .HasForeignKey(b => b.CariKartId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Fatura ilişkileri
        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.CariKart)
            .WithMany()
            .HasForeignKey(f => f.CariKartId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fatura>().HasOne(f => f.Siparis).WithMany().HasForeignKey(f => f.SiparisId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fatura>().HasOne(f => f.Irsaliye).WithMany().HasForeignKey(f => f.IrsaliyeId).OnDelete(DeleteBehavior.Restrict);

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
        modelBuilder.Entity<FaturaKalemi>().HasOne(k => k.SiparisKalemi).WithMany().HasForeignKey(k => k.SiparisKalemiId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<FaturaKalemi>().HasOne(k => k.IrsaliyeKalemi).WithMany().HasForeignKey(k => k.IrsaliyeKalemiId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Siparis>()
            .HasOne(x => x.CariKart).WithMany().HasForeignKey(x => x.CariKartId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Siparis>()
            .HasOne(x => x.ParaBirimi).WithMany().HasForeignKey(x => x.ParaBirimiId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Siparis>()
            .HasOne(x => x.OlusturanKullanici).WithMany().HasForeignKey(x => x.OlusturanKullaniciId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Siparis>()
            .HasOne(x => x.GuncelleyenKullanici).WithMany().HasForeignKey(x => x.GuncelleyenKullaniciId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SiparisKalemi>()
            .HasOne(x => x.Siparis).WithMany(x => x.Kalemler).HasForeignKey(x => x.SiparisId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SiparisKalemi>()
            .HasOne(x => x.Urun).WithMany().HasForeignKey(x => x.UrunId).OnDelete(DeleteBehavior.Restrict);

        // İrsaliye ilişkileri
        modelBuilder.Entity<Irsaliye>()
            .HasOne(i => i.CariKart)
            .WithMany()
            .HasForeignKey(i => i.CariKartId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Irsaliye>()
            .HasOne(i => i.DonusturulenFatura)
            .WithMany()
            .HasForeignKey(i => i.DonusturulenFaturaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Irsaliye>()
            .HasOne(i => i.Siparis).WithMany().HasForeignKey(i => i.SiparisId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Irsaliye>()
            .HasOne(i => i.OlusturanKullanici).WithMany().HasForeignKey(i => i.OlusturanKullaniciId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Irsaliye>()
            .HasOne(i => i.GuncelleyenKullanici).WithMany().HasForeignKey(i => i.GuncelleyenKullaniciId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IrsaliyeKalemi>()
            .HasOne(k => k.Irsaliye)
            .WithMany(i => i.Kalemler)
            .HasForeignKey(k => k.IrsaliyeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IrsaliyeKalemi>()
            .HasOne(k => k.Urun)
            .WithMany()
            .HasForeignKey(k => k.UrunId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IrsaliyeKalemi>()
            .HasOne(k => k.SiparisKalemi).WithMany().HasForeignKey(k => k.SiparisKalemiId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IrsaliyeKalemiStokDagilimi>()
            .HasOne(x => x.IrsaliyeKalemi).WithMany(x => x.StokDagilimlari)
            .HasForeignKey(x => x.IrsaliyeKalemiId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<IrsaliyeKalemiStokDagilimi>()
            .HasOne(x => x.Depo).WithMany().HasForeignKey(x => x.DepoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global sorgu filtresi - silinmis kayitlari gizle
        modelBuilder.Entity<Kullanici>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<CariKart>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<CariTipiTanimi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<MerkeziTanim>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<CariHareket>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Urun>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<StokHareketi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<UrunSatisFiyati>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<UrunLokasyonStogu>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<FaturaKalemiStokDagilimi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Fatura>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<FaturaKalemi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Siparis>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<SiparisKalemi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Irsaliye>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<IrsaliyeKalemi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<IrsaliyeKalemiStokDagilimi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<KasaHareketi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<BankaHareketi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Sube>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<Depo>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<MaliDonem>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<ParaBirimi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<KdvOrani>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<BelgeSerisi>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<CekSenet>().HasQueryFilter(x => !x.SilindiMi);
        modelBuilder.Entity<HesapPlani>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<YevmiyeFis>().HasQueryFilter(x => !x.Silindi);
        modelBuilder.Entity<YevmiyeSatiri>().HasQueryFilter(x => !x.Silindi);

        // Indexler
        modelBuilder.Entity<Kullanici>().HasIndex(k => k.KullaniciAdi).IsUnique();
        modelBuilder.Entity<Urun>().HasIndex(u => u.Kod).IsUnique();
        modelBuilder.Entity<CariKart>().HasIndex(c => c.Kod).IsUnique();
        modelBuilder.Entity<CariTipiTanimi>().HasIndex(x => x.Kod).IsUnique();
        modelBuilder.Entity<MerkeziTanim>().HasIndex(x => new { x.Grup, x.Kod }).IsUnique();
        modelBuilder.Entity<CariHareket>().HasIndex(h => h.Tarih);
        modelBuilder.Entity<CariHareket>().HasIndex(h => new { h.CariKartId, h.Tarih, h.Id });
        modelBuilder.Entity<Fatura>().HasIndex(h => new { h.SirketId, h.CariKartId, h.Tarih, h.Id });
        modelBuilder.Entity<Siparis>().HasIndex(h => new { h.CariKartId, h.SiparisTarihi, h.Id });
        modelBuilder.Entity<Irsaliye>().HasIndex(h => new { h.CariKartId, h.Tarih, h.Id });
        modelBuilder.Entity<KasaHareketi>().HasIndex(h => new { h.CariKartId, h.Tarih, h.Id });
        modelBuilder.Entity<BankaHareketi>().HasIndex(h => new { h.CariKartId, h.Tarih, h.Id });
        modelBuilder.Entity<CekSenet>().HasIndex(h => new { h.CariKartId, h.VadeTarihi, h.Id });
        modelBuilder.Entity<CariKart>().Property(x => x.RiskLimiti).HasPrecision(18, 2);
        modelBuilder.Entity<CariKart>().Property(x => x.Iban).HasMaxLength(34);
        modelBuilder.Entity<CariNotu>(e => {
            e.ToTable("CariNotlari");
            e.HasQueryFilter(x => !x.Silindi);
            e.HasIndex(x => new { x.SirketId, x.CariKartId, x.OlusturmaTarihi });
            e.Property(x => x.Baslik).HasMaxLength(150);
            e.Property(x => x.Metin).HasMaxLength(4000);
            e.Property(x => x.KullaniciAdi).HasMaxLength(150);
            e.HasOne(x => x.CariKart).WithMany().HasForeignKey(x => x.CariKartId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StokHareketi>().HasIndex(h => h.Tarih);
        modelBuilder.Entity<Irsaliye>().HasIndex(i => i.Tarih);
        modelBuilder.Entity<Irsaliye>().HasIndex(i => i.SiparisId);
        modelBuilder.Entity<IrsaliyeKalemiStokDagilimi>().HasIndex(x => new { x.IrsaliyeKalemiId, x.DepoId }).IsUnique();
        modelBuilder.Entity<Sirket>().HasIndex(s => s.SirketKodu).IsUnique();
        modelBuilder.Entity<Sube>().HasIndex(s => new { s.SirketId, s.SubeKodu }).IsUnique();
        modelBuilder.Entity<Depo>().HasIndex(d => new { d.SubeId, d.DepoKodu }).IsUnique();
        modelBuilder.Entity<MaliDonem>().HasIndex(d => new { d.SirketId, d.DonemAdi }).IsUnique();
        modelBuilder.Entity<ParaBirimi>().HasIndex(p => p.Kod).IsUnique();
        modelBuilder.Entity<KdvOrani>().HasIndex(k => k.Kod).IsUnique();
        modelBuilder.Entity<BelgeSerisi>().HasIndex(s => new { s.SirketId, s.BelgeTipi, s.Seri }).IsUnique();
        modelBuilder.Entity<HesapPlani>().HasIndex(h => new { h.SirketId, h.HesapKodu }).IsUnique();
        modelBuilder.Entity<HesapPlani>().HasIndex(h => h.UstHesapId);
        modelBuilder.Entity<YevmiyeFis>().HasIndex(f => new { f.SirketId, f.FisNo }).IsUnique();
        modelBuilder.Entity<YevmiyeFis>().HasIndex(f => new { f.SirketId, f.BelgeTipi, f.BelgeId }).IsUnique();
        modelBuilder.Entity<YevmiyeFis>().HasIndex(f => new { f.SirketId, f.Tarih });
        modelBuilder.Entity<YevmiyeSatiri>().HasIndex(s => s.FisId);
        modelBuilder.Entity<YevmiyeSatiri>().HasIndex(s => s.HesapId);
        modelBuilder.Entity<CariKart>().HasIndex(c => new { c.SirketId, c.SubeId });
        modelBuilder.Entity<Urun>().HasIndex(u => new { u.SirketId, u.DepoId });
        modelBuilder.Entity<UrunSatisFiyati>().HasIndex(f => new { f.UrunId, f.TaksitSayisi, f.ParaBirimiId }).IsUnique();
        modelBuilder.Entity<UrunLokasyonStogu>().HasIndex(ls => new { ls.UrunId, ls.DepoId }).IsUnique();
        modelBuilder.Entity<FaturaKalemiStokDagilimi>().HasIndex(d => new { d.FaturaKalemiId, d.DepoId });
        modelBuilder.Entity<Fatura>().HasIndex(f => new { f.SirketId, f.SubeId, f.Tarih });
        modelBuilder.Entity<Fatura>().HasIndex(f => new { f.SirketId, f.MaliDonemId });
        modelBuilder.Entity<Fatura>().HasIndex(f => f.SiparisId);
        modelBuilder.Entity<Fatura>().HasIndex(f => f.IrsaliyeId);
        modelBuilder.Entity<FaturaKalemi>().HasIndex(f => f.SiparisKalemiId);
        modelBuilder.Entity<FaturaKalemi>().HasIndex(f => f.IrsaliyeKalemiId);
        modelBuilder.Entity<Siparis>().HasIndex(x => x.SiparisNo).IsUnique();
        modelBuilder.Entity<Siparis>().HasIndex(x => new { x.SiparisTarihi, x.SiparisTuru, x.Durum });
        modelBuilder.Entity<CekSenet>().HasIndex(x => x.BelgeNo);
        modelBuilder.Entity<CekSenet>().HasIndex(x => x.VadeTarihi);
        modelBuilder.Entity<CekSenet>().HasIndex(x => x.CariKartId);
        modelBuilder.Entity<SiparisKalemi>().HasIndex(x => x.SiparisId);

        modelBuilder.Entity<Siparis>(entity =>
        {
            entity.Property(x => x.SiparisNo).IsRequired().HasMaxLength(30);
            entity.Property(x => x.TeslimatAdSoyad).HasMaxLength(200);
            entity.Property(x => x.TeslimatTelefon).HasMaxLength(30);
            entity.Property(x => x.TeslimatAdres).HasColumnType("text");
            entity.Property(x => x.TeslimatIl).HasMaxLength(100);
            entity.Property(x => x.TeslimatIlce).HasMaxLength(100);
            entity.Property(x => x.ReferansNo).HasMaxLength(100);
            entity.Property(x => x.Aciklama).HasColumnType("text");
            entity.Property(x => x.Kur).HasPrecision(18, 6);
            entity.Property(x => x.BrutToplam).HasPrecision(18, 4);
            entity.Property(x => x.IskontoToplami).HasPrecision(18, 4);
            entity.Property(x => x.AraToplam).HasPrecision(18, 4);
            entity.Property(x => x.KdvToplami).HasPrecision(18, 4);
            entity.Property(x => x.GenelToplam).HasPrecision(18, 4);
        });
        modelBuilder.Entity<SiparisKalemi>(entity =>
        {
            entity.Property(x => x.UrunKodu).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Barkod).HasMaxLength(50);
            entity.Property(x => x.UrunAdi).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Birim).IsRequired().HasMaxLength(30);
            entity.Property(x => x.Aciklama).HasMaxLength(500);
            entity.Property(x => x.Miktar).HasPrecision(18, 4);
            entity.Property(x => x.BirimFiyat).HasPrecision(18, 4);
            entity.Property(x => x.IskontoOrani).HasPrecision(5, 2);
            entity.Property(x => x.IskontoTutari).HasPrecision(18, 4);
            entity.Property(x => x.KdvOrani).HasPrecision(5, 2);
            entity.Property(x => x.KdvTutari).HasPrecision(18, 4);
            entity.Property(x => x.SatirToplami).HasPrecision(18, 4);
            entity.Property(x => x.SevkEdilenMiktar).HasPrecision(18, 4);
            entity.Property(x => x.FaturalananMiktar).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Sirket>(entity =>
        {
            entity.Property(s => s.SirketKodu).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Unvan).IsRequired().HasMaxLength(250);
            entity.Property(s => s.VergiNo).HasMaxLength(20);
            entity.Property(s => s.VergiDairesi).HasMaxLength(100);
            entity.Property(s => s.Telefon).HasMaxLength(30);
            entity.Property(s => s.Email).HasMaxLength(150);
            entity.Property(s => s.Adres).HasMaxLength(500);
        });
        modelBuilder.Entity<Sube>(entity =>
        {
            entity.Property(s => s.SubeKodu).IsRequired().HasMaxLength(50);
            entity.Property(s => s.SubeAdi).IsRequired().HasMaxLength(150);
            entity.Property(s => s.Telefon).HasMaxLength(30);
            entity.Property(s => s.Adres).HasMaxLength(500);
        });
        modelBuilder.Entity<Depo>(entity =>
        {
            entity.Property(d => d.DepoKodu).IsRequired().HasMaxLength(50);
            entity.Property(d => d.DepoAdi).IsRequired().HasMaxLength(150);
        });
        modelBuilder.Entity<MaliDonem>(entity =>
        {
            entity.Property(d => d.DonemAdi).IsRequired().HasMaxLength(50);
        });
        modelBuilder.Entity<ParaBirimi>(entity =>
        {
            entity.Property(p => p.Kod).IsRequired().HasMaxLength(10);
            entity.Property(p => p.Ad).IsRequired().HasMaxLength(50);
            entity.Property(p => p.Sembol).IsRequired().HasMaxLength(10);
        });
        modelBuilder.Entity<KdvOrani>(entity =>
        {
            entity.Property(k => k.Kod).IsRequired().HasMaxLength(20);
            entity.Property(k => k.Oran).HasPrecision(5, 2);
        });
        modelBuilder.Entity<BelgeSerisi>(entity =>
        {
            entity.Property(s => s.BelgeTipi).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Seri).IsRequired().HasMaxLength(20);
            entity.Property(s => s.SonNumara).IsRequired();
            entity.Property(s => s.Prefix).HasMaxLength(20);
            entity.Property(s => s.Format).HasMaxLength(50);
        });
        modelBuilder.Entity<HesapPlani>(entity =>
        {
            entity.Property(h => h.HesapKodu).IsRequired().HasMaxLength(20);
            entity.Property(h => h.HesapAdi).IsRequired().HasMaxLength(200);
        });
        modelBuilder.Entity<YevmiyeFis>(entity =>
        {
            entity.Property(f => f.FisNo).IsRequired().HasMaxLength(50);
            entity.Property(f => f.BelgeTipi).IsRequired().HasMaxLength(50);
            entity.Property(f => f.Aciklama).IsRequired().HasMaxLength(500);
        });
        modelBuilder.Entity<YevmiyeSatiri>(entity =>
        {
            entity.Property(s => s.Borc).HasPrecision(18, 4);
            entity.Property(s => s.Alacak).HasPrecision(18, 4);
            entity.Property(s => s.Aciklama).IsRequired().HasMaxLength(500);
        });

        // İrsaliye ve İrsaliyeKalemi Kısıtlamaları
        modelBuilder.Entity<Irsaliye>(entity =>
        {
            entity.Property(i => i.IrsaliyeNo).IsRequired().HasMaxLength(50);
            entity.Property(i => i.Aciklama).HasMaxLength(500);
            entity.Property(i => i.TeslimatAdSoyad).HasMaxLength(200);
            entity.Property(i => i.TeslimatTelefon).HasMaxLength(30);
            entity.Property(i => i.TeslimatAdres).HasMaxLength(500);
            entity.Property(i => i.TeslimatIl).HasMaxLength(100);
            entity.Property(i => i.TeslimatIlce).HasMaxLength(100);
            entity.Property(i => i.ReferansNo).HasMaxLength(100);
            entity.Property(i => i.Tasiyici).HasMaxLength(150);
            entity.Property(i => i.AracPlaka).HasMaxLength(30);
            entity.Property(i => i.SoforAdi).HasMaxLength(150);
            entity.Property(i => i.SoforTelefon).HasMaxLength(30);
            entity.Property(i => i.AraToplam).HasPrecision(18, 4);
            entity.Property(i => i.KdvToplam).HasPrecision(18, 4);
            entity.Property(i => i.GenelToplam).HasPrecision(18, 4);
            entity.HasIndex(i => i.IrsaliyeNo).IsUnique().HasFilter("\"IrsaliyeNo\" IS NOT NULL AND \"IrsaliyeNo\" <> ''");
        });

        modelBuilder.Entity<IrsaliyeKalemi>(entity =>
        {
            entity.Property(k => k.UrunKodu).IsRequired().HasMaxLength(50);
            entity.Property(k => k.UrunAd).IsRequired().HasMaxLength(200);
            entity.Property(k => k.Barkod).HasMaxLength(100);
            entity.Property(k => k.Aciklama).HasMaxLength(500);
            entity.Property(k => k.Birim).IsRequired().HasMaxLength(20);
            entity.Property(k => k.Miktar).HasPrecision(18, 4);
            entity.Property(k => k.BirimFiyat).HasPrecision(18, 4);
            entity.Property(k => k.KdvOrani).HasPrecision(5, 2);
            entity.Property(k => k.KdvTutari).HasPrecision(18, 4);
            entity.Property(k => k.SatirToplami).HasPrecision(18, 4);
        });
        modelBuilder.Entity<IrsaliyeKalemiStokDagilimi>().Property(x => x.Miktar).HasPrecision(18, 4);

        // Ürün Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<Urun>(entity =>
        {
            entity.Property(u => u.Kod).IsRequired().HasMaxLength(50);
            entity.Property(u => u.Ad).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Birim).IsRequired().HasMaxLength(20);
            entity.Property(u => u.Kategori).HasMaxLength(100);
            entity.Property(u => u.Barkod).HasMaxLength(50);
            entity.Property(u => u.Aciklama).HasColumnType("text");

            entity.Property(u => u.AlisFiyati).HasPrecision(18, 4);
            entity.Property(u => u.PerakendeSatisFiyati).HasPrecision(18, 4);
            entity.Property(u => u.ToptanSatisFiyati).HasPrecision(18, 4);
            entity.Property(u => u.StokMiktari).HasPrecision(18, 4);
            entity.Property(u => u.MinimumStok).HasPrecision(18, 4);
            entity.Property(u => u.KdvOrani).HasPrecision(5, 2);
        });

        // StokHareketi Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<StokHareketi>(entity =>
        {
            entity.Property(h => h.Aciklama).IsRequired().HasMaxLength(500);
            entity.Property(h => h.BelgeNo).HasMaxLength(50);
            entity.Property(h => h.Miktar).HasPrecision(18, 4);
            entity.Property(h => h.BirimFiyat).HasPrecision(18, 4);
        });

        // UrunSatisFiyati Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<UrunSatisFiyati>(entity =>
        {
            entity.Property(f => f.Fiyat).HasPrecision(18, 4);
            entity.HasOne(f => f.Urun)
                .WithMany(u => u.SatisFiyatlari)
                .HasForeignKey(f => f.UrunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UrunLokasyonStogu Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<UrunLokasyonStogu>(entity =>
        {
            entity.Property(ls => ls.MevcutMiktar).HasPrecision(18, 4);
            entity.Property(ls => ls.RezerveMiktar).HasPrecision(18, 4);
            entity.HasOne(ls => ls.Urun)
                .WithMany(u => u.LokasyonStoklari)
                .HasForeignKey(ls => ls.UrunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ls => ls.Depo)
                .WithMany()
                .HasForeignKey(ls => ls.DepoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // FaturaKalemiStokDagilimi Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<FaturaKalemiStokDagilimi>(entity =>
        {
            entity.Property(d => d.Miktar).HasPrecision(18, 4);
            entity.HasOne(d => d.FaturaKalemi)
                .WithMany(k => k.StokDagilimlari)
                .HasForeignKey(d => d.FaturaKalemiId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.Depo)
                .WithMany()
                .HasForeignKey(d => d.DepoId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(c => c.Versiyon).IsConcurrencyToken();
        });
        modelBuilder.Entity<CariTipiTanimi>(entity =>
        {
            entity.Property(x => x.Kod).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Ad).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Aciklama).HasMaxLength(500);
        });
        modelBuilder.Entity<MerkeziTanim>(entity =>
        {
            entity.Property(x => x.Grup).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Kod).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Ad).IsRequired().HasMaxLength(150);
            entity.Property(x => x.KisaAd).HasMaxLength(30);
            entity.Property(x => x.Aciklama).HasMaxLength(500);
        });

        var cariTipiSeedTarihi = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<CariTipiTanimi>().HasData(
            new CariTipiTanimi { Id = CariTipiTanimi.BireyselId, Kod = "BIREYSEL", Ad = "Şahıs / Bireysel", AktifMi = true, SiraNo = 20, OlusturmaTarihi = cariTipiSeedTarihi },
            new CariTipiTanimi { Id = CariTipiTanimi.KurumsalId, Kod = "KURUMSAL", Ad = "Kurumsal / Şirket", AktifMi = true, SiraNo = 10, OlusturmaTarihi = cariTipiSeedTarihi });

        // CariHareket Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<CariHareket>(entity =>
        {
            entity.Property(h => h.Aciklama).IsRequired().HasMaxLength(500);
            entity.Property(h => h.BelgeNo).HasMaxLength(50);
            entity.Property(h => h.Borc).HasPrecision(18, 4);
            entity.Property(h => h.Alacak).HasPrecision(18, 4);
        });

        // Fatura Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<Fatura>(entity =>
        {
            entity.Property(f => f.FaturaNo).IsRequired().HasMaxLength(50);
            entity.Property(f => f.Aciklama).HasMaxLength(500);

            entity.Property(f => f.AraToplam).HasPrecision(18, 4);
            entity.Property(f => f.SatirIskontosuToplam).HasPrecision(18, 4);
            entity.Property(f => f.GenelIskontoOrani).HasPrecision(5, 2);
            entity.Property(f => f.GenelIskontoTutari).HasPrecision(18, 4);
            entity.Property(f => f.KdvToplam).HasPrecision(18, 4);
            entity.Property(f => f.YuvarlamaTutari).HasPrecision(18, 4);
            entity.Property(f => f.GenelToplam).HasPrecision(18, 4);

            entity.Property(f => f.Versiyon).IsConcurrencyToken();
            entity.HasIndex(f => f.FaturaNo).IsUnique().HasFilter("\"FaturaNo\" IS NOT NULL AND \"FaturaNo\" <> ''");
        });

        // FaturaKalemi Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<FaturaKalemi>(entity =>
        {
            entity.Property(k => k.UrunKodu).IsRequired().HasMaxLength(50);
            entity.Property(k => k.UrunAd).IsRequired().HasMaxLength(200);
            entity.Property(k => k.Birim).IsRequired().HasMaxLength(20);

            entity.Property(k => k.Miktar).HasPrecision(18, 4);
            entity.Property(k => k.BirimFiyat).HasPrecision(18, 4);
            entity.Property(k => k.IskontoOrani).HasPrecision(5, 2);
            entity.Property(k => k.IskontoTutari).HasPrecision(18, 4);
            entity.Property(k => k.KdvOrani).HasPrecision(5, 2);
            entity.Property(k => k.KdvTutari).HasPrecision(18, 4);
            entity.Property(k => k.SatirToplami).HasPrecision(18, 4);
        });

        // KasaHareketi Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<KasaHareketi>(entity =>
        {
            entity.Property(k => k.Aciklama).IsRequired().HasMaxLength(500);
            entity.Property(k => k.Belge).HasMaxLength(50);
            entity.Property(k => k.Tutar).HasPrecision(18, 4);
            entity.HasIndex(k => k.Tarih);
        });

        // BankaHareketi Kısıtlamaları ve Hassasiyetleri
        modelBuilder.Entity<BankaHareketi>(entity =>
        {
            entity.Property(b => b.Aciklama).IsRequired().HasMaxLength(500);
            entity.Property(b => b.HesapNo).HasMaxLength(50);
            entity.Property(b => b.BankaAdi).HasMaxLength(100);
            entity.Property(b => b.Tutar).HasPrecision(18, 4);
            entity.HasIndex(b => b.Tarih);
        });

        // Seed Data
        modelBuilder.Entity<Kullanici>().HasData(
            new Kullanici 
            { 
                Id = 1, 
                KullaniciAdi = "admin", 
                SifreHash = "$2a$11$bFJTJsZZdqJQwi75rB4wM.DQgaRKmvwOSvi0VYrjuKdFj1/QC2UfS", 
                Ad = "Sistem", 
                Soyad = "Yöneticisi", 
                Rol = "Yonetici", 
                Aktif = true,
                OlusturmaTarihi = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Sirket>().HasData(new Sirket
        {
            Id = VarsayilanSirketId,
            SirketKodu = "MERKEZ",
            Unvan = "Varsayılan Şirket",
            AktifMi = true,
            OlusturmaTarihi = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc)
        });
        modelBuilder.Entity<Sube>().HasData(new Sube
        {
            Id = 1,
            SirketId = VarsayilanSirketId,
            SubeKodu = "MERKEZ",
            SubeAdi = "Merkez Şube",
            AktifMi = true,
            OlusturmaTarihi = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc)
        });
        var depoSeedTarihi = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Depo>().HasData(
            new Depo { Id = 1, SubeId = 1, DepoKodu = "MAGAZA", DepoAdi = "Mağaza", DepoTuru = DepoTuru.Magaza, AktifMi = true, OlusturmaTarihi = depoSeedTarihi },
            new Depo { Id = 2, SubeId = 1, DepoKodu = "DEPO1", DepoAdi = "Depo 1", DepoTuru = DepoTuru.Depo, AktifMi = true, OlusturmaTarihi = depoSeedTarihi },
            new Depo { Id = 3, SubeId = 1, DepoKodu = "DEPO2", DepoAdi = "Depo 2", DepoTuru = DepoTuru.Depo, AktifMi = true, OlusturmaTarihi = depoSeedTarihi }
        );
        modelBuilder.Entity<MaliDonem>().HasData(new MaliDonem
        {
            Id = 1,
            SirketId = VarsayilanSirketId,
            DonemAdi = "2026",
            Baslangic = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Bitis = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            OlusturmaTarihi = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc)
        });
        modelBuilder.Entity<ParaBirimi>().HasData(new ParaBirimi
        {
            Id = 1,
            Kod = "TRY",
            Ad = "Türk Lirası",
            Sembol = "₺",
            VarsayilanMi = true,
            OlusturmaTarihi = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc)
        });
        modelBuilder.Entity<KdvOrani>().HasData(new KdvOrani
        {
            Id = 1,
            Kod = "KDV20",
            Oran = 20,
            AktifMi = true,
            OlusturmaTarihi = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc)
        });
        modelBuilder.Entity<BelgeSerisi>().HasData(new BelgeSerisi
        {
            Id = 1,
            SirketId = VarsayilanSirketId,
            BelgeTipi = "Fatura",
            Seri = "FTR",
            SonNumara = 0,
            Prefix = "FTR",
            Format = "{0}{1:000000}",
            OlusturmaTarihi = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc)
        });
        var hesapSeedTarihi = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<HesapPlani>().HasData(
            new HesapPlani { Id = 100, SirketId = VarsayilanSirketId, HesapKodu = "100", HesapAdi = "KASA", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Merkez Ve Şube Kasaları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 102, SirketId = VarsayilanSirketId, HesapKodu = "102", HesapAdi = "BANKALAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Banka Mevduat Hesapları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 108, SirketId = VarsayilanSirketId, HesapKodu = "108", HesapAdi = "DİĞER HAZIR DEĞERLER (POS)", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Kredi Kartı POS Tahsilatları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 120, SirketId = VarsayilanSirketId, HesapKodu = "120", HesapAdi = "ALICILAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Müşteri Alacakları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 153, SirketId = VarsayilanSirketId, HesapKodu = "153", HesapAdi = "TİCARİ MALLAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Stok Ve Ticari Mallar", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 191, SirketId = VarsayilanSirketId, HesapKodu = "191", HesapAdi = "İNDİRİLECEK KDV", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Alış KDV Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 320, SirketId = VarsayilanSirketId, HesapKodu = "320", HesapAdi = "SATICILAR", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Tedarikçi Borçları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 391, SirketId = VarsayilanSirketId, HesapKodu = "391", HesapAdi = "HESAPLANAN KDV", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Satış KDV Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 600, SirketId = VarsayilanSirketId, HesapKodu = "600", HesapAdi = "YURT İÇİ SATIŞLAR", Tip = HesapTipi.Gelir, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Satış Gelirleri", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 610, SirketId = VarsayilanSirketId, HesapKodu = "610", HesapAdi = "SATIŞTAN İADELER (-)", Tip = HesapTipi.Gelir, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Satış İade İskontoları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 621, SirketId = VarsayilanSirketId, HesapKodu = "621", HesapAdi = "SATILAN TİCARİ MALLAR MALİYETİ (-)", Tip = HesapTipi.Gider, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Satılan Malların Maliyeti", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 653, SirketId = VarsayilanSirketId, HesapKodu = "653", HesapAdi = "KOMİSYON GİDERLERİ (-)", Tip = HesapTipi.Gider, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Banka Ve POS Komisyon Giderleri", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new HesapPlani { Id = 780, SirketId = VarsayilanSirketId, HesapKodu = "780", HesapAdi = "FİNANSMAN GİDERLERİ", Tip = HesapTipi.Gider, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, ParaBirimiKodu = "TRY", SistemHesabi = true, Aciklama = "Finansal Faiz Ve Masraf Giderleri", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi });

        modelBuilder.Entity<MuhasebeBaglantiKodu>().HasData(
            new MuhasebeBaglantiKodu { Id = 1, SirketId = VarsayilanSirketId, KategoriKodu = "KASA", KategoriAdi = "Kasa Hesabı", VarSayilanHesapKodu = "100", Aciklama = "Varsayılan Nakit Kasa Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 2, SirketId = VarsayilanSirketId, KategoriKodu = "BANKA", KategoriAdi = "Banka Hesabı", VarSayilanHesapKodu = "102", Aciklama = "Varsayılan Banka Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 3, SirketId = VarsayilanSirketId, KategoriKodu = "POS", KategoriAdi = "POS Tahsilat Hesabı", VarSayilanHesapKodu = "108", Aciklama = "POS Kredi Kartı Alacakları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 4, SirketId = VarsayilanSirketId, KategoriKodu = "ALICILAR", KategoriAdi = "Alıcılar / Müşteri", VarSayilanHesapKodu = "120", Aciklama = "Ticari Müşteri Alacakları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 5, SirketId = VarsayilanSirketId, KategoriKodu = "SATICILAR", KategoriAdi = "Satıcılar / Tedarikçi", VarSayilanHesapKodu = "320", Aciklama = "Ticari Tedarikçi Borçları", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 6, SirketId = VarsayilanSirketId, KategoriKodu = "YURTICI_SATIS", KategoriAdi = "Yurtiçi Satışlar", VarSayilanHesapKodu = "600", Aciklama = "Brüt Satış Geliri Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 7, SirketId = VarsayilanSirketId, KategoriKodu = "SATIS_KDV", KategoriAdi = "Hesaplanan KDV (Satış)", VarSayilanHesapKodu = "391", Aciklama = "Satış KDV Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 8, SirketId = VarsayilanSirketId, KategoriKodu = "INDIRILECEK_KDV", KategoriAdi = "İndirilecek KDV (Alış)", VarSayilanHesapKodu = "191", Aciklama = "Alış İndirilecek KDV Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 9, SirketId = VarsayilanSirketId, KategoriKodu = "STOK", KategoriAdi = "Ticari Mallar / Stok", VarSayilanHesapKodu = "153", Aciklama = "Ticari Mallar Stok Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 10, SirketId = VarsayilanSirketId, KategoriKodu = "SATIS_IADE", KategoriAdi = "Satıştan İadeler", VarSayilanHesapKodu = "610", Aciklama = "Satış İadesi Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 11, SirketId = VarsayilanSirketId, KategoriKodu = "STMM", KategoriAdi = "Satılan Malın Maliyeti", VarSayilanHesapKodu = "621", Aciklama = "STMM Maliyet Hesabı", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi },
            new MuhasebeBaglantiKodu { Id = 12, SirketId = VarsayilanSirketId, KategoriKodu = "KOMISYON", KategoriAdi = "Banka / POS Komisyon Gideri", VarSayilanHesapKodu = "653", Aciklama = "Banka Komisyon Kesintileri", AktifMi = true, OlusturmaTarihi = hesapSeedTarihi });

        modelBuilder.Entity<KullaniciYetkileri>().HasData(
            new KullaniciYetkileri
            {
                Id = 1,
                KullaniciId = 1,
                // Tüm yetkiler Süper Admin için açık
                CariGoruntule = true, CariEkle = true, CariDuzenle = true, CariSil = true,
                PersonelGoruntule = true, PersonelEkle = true, PersonelDuzenle = true, PersonelSil = true,
                BankaGoruntule = true, BankaEkle = true, BankaDuzenle = true, BankaSil = true,
                StokGoruntule = true, StokEkle = true, StokDuzenle = true, StokSil = true, StokFiyatDegistir = true,
                SatisYap = true, SatisIptalEt = true, SatisBeklet = true, FisYazdir = true, SatisGecmisiGoruntule = true,
                KasaYonet = true, TahsilatYap = true, OdemeYap = true, GelirEkle = true, GiderEkle = true,
                RaporGoruntule = true, FinansRaporuGoruntule = true, StokRaporuGoruntule = true, SatisRaporuGoruntule = true,
                FaturaGoruntule = true, FaturaOlustur = true, FaturaOnayla = true, FaturaIptal = true,
                IrsaliyeGoruntule = true, IrsaliyeOlustur = true, IrsaliyeOnayla = true, IrsaliyeIptal = true,
                MuhasebeFisGoruntule = true, MuhasebeFisEkle = true, MuhasebeFisDuzenle = true, MuhasebeFisOnayla = true, MuhasebeFisIptal = true, HesapPlaniYonet = true, MizanGoruntule = true, MaliyetHesaplariGoruntule = true,
                AyarlariYonet = true, KullaniciYonet = true, AuditLogGoruntule = true,
                OlusturmaTarihi = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<SistemAyarlari>().HasData(
            new SistemAyarlari
            {
                Id = 1,
                FirmaUnvani = "Örnek Ticaret Ltd. Şti.",
                VergiDairesi = "Kadıköy V.D.",
                VergiNo = "1234567890",
                Telefon = "0216 555 00 00",
                Eposta = "info@ornekticaret.com",
                WebSitesi = "www.ornekticaret.com",
                Adres = "Atatürk Cad. No: 15/A Kadıköy / İstanbul",
                FisHeader = "Örnek Ticaret Ltd. Şti.",
                FisFooter = "Bizi tercih ettiğiniz için teşekkür ederiz!",
                VarsayilanKdv = 20.00m,
                ParaBirimiSembolu = "₺",
                ParaBirimiKodu = "TRY",
                VarsayilanVadeGunu = 30,
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
                PerakendeSatisFiyati = 65.0000m, 
                ToptanSatisFiyati = 60.0000m, 
                KdvOrani = 20.00m, 
                StokMiktari = 150.0000m, 
                MinimumStok = 20.0000m, 
                Aktif = true,
                Versiyon = 1,
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
                PerakendeSatisFiyati = 399.0000m, 
                ToptanSatisFiyati = 350.0000m,
                KdvOrani = 20.00m, 
                StokMiktari = 25.0000m, 
                MinimumStok = 5.0000m, 
                Aktif = true,
                OlusturmaTarihi = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    // Fatura, çek/senet gibi modüller kasa/hesap seçmeden hareket oluşturabiliyor; böyle bir hareket
    // hiçbir kasanın bakiyesinde görünmez. Kasa boşsa varsayılan (ilk aktif) kasaya, banka hesabı boşsa
    // sistemde tek aktif hesap varsa ona bağlanır.
    private async Task KasaVeBankaBaglantisiniTamamlaAsync(CancellationToken cancellationToken)
    {
        var kasasizlar = ChangeTracker.Entries<KasaHareketi>()
            .Where(e => e.State == EntityState.Added && e.Entity.KasaId == null).Select(e => e.Entity).ToList();
        if (kasasizlar.Count > 0)
        {
            var kasa = await Kasalar.AsNoTracking().Where(k => k.AktifMi).OrderBy(k => k.Id)
                .Select(k => new { k.Id, k.SirketId }).FirstOrDefaultAsync(cancellationToken);
            if (kasa != null)
                foreach (var h in kasasizlar) { h.KasaId = kasa.Id; h.SirketId ??= kasa.SirketId; }
        }

        var hesapsizlar = ChangeTracker.Entries<BankaHareketi>()
            .Where(e => e.State == EntityState.Added && e.Entity.BankaHesabiId == null).Select(e => e.Entity).ToList();
        if (hesapsizlar.Count > 0)
        {
            var hesaplar = await BankaHesaplari.AsNoTracking().Where(b => b.AktifMi).Take(2)
                .Select(b => new { b.Id, b.SirketId }).ToListAsync(cancellationToken);
            if (hesaplar.Count == 1)
                foreach (var h in hesapsizlar) { h.BankaHesabiId = hesaplar[0].Id; h.SirketId ??= hesaplar[0].SirketId; }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await KasaVeBankaBaglantisiniTamamlaAsync(cancellationToken);

        foreach (var entry in ChangeTracker.Entries<CariKart>().Where(e => e.State == EntityState.Added))
            entry.Entity.SirketId ??= VarsayilanSirketId;

        foreach (var entry in ChangeTracker.Entries<Urun>().Where(e => e.State == EntityState.Added))
            entry.Entity.SirketId ??= VarsayilanSirketId;

        foreach (var entry in ChangeTracker.Entries<Fatura>().Where(e => e.State == EntityState.Added))
            entry.Entity.SirketId ??= VarsayilanSirketId;

        // 1. CariKart bakiye güncellemelerini tetikleyen CariHareket değişikliklerini tespit edip delta hesapla
        var cariDeltas = new Dictionary<int, (decimal BorcDelta, decimal AlacakDelta)>();

        foreach (var entry in ChangeTracker.Entries<CariHareket>())
        {
            if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                continue;

            int? oldCariKartId = null;
            int? newCariKartId = null;

            decimal oldBorc = 0;
            decimal oldAlacak = 0;
            bool oldSilindi = false;

            decimal newBorc = 0;
            decimal newAlacak = 0;
            bool newSilindi = false;

            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                oldCariKartId = entry.OriginalValues.GetValue<int>("CariKartId");
                oldBorc = entry.OriginalValues.GetValue<decimal>("Borc");
                oldAlacak = entry.OriginalValues.GetValue<decimal>("Alacak");
                oldSilindi = entry.OriginalValues.GetValue<bool>("Silindi");
            }

            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
            {
                newCariKartId = entry.CurrentValues.GetValue<int>("CariKartId");
                newBorc = entry.CurrentValues.GetValue<decimal>("Borc");
                newAlacak = entry.CurrentValues.GetValue<decimal>("Alacak");
                newSilindi = entry.CurrentValues.GetValue<bool>("Silindi");
            }

            // Fiziksel olarak silindiyse
            if (entry.State == EntityState.Deleted)
            {
                if (oldCariKartId.HasValue && !oldSilindi)
                {
                    var current = cariDeltas.GetValueOrDefault(oldCariKartId.Value);
                    cariDeltas[oldCariKartId.Value] = (current.BorcDelta - oldBorc, current.AlacakDelta - oldAlacak);
                }
            }
            // Eklendiyse
            else if (entry.State == EntityState.Added)
            {
                if (newCariKartId.HasValue && !newSilindi)
                {
                    var current = cariDeltas.GetValueOrDefault(newCariKartId.Value);
                    cariDeltas[newCariKartId.Value] = (current.BorcDelta + newBorc, current.AlacakDelta + newAlacak);
                }
            }
            // Güncellendiyse
            else if (entry.State == EntityState.Modified)
            {
                if (oldCariKartId != newCariKartId)
                {
                    // Eski cari karttan düş
                    if (oldCariKartId.HasValue && !oldSilindi)
                    {
                        var current = cariDeltas.GetValueOrDefault(oldCariKartId.Value);
                        cariDeltas[oldCariKartId.Value] = (current.BorcDelta - oldBorc, current.AlacakDelta - oldAlacak);
                    }
                    // Yeni cari karta ekle
                    if (newCariKartId.HasValue && !newSilindi)
                    {
                        var current = cariDeltas.GetValueOrDefault(newCariKartId.Value);
                        cariDeltas[newCariKartId.Value] = (current.BorcDelta + newBorc, current.AlacakDelta + newAlacak);
                    }
                }
                else if (newCariKartId.HasValue)
                {
                    var cardId = newCariKartId.Value;
                    var current = cariDeltas.GetValueOrDefault(cardId);

                    if (!oldSilindi && newSilindi)
                    {
                        cariDeltas[cardId] = (current.BorcDelta - oldBorc, current.AlacakDelta - oldAlacak);
                    }
                    else if (oldSilindi && !newSilindi)
                    {
                        cariDeltas[cardId] = (current.BorcDelta + newBorc, current.AlacakDelta + newAlacak);
                    }
                    else if (!oldSilindi && !newSilindi)
                    {
                        cariDeltas[cardId] = (current.BorcDelta + (newBorc - oldBorc), current.AlacakDelta + (newAlacak - oldAlacak));
                    }
                }
            }
        }

        // 2. StokHareketi değişikliklerini tespit edip delta envanter hesabı yap
        var stokDeltas = new Dictionary<int, decimal>();

        foreach (var entry in ChangeTracker.Entries<StokHareketi>())
        {
            if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                continue;

            int? oldUrunId = null;
            int? newUrunId = null;

            decimal oldMiktar = 0;
            StokHareketTuru oldTuru = StokHareketTuru.Giris;
            bool oldSilindi = false;

            decimal newMiktar = 0;
            StokHareketTuru newTuru = StokHareketTuru.Giris;
            bool newSilindi = false;

            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                oldUrunId = entry.OriginalValues.GetValue<int>("UrunId");
                oldMiktar = entry.OriginalValues.GetValue<decimal>("Miktar");
                oldTuru = entry.OriginalValues.GetValue<StokHareketTuru>("HareketTuru");
                oldSilindi = entry.OriginalValues.GetValue<bool>("Silindi");
            }

            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
            {
                newUrunId = entry.CurrentValues.GetValue<int>("UrunId");
                newMiktar = entry.CurrentValues.GetValue<decimal>("Miktar");
                newTuru = entry.CurrentValues.GetValue<StokHareketTuru>("HareketTuru");
                newSilindi = entry.CurrentValues.GetValue<bool>("Silindi");
            }

            decimal GetSign(StokHareketTuru tur) => tur == StokHareketTuru.Giris ? 1 : -1;

            // Fiziksel Silme
            if (entry.State == EntityState.Deleted)
            {
                if (oldUrunId.HasValue && !oldSilindi)
                {
                    stokDeltas[oldUrunId.Value] = stokDeltas.GetValueOrDefault(oldUrunId.Value) - (oldMiktar * GetSign(oldTuru));
                }
            }
            // Ekleme
            else if (entry.State == EntityState.Added)
            {
                if (newUrunId.HasValue && !newSilindi)
                {
                    stokDeltas[newUrunId.Value] = stokDeltas.GetValueOrDefault(newUrunId.Value) + (newMiktar * GetSign(newTuru));
                }
            }
            // Güncelleme
            else if (entry.State == EntityState.Modified)
            {
                if (oldUrunId != newUrunId)
                {
                    if (oldUrunId.HasValue && !oldSilindi)
                    {
                        stokDeltas[oldUrunId.Value] = stokDeltas.GetValueOrDefault(oldUrunId.Value) - (oldMiktar * GetSign(oldTuru));
                    }
                    if (newUrunId.HasValue && !newSilindi)
                    {
                        stokDeltas[newUrunId.Value] = stokDeltas.GetValueOrDefault(newUrunId.Value) + (newMiktar * GetSign(newTuru));
                    }
                }
                else if (newUrunId.HasValue)
                {
                    var urunId = newUrunId.Value;
                    var curDelta = stokDeltas.GetValueOrDefault(urunId);

                    if (!oldSilindi && newSilindi)
                    {
                        stokDeltas[urunId] = curDelta - (oldMiktar * GetSign(oldTuru));
                    }
                    else if (oldSilindi && !newSilindi)
                    {
                        stokDeltas[urunId] = curDelta + (newMiktar * GetSign(newTuru));
                    }
                    else if (!oldSilindi && !newSilindi)
                    {
                        decimal oldVal = oldMiktar * GetSign(oldTuru);
                        decimal newVal = newMiktar * GetSign(newTuru);
                        stokDeltas[urunId] = curDelta + (newVal - oldVal);
                    }
                }
            }
        }

        // 3. CariKart bakiyelerini delta kadar güncelle
        foreach (var kvp in cariDeltas)
        {
            var cariKartId = kvp.Key;
            var (borcDelta, alacakDelta) = kvp.Value;

            if (borcDelta == 0 && alacakDelta == 0)
                continue;

            var cariKart = await CariKartlar.FirstOrDefaultAsync(c => c.Id == cariKartId, cancellationToken);
            if (cariKart != null)
            {
                cariKart.Borc += borcDelta;
                cariKart.Alacak += alacakDelta;
                cariKart.GuncellemeTarihi = DateTime.UtcNow;

                var entry = Entry(cariKart);
                if (!entry.Property(c => c.Versiyon).IsModified)
                {
                    cariKart.Versiyon++;
                }
            }
        }

        // 4. Urun stok miktarlarını delta kadar güncelle ve negatif stok kontrollerini gerçekleştir
        foreach (var kvp in stokDeltas)
        {
            var urunId = kvp.Key;
            var delta = kvp.Value;

            if (delta == 0)
                continue;

            var urun = await Urunler.FirstOrDefaultAsync(u => u.Id == urunId, cancellationToken);
            if (urun != null)
            {
                urun.StokMiktari += delta;
                urun.GuncellemeTarihi = DateTime.UtcNow;

                // Eksi stoğa düşme kontrolü (Yetersiz stok hatası fırlatarak transaction rollback et)
                if (urun.StokMiktari < 0)
                {
                    throw new InvalidOperationException($"'{urun.Ad}' ürünü için yetersiz stok! Stok miktarı eksiye düşemez. Kalan: {urun.StokMiktari - delta}, Talep Edilen Fark: {delta}");
                }
            }
        }

        // 5. Doğrudan değiştirilen CariKart'ların Concurrency Versiyon değerini arttır
        var degisenCariKartlar = ChangeTracker.Entries<CariKart>()
            .Where(e => e.State == EntityState.Modified)
            .ToList();

        foreach (var entry in degisenCariKartlar)
        {
            if (!entry.Property(c => c.Versiyon).IsModified)
            {
                entry.Entity.Versiyon++;
                entry.Entity.GuncellemeTarihi = DateTime.UtcNow;
            }
        }

        // 6. Doğrudan değiştirilen Fatura'ların Concurrency Versiyon değerini arttır
        var degisenFaturalar = ChangeTracker.Entries<Fatura>()
            .Where(e => e.State == EntityState.Modified)
            .ToList();

        foreach (var entry in degisenFaturalar)
        {
            if (!entry.Property(f => f.Versiyon).IsModified)
            {
                entry.Entity.Versiyon++;
                entry.Entity.GuncellemeTarihi = DateTime.UtcNow;
            }
        }

        // 7. Doğrudan değiştirilen Urun'lerin Concurrency Versiyon değerini arttır
        var degisenUrunler = ChangeTracker.Entries<Urun>()
            .Where(e => e.State == EntityState.Modified)
            .ToList();

        foreach (var entry in degisenUrunler)
        {
            if (!entry.Property(u => u.Versiyon).IsModified)
            {
                entry.Entity.Versiyon++;
                entry.Entity.GuncellemeTarihi = DateTime.UtcNow;
            }
        }

        // 8. Doğrudan değiştirilen Irsaliye'lerin Concurrency Versiyon değerini arttır
        var degisenIrsaliyeler = ChangeTracker.Entries<Irsaliye>()
            .Where(e => e.State == EntityState.Modified)
            .ToList();

        foreach (var entry in degisenIrsaliyeler)
        {
            if (!entry.Property(i => i.Versiyon).IsModified)
            {
                entry.Entity.Versiyon++;
                entry.Entity.GuncellemeTarihi = DateTime.UtcNow;
            }
        }

        var degisenSiparisler = ChangeTracker.Entries<Siparis>()
            .Where(e => e.State == EntityState.Modified).ToList();
        foreach (var entry in degisenSiparisler)
        {
            if (!entry.Property(x => x.Versiyon).IsModified)
            {
                entry.Entity.Versiyon++;
                entry.Entity.GuncellemeTarihi = DateTime.UtcNow;
            }
        }

        // 9. OTOMATİK AUDIT LOG INTERCEPTION (AuditLog hariç tüm değişiklikleri yakala)
        var auditKayitlari = new List<(AuditLog log, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)>();
        var izlenenVarliklar = ChangeTracker.Entries()
            .Where(e => !(e.Entity is AuditLog) && 
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList();

        foreach (var entry in izlenenVarliklar)
        {
            var tabloAdi = entry.Entity.GetType().Name;
            var kayitIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            var kayitId = kayitIdProp?.CurrentValue?.ToString() ?? "0";

            var auditLog = new AuditLog
            {
                TabloAdi = tabloAdi,
                KayitId = kayitId,
                Tarih = DateTime.UtcNow,
                KullaniciId = _aktifFirmaBaglami?.KullaniciId,
                KullaniciAdi = _aktifFirmaBaglami?.KullaniciId is null ? "Sistem" : _aktifFirmaBaglami.KullaniciAdi,
                IstemciBilgisi = Environment.MachineName
            };

            if (entry.State == EntityState.Added)
            {
                auditLog.IslemTuru = AuditIslemTuru.Ekleme;
                var yeniDegerler = entry.Properties
                    .Where(p => DenetimAlaninaDahilEt(p.Metadata.Name))
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                auditLog.YeniDegerJson = System.Text.Json.JsonSerializer.Serialize(yeniDegerler);
            }
            else if (entry.State == EntityState.Deleted)
            {
                auditLog.IslemTuru = AuditIslemTuru.Silme;
                var eskiDegerler = entry.Properties
                    .Where(p => DenetimAlaninaDahilEt(p.Metadata.Name))
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                auditLog.EskiDegerJson = System.Text.Json.JsonSerializer.Serialize(eskiDegerler);
            }
            else if (entry.State == EntityState.Modified)
            {
                // Soft delete kontrolü
                var silindiProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Silindi");
                bool isSoftDelete = silindiProp != null && 
                                   silindiProp.OriginalValue is false && 
                                   silindiProp.CurrentValue is true;

                auditLog.IslemTuru = isSoftDelete ? AuditIslemTuru.SoftDelete : AuditIslemTuru.Guncelleme;

                var degisenEski = new Dictionary<string, object?>();
                var degisenYeni = new Dictionary<string, object?>();

                foreach (var prop in entry.Properties.Where(p => p.IsModified && DenetimAlaninaDahilEt(p.Metadata.Name)))
                {
                    degisenEski[prop.Metadata.Name] = prop.OriginalValue;
                    degisenYeni[prop.Metadata.Name] = prop.CurrentValue;
                }

                auditLog.EskiDegerJson = System.Text.Json.JsonSerializer.Serialize(degisenEski);
                auditLog.YeniDegerJson = System.Text.Json.JsonSerializer.Serialize(degisenYeni);
            }

            auditKayitlari.Add((auditLog, entry));
        }

        // Yeni kimlik değerleri için iki SaveChanges gerekir; ikisi de aynı işlemde kalmalı.
        // Çağıran servis bir işlem açtıysa onun sahipliğini burada devralmayız.
        await using var auditTransaction = auditKayitlari.Count > 0 && Database.CurrentTransaction == null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        var sonuc = await base.SaveChangesAsync(cancellationToken);

        // Save sonrası: Yeni eklenen kayıtların oluşan gerçek Primary Key (Id) değerlerini güncelle
        if (auditKayitlari.Count > 0)
        {
            foreach (var item in auditKayitlari)
            {
                if (item.log.IslemTuru == AuditIslemTuru.Ekleme)
                {
                    var pkProp = item.entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                    if (pkProp?.CurrentValue != null)
                    {
                        item.log.KayitId = pkProp.CurrentValue.ToString() ?? "0";
                    }
                }
                AuditLoglar.Add(item.log);
            }

            await base.SaveChangesAsync(cancellationToken);
        }

        if (auditTransaction != null)
            await auditTransaction.CommitAsync(cancellationToken);

        return sonuc;
    }

    private static bool DenetimAlaninaDahilEt(string alanAdi) =>
        !string.Equals(alanAdi, nameof(Kullanici.SifreHash), StringComparison.Ordinal);
}
