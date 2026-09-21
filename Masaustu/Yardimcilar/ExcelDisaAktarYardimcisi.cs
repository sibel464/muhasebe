using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace Masaustu.Yardimcilar;

public static class ExcelDisaAktarYardimcisi
{
    public static void AktarCsv<T>(IEnumerable<T> veriListesi, string varsayilanDosyaAdi)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel CSV Dosyası (*.csv)|*.csv|Tüm Dosyalar (*.*)|*.*",
                FileName = $"{varsayilanDosyaAdi}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Excel / CSV Dışa Aktar"
            };

            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Başlık satırı
            var basliklar = new List<string>();
            foreach (var prop in props)
            {
                basliklar.Add(EscapedCsvCell(prop.Name));
            }
            sb.AppendLine(string.Join(";", basliklar));

            // Veri satırları
            foreach (var item in veriListesi)
            {
                var satir = new List<string>();
                foreach (var prop in props)
                {
                    var val = prop.GetValue(item, null);
                    if (val is DateTime dt)
                    {
                        satir.Add(EscapedCsvCell(dt.ToString("dd.MM.yyyy HH:mm")));
                    }
                    else if (val is decimal dec)
                    {
                        satir.Add(EscapedCsvCell(dec.ToString("N2")));
                    }
                    else
                    {
                        satir.Add(EscapedCsvCell(val?.ToString() ?? ""));
                    }
                }
                sb.AppendLine(string.Join(";", satir));
            }

            // UTF8 Encoding with BOM (Excel Türkçe Karakter Desteği)
            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
            MessageBox.Show($"Dosya başarıyla aktarıldı:\n{dialog.FileName}", "Dışa Aktarma Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dışa aktarma sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string EscapedCsvCell(string input)
    {
        if (input.Contains(";") || input.Contains("\"") || input.Contains("\n") || input.Contains("\r"))
        {
            return $"\"{input.Replace("\"", "\"\"")}\"";
        }
        return input;
    }
}
