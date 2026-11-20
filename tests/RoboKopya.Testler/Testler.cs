using RoboKopya.Cekirdek;
using Xunit;

public class PlanTestleri
{
    static Func<string, bool?> Tur(params (string yol, bool klasor)[] v) => y => v.Where(x => x.yol == y).Select(x => (bool?)x.klasor).FirstOrDefault();

    [Theory]
    [InlineData("G:", "G:\\")]
    [InlineData("\"G:\\\"", "G:\\")]
    [InlineData("G:\"", "G:\\")]
    [InlineData("  D:\\Yedek ", "D:\\Yedek")]
    public void HedefDuzeltilir(string ham, string beklenen) => Assert.Equal(beklenen, Planlayici.HedefDuzelt(ham));

    [Fact]
    public void KokYoluNoktaAlir()
    {
        Assert.Equal("G:\\.", Planlayici.RY("G:\\"));
        Assert.Equal("E:\\Oyunlar", Planlayici.RY("E:\\Oyunlar"));
    }

    [Fact]
    public void KlasorIsiDogruArgumanlarlaKurulur()
    {
        var (isler, atl) = Planlayici.Planla("G:", new[] { "E:\\Oyunlar" }, Tur(("E:\\Oyunlar", true)));
        Assert.Empty(atl);
        var a = isler.Single().Argumanlar(false);
        Assert.StartsWith("\"E:\\Oyunlar\" \"G:\\Oyunlar\" /E /COPY:DAT /DCOPY:DAT /MT:16 /R:1 /W:1 /NP /NDL /XJ", a);
        Assert.Equal(1, a.Split(' ').Count(x => x == "/NP"));
        Assert.DoesNotContain("/L", a.Split(' '));
        Assert.Contains("/L", isler[0].Argumanlar(true).Split(' '));
    }

    [Fact]
    public void DosyaIsiKokHedefte()
    {
        var (isler, _) = Planlayici.Planla("G:\\", new[] { "E:\\a b\\film ı.mkv" }, Tur(("E:\\a b\\film ı.mkv", false)));
        Assert.StartsWith("\"E:\\a b\" \"G:\\.\" \"film ı.mkv\" /COPY:DAT /J /R:1 /W:1 /NDL", isler.Single().Argumanlar(false));
    }

    [Fact]
    public void KaynakKoktekiDosya()
    {
        var (isler, _) = Planlayici.Planla("G:\\", new[] { "E:\\x.bin" }, Tur(("E:\\x.bin", false)));
        Assert.StartsWith("\"E:\\.\" \"G:\\.\" \"x.bin\"", isler.Single().Argumanlar(false));
    }

    [Theory]
    [InlineData("E:\\Oyunlar\\Alt")]
    [InlineData("E:\\Oyunlar")]
    [InlineData("E:\\")]
    public void KendiIcineKopyaEngellenir(string hedef)
    {
        var (isler, atl) = Planlayici.Planla(hedef, new[] { "E:\\Oyunlar" }, Tur(("E:\\Oyunlar", true)));
        Assert.Empty(isler);
        Assert.Single(atl);
    }

    [Fact]
    public void BuyukKucukHarfFarkiEngeliAsamaz()
    {
        var (isler, _) = Planlayici.Planla("e:\\oyunlar\\alt", new[] { "E:\\Oyunlar" }, Tur(("E:\\Oyunlar", true)));
        Assert.Empty(isler);
    }

    [Fact]
    public void BenzerAdliKlasorEngellenmez()
    {
        var (isler, _) = Planlayici.Planla("E:\\Oyunlar2", new[] { "E:\\Oyunlar" }, Tur(("E:\\Oyunlar", true)));
        Assert.Single(isler);
    }

    [Fact]
    public void AyniKlasoreDosyaEngellenir()
    {
        var (isler, atl) = Planlayici.Planla("E:\\a\\", new[] { "E:\\a\\x.txt" }, Tur(("E:\\a\\x.txt", false)));
        Assert.Empty(isler); Assert.Single(atl);
    }

    [Fact]
    public void SurucuKokuKopyalanmaz()
    {
        var (isler, atl) = Planlayici.Planla("G:\\", new[] { "E:\\" }, Tur(("E:\\", true)));
        Assert.Empty(isler); Assert.Single(atl);
    }

    [Fact]
    public void OlmayanKaynakSessizceAtlanir()
    {
        var (isler, atl) = Planlayici.Planla("G:\\", new[] { "E:\\yok", "" }, Tur());
        Assert.Empty(isler); Assert.Empty(atl);
    }

    [Fact]
    public void SilenBayrakYok()
    {
        var yasak = new[] { "/MIR", "/PURGE", "/MOV", "/MOVE", "/CREATE" };
        var (isler, _) = Planlayici.Planla("G:\\", new[] { "E:\\K", "E:\\f.txt" }, Tur(("E:\\K", true), ("E:\\f.txt", false)));
        foreach (var i in isler) foreach (var l in new[] { true, false })
            Assert.DoesNotContain(i.Argumanlar(l).Split(' '), b => yasak.Contains(b.ToUpperInvariant()));
    }
}

public class CiktiTestleri
{
    [Theory]
    [InlineData("\t    New File  \t\t    1234567\tE:\\Oyunlar\\a.bin", 1234567, "E:\\Oyunlar\\a.bin")]
    [InlineData("\t    Yeni Dosya\t\t          0\tE:\\x y\\boş ı.txt", 0, "E:\\x y\\boş ı.txt")]
    [InlineData("\t\t\t   42\tE:\\a.txt", 42, "E:\\a.txt")]
    public void DosyaSatiriOkunur(string satir, long boyut, string yol)
    {
        Assert.True(Cikti.DosyaSatiri(satir, out var b, out var y));
        Assert.Equal(boyut, b); Assert.Equal(yol, y);
    }

    [Theory]
    [InlineData("\t*EXTRA File \t\t      999\tG:\\fazla.txt")]
    [InlineData("")]
    [InlineData("   ------------------------------------------------------------------------------")]
    [InlineData("2026/11/20 13:35:33 ERROR 5 (0x00000005) Copying File E:\\a.txt")]
    [InlineData("Waiting 1 seconds... Retrying...")]
    public void DosyaSatiriDegil(string satir) => Assert.False(Cikti.DosyaSatiri(satir, out _, out _));

    [Fact]
    public void HataSatiriYakalanir()
    {
        Assert.True(Cikti.HataSatiri("2026/11/20 13:35:33 ERROR 5 (0x00000005) Copying File E:\\a.txt"));
        Assert.True(Cikti.HataSatiri("2026/11/20 13:35:33 HATA 32 (0x00000020) Dosya kopyalanıyor"));
        Assert.False(Cikti.HataSatiri("\t    New File  \t\t    12\tE:\\a.bin"));
    }
}

public class BicimTestleri
{
    [Theory]
    [InlineData(0.514, "%51 tamamlandı")]
    [InlineData(0.999, "%99 tamamlandı")]
    [InlineData(1.0, "%100 tamamlandı")]
    [InlineData(1.5, "%100 tamamlandı")]
    [InlineData(-1, "%0 tamamlandı")]
    public void Yuzde(double o, string s) => Assert.Equal(s, Bicim.Yuzde(o));

    [Theory]
    [InlineData(500, "500 bayt")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(124554051584L, "116 GB")]
    [InlineData(5368709120L, "5 GB")]
    public void Boyut(long b, string s) => Assert.Equal(s, Bicim.Boyut(b));

    [Fact]
    public void Hiz()
    {
        Assert.Equal("85,5 MB/sn", Bicim.Hiz(85.5 * 1048576));
        Assert.Equal("294 MB/sn", Bicim.Hiz(294 * 1048576));
    }

    [Theory]
    [InlineData(12.2, "Yaklaşık 13 saniye")]
    [InlineData(125, "Yaklaşık 3 dakika")]
    [InlineData(3600, "Yaklaşık 1 saat")]
    [InlineData(5400, "Yaklaşık 1 saat 30 dakika")]
    [InlineData(7199, "Yaklaşık 2 saat")]
    [InlineData(double.NaN, "Hesaplanıyor...")]
    [InlineData(double.PositiveInfinity, "Hesaplanıyor...")]
    public void KalanSure(double sn, string s) => Assert.Equal(s, Bicim.KalanSure(sn));

    [Fact]
    public void Adet() => Assert.Equal("6.200", Bicim.Adet(6200));

    [Fact]
    public void HizOlcerPencereli()
    {
        var h = new HizOlcer(3);
        Assert.Equal(0, h.Ekle(0, 0));
        Assert.Equal(100, h.Ekle(1, 100), 3);
        h.Ekle(2, 200); h.Ekle(3, 300);
        Assert.Equal((3300 - 100) / 3.0, h.Ekle(4, 3300), 3); // t=0 örneği pencereden düştü, t=1 baz alınır
    }
}
