using System.Diagnostics;
using System.Text;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Ayar;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;
using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public class BelgeYazdirmaServisi : IBelgeYazdirmaServisi
{
    private FirmaBilgisi _firmaBilgisi = new();
    private readonly ISistemAyarlariServisi? _ayarlarServisi;
    private readonly IDbContextFactory<MuhasebeBaglami>? _baglamFabrikasi;
    private readonly IAktifFirmaBaglami? _aktifFirma;

    public BelgeYazdirmaServisi(ISistemAyarlariServisi? ayarlarServisi = null, IDbContextFactory<MuhasebeBaglami>? baglamFabrikasi = null, IAktifFirmaBaglami? aktifFirma = null)
    {
        _ayarlarServisi = ayarlarServisi;
        _baglamFabrikasi = baglamFabrikasi;
        _aktifFirma = aktifFirma;
        if (_baglamFabrikasi == null)
            _ = FirmaBilgisiniYukleAsync();
    }

    private async Task FirmaBilgisiniYukleAsync()
    {
        if (_ayarlarServisi == null)
            return;

        try
        {
            _firmaBilgisi = FirmaBilgisiOlustur(await _ayarlarServisi.AyarlariGetirAsync(), null, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Firma bilgileri yüklenemedi: {ex.Message}");
        }
    }

    // Her yazdırmada güncel künye okunur: Ayarlar'da yapılan değişiklik programı kapatıp açmadan belgeye yansır,
    // belge hangi şirketinse o şirketin künyesi basılır.
    public async Task<FirmaBilgisi> FirmaBilgisiGetirAsync(Guid? sirketId = null)
    {
        if (_baglamFabrikasi == null)
            return _firmaBilgisi;
        try
        {
            var id = sirketId is Guid g && g != Guid.Empty ? g
                : _aktifFirma?.SirketId is Guid a && a != Guid.Empty ? a : MuhasebeBaglami.VarsayilanSirketId;
            await using var baglam = await _baglamFabrikasi.CreateDbContextAsync();
            var ayar = await baglam.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
            var sirket = await baglam.Sirketler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return _firmaBilgisi = FirmaBilgisiOlustur(ayar, sirket, id == MuhasebeBaglami.VarsayilanSirketId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Firma bilgileri yüklenemedi: {ex.Message}");
            return _firmaBilgisi;
        }
    }

    // Varsayılan şirkette künye Ayarlar sayfasından gelir, boş alan şirket kartından tamamlanır. Diğer şirketlerde künye
    // yalnız o şirketin kartıdır (başka şirketin vergi numarası basılmasın). Belge notları ve web sitesi Ayarlar'dandır.
    public static FirmaBilgisi FirmaBilgisiOlustur(SistemAyarlari? ayar, Sirket? sirket, bool varsayilanSirket)
    {
        string Sec(string? ayarDegeri, string? sirketDegeri) =>
            (varsayilanSirket && !string.IsNullOrWhiteSpace(ayarDegeri) ? ayarDegeri : sirketDegeri)?.Trim() ?? string.Empty;
        return new FirmaBilgisi
        {
            FirmaUnvani = Sec(ayar?.FirmaUnvani, sirket?.Unvan),
            VergiDairesi = Sec(ayar?.VergiDairesi, sirket?.VergiDairesi),
            VergiNo = Sec(ayar?.VergiNo, sirket?.VergiNo),
            Telefon = Sec(ayar?.Telefon, sirket?.Telefon),
            Eposta = Sec(ayar?.Eposta, sirket?.Email),
            Adres = Sec(ayar?.Adres, sirket?.Adres),
            MersisNo = varsayilanSirket ? ayar?.MersisNo?.Trim() ?? string.Empty : string.Empty,
            WebSitesi = ayar?.WebSitesi?.Trim() ?? string.Empty,
            BelgeNotu = ayar?.FisFooter?.Trim() ?? string.Empty,
            FaturaNotu = ayar?.FaturaNotu?.Trim() ?? string.Empty,
            IrsaliyeNotu = ayar?.IrsaliyeNotu?.Trim() ?? string.Empty
        };
    }

    private static string H(string? metin) => System.Net.WebUtility.HtmlEncode(metin ?? string.Empty);

    // Belge başlığındaki firma künyesi; boş alanlar yazılmaz.
    public static string FirmaKunyesiHtml(FirmaBilgisi f, bool vergiBilgisi = true)
    {
        var satirlar = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Adres)) satirlar.Add(H(f.Adres));
        var iletisim = new[] { string.IsNullOrWhiteSpace(f.Telefon) ? null : "Tel: " + f.Telefon, f.Eposta }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(H).ToList();
        if (iletisim.Count > 0) satirlar.Add(string.Join(" | ", iletisim));
        if (vergiBilgisi && (!string.IsNullOrWhiteSpace(f.VergiDairesi) || !string.IsNullOrWhiteSpace(f.VergiNo)))
            satirlar.Add("Vergi Dairesi: " + H(f.VergiDairesi) + " - No: " + H(f.VergiNo));
        if (vergiBilgisi && !string.IsNullOrWhiteSpace(f.MersisNo)) satirlar.Add("Mersis No: " + H(f.MersisNo));
        return "<div class='company-title'>" + H(f.FirmaUnvani) + "</div><div class='company-info'>" + string.Join("<br>", satirlar) + "</div>";
    }

    public FirmaBilgisi FirmaBilgisiGetir() => _firmaBilgisi;

    public void FirmaBilgisiGuncelle(FirmaBilgisi bilgi)
    {
        _firmaBilgisi = bilgi ?? new FirmaBilgisi();
    }

    private string GetCommonCss()
    {
        return @"
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
        * { box-sizing: border-box; font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { margin: 0; padding: 24px; color: #1E293B; background: #FFF; font-size: 13px; line-height: 1.5; }
        @media print {
            body { padding: 0; }
            @page { size: A4 portrait; margin: 12mm 15mm; }
            .no-print { display: none !important; }
            thead { display: table-header-group; }
            tfoot { display: table-footer-group; }
            tr { page-break-inside: avoid; }
        }
        .container { max-width: 800px; margin: 0 auto; }
        .header-table { width: 100%; border-collapse: collapse; margin-bottom: 24px; }
        .header-table td { vertical-align: top; }
        .company-title { font-size: 20px; font-weight: 700; color: #0F172A; margin: 0 0 4px 0; }
        .company-info { font-size: 12px; color: #64748B; margin: 0; line-height: 1.4; }
        .doc-title-box { text-align: right; }
        .doc-title { font-size: 22px; font-weight: 800; color: #2563EB; margin: 0 0 6px 0; text-transform: uppercase; letter-spacing: 0.5px; }
        .doc-badge { display: inline-block; padding: 4px 10px; border-radius: 6px; font-size: 11px; font-weight: 700; text-transform: uppercase; }
        .badge-success { background: #DCFCE7; color: #166534; }
        .badge-warning { background: #FEF3C7; color: #92400E; }
        .badge-danger { background: #FEE2E2; color: #991B1B; }
        
        .info-grid { width: 100%; border-collapse: collapse; margin-bottom: 20px; background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 8px; }
        .info-grid td { padding: 12px 16px; vertical-align: top; width: 50%; font-size: 12px; }
        .info-label { font-weight: 700; color: #475569; margin-bottom: 4px; text-transform: uppercase; font-size: 10px; letter-spacing: 0.5px; }
        .info-value { color: #0F172A; font-size: 13px; }

        .data-table { width: 100%; border-collapse: collapse; margin-bottom: 24px; }
        .data-table th { background: #F1F5F9; color: #475569; font-weight: 600; font-size: 11px; text-transform: uppercase; padding: 10px 12px; border-bottom: 2px solid #CBD5E1; text-align: left; }
        .data-table td { padding: 10px 12px; border-bottom: 1px solid #E2E8F0; font-size: 12px; }
        .data-table tr:nth-child(even) td { background: #F8FAFC; }
        .text-right { text-align: right; }
        .text-center { text-align: center; }

        .totals-table { width: 320px; margin-left: auto; border-collapse: collapse; margin-bottom: 24px; }
        .totals-table td { padding: 6px 12px; font-size: 12px; }
        .totals-table tr.grand-total td { font-size: 15px; font-weight: 700; color: #0F172A; border-top: 2px solid #0F172A; border-bottom: 2px solid #0F172A; padding: 10px 12px; background: #EEF2FF; }

        .footer-note { border-top: 1px solid #E2E8F0; padding-top: 16px; font-size: 11px; color: #64748B; text-align: center; margin-top: 30px; }
        .signature-table { width: 100%; border-collapse: collapse; margin-top: 40px; page-break-inside: avoid; }
        .signature-table td { width: 50%; text-align: center; vertical-align: top; padding: 0 40px; }
        .signature-line { border-top: 1px dashed #94A3B8; margin-top: 50px; padding-top: 6px; font-size: 12px; font-weight: 600; color: #475569; }

        .toolbar { position: fixed; top: 16px; right: 16px; background: #0F172A; padding: 8px 16px; border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); display: flex; gap: 8px; z-index: 1000; }
        .toolbar button { background: #2563EB; color: #FFF; border: none; padding: 8px 16px; border-radius: 6px; font-weight: 600; font-size: 12px; cursor: pointer; display: flex; align-items: center; gap: 6px; }
        .toolbar button:hover { background: #1D4ED8; }
        ";
    }

    public string FaturaHtmlUret(Fatura fatura, FirmaBilgisi? firma = null)
    {
        var f = firma ?? _firmaBilgisi;
        var turAdi = fatura.Tur == FaturaTuru.Satis ? "SATIŞ FATURASI" : (fatura.Tur == FaturaTuru.Alis ? "ALIŞ FATURASI" : (fatura.Tur == FaturaTuru.SatisIade ? "SATIŞ İADE FATURASI" : "ALIŞ İADE FATURASI"));
        var durumBadge = fatura.Durum == BelgeDurumu.Onaylandi ? "<span class='doc-badge badge-success'>ONAYLANDI</span>" : (fatura.Durum == BelgeDurumu.Taslak ? "<span class='doc-badge badge-warning'>TASLAK</span>" : "<span class='doc-badge badge-danger'>İPTAL</span>");

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'><title>Fatura - " + H(fatura.FaturaNo) + "</title><style>" + GetCommonCss() + "</style></head><body>");
        
        // Toolbar
        sb.AppendLine("<div class='toolbar no-print'><button onclick='window.print()'>🖨️ Yazdır / PDF Kaydet</button></div>");

        sb.AppendLine("<div class='container'>");

        // Header Table
        sb.AppendLine("<table class='header-table'><tr>");
        sb.AppendLine("<td>" + FirmaKunyesiHtml(f) + "</td>");
        sb.AppendLine("<td class='doc-title-box'><div class='doc-title'>" + turAdi + "</div><div>" + durumBadge + "</div><div style='margin-top:6px; font-weight:700; color:#1E293B;'>No: " + H(fatura.FaturaNo) + "</div><div style='color:#64748B; font-size:12px;'>Tarih: " + fatura.Tarih.ToString("dd.MM.yyyy") + "</div></td>");
        sb.AppendLine("</tr></table>");

        // Customer Info Grid
        sb.AppendLine("<table class='info-grid'><tr>");
        sb.AppendLine("<td><div class='info-label'>MÜŞTERİ / CARİ BİLGİLERİ</div><div class='info-value'><strong>" + H(fatura.CariKart?.Unvan ?? "Perakende Müşteri") + "</strong><br>" + H(fatura.CariKart?.Adres ?? "-") + "<br>V.D.: " + H(fatura.CariKart?.VergiDairesi ?? "-") + " / No: " + H(fatura.CariKart?.VergiNo ?? "-") + "<br>Tel: " + H(fatura.CariKart?.Telefon ?? "-") + "</div></td>");
        sb.AppendLine("<td><div class='info-label'>BELGE & SEVK BİLGİLERİ</div><div class='info-value'>Düzenleme Tarihi: <strong>" + fatura.Tarih.ToString("dd.MM.yyyy HH:mm") + "</strong><br>Belge Türü: <strong>" + turAdi + "</strong><br>Açıklama: " + H(string.IsNullOrWhiteSpace(fatura.Aciklama) ? "-" : fatura.Aciklama) + "</div></td>");
        sb.AppendLine("</tr></table>");

        // Line Items Table
        sb.AppendLine("<table class='data-table'><thead><tr>");
        sb.AppendLine("<th style='width:35px;' class='text-center'>#</th><th>ÜRÜN / HİZMET ADI</th><th style='width:70px;' class='text-center'>MİKTAR</th><th style='width:55px;' class='text-center'>BİRİM</th><th style='width:90px;' class='text-right'>B.FİYAT</th><th style='width:55px;' class='text-center'>KDV</th><th style='width:105px;' class='text-right'>TUTAR</th>");
        sb.AppendLine("</tr></thead><tbody>");

        int sira = 1;
        foreach (var k in fatura.Kalemler)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine("<td class='text-center'>" + sira++ + "</td>");
            sb.AppendLine("<td><strong>" + H(k.UrunAd) + "</strong><br><small style='color:#64748B;'>" + H(k.UrunKodu) + "</small></td>");
            sb.AppendLine("<td class='text-center'>" + k.Miktar.ToString("N2") + "</td>");
            sb.AppendLine("<td class='text-center'>" + H(k.Birim) + "</td>");
            sb.AppendLine("<td class='text-right'>" + k.BirimFiyat.ToString("N2") + " ₺</td>");
            sb.AppendLine("<td class='text-center'>%" + k.KdvOrani.ToString("0") + "</td>");
            sb.AppendLine("<td class='text-right'><strong>" + k.SatirToplami.ToString("N2") + " ₺</strong></td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Totals Table
        sb.AppendLine("<table class='totals-table'>");
        sb.AppendLine("<tr><td>Ara Toplam:</td><td class='text-right'>" + fatura.AraToplam.ToString("N2") + " ₺</td></tr>");
        if (fatura.SatirIskontosuToplam > 0 || fatura.GenelIskontoTutari > 0)
        {
            sb.AppendLine("<tr><td>İskonto Toplamı:</td><td class='text-right'>-" + (fatura.SatirIskontosuToplam + fatura.GenelIskontoTutari).ToString("N2") + " ₺</td></tr>");
        }
        sb.AppendLine("<tr><td>Hesaplanan KDV:</td><td class='text-right'>" + fatura.KdvToplam.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr class='grand-total'><td>GENEL TOPLAM:</td><td class='text-right'>" + fatura.GenelToplam.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("</table>");

        // Signatures
        sb.AppendLine("<table class='signature-table'><tr>");
        sb.AppendLine("<td><div class='signature-line'>TESLİM EDEN<br><small>" + H(f.FirmaUnvani) + "</small></div></td>");
        sb.AppendLine("<td><div class='signature-line'>TESLİM ALAN<br><small>" + H(fatura.CariKart?.Unvan ?? "Müşteri") + "</small></div></td>");
        sb.AppendLine("</tr></table>");

        // Footer Note
        var faturaNotu = string.IsNullOrWhiteSpace(f.FaturaNotu) ? f.BelgeNotu : f.FaturaNotu;
        sb.AppendLine("<div class='footer-note'>" + H(faturaNotu) + "<br><small>" + (string.IsNullOrWhiteSpace(f.WebSitesi) ? "" : H(f.WebSitesi) + " | ") + "Bu belge bilgisayar çıktısıdır.</small></div>");

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    public string CariEkstreHtmlUret(CariKart cari, List<CariHareket> hareketler, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null)
    {
        var f = firma ?? _firmaBilgisi;
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'><title>Cari Hesap Ekstresi - " + H(cari.Unvan) + "</title><style>" + GetCommonCss() + "</style></head><body>");
        
        sb.AppendLine("<div class='toolbar no-print'><button onclick='window.print()'>🖨️ Yazdır / PDF Kaydet</button></div>");
        sb.AppendLine("<div class='container'>");

        // Header Table
        sb.AppendLine("<table class='header-table'><tr>");
        sb.AppendLine("<td>" + FirmaKunyesiHtml(f, false) + "</td>");
        sb.AppendLine("<td class='doc-title-box'><div class='doc-title'>CARİ HESAP EKSTRESİ</div><div style='color:#64748B; font-size:12px;'>Rapor Tarihi: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</div></td>");
        sb.AppendLine("</tr></table>");

        // Info Grid
        sb.AppendLine("<table class='info-grid'><tr>");
        sb.AppendLine("<td><div class='info-label'>CARİ HESAP KÜNYESİ</div><div class='info-value'><strong>" + H(cari.Unvan) + "</strong> (Kod: " + H(cari.Kod) + ")<br>V.D.: " + H(cari.VergiDairesi) + " / No: " + H(cari.VergiNo) + "<br>Tel: " + H(cari.Telefon) + "</div></td>");
        sb.AppendLine("<td><div class='info-label'>FİNANSAL DURUM ÖZETİ</div><div class='info-value'>Toplam Borç: <strong>" + cari.Borc.ToString("N2") + " ₺</strong><br>Toplam Alacak: <strong>" + cari.Alacak.ToString("N2") + " ₺</strong><br>Güncel Bakiye: <strong style='font-size:14px; color:" + (cari.Bakiye > 0 ? "#2563EB" : (cari.Bakiye < 0 ? "#DC2626" : "#16A34A")) + ";'>" + Math.Abs(cari.Bakiye).ToString("N2") + " ₺ " + (cari.Bakiye > 0 ? "(Borçlu)" : (cari.Bakiye < 0 ? "(Alacaklı)" : "(Bakiye Sıfır)")) + "</strong></div></td>");
        sb.AppendLine("</tr></table>");

        // Movements Table
        sb.AppendLine("<table class='data-table'><thead><tr>");
        sb.AppendLine("<th style='width:80px;'>TARİH</th><th style='width:90px;'>İŞLEM TÜRÜ</th><th>AÇIKLAMA</th><th style='width:80px;'>BELGE NO</th><th style='width:95px;' class='text-right'>BORÇ (₺)</th><th style='width:95px;' class='text-right'>ALACAK (₺)</th><th style='width:105px;' class='text-right'>BAKİYE (₺)</th>");
        sb.AppendLine("</tr></thead><tbody>");

        decimal kumuBakiye = 0;
        foreach (var h in hareketler.OrderBy(x => x.Tarih).ThenBy(x => x.Id))
        {
            kumuBakiye += (h.Borc - h.Alacak);
            sb.AppendLine("<tr>");
            sb.AppendLine("<td>" + h.Tarih.ToString("dd.MM.yyyy") + "</td>");
            sb.AppendLine("<td><span style='font-weight:600; font-size:11px;'>" + h.HareketTuru + "</span></td>");
            sb.AppendLine("<td>" + H(h.Aciklama) + "</td>");
            sb.AppendLine("<td>" + H(string.IsNullOrWhiteSpace(h.BelgeNo) ? "-" : h.BelgeNo) + "</td>");
            sb.AppendLine("<td class='text-right'>" + (h.Borc > 0 ? h.Borc.ToString("N2") : "-") + "</td>");
            sb.AppendLine("<td class='text-right'>" + (h.Alacak > 0 ? h.Alacak.ToString("N2") : "-") + "</td>");
            sb.AppendLine("<td class='text-right'><strong>" + kumuBakiye.ToString("N2") + " ₺</strong></td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Totals
        sb.AppendLine("<table class='totals-table'>");
        sb.AppendLine("<tr><td>Toplam Borç:</td><td class='text-right'>" + hareketler.Sum(x => x.Borc).ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr><td>Toplam Alacak:</td><td class='text-right'>" + hareketler.Sum(x => x.Alacak).ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr class='grand-total'><td>SON BAKİYE:</td><td class='text-right'>" + kumuBakiye.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class='footer-note'>Bu ekstre ticari kayıtlarımıza göre düzenlenmiştir. Mutabakatsızlık halinde lütfen 7 gün içinde bildiriniz.<br><small>" + H(f.FirmaUnvani) + (string.IsNullOrWhiteSpace(f.Telefon) ? "" : " | " + H(f.Telefon)) + "</small></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    public string TahsilatMakbuzuHtmlUret(CariKart cari, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, string? kasaBankaAdi = null, FirmaBilgisi? firma = null)
        => MakbuzHtmlUret(cari, tutar, true, aciklama, belgeNo, tarih, kasaBankaAdi, firma);

    public string OdemeMakbuzuHtmlUret(CariKart cari, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, string? kasaBankaAdi = null, FirmaBilgisi? firma = null)
        => MakbuzHtmlUret(cari, tutar, false, aciklama, belgeNo, tarih, kasaBankaAdi, firma);

    private string MakbuzHtmlUret(CariKart cari, decimal tutar, bool isTahsilat, string aciklama, string? belgeNo, DateTime? tarih, string? kasaBankaAdi, FirmaBilgisi? firma)
    {
        var f = firma ?? _firmaBilgisi;
        var makbuzBaslik = isTahsilat ? "TAHSİLAT MAKBUZU" : "TEDİYE (ÖDEME) MAKBUZU";
        var islemTarihi = tarih ?? DateTime.Now;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'><title>" + makbuzBaslik + "</title><style>" + GetCommonCss() + "</style></head><body>");
        sb.AppendLine("<div class='toolbar no-print'><button onclick='window.print()'>🖨️ Yazdır / PDF Kaydet</button></div>");
        sb.AppendLine("<div class='container' style='border: 2px solid #E2E8F0; padding: 24px; border-radius: 12px;'>");

        sb.AppendLine("<table class='header-table'><tr>");
        sb.AppendLine("<td>" + FirmaKunyesiHtml(f, false) + "</td>");
        sb.AppendLine("<td class='doc-title-box'><div class='doc-title' style='font-size:18px; color:" + (isTahsilat ? "#16A34A" : "#DC2626") + ";'>" + makbuzBaslik + "</div><div style='font-weight:700;'>No: " + H(string.IsNullOrWhiteSpace(belgeNo) ? "MKB-" + islemTarihi.ToString("yyyyMMddHHmm") : belgeNo) + "</div><div style='color:#64748B;'>Tarih: " + islemTarihi.ToString("dd.MM.yyyy") + "</div></td>");
        sb.AppendLine("</tr></table>");

        sb.AppendLine("<table class='info-grid'><tr>");
        sb.AppendLine("<td><div class='info-label'>" + (isTahsilat ? "ÖDEMEYİ YAPAN (CARİ)" : "ÖDEME YAPILAN (CARİ)") + "</div><div class='info-value'><strong>" + H(cari.Unvan) + "</strong><br>V.D.: " + H(cari.VergiDairesi) + " - " + H(cari.VergiNo) + "<br>Tel: " + H(cari.Telefon) + "</div></td>");
        sb.AppendLine("<td><div class='info-label'>ÖDEME VE HESAP KANALI</div><div class='info-value'>Kanal: <strong>" + H(kasaBankaAdi ?? "Nakit Kasa") + "</strong><br>İşlem Türü: <strong>" + (isTahsilat ? "Tahsilat" : "Ödeme") + "</strong><br>Açıklama: " + H(aciklama) + "</div></td>");
        sb.AppendLine("</tr></table>");

        // Amount Box
        sb.AppendLine("<div style='background:#EEF2FF; border: 1px solid #C7D2FE; border-radius:8px; padding:20px; text-align:center; margin-bottom:30px;'>");
        sb.AppendLine("<div style='font-size:12px; font-weight:700; color:#4338CA; text-transform:uppercase;'>İŞLEM TUTARI</div>");
        sb.AppendLine("<div style='font-size:28px; font-weight:800; color:#1E1B4B; margin:6px 0;'>" + tutar.ToString("N2") + " ₺</div>");
        sb.AppendLine("</div>");

        // Signatures
        sb.AppendLine("<table class='signature-table'><tr>");
        sb.AppendLine("<td><div class='signature-line'>TESLİM EDEN<br><small>" + H(isTahsilat ? cari.Unvan : f.FirmaUnvani) + "</small></div></td>");
        sb.AppendLine("<td><div class='signature-line'>TESLİM ALAN<br><small>" + H(isTahsilat ? f.FirmaUnvani : cari.Unvan) + "</small></div></td>");
        sb.AppendLine("</tr></table>");

        sb.AppendLine("<div class='footer-note' style='margin-top:20px;'>" + H(f.BelgeNotu) + "</div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    public string KasaHareketDokumuHtmlUret(List<KasaHareketi> hareketler, decimal toplamBakiye, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null)
    {
        var f = firma ?? _firmaBilgisi;
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'><title>Kasa Hareket Dökümü</title><style>" + GetCommonCss() + "</style></head><body>");
        sb.AppendLine("<div class='toolbar no-print'><button onclick='window.print()'>🖨️ Yazdır / PDF Kaydet</button></div>");
        sb.AppendLine("<div class='container'>");

        sb.AppendLine("<table class='header-table'><tr>");
        sb.AppendLine("<td>" + FirmaKunyesiHtml(f, false) + "</td>");
        sb.AppendLine("<td class='doc-title-box'><div class='doc-title'>KASA HAREKET DÖKÜMÜ</div><div style='color:#64748B;'>Rapor Tarihi: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</div></td>");
        sb.AppendLine("</tr></table>");

        sb.AppendLine("<table class='data-table'><thead><tr>");
        sb.AppendLine("<th>TARİH</th><th>TÜR</th><th>AÇIKLAMA</th><th>BELGE</th><th class='text-right'>GELİR (₺)</th><th class='text-right'>GİDER (₺)</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var h in hareketler.OrderByDescending(x => x.Tarih))
        {
            sb.AppendLine("<tr>");
            sb.AppendLine("<td>" + h.Tarih.ToString("dd.MM.yyyy") + "</td>");
            sb.AppendLine("<td><span class='doc-badge " + (h.Tur == HareketTuru.Gelir ? "badge-success" : "badge-danger") + "'>" + h.Tur + "</span></td>");
            sb.AppendLine("<td>" + H(h.Aciklama) + "</td>");
            sb.AppendLine("<td>" + H(h.Belge ?? "-") + "</td>");
            sb.AppendLine("<td class='text-right'>" + (h.Tur == HareketTuru.Gelir ? h.Tutar.ToString("N2") + " ₺" : "-") + "</td>");
            sb.AppendLine("<td class='text-right'>" + (h.Tur == HareketTuru.Gider ? h.Tutar.ToString("N2") + " ₺" : "-") + "</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<table class='totals-table'>");
        sb.AppendLine("<tr><td>Toplam Gelir:</td><td class='text-right'>" + hareketler.Where(x => x.Tur == HareketTuru.Gelir).Sum(x => x.Tutar).ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr><td>Toplam Gider:</td><td class='text-right'>" + hareketler.Where(x => x.Tur == HareketTuru.Gider).Sum(x => x.Tutar).ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr class='grand-total'><td>KASA MEVCUDU:</td><td class='text-right'>" + toplamBakiye.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    public string BankaHareketDokumuHtmlUret(List<BankaHareketi> hareketler, decimal toplamBakiye, string? bankaAdi = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null)
    {
        var f = firma ?? _firmaBilgisi;
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'><title>Banka Hareket Dökümü</title><style>" + GetCommonCss() + "</style></head><body>");
        sb.AppendLine("<div class='toolbar no-print'><button onclick='window.print()'>🖨️ Yazdır / PDF Kaydet</button></div>");
        sb.AppendLine("<div class='container'>");

        sb.AppendLine("<table class='header-table'><tr>");
        sb.AppendLine("<td><div class='company-title'>" + H(f.FirmaUnvani) + "</div><div class='company-info'>" + H(bankaAdi ?? "Tüm Bankalar") + " Hesap Dökümü</div></td>");
        sb.AppendLine("<td class='doc-title-box'><div class='doc-title'>BANKA HAREKET DÖKÜMÜ</div><div style='color:#64748B;'>Rapor Tarihi: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</div></td>");
        sb.AppendLine("</tr></table>");

        sb.AppendLine("<table class='data-table'><thead><tr>");
        sb.AppendLine("<th>TARİH</th><th>BANKA / HESAP</th><th>TÜR</th><th>AÇIKLAMA</th><th class='text-right'>GELEN (₺)</th><th class='text-right'>GİDEN (₺)</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var h in hareketler.OrderByDescending(x => x.Tarih))
        {
            sb.AppendLine("<tr>");
            sb.AppendLine("<td>" + h.Tarih.ToString("dd.MM.yyyy") + "</td>");
            sb.AppendLine("<td><strong>" + H(h.BankaAdi ?? "Banka") + "</strong><br><small>" + H(h.HesapNo ?? "-") + "</small></td>");
            sb.AppendLine("<td><span class='doc-badge " + (h.Tur == HareketTuru.Gelir ? "badge-success" : "badge-danger") + "'>" + (h.Tur == HareketTuru.Gelir ? "Gelen EFT" : "Giden EFT") + "</span></td>");
            sb.AppendLine("<td>" + H(h.Aciklama) + "</td>");
            sb.AppendLine("<td class='text-right'>" + (h.Tur == HareketTuru.Gelir ? h.Tutar.ToString("N2") + " ₺" : "-") + "</td>");
            sb.AppendLine("<td class='text-right'>" + (h.Tur == HareketTuru.Gider ? h.Tutar.ToString("N2") + " ₺" : "-") + "</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<table class='totals-table'>");
        sb.AppendLine("<tr><td>Toplam Gelen:</td><td class='text-right'>" + hareketler.Where(x => x.Tur == HareketTuru.Gelir).Sum(x => x.Tutar).ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr><td>Toplam Giden:</td><td class='text-right'>" + hareketler.Where(x => x.Tur == HareketTuru.Gider).Sum(x => x.Tutar).ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr class='grand-total'><td>BANKA MEVCUDU:</td><td class='text-right'>" + toplamBakiye.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    public async Task<string> FaturaYazdirAsync(Fatura fatura, string? dosyaYolu = null, FirmaBilgisi? firma = null)
    {
        var html = FaturaHtmlUret(fatura, firma ?? await FirmaBilgisiGetirAsync(fatura.SirketId));
        var dosyaAdi = "Fatura_" + fatura.FaturaNo.Replace(" ", "_").Replace("/", "_") + ".html";
        return await DosyayaKaydetVeAcAsync(html, dosyaYolu, dosyaAdi);
    }

    public async Task<string> CariEkstreYazdirAsync(CariKart cari, List<CariHareket> hareketler, string? dosyaYolu = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null)
    {
        var html = CariEkstreHtmlUret(cari, hareketler, baslangic, bitis, firma ?? await FirmaBilgisiGetirAsync(cari.SirketId));
        var dosyaAdi = "CariEkstre_" + cari.Kod.Replace(" ", "_") + ".html";
        return await DosyayaKaydetVeAcAsync(html, dosyaYolu, dosyaAdi);
    }

    public async Task<string> MakbuzYazdirAsync(CariKart cari, decimal tutar, bool isTahsilat, string aciklama, string? dosyaYolu = null, string? belgeNo = null, DateTime? tarih = null, string? kasaBankaAdi = null, FirmaBilgisi? firma = null)
    {
        var html = MakbuzHtmlUret(cari, tutar, isTahsilat, aciklama, belgeNo, tarih, kasaBankaAdi, firma ?? await FirmaBilgisiGetirAsync(cari.SirketId));
        var dosyaAdi = (isTahsilat ? "TahsilatMakbuzu_" : "OdemeMakbuzu_") + (belgeNo ?? DateTime.Now.ToString("yyyyMMddHHmm")) + ".html";
        return await DosyayaKaydetVeAcAsync(html, dosyaYolu, dosyaAdi);
    }

    public async Task<string> KasaDokumuYazdirAsync(List<KasaHareketi> hareketler, decimal toplamBakiye, string? dosyaYolu = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null)
    {
        var html = KasaHareketDokumuHtmlUret(hareketler, toplamBakiye, baslangic, bitis, firma ?? await FirmaBilgisiGetirAsync());
        var dosyaAdi = "KasaDokumu_" + DateTime.Now.ToString("yyyyMMdd") + ".html";
        return await DosyayaKaydetVeAcAsync(html, dosyaYolu, dosyaAdi);
    }

    public async Task<string> BankaDokumuYazdirAsync(List<BankaHareketi> hareketler, decimal toplamBakiye, string? dosyaYolu = null, string? bankaAdi = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null)
    {
        var html = BankaHareketDokumuHtmlUret(hareketler, toplamBakiye, bankaAdi, baslangic, bitis, firma ?? await FirmaBilgisiGetirAsync());
        var dosyaAdi = "BankaDokumu_" + (bankaAdi ?? "Genel") + "_" + DateTime.Now.ToString("yyyyMMdd") + ".html";
        return await DosyayaKaydetVeAcAsync(html, dosyaYolu, dosyaAdi);
    }

    public string HesapEkstresiHtmlUret(HesapEkstresiBelgesi belge, FirmaBilgisi? firma = null)
    {
        var f = firma ?? _firmaBilgisi;
        var donem = belge.Baslangic is DateTime b && belge.Bitis is DateTime s
            ? (b.Date == s.Date ? b.ToString("dd.MM.yyyy") : $"{b:dd.MM.yyyy} - {s:dd.MM.yyyy}")
            : belge.Baslangic is DateTime b2 ? $"{b2:dd.MM.yyyy} ve sonrası" : belge.Bitis is DateTime s2 ? $"{s2:dd.MM.yyyy} ve öncesi" : "Tüm hareketler";
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'><title>" + H(belge.Baslik) + "</title><style>" + GetCommonCss() + "</style></head><body>");
        sb.AppendLine("<div class='toolbar no-print'><button onclick='window.print()'>🖨️ Yazdır / PDF Kaydet</button></div>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine("<table class='header-table'><tr>");
        sb.AppendLine("<td><div class='company-title'>" + H(f.FirmaUnvani) + "</div><div class='company-info'>" + H(belge.HesapAdi) + "<br>Dönem: " + donem + "</div></td>");
        sb.AppendLine("<td class='doc-title-box'><div class='doc-title'>" + H(belge.Baslik.ToUpper(new System.Globalization.CultureInfo("tr-TR"))) + "</div><div style='color:#64748B;'>Rapor Tarihi: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</div></td>");
        sb.AppendLine("</tr></table>");

        sb.AppendLine("<table class='data-table'><thead><tr>");
        sb.AppendLine("<th>TARİH</th><th>FİŞ NO</th><th>TÜR</th><th>CARİ</th><th>AÇIKLAMA</th><th class='text-right'>GİRİŞ (₺)</th><th class='text-right'>ÇIKIŞ (₺)</th><th class='text-right'>BAKİYE (₺)</th>");
        sb.AppendLine("</tr></thead><tbody>");
        sb.AppendLine("<tr><td colspan='7'><strong>Devreden Bakiye</strong></td><td class='text-right'><strong>" + belge.Devir.ToString("N2") + "</strong></td></tr>");
        foreach (var r in belge.Satirlar)
        {
            sb.AppendLine("<tr><td>" + r.Tarih.ToString("dd.MM.yyyy HH:mm") + "</td><td>" + H(r.FisNo) + "</td><td>" + H(r.Tur) + "</td><td>" + H(r.Cari) + "</td><td>" + H(r.Aciklama) + "</td>" +
                "<td class='text-right'>" + (r.Giris > 0 ? r.Giris.ToString("N2") : "-") + "</td>" +
                "<td class='text-right'>" + (r.Cikis > 0 ? r.Cikis.ToString("N2") : "-") + "</td>" +
                "<td class='text-right'>" + r.Bakiye.ToString("N2") + "</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<table class='totals-table'>");
        sb.AppendLine("<tr><td>Devreden Bakiye:</td><td class='text-right'>" + belge.Devir.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr><td>Toplam Giriş (" + belge.Satirlar.Count(x => x.Giris > 0) + " işlem):</td><td class='text-right'>" + belge.ToplamGiris.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr><td>Toplam Çıkış (" + belge.Satirlar.Count(x => x.Cikis > 0) + " işlem):</td><td class='text-right'>" + belge.ToplamCikis.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("<tr class='grand-total'><td>DÖNEM SONU BAKİYE:</td><td class='text-right'>" + belge.Kapanis.ToString("N2") + " ₺</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine("<p style='color:#64748B;font-size:11px;margin-top:16px;'>İptal edilen işlemler ve iptal ters kayıtları birbirini götürdüğü için ekstrede gösterilmez.</p>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    public async Task<string> HesapEkstresiYazdirAsync(HesapEkstresiBelgesi belge, FirmaBilgisi? firma = null)
    {
        var html = HesapEkstresiHtmlUret(belge, firma ?? await FirmaBilgisiGetirAsync());
        var guvenliAd = string.Concat(belge.HesapAdi.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        var dosyaAdi = $"{(belge.Baslik.StartsWith("Gün Sonu") ? "GunSonu" : "Ekstre")}_{guvenliAd}_{DateTime.Now:yyyyMMddHHmm}.html";
        return await DosyayaKaydetVeAcAsync(html, null, dosyaAdi);
    }

    public Task BelgeyiGoruntuleVeYazdirAsync(string htmlIcerik, string geciciBelgeAdi)
    {
        // Uzantısız dosyayı Windows tarayıcıda açamaz, "hangi programla açılsın" diye sorar.
        var dosyaAdi = geciciBelgeAdi.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? geciciBelgeAdi : geciciBelgeAdi + ".html";
        var tempPath = Path.Combine(Path.GetTempPath(), string.Concat(dosyaAdi.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)));
        File.WriteAllText(tempPath, htmlIcerik, Encoding.UTF8);
        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private async Task<string> DosyayaKaydetVeAcAsync(string htmlIcerik, string? hedefDosyaYolu, string varsayilanDosyaAdi)
    {
        string path = hedefDosyaYolu ?? Path.Combine(Path.GetTempPath(), varsayilanDosyaAdi);
        await File.WriteAllTextAsync(path, htmlIcerik, Encoding.UTF8);

        // Kullanıcıya otomatik aç
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }

        return path;
    }
}
