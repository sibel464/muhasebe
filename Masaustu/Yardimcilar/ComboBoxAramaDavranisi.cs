using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Masaustu.Yardimcilar;

public static class ComboBoxAramaDavranisi
{
    public static readonly DependencyProperty EtkinProperty =
        DependencyProperty.RegisterAttached(
            "Etkin",
            typeof(bool),
            typeof(ComboBoxAramaDavranisi),
            new PropertyMetadata(false, EtkinDegisti));

    private static readonly DependencyProperty MetinKutusuProperty =
        DependencyProperty.RegisterAttached(
            "MetinKutusu",
            typeof(TextBox),
            typeof(ComboBoxAramaDavranisi));

    public static void SetEtkin(DependencyObject element, bool value) => element.SetValue(EtkinProperty, value);
    public static bool GetEtkin(DependencyObject element) => (bool)element.GetValue(EtkinProperty);

    private static void EtkinDegisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox comboBox)
            return;

        if ((bool)e.NewValue)
        {
            comboBox.Loaded += ComboBoxYuklendi;
            comboBox.Unloaded += ComboBoxKaldirildi;
        }
        else
        {
            comboBox.Loaded -= ComboBoxYuklendi;
            comboBox.Unloaded -= ComboBoxKaldirildi;
            AramayiKapat(comboBox);
        }
    }

    private static void ComboBoxYuklendi(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
            return;

        comboBox.ApplyTemplate();
        AramayiKapat(comboBox);

        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is not TextBox metinKutusu)
            return;

        SetMetinKutusu(comboBox, metinKutusu);
        metinKutusu.TextChanged += MetinDegisti;
        comboBox.Items.Filter = item => EslesenOgeMi(comboBox, item, metinKutusu.Text);
    }

    private static void ComboBoxKaldirildi(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox)
            AramayiKapat(comboBox);
    }

    private static void MetinDegisti(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox metinKutusu || metinKutusu.TemplatedParent is not ComboBox comboBox)
            return;

        comboBox.Items.Refresh();
    }

    private static bool EslesenOgeMi(ComboBox comboBox, object item, string aramaMetni)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return true;

        var metin = OgeMetni(comboBox, item);
        return metin.Contains(aramaMetni.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }

    private static string OgeMetni(ComboBox comboBox, object item)
    {
        if (item is ComboBoxItem comboBoxItem)
            return comboBoxItem.Content?.ToString() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(comboBox.DisplayMemberPath))
        {
            object? deger = item;
            foreach (var parca in comboBox.DisplayMemberPath.Split('.'))
            {
                deger = deger?.GetType().GetProperty(parca, BindingFlags.Instance | BindingFlags.Public)?.GetValue(deger);
                if (deger is null)
                    break;
            }

            return deger?.ToString() ?? string.Empty;
        }

        return item?.ToString() ?? string.Empty;
    }

    private static void AramayiKapat(ComboBox comboBox)
    {
        if (GetMetinKutusu(comboBox) is TextBox metinKutusu)
            metinKutusu.TextChanged -= MetinDegisti;

        SetMetinKutusu(comboBox, null);
        comboBox.Items.Filter = null;
    }

    private static void SetMetinKutusu(DependencyObject element, TextBox? value) => element.SetValue(MetinKutusuProperty, value);
    private static TextBox? GetMetinKutusu(DependencyObject element) => (TextBox?)element.GetValue(MetinKutusuProperty);
}
