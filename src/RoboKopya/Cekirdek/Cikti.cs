using System.Globalization;
using System.Text.RegularExpressions;

namespace RoboKopya.Cekirdek;

/// <summary>robocopy /BYTES /FP /NDL çıktısını okur. Dil bağımsız: sınıf metnine değil sekme düzenine bakar.</summary>
public static class Cikti
{
    static readonly Regex HataKodu = new(@"\(0x[0-9A-Fa-f]{8}\)", RegexOptions.Compiled);

    public static bool DosyaSatiri(string satir, out long boyut, out string yol)
    {
        boyut = 0; yol = "";
        var p = satir.Split('\t').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        if (p.Length < 2) return false;
        if (p.Length >= 3 && p[0].StartsWith('*')) return false; // *EXTRA File: hedefte fazlalık, kopyalanmaz
        if (!long.TryParse(p[^2], NumberStyles.None, CultureInfo.InvariantCulture, out boyut)) return false;
        yol = p[^1];
        return true;
    }

    public static bool HataSatiri(string satir) => HataKodu.IsMatch(satir);
}

public static class Bicim
{
    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Boyut(long b)
    {
        string[] b2 = { "bayt", "KB", "MB", "GB", "TB" };
        double d = b; int i = 0;
        while (d >= 1024 && i < b2.Length - 1) { d /= 1024; i++; }
        return i == 0 ? $"{b} bayt" : d.ToString(d >= 100 ? "0" : "0.#", Tr) + " " + b2[i];
    }

    public static string Hiz(double baytSn) => (baytSn / 1048576).ToString(baytSn >= 100 * 1048576 ? "0" : "0.#", Tr) + " MB/sn";

    public static string Adet(long n) => n.ToString("N0", Tr);

    public static string KalanSure(double sn)
    {
        if (double.IsNaN(sn) || double.IsInfinity(sn) || sn < 0) return "Hesaplanıyor...";
        if (sn < 60) return $"Yaklaşık {Math.Max(1, (int)Math.Ceiling(sn))} saniye";
        if (sn < 3600) return $"Yaklaşık {(int)Math.Ceiling(sn / 60)} dakika";
        int s = (int)(sn / 3600), d = (int)Math.Ceiling((sn - s * 3600) / 60);
        if (d == 60) { s++; d = 0; }
        return d == 0 ? $"Yaklaşık {s} saat" : $"Yaklaşık {s} saat {d} dakika";
    }

    public static string Yuzde(double oran) => "%" + (int)Math.Floor(Math.Clamp(oran, 0, 1) * 100) + " tamamlandı";
}

/// <summary>Son birkaç saniyedeki bayt artışından hız.</summary>
public sealed class HizOlcer
{
    readonly Queue<(double t, long b)> _o = new();
    readonly double _pencere;
    public HizOlcer(double pencereSn = 3) => _pencere = pencereSn;

    public void Sifirla() => _o.Clear();

    public double Ekle(double t, long bayt)
    {
        _o.Enqueue((t, bayt));
        while (_o.Count > 2 && t - _o.Peek().t > _pencere) _o.Dequeue();
        var ilk = _o.Peek();
        return t - ilk.t <= 0 ? 0 : Math.Max(0, (bayt - ilk.b) / (t - ilk.t));
    }
}
