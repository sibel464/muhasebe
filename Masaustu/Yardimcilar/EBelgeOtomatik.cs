using Microsoft.Extensions.DependencyInjection;
using Uygulama.Arayuzler;

namespace Masaustu.Yardimcilar;

// "Onaylanınca e-belgeyi otomatik gönder" ayarı: satış ekranları onaydan sonra bunu çağırır. Gönderim hatası satışı
// geri almaz; kullanıcıya kısa bir not döner (Fatura listesindeki 📨 ile yeniden denenir). Ayar kapalıysa null.
public static class EBelgeOtomatik
{
    public static async Task<string?> GonderAsync(int faturaId)
    {
        if (App.HizmetSaglayici == null) return null;
        try
        {
            using var kapsam = App.HizmetSaglayici.CreateScope();
            if (kapsam.ServiceProvider.GetService<IEBelgeServisi>() is not { } servis) return null;
            var sonuc = await servis.OtomatikGonderAsync(faturaId);
            if (sonuc == null) return null;
            return sonuc.Basarili
                ? "e-Belge gönderildi."
                : "⚠ e-Belge gönderilemedi: " + sonuc.Mesaj + " (Fatura listesinde 📨 ile yeniden deneyin.)";
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "EBelgeOtomatik");
            return "⚠ e-Belge gönderilemedi: " + ex.Message + " (Fatura listesinde 📨 ile yeniden deneyin.)";
        }
    }
}
