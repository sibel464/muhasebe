using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using Altyapi.Servisler;
using Uygulama.Dto.Rapor;

namespace Masaustu.Yardimcilar;

// Liste ekranlarının "Excel'e aktar" düğmesi: yalnız kullanıcıya anlamlı basit alanlar Türkçe başlıklarla .xlsx'e yazılır.
// Şifre özeti gibi gizli alanlar, kimlik numaraları ve bağlı nesneler hiçbir zaman dosyaya çıkmaz.
public static class ExcelDisaAktarYardimcisi
{
    private static readonly string[] GizliParcalar = ["Sifre", "Hash", "Salt", "Token", "Parola", "Anahtar"];
    private static readonly HashSet<string> TeknikAlanlar = ["Id", "Versiyon", "Silindi", "SilinmeTarihi", "SilinmeNedeni", "SatirSurumu", "xmin", "FotografYolu"];

    private static readonly Dictionary<string, string> Kelimeler = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kullanici"] = "Kullanıcı", ["Adi"] = "Adı", ["Unvan"] = "Ünvan", ["Tur"] = "Tür", ["Turu"] = "Türü", ["Urun"] = "Ürün",
        ["Urunu"] = "Ürünü", ["Odeme"] = "Ödeme", ["Siparis"] = "Sipariş", ["Irsaliye"] = "İrsaliye", ["Kdv"] = "KDV",
        ["Fiyati"] = "Fiyatı", ["Degeri"] = "Değeri", ["Giris"] = "Giriş", ["Cikis"] = "Çıkış", ["Sayisi"] = "Sayısı",
        ["Hatali"] = "Hatalı", ["Bitis"] = "Bitiş", ["Baslangic"] = "Başlangıç", ["Degistirme"] = "Değiştirme",
        ["Degistirmeli"] = "Değiştirmeli", ["Eposta"] = "E-posta", ["Iban"] = "IBAN", ["Il"] = "İl", ["Ilce"] = "İlçe",
        ["Aciklama"] = "Açıklama", ["Islem"] = "İşlem", ["Detayi"] = "Detayı", ["Miktari"] = "Miktarı", ["Toplami"] = "Toplamı",
        ["Iskonto"] = "İskonto", ["Doviz"] = "Döviz", ["Borc"] = "Borç", ["Tasiyici"] = "Taşıyıcı", ["Arac"] = "Araç",
        ["Tutari"] = "Tutarı", ["Musteri"] = "Müşteri", ["Tedarikci"] = "Tedarikçi", ["Olusturma"] = "Oluşturma",
        ["Guncelleme"] = "Güncelleme", ["Magaza"] = "Mağaza", ["Uyari"] = "Uyarı", ["Sube"] = "Şube", ["Cek"] = "Çek",
        ["Kesideci"] = "Keşideci", ["Portfoy"] = "Portföy", ["Ulke"] = "Ülke", ["Is"] = "İş", ["Limiti"] = "Limiti",
        ["Gun"] = "Gün", ["Odenecek"] = "Ödenecek", ["Kodu"] = "Kodu", ["Tc"] = "T.C.", ["Vkn"] = "VKN", ["Iade"] = "İade",
        ["Gonderen"] = "Gönderen", ["Alici"] = "Alıcı", ["Sevkiyat"] = "Sevkiyat", ["Durumu"] = "Durumu", ["Hesabi"] = "Hesabı",
        ["Sekli"] = "Şekli", ["Satir"] = "Satır", ["Orani"] = "Oranı", ["Gunu"] = "Günü", ["Mesaji"] = "Mesajı", ["Mukellefi"] = "Mükellefi", ["Iskontosu"] = "İskontosu", ["Gecikme"] = "Gecikme", ["Yontemi"] = "Yöntemi", ["Kategori"] = "Kategori", ["Birimi"] = "Birimi"
    };

    public static void Aktar<T>(IEnumerable<T> veriListesi, string varsayilanDosyaAdi)
    {
        try
        {
            var baslik = varsayilanDosyaAdi.Replace('_', ' ');
            var dialog = new SaveFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                FileName = $"{varsayilanDosyaAdi}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Title = "Excel'e Aktar"
            };
            if (dialog.ShowDialog() != true) return;

            File.WriteAllBytes(dialog.FileName, new ExcelExportServisi().ExcelOlustur(TabloOlustur(veriListesi, baslik)));
            if (MessageBox.Show($"Dosya kaydedildi:\n{dialog.FileName}\n\nŞimdi açılsın mı?", "Excel'e Aktar", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dışa aktarma sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static RaporTablosu TabloOlustur<T>(IEnumerable<T> veriListesi, string baslik)
    {
        var liste = veriListesi.Cast<object?>().Where(x => x != null).ToList();
        var tur = typeof(T) == typeof(object) ? liste.FirstOrDefault()?.GetType() ?? typeof(T) : typeof(T);
        var alanlar = tur.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && Aktarilabilir(p))
            .ToList();

        var tablo = new RaporTablosu { Baslik = baslik };
        foreach (var p in alanlar)
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var sayisal = t == typeof(decimal) || t == typeof(double) || t == typeof(float) || t == typeof(int) || t == typeof(long);
            tablo.Kolonlar.Add(new RaporKolonu(Baslik(p.Name), sayisal, t == typeof(decimal) || t == typeof(double) || t == typeof(float)));
        }
        foreach (var item in liste)
            tablo.Satirlar.Add(alanlar.Select(p => RaporTablosuOlusturucu.Hucre(p.GetValue(item), p.Name)).ToArray());
        return tablo;
    }

    private static bool Aktarilabilir(PropertyInfo p)
    {
        if (TeknikAlanlar.Contains(p.Name) || p.Name.EndsWith("Id", StringComparison.Ordinal)) return false;
        if (GizliParcalar.Any(g => p.Name.Contains(g, StringComparison.OrdinalIgnoreCase))) return false;
        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateOnly);
    }

    // "SonGirisTarihi" → "Son Giriş Tarihi"
    public static string Baslik(string alanAdi)
    {
        var kelimeler = Regex.Matches(alanAdi, "[A-ZÇĞİÖŞÜ]?[a-zçğıöşü0-9]+|[A-ZÇĞİÖŞÜ]+(?![a-zçğıöşü])").Select(m => m.Value).ToList();
        if (kelimeler.Count == 0) return alanAdi;
        return string.Join(" ", kelimeler.Select(k => Kelimeler.TryGetValue(k, out var tr) ? tr : char.ToUpper(k[0], new System.Globalization.CultureInfo("tr-TR")) + k[1..]))
            .Replace("E Fatura", "e-Fatura").Replace("E Belge", "e-Belge").Replace("E Arsiv", "e-Arşiv");
    }
}
