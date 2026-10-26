using System.Net;
using System.Net.Sockets;
using System.Text;
using Altyapi.Eposta;
using EtkiAlani.Modeller.Ayar;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class EpostaTests
{
    // Testte gerçek sunucu yerine yalın SMTP konuşan küçük bir sunucu: AUTH PLAIN'i doğrular, gelen iletiyi saklar.
    private sealed class SahteSmtp : IAsyncDisposable
    {
        private readonly TcpListener _dinleyici = new(IPAddress.Loopback, 0);
        private readonly Task _gorev;
        public int Port => ((IPEndPoint)_dinleyici.LocalEndpoint).Port;
        public string Ileti { get; private set; } = "";
        public string Kimlik { get; private set; } = "";

        public SahteSmtp(string beklenenSifre)
        {
            _dinleyici.Start();
            _gorev = Task.Run(async () =>
            {
                using var istemci = await _dinleyici.AcceptTcpClientAsync();
                using var akis = istemci.GetStream();
                var okuyucu = new StreamReader(akis, Encoding.ASCII);
                var yazici = new StreamWriter(akis, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                await yazici.WriteLineAsync("220 sahte ESMTP");
                while (await okuyucu.ReadLineAsync() is { } satir)
                {
                    var komut = satir.ToUpperInvariant();
                    if (komut.StartsWith("EHLO")) { await yazici.WriteLineAsync("250-sahte"); await yazici.WriteLineAsync("250-AUTH PLAIN"); await yazici.WriteLineAsync("250 OK"); }
                    else if (komut.StartsWith("AUTH PLAIN"))
                    {
                        var parca = satir.Split(' ');
                        var b64 = parca.Length > 2 ? parca[2] : "";
                        if (b64.Length == 0) { await yazici.WriteLineAsync("334 "); b64 = await okuyucu.ReadLineAsync() ?? ""; }
                        Kimlik = Encoding.UTF8.GetString(Convert.FromBase64String(b64)).Replace('\0', '|');
                        await yazici.WriteLineAsync(Kimlik.EndsWith("|" + beklenenSifre) ? "235 tamam" : "535 kimlik hatali");
                    }
                    else if (komut.StartsWith("MAIL") || komut.StartsWith("RCPT") || komut.StartsWith("RSET") || komut.StartsWith("NOOP")) await yazici.WriteLineAsync("250 OK");
                    else if (komut == "DATA")
                    {
                        await yazici.WriteLineAsync("354 devam");
                        var sb = new StringBuilder();
                        while (await okuyucu.ReadLineAsync() is { } veri && veri != ".") sb.AppendLine(veri);
                        Ileti = sb.ToString();
                        await yazici.WriteLineAsync("250 alindi");
                    }
                    else if (komut == "QUIT") { await yazici.WriteLineAsync("221 bye"); break; }
                    else await yazici.WriteLineAsync("250 OK");
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            try { await _gorev.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            _dinleyici.Stop();
        }
    }

    private static SistemAyarlari Ayar(int port, string sifre) => new()
    {
        FirmaUnvani = "Deneme Ticaret", SmtpSunucu = "127.0.0.1", SmtpPort = port, SmtpGuvenlik = 3,
        SmtpKullanici = "muhasebe@deneme.com", SmtpSifre = SmtpSifreleyici.Sifrele(sifre), EpostaGonderenAdi = "Deneme Muhasebe"
    };

    [Fact]
    public void Smtp_sifresi_sifrelenir_ve_cozulur()
    {
        var kayit = SmtpSifreleyici.Sifrele("Gizli-Şifre 123");
        Assert.StartsWith("v1:", kayit);
        Assert.DoesNotContain("Gizli", kayit);
        Assert.Equal("Gizli-Şifre 123", SmtpSifreleyici.Coz(kayit));
        Assert.Null(SmtpSifreleyici.Coz(null));
    }

    [Fact]
    public async Task Ekli_eposta_kimlik_dogrulamayla_gonderilir()
    {
        await using var sunucu = new SahteSmtp("dogru-sifre");
        var servis = new EpostaServisi(() => Task.FromResult(Ayar(sunucu.Port, "dogru-sifre")));
        Assert.True(await servis.YapilandirildiMiAsync());
        await servis.GonderAsync(new EpostaMesaji
        {
            Alicilar = "musteri@ornek.com; muhasebe2@ornek.com", Konu = "Ekim ayı ekstreniz", Metin = "Ekte bilgilerinize sunarız.",
            Ekler = [new EpostaEki("Ekstre.pdf", Encoding.ASCII.GetBytes("%PDF-1.4 deneme"))]
        });
        await sunucu.DisposeAsync();
        Assert.Contains("muhasebe@deneme.com|dogru-sifre", sunucu.Kimlik);
        Assert.Contains("Ekstre.pdf", sunucu.Ileti);
        Assert.Contains("musteri@ornek.com", sunucu.Ileti);
    }

    [Fact]
    public async Task Yanlis_sifre_ve_hatali_adres_anlasilir_hata_verir()
    {
        await using var sunucu = new SahteSmtp("dogru-sifre");
        var servis = new EpostaServisi(() => Task.FromResult(Ayar(sunucu.Port, "yanlis")));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GonderAsync(new EpostaMesaji { Alicilar = "a@b.com", Konu = "x" }));
        Assert.Contains("uygulama şifresi", ex.Message);

        var adres = Assert.Throws<InvalidOperationException>(() => EpostaServisi.AlicilariCoz("musteri@ornek"));
        Assert.Contains("Geçersiz", adres.Message);
        var ayarsiz = new EpostaServisi(() => Task.FromResult(new SistemAyarlari { FirmaUnvani = "X" }));
        Assert.False(await ayarsiz.YapilandirildiMiAsync());
    }

    [Fact]
    public async Task Html_belge_edge_ile_pdfe_cevrilir()
    {
        if (PdfDonusturucu.TarayiciYolu() == null) return; // Edge yoksa (sunucu ortamı) atlanır
        var html = new Altyapi.Servisler.BelgeYazdirmaServisi().MutabakatMektubuHtmlUret(
            new EtkiAlani.Modeller.Cari.CariKart { Kod = "C-1", Unvan = "Örnek Müşteri A.Ş." }, new DateTime(2026, 9, 30), 1250.5m, null,
            new EtkiAlani.Modeller.Ortak.FirmaBilgisi { FirmaUnvani = "Deneme Ticaret" });
        var pdf = await PdfDonusturucu.HtmldenPdfAsync(html);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.True(pdf.Length > 2000);
        var klasor = Environment.GetEnvironmentVariable("RAPOR_CIKTI_KLASORU");
        if (!string.IsNullOrEmpty(klasor)) await File.WriteAllBytesAsync(Path.Combine(klasor, "mutabakat.pdf"), pdf);
    }
}
