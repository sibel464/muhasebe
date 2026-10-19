using Altyapi.Veri;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class AgAyarlariTests
{
    [Fact]
    public void Baglanti_kodu_uretilir_cozulur_bosluk_ve_satir_kirilmalarina_dayanir()
    {
        var kod = new AgAyarlari.BaglantiKodu(["MUHASEBE-PC", "192.168.1.20"], 5544, "Muhasebe", "postgres", "gizli+/=şifre");
        var metin = AgAyarlari.KodUret(kod);
        Assert.StartsWith("MYS1-", metin);
        Assert.DoesNotContain("gizli", metin);

        var cozulen = AgAyarlari.KodCoz("  " + metin[..20] + "\r\n" + metin[20..] + " ");
        Assert.NotNull(cozulen);
        Assert.Equal(kod.Adresler, cozulen.Adresler);
        Assert.Equal(5544, cozulen.Port);
        Assert.Equal("gizli+/=şifre", cozulen.Sifre);

        Assert.Null(AgAyarlari.KodCoz("MYS1-bozuk!!"));
        Assert.Null(AgAyarlari.KodCoz("baska-kod"));
        Assert.Null(AgAyarlari.KodCoz(""));
    }

    [Fact]
    public void Ag_bloklari_tekrar_yazilinca_cogalmaz_ve_kaldirilinca_ozgun_icerik_kalir()
    {
        const string conf = "port = 5544\nmax_connections = 100\n";
        var bir = AgAyarlari.KonfigurasyonaAgBloguYaz(conf);
        var iki = AgAyarlari.KonfigurasyonaAgBloguYaz(bir);
        Assert.Equal(bir, iki);
        Assert.Contains("listen_addresses = '*'", iki);
        var geri = AgAyarlari.BlokuKaldir(iki);
        Assert.DoesNotContain("listen_addresses", geri);
        Assert.Contains("max_connections = 100", geri);

        var hba = AgAyarlari.ErisimeAgBloguYaz("host all all 127.0.0.1/32 scram-sha-256\n");
        Assert.Contains("samenet", hba);
        Assert.Contains("127.0.0.1/32", AgAyarlari.BlokuKaldir(hba));
        Assert.DoesNotContain("samenet", AgAyarlari.BlokuKaldir(hba));
    }

    [Fact]
    public void Mod_cozumu_bilinmeyende_tek_bilgisayar()
    {
        Assert.Equal(AgModu.Sunucu, AgAyarlari.ModCoz("sunucu"));
        Assert.Equal(AgModu.Istemci, AgAyarlari.ModCoz("Istemci"));
        Assert.Equal(AgModu.Tek, AgAyarlari.ModCoz(null));
        Assert.Equal(AgModu.Tek, AgAyarlari.ModCoz("saçma"));
    }
}
