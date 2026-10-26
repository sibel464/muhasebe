using System.Diagnostics;
using System.Text;

namespace Altyapi.Eposta;

// HTML belgeyi (fatura, ekstre, mutabakat) Windows'ta hazır bulunan Edge (ya da Chrome) ile başsız yazdırarak PDF'e çevirir.
// Kullanıcının açık tarayıcısına karışmamak için her dönüşüm ayrı, geçici bir tarayıcı profiliyle çalışır.
public static class PdfDonusturucu
{
    private static readonly string[] Adaylar =
    [
        @"Microsoft\Edge\Application\msedge.exe",
        @"Google\Chrome\Application\chrome.exe"
    ];

    public static string? TarayiciYolu()
    {
        foreach (var kok in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
            foreach (var aday in Adaylar)
            {
                var yol = Path.Combine(kok, aday);
                if (File.Exists(yol)) return yol;
            }
        return null;
    }

    public static async Task<byte[]> HtmldenPdfAsync(string html, CancellationToken iptal = default)
    {
        var tarayici = TarayiciYolu() ?? throw new InvalidOperationException("PDF oluşturmak için Microsoft Edge bulunamadı.");
        var klasor = Path.Combine(Path.GetTempPath(), "MuhasebePdf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var htmlYolu = Path.Combine(klasor, "belge.html");
            var pdfYolu = Path.Combine(klasor, "belge.pdf");
            // Ekrandaki "Yazdır" düğmesi gibi otomatik yazdırma betikleri başsız tarayıcıyı bekletmesin.
            await File.WriteAllTextAsync(htmlYolu, html.Replace("onload=\"window.print()\"", "").Replace("onload='window.print()'", ""), new UTF8Encoding(true), iptal);
            var bilgi = new ProcessStartInfo(tarayici)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
            };
            foreach (var arg in new[]
                     {
                         "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--disable-extensions",
                         "--run-all-compositor-stages-before-draw", "--no-pdf-header-footer", "--virtual-time-budget=5000",
                         $"--user-data-dir={Path.Combine(klasor, "profil")}", $"--print-to-pdf={pdfYolu}", new Uri(htmlYolu).AbsoluteUri
                     })
                bilgi.ArgumentList.Add(arg);
            using var surec = Process.Start(bilgi) ?? throw new InvalidOperationException("PDF dönüştürücü başlatılamadı.");
            using var zaman = CancellationTokenSource.CreateLinkedTokenSource(iptal);
            zaman.CancelAfter(TimeSpan.FromSeconds(60));
            try { await surec.WaitForExitAsync(zaman.Token); }
            catch (OperationCanceledException)
            {
                try { surec.Kill(true); } catch { }
                throw new TimeoutException("PDF oluşturma zaman aşımına uğradı.");
            }
            if (!File.Exists(pdfYolu))
                throw new InvalidOperationException("PDF oluşturulamadı (tarayıcı dosya üretmedi).");
            var veri = await File.ReadAllBytesAsync(pdfYolu, iptal);
            if (veri.Length < 5 || Encoding.ASCII.GetString(veri, 0, 5) != "%PDF-")
                throw new InvalidOperationException("Oluşan dosya geçerli bir PDF değil.");
            return veri;
        }
        finally
        {
            try { Directory.Delete(klasor, true); } catch { }
        }
    }
}
