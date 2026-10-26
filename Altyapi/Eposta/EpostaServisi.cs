using System.Net.Sockets;
using EtkiAlani.Modeller.Ayar;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Uygulama.Arayuzler;

namespace Altyapi.Eposta;

// Ayarlar'daki SMTP bilgileriyle e-posta gönderir (465 SSL ve 587 STARTTLS desteklenir). Hatalar kullanıcıya
// anlaşılır Türkçe iletiye çevrilir; şifre hiçbir iletide ya da günlükte yer almaz.
public class EpostaServisi : IEpostaServisi
{
    private readonly Func<Task<SistemAyarlari>> _ayarlar;

    public EpostaServisi(ISistemAyarlariServisi ayarServisi) => _ayarlar = ayarServisi.AyarlariGetirAsync;
    public EpostaServisi(Func<Task<SistemAyarlari>> ayarlar) => _ayarlar = ayarlar;

    public async Task<bool> YapilandirildiMiAsync()
    {
        var a = await _ayarlar();
        return !string.IsNullOrWhiteSpace(a.SmtpSunucu) && GondericiAdresi(a) != null;
    }

    public Task<byte[]> HtmldenPdfAsync(string html, CancellationToken iptal = default) => PdfDonusturucu.HtmldenPdfAsync(html, iptal);

    private static MailboxAddress? GondericiAdresi(SistemAyarlari a)
    {
        var ad = string.IsNullOrWhiteSpace(a.EpostaGonderenAdi) ? a.FirmaUnvani : a.EpostaGonderenAdi!;
        foreach (var aday in new[] { a.SmtpKullanici, a.Eposta })
            if (!string.IsNullOrWhiteSpace(aday) && MailboxAddress.TryParse(aday.Trim(), out var adres) && adres.Address.Contains('@'))
                return new MailboxAddress(ad, adres.Address);
        return null;
    }

    public static List<MailboxAddress> AlicilariCoz(string metin)
    {
        var liste = new List<MailboxAddress>();
        foreach (var parca in (metin ?? "").Split([';', ',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!MailboxAddress.TryParse(parca, out var adres) || !System.Text.RegularExpressions.Regex.IsMatch(adres.Address, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new InvalidOperationException($"Geçersiz e-posta adresi: {parca}");
            liste.Add(adres);
        }
        if (liste.Count == 0) throw new InvalidOperationException("Alıcı e-posta adresini yazın.");
        return liste;
    }

    public async Task GonderAsync(EpostaMesaji mesaj, CancellationToken iptal = default)
    {
        var a = await _ayarlar();
        if (string.IsNullOrWhiteSpace(a.SmtpSunucu))
            throw new InvalidOperationException("E-posta ayarları yapılmamış. Ayarlar → E-posta bölümünden SMTP sunucusunu girin.");
        var gonderen = GondericiAdresi(a) ?? throw new InvalidOperationException("Gönderen e-posta adresi yok. Ayarlar'da SMTP kullanıcı adını (e-posta adresi) yazın.");

        var mime = new MimeMessage();
        mime.From.Add(gonderen);
        mime.To.AddRange(AlicilariCoz(mesaj.Alicilar));
        mime.Subject = string.IsNullOrWhiteSpace(mesaj.Konu) ? a.FirmaUnvani : mesaj.Konu.Trim();
        var govde = new BodyBuilder { TextBody = mesaj.Metin ?? "" };
        foreach (var ek in mesaj.Ekler)
            govde.Attachments.Add(ek.DosyaAdi, ek.Icerik, ContentType.Parse(ek.IcerikTuru));
        mime.Body = govde.ToMessageBody();

        var guvenlik = a.SmtpGuvenlik switch
        {
            1 => SecureSocketOptions.SslOnConnect,
            2 => SecureSocketOptions.StartTls,
            3 => SecureSocketOptions.None,
            _ => a.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable
        };
        using var istemci = new SmtpClient { Timeout = 30000 };
        try
        {
            await istemci.ConnectAsync(a.SmtpSunucu.Trim(), a.SmtpPort, guvenlik, iptal);
            var sifre = SmtpSifreleyici.Coz(a.SmtpSifre);
            if (!string.IsNullOrWhiteSpace(a.SmtpKullanici) && !string.IsNullOrEmpty(sifre))
                await istemci.AuthenticateAsync(a.SmtpKullanici.Trim(), sifre, iptal);
            await istemci.SendAsync(mime, iptal);
            await istemci.DisconnectAsync(true, iptal);
        }
        catch (AuthenticationException)
        {
            throw new InvalidOperationException("E-posta sunucusu kullanıcı adı / şifreyi kabul etmedi. Gmail ve Outlook'ta normal şifre yerine 'uygulama şifresi' oluşturup onu girin.");
        }
        catch (SslHandshakeException)
        {
            throw new InvalidOperationException("Güvenli bağlantı kurulamadı. Ayarlar'da güvenlik türünü ve portu kontrol edin (465 → SSL, 587 → STARTTLS).");
        }
        catch (SocketException)
        {
            throw new InvalidOperationException($"E-posta sunucusuna ({a.SmtpSunucu}:{a.SmtpPort}) bağlanılamadı. İnternet bağlantısını, sunucu adını ve portu kontrol edin.");
        }
        catch (SmtpCommandException ex)
        {
            throw new InvalidOperationException("E-posta sunucusu gönderimi reddetti: " + ex.Message);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("E-posta sunucusu yanıt vermedi (zaman aşımı).");
        }
        catch (ServiceNotConnectedException)
        {
            throw new InvalidOperationException("E-posta sunucusuyla bağlantı koptu.");
        }
    }
}
