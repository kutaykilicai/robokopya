using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RoboKopya.Cekirdek;

namespace RoboKopya;

static class Program
{
    static bool? Tur(string y) => Directory.Exists(y) ? true : File.Exists(y) ? false : null;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test") return KendiniSina(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "robokopya-test.txt"));
        if (args.Length > 1 && args[0] == "--ekran-goruntusu") return EkranGoruntusu(args[1], args.Length > 2 ? args[2] : null);

        int hi = Array.FindIndex(args, a => a.Equals("-Hedef", StringComparison.OrdinalIgnoreCase));
        string? hamHedef = hi >= 0 && hi + 1 < args.Length ? args[hi + 1] : args.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(hamHedef)) { MessageBox.Show("Hedef klasör verilmedi.", "Robocopy ile yapıştır"); return 2; }
        var hedef = Planlayici.HedefDuzelt(hamHedef);

        var liste = Path.Combine(AppContext.BaseDirectory, "secim.txt");
        if (!File.Exists(liste))
        {
            MessageBox.Show("Kopyalanacak bir şey seçilmemiş. Önce sağ tık > Robocopy ile kopyala.", "Robocopy ile yapıştır", MessageBoxButton.OK, MessageBoxImage.Information);
            return 1;
        }
        var kaynaklar = File.ReadAllLines(liste, Encoding.UTF8).Where(s => s.Length > 0 && Tur(s.Trim()) != null).Select(s => s.Trim()).ToList();
        var (isler, atlanan) = Planlayici.Planla(hedef, kaynaklar, Tur);

        string kaynakAdi = kaynaklar.Count == 0 ? "" : kaynaklar.Count == 1 ? Planlayici.Ad(kaynaklar[0]) : Planlayici.Ad(Planlayici.UstDizin(kaynaklar[0]));
        var motor = new Motor();
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var w = new AnaPencere(kaynakAdi, Planlayici.Ad(hedef), motor, () => motor.Calistir(isler), atlanan);
        app.Run(w);
        return motor.HataliIs > 0 ? 8 : 0;
    }

    /// <summary>Pencere açmadan küçük bir geçici kopyalama yapar ve sonucu dosyaya yazar.</summary>
    static int KendiniSina(string rapor)
    {
        var sb = new StringBuilder();
        bool ok = true;
        void Kontrol(bool k, string ad) { sb.AppendLine((k ? "GECTI " : "KALDI ") + ad); ok &= k; }
        var kok = Path.Combine(Path.GetTempPath(), "robokopya-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var kaynakKlasor = Path.Combine(kok, "kaynak", "Klasör Ğüş");
            Directory.CreateDirectory(Path.Combine(kaynakKlasor, "alt"));
            File.WriteAllBytes(Path.Combine(kaynakKlasor, "büyük ı.bin"), new byte[3 * 1024 * 1024]);
            File.WriteAllText(Path.Combine(kaynakKlasor, "alt", "küçük.txt"), "merhaba");
            var tekDosya = Path.Combine(kok, "kaynak", "tek dosya ç.bin");
            File.WriteAllBytes(tekDosya, new byte[1024 * 1024 + 7]);
            var hedef = Path.Combine(kok, "hedef");
            Directory.CreateDirectory(hedef);

            var (isler, atlanan) = Planlayici.Planla("\"" + hedef + "\\\"", new[] { kaynakKlasor, tekDosya }, Tur);
            Kontrol(isler.Count == 2 && atlanan.Count == 0, "plan: 2 iş");
            Kontrol(Planlayici.Planla(Path.Combine(kaynakKlasor, "alt"), new[] { kaynakKlasor }, Tur).Isler.Count == 0, "klasör kendi içine engellendi");
            Kontrol(Planlayici.Planla(Path.Combine(kok, "kaynak"), new[] { tekDosya }, Tur).Isler.Count == 0, "aynı klasöre dosya engellendi");

            var m = new Motor();
            m.Calistir(isler).GetAwaiter().GetResult();
            sb.AppendLine($"toplam={m.ToplamBayt} oge={m.ToplamOge} biten={m.BitenOge} kopyalanan={m.KopyalananBayt()} hatali={m.HataliIs} son='{m.SuAnki}'");
            Kontrol(m.ToplamBayt == 3 * 1024 * 1024 + 7 + 1024 * 1024 + 7, "toplam bayt");
            Kontrol(m.ToplamOge == 3 && m.BitenOge == 3, "öğe sayısı");
            Kontrol(m.HataliIs == 0 && m.KopyalananBayt() == m.ToplamBayt, "tamamlandı");
            Kontrol(new FileInfo(Path.Combine(hedef, "Klasör Ğüş", "büyük ı.bin")).Length == 3 * 1024 * 1024, "klasör kopyası");
            Kontrol(File.ReadAllText(Path.Combine(hedef, "Klasör Ğüş", "alt", "küçük.txt")) == "merhaba", "alt klasör");
            Kontrol(new FileInfo(Path.Combine(hedef, "tek dosya ç.bin")).Length == 1024 * 1024 + 7, "tek dosya");
            Kontrol(m.SuAnki == "tek dosya ç.bin", "Türkçe ad çözümü");

            var m2 = new Motor();
            m2.Calistir(isler).GetAwaiter().GetResult();
            Kontrol(m2.ToplamOge == 0 && m2.HataliIs == 0, "ikinci kez: kopyalanacak yok");
            sb.AppendLine("Windows yapı: " + Environment.OSVersion.Version);
        }
        catch (Exception e) { sb.AppendLine("HATA " + e); ok = false; }
        finally { try { Directory.Delete(kok, true); } catch { } }
        sb.AppendLine(ok ? "SONUC: GECTI" : "SONUC: KALDI");
        File.WriteAllText(rapor, sb.ToString(), Encoding.UTF8);
        return ok ? 0 : 1;
    }

    /// <summary>Pencereyi göstermeden içeriğini PNG'ye çizer (düzen kontrolü için).</summary>
    static int EkranGoruntusu(string png, string? tema)
    {
        var w = new AnaPencere("Oyunlar", "HARİCİ (G:)", null, null, new());
        if (tema != null) w.TemaZorla(tema == "koyu");
        var kok = (FrameworkElement)w.Content;
        w.Content = null;
        var cerceve = new Border { Width = 480 };
        foreach (var k in w.Resources.Keys) cerceve.Resources[k] = w.Resources[k];
        cerceve.Background = (Brush)cerceve.Resources["Zemin"];
        TextElement.SetFontFamily(cerceve, w.FontFamily);
        TextElement.SetFontSize(cerceve, w.FontSize);
        TextElement.SetForeground(cerceve, (Brush)cerceve.Resources["Metin"]);
        cerceve.Child = kok;
        w.Ornek();
        cerceve.Measure(new Size(480, double.PositiveInfinity));
        cerceve.Arrange(new Rect(cerceve.DesiredSize));
        cerceve.UpdateLayout();
        w.CizDisaridan();
        cerceve.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)(480 * 1.5), (int)(cerceve.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        bmp.Render(cerceve);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(png);
        enc.Save(f);
        return 0;
    }
}
