using System.Text.RegularExpressions;

namespace RoboKopya.Cekirdek;

/// <summary>Tek bir robocopy çağrısı.</summary>
public sealed record KopyaIsi(bool Klasor, string KaynakYol, string KaynakDizin, string HedefDizin, string? DosyaAdi)
{
    public string Argumanlar(bool liste) => Planlayici.Argumanlar(this, liste);
}

/// <summary>yapistir.ps1 ile aynı kurallar; Windows yollarını işletim sisteminden bağımsız işler.</summary>
public static class Planlayici
{
    // yapistir.ps1 ile birebir aynı kopyalama bayrakları.
    public static readonly string[] KlasorBayraklari = { "/E", "/COPY:DAT", "/DCOPY:DAT", "/MT:16", "/R:1", "/W:1", "/NP", "/NDL", "/XJ" };
    public static readonly string[] DosyaBayraklari = { "/COPY:DAT", "/J", "/R:1", "/W:1", "/NDL" };
    // Yalnızca çıktı biçimini değiştirir: ilerleme yüzdesi yok, bayt cinsinden boyut, tam yol, başlık/özet yok.
    public static readonly string[] CiktiBayraklari = { "/NP", "/BYTES", "/FP", "/NJH", "/NJS" };
    // Kuru liste: hiçbir şey kopyalamaz, hedefteki fazlalıkları listelemez.
    public static readonly string[] ListeBayraklari = { "/L", "/XX" };

    public static string HedefDuzelt(string hedef)
    {
        hedef = hedef.Trim().Trim('"');
        if (Regex.IsMatch(hedef, "^[A-Za-z]:$")) hedef += "\\";
        return hedef;
    }

    /// <summary>Robocopy "G:\" yolunu tırnakla sorunlu okur; "G:\." verilir.</summary>
    public static string RY(string y) => y.EndsWith('\\') ? y + "." : y;

    public static string Birlestir(string a, string b) => a.EndsWith('\\') ? a + b : a + "\\" + b;

    public static string Kirp(string y) => Regex.IsMatch(y, @"^[A-Za-z]:\\$") ? y : y.TrimEnd('\\');

    public static string Ad(string y)
    {
        y = Kirp(y);
        int i = y.LastIndexOf('\\');
        return i < 0 || i == y.Length - 1 ? y : y[(i + 1)..];
    }

    public static string UstDizin(string y)
    {
        y = Kirp(y);
        int i = y.LastIndexOf('\\');
        if (i < 0) return y;
        var u = y[..i];
        return Regex.IsMatch(u, "^[A-Za-z]:$") ? u + "\\" : u;
    }

    /// <param name="tur">true: klasör, false: dosya, null: yok.</param>
    public static (List<KopyaIsi> Isler, List<string> Atlananlar) Planla(string hedef, IEnumerable<string> kaynaklar, Func<string, bool?> tur)
    {
        hedef = HedefDuzelt(hedef);
        var isler = new List<KopyaIsi>();
        var atlanan = new List<string>();
        var h = hedef.TrimEnd('\\');
        foreach (var ham in kaynaklar)
        {
            if (string.IsNullOrWhiteSpace(ham)) continue;
            var k = Kirp(ham.Trim());
            var t = tur(k);
            if (t is null) continue;
            if (t == true)
            {
                if (Regex.IsMatch(k, @"^[A-Za-z]:\\$")) { atlanan.Add($"Atlandı (sürücü kökü kopyalanmaz): {k}"); continue; }
                var src = k.TrimEnd('\\');
                var dst = Birlestir(hedef, Ad(k));
                if (dst.TrimEnd('\\').Equals(src, StringComparison.OrdinalIgnoreCase)
                    || h.StartsWith(src + "\\", StringComparison.OrdinalIgnoreCase)
                    || h.Equals(src, StringComparison.OrdinalIgnoreCase))
                { atlanan.Add($"Atlandı (klasör kendi içine kopyalanamaz): {k}"); continue; }
                isler.Add(new KopyaIsi(true, k, k, dst, null));
            }
            else
            {
                var dizin = UstDizin(k);
                if (dizin.TrimEnd('\\').Equals(h, StringComparison.OrdinalIgnoreCase)) { atlanan.Add($"Atlandı (aynı klasör): {k}"); continue; }
                isler.Add(new KopyaIsi(false, k, dizin, hedef, Ad(k)));
            }
        }
        return (isler, atlanan);
    }

    public static string Argumanlar(KopyaIsi i, bool liste)
    {
        var parca = new List<string> { $"\"{RY(i.KaynakDizin)}\"", $"\"{RY(i.HedefDizin)}\"" };
        if (i.DosyaAdi != null) parca.Add($"\"{i.DosyaAdi}\"");
        foreach (var b in (i.Klasor ? KlasorBayraklari : DosyaBayraklari).Concat(CiktiBayraklari).Concat(liste ? ListeBayraklari : Array.Empty<string>()))
            if (!parca.Contains(b)) parca.Add(b);
        return string.Join(' ', parca);
    }
}
