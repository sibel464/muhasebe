using System;
using System.Threading;
using System.Windows;
using Masaustu.Gorunumler.Sayfa.Muhasebe;
using Masaustu.GorunumModeli.Muhasebe;
using Uygulama.Arayuzler;

namespace TestKonsolu;

public class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new Application();
            
            // Dummy service mock via Moq or just passing null if it doesn't crash on init
            var gm = new HesapEkleDuzenleGorunumModeli(null); 
            var window = new HesapEkleDuzenlePenceresi(gm);
            
            // Test cancel button
            bool isCancelFired = false;
            gm.IptalEt += () => { isCancelFired = true; };
            
            gm.KapatKomutu.Execute(null);
            
            Console.WriteLine($"KapatKomutu Executed. IptalEt fired: {isCancelFired}");
            Console.WriteLine($"Window DialogResult: {window.DialogResult}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("EXCEPTION CAUGHT:");
            Console.WriteLine(ex.Message);
        }
    }
}
