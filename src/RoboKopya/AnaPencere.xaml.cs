using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using RoboKopya.Cekirdek;

namespace RoboKopya;

public partial class AnaPencere : Window
{
    readonly Motor? _m;
    readonly Func<Task>? _baslat;
    readonly List<string> _atlanan;
    readonly HizOlcer _hiz = new(3), _eta = new(10);
    readonly Stopwatch _sw = Stopwatch.StartNew();
    readonly SortedDictionary<int, double> _ornek = new();
    readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(250) };
    double _maxHiz = 1, _oran, _sonHiz;
    bool _bitti;
    public readonly bool Koyu = KoyuTemaMi();

    public AnaPencere(string kaynak, string hedef, Motor? m, Func<Task>? baslat, List<string> atlanan)
    {
        TemaUygula(Koyu);
        InitializeComponent();
        _m = m; _baslat = baslat; _atlanan = atlanan;
        KaynakAdi.Text = kaynak; HedefAdi.Text = hedef;
        _t.Tick += (_, _) => Tik();
        if (_baslat != null) Loaded += async (_, _) => await Calis();
    }

    static bool KoyuTemaMi()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int v && v == 0; }
        catch { return false; }
    }

    void TemaUygula(bool koyu)
    {
        Color R(string h) => (Color)ColorConverter.ConvertFromString(h);
        var renk = koyu
            ? new Dictionary<string, string> { ["Zemin"] = "#202020", ["Metin"] = "#FFFFFF", ["Ikincil"] = "#C5C5C5", ["Vurgu"] = "#0A84FF", ["Cizgi"] = "#3D3D3D", ["Kart"] = "#2B2B2B", ["Ustunde"] = "#1FFFFFFF", ["Dolgu"] = "#2C5A33", ["Egri"] = "#4FC75E" }
            : new Dictionary<string, string> { ["Zemin"] = "#F3F3F3", ["Metin"] = "#1A1A1A", ["Ikincil"] = "#5C5C5C", ["Vurgu"] = "#0A84FF", ["Cizgi"] = "#DADADA", ["Kart"] = "#FFFFFF", ["Ustunde"] = "#14000000", ["Dolgu"] = "#C9EDC4", ["Egri"] = "#2F9E44" };
        foreach (var (k, v) in renk) Resources[k] = new SolidColorBrush(R(v));
    }

    // Windows 11: koyu başlık, yuvarlak köşe, mümkünse Mica.
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref Kenar m);
    struct Kenar { public int L, R, T, B; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var h = new WindowInteropHelper(this).Handle;
        int koyu = Koyu ? 1 : 0, kose = 2, mica = 2;
        DwmSetWindowAttribute(h, 20, ref koyu, 4);
        DwmSetWindowAttribute(h, 33, ref kose, 4);
        if (Environment.OSVersion.Version.Build < 22621) return;
        var k = new Kenar { L = -1, R = -1, T = -1, B = -1 };
        if (DwmExtendFrameIntoClientArea(h, ref k) != 0) return;
        if (DwmSetWindowAttribute(h, 38, ref mica, 4) != 0) return;
        HwndSource.FromHwnd(h).CompositionTarget.BackgroundColor = Colors.Transparent;
        Background = Brushes.Transparent;
    }

    async Task Calis()
    {
        _t.Start();
        await _baslat!();
        _t.Stop();
        Tik();
        Sonuc();
    }

    void Tik()
    {
        var m = _m!;
        long top = m.ToplamBayt, kop = m.KopyalananBayt();
        double t = _sw.Elapsed.TotalSeconds, hiz = 0, eta = 0;
        if (m.Duraklatildi) { _hiz.Sifirla(); _eta.Sifirla(); }
        else if (!m.Listeleniyor) { hiz = _hiz.Ekle(t, kop); eta = _eta.Ekle(t, kop); }
        Guncelle(m.Listeleniyor, m.Duraklatildi, top == 0 ? (m.Listeleniyor ? 0 : 1) : (double)kop / top,
                 top - kop, m.ToplamOge, m.ToplamOge - m.BitenOge, m.SuAnki, hiz, eta > 0 ? (top - kop) / eta : double.NaN);
    }

    public void Guncelle(bool listeleniyor, bool durdu, double oran, long kalanBayt, long toplamOge, long kalanOge, string ad, double hiz, double kalanSn)
    {
        if (listeleniyor)
        {
            Title = "Hesaplanıyor...";
            IslemBasi.Text = $"Öğeler sayılıyor ({Bicim.Adet(toplamOge)}): ";
            return;
        }
        _oran = Math.Clamp(oran, 0, 1); _sonHiz = hiz;
        IslemBasi.Text = $"{Bicim.Adet(toplamOge)} öğe kopyalanıyor: ";
        Title = Bicim.Yuzde(_oran);
        YuzdeMetni.Text = durdu ? Bicim.Yuzde(_oran) + " (duraklatıldı)" : Bicim.Yuzde(_oran);
        AdMetni.Text = ad;
        KalanSureMetni.Text = durdu ? "Duraklatıldı" : Bicim.KalanSure(kalanSn);
        KalanOgeMetni.Text = $"{Bicim.Adet(Math.Max(0, kalanOge))} ({Bicim.Boyut(Math.Max(0, kalanBayt))})";
        int px = (int)(_oran * 1000);
        if (!durdu && hiz > 0) { _ornek[px] = hiz; _maxHiz = Math.Max(_maxHiz, hiz); }
        Ciz();
    }

    void Grafik_SizeChanged(object s, SizeChangedEventArgs e)
    {
        Grafik.Clip = new RectangleGeometry(new Rect(0, 0, Grafik.ActualWidth, Grafik.ActualHeight), 7, 7);
        Ciz();
    }

    void Ciz()
    {
        double W = Grafik.ActualWidth, H = Grafik.ActualHeight;
        if (W <= 0 || H <= 0) return;
        double ust = _maxHiz * 1.2;
        IlerlemeDolgusu.Width = _oran * W; IlerlemeDolgusu.Height = H;
        var pts = new PointCollection { new(0, H) };
        double sonX = 0;
        foreach (var (px, h) in _ornek) { sonX = px / 1000.0 * W; pts.Add(new Point(sonX, H - h / ust * H)); }
        pts.Add(new Point(sonX, H));
        HizEgrisi.Points = pts;
        double y = Math.Round(H - _sonHiz / ust * H) + 0.5;
        HizCizgisi.X1 = 0; HizCizgisi.X2 = W; HizCizgisi.Y1 = HizCizgisi.Y2 = y;
        HizCizgisi.Visibility = HizEtiketi.Visibility = _sonHiz > 0 ? Visibility.Visible : Visibility.Hidden;
        HizEtiketi.Text = "Hız: " + Bicim.Hiz(_sonHiz);
        HizEtiketi.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        System.Windows.Controls.Canvas.SetLeft(HizEtiketi, W - HizEtiketi.DesiredSize.Width - 8);
        System.Windows.Controls.Canvas.SetTop(HizEtiketi, Math.Clamp(y - HizEtiketi.DesiredSize.Height - 1, 1, H - HizEtiketi.DesiredSize.Height - 1));
    }

    void Sonuc()
    {
        var m = _m!;
        _bitti = true;
        if (m.Iptal) { Close(); return; }
        DuraklatDugmesi.Visibility = IptalDugmesi.Visibility = Visibility.Collapsed;
        if (m.HataliIs > 0 || _atlanan.Count > 0)
        {
            var satirlar = new List<string>(_atlanan);
            if (m.HataliIs > 0)
            {
                Title = YuzdeMetni.Text = "Kopyalama hatalarla bitti";
                HataBasligi.Text = $"{m.HataliIs} öğede hata oluştu.";
                satirlar.AddRange(m.Hatalar.Count > 0 ? m.Hatalar : new List<string> { "Robocopy hata koduyla çıktı." });
            }
            else
            {
                Title = YuzdeMetni.Text = "Kopyalama tamamlandı";
                HataBasligi.Text = "Bazı öğeler atlandı.";
            }
            HataMetni.Text = string.Join(Environment.NewLine, satirlar);
            HataPaneli.Visibility = Visibility.Visible;
            return;
        }
        Title = YuzdeMetni.Text = "Kopyalama tamamlandı";
        var kapat = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        kapat.Tick += (_, _) => { kapat.Stop(); Close(); };
        kapat.Start();
    }

    bool IptalOnayi() => MessageBox.Show(this, "Kopyalama iptal edilsin mi?\nO an kopyalanan dosya hedefte yarım kalabilir.",
        "Robocopy ile yapıştır", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    void Iptal_Click(object s, RoutedEventArgs e)
    {
        if (_m != null && !_bitti && IptalOnayi()) _m.IptalEt();
    }

    void Duraklat_Click(object s, RoutedEventArgs e)
    {
        if (_m == null || _bitti) return;
        _m.DuraklatDevam();
        DuraklatDugmesi.Content = _m.Duraklatildi ? "\uE768" : "\uE769";
        DuraklatDugmesi.ToolTip = _m.Duraklatildi ? "Sürdür" : "Duraklat";
        Tik();
    }

    void Ayrinti_Click(object s, RoutedEventArgs e)
    {
        bool ac = AyrintiPaneli.Visibility != Visibility.Visible;
        AyrintiPaneli.Visibility = ac ? Visibility.Visible : Visibility.Collapsed;
        AyrintiMetni.Text = ac ? "Daha az ayrıntı" : "Daha fazla ayrıntı";
        AyrintiOku.Text = ac ? "\uE70E" : "\uE70D";
    }

    void Kapat_Click(object s, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_m != null && !_bitti && !_m.Iptal)
        {
            if (IptalOnayi()) _m.IptalEt();
            e.Cancel = true; // iptal edilirse süreç bitince Calis() pencereyi kapatır
        }
        base.OnClosing(e);
    }

    /// <summary>Yalnızca --ekran-goruntusu için örnek durum.</summary>
    public void Ornek()
    {
        var r = new Random(4);
        for (int i = 0; i <= 510; i += 6) { _ornek[i] = 90e6 + r.NextDouble() * 40e6 + (i < 60 ? -60e6 + i * 1e6 : 0); }
        _maxHiz = _ornek.Values.Max();
        IslemBasi.Text = "1.284 öğe kopyalanıyor: ";
        _oran = 0.51; _sonHiz = 118e6;
        YuzdeMetni.Text = Title = Bicim.Yuzde(_oran);
        AdMetni.Text = "Oyun Kayıtları 2026-11-20.zip";
        KalanSureMetni.Text = Bicim.KalanSure(517);
        KalanOgeMetni.Text = $"{Bicim.Adet(611)} ({Bicim.Boyut(56_900_000_000)})";
    }

    public void CizDisaridan() => Ciz();

    public void TemaZorla(bool koyu) => TemaUygula(koyu);
}
